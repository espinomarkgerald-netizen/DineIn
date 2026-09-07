using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

// Network wrapper around the real group/members, not a second customer state machine.
public class MultiplayerCustomerSpawn : MonoBehaviourPun, IPunInstantiateMagicCallback, IPunObservable
{
    [SerializeField] private CustomerGroup groupPrefab;
    [SerializeField] private CustomerAgent greenPrefab;
    [SerializeField] private CustomerAgent pinkPrefab;
    [SerializeField] private CustomerAgent bluePrefab;
    public CustomerGroup Group { get; private set; }
    public bool ReadyForInteraction { get; private set; }
    private Vector3[] positions;
    private Quaternion[] rotations;
    private bool receivedPose;
    private bool simulating;
    private LobbyLineManager interactionLine;
    private MultiplayerSessionManager session;
    private Animator[] animators;
    private float[] speeds;
    private float nextSnapshot;
    private bool serviceStarted;
    private CustomerGroup.GroupState phase;
    private Vector3 queueDestination;
    private float patience;
    private bool showPatience;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        session = MultiplayerSessionManager.Instance;
        var bridge = session != null ? session.GetComponent<MultiplayerCustomerSpawnBridge>() : null;
        Group = bridge != null ? bridge.PendingGroup : null;
        simulating = Group != null && session.IsAuthority;
        if (simulating) interactionLine = FindFirstObjectByType<LobbyLineManager>();
        var data = photonView.InstantiationData;
        var type = (CustomerGroup.CustomerType)(int)data[0];
        int size = (int)data[1];
        if (!simulating)
        {
            Group = Instantiate(groupPrefab, transform.position, transform.rotation, transform);
            Group.members.Clear();
            Group.SetCustomerType(type);
            Group.SetServiceType((bool)data[2] ? CustomerGroup.ServiceType.Takeout : CustomerGroup.ServiceType.DineIn);
            CustomerAgent prefab = type == CustomerGroup.CustomerType.Pink ? pinkPrefab
                : type == CustomerGroup.CustomerType.Blue ? bluePrefab : greenPrefab;
            if (prefab == null) prefab = greenPrefab;
            for (int i = 0; i < size; i++)
            {
                var member = Instantiate(prefab,
                    transform.position + new Vector3(i % 2 * 0.6f, 0, i / 2 * 0.6f),
                    Quaternion.identity, Group.transform);
                Group.members.Add(member);
            }
            // Observation only: no local navigation, timers, interaction, or customer scripts.
            foreach (var behaviour in Group.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var agent in Group.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
            foreach (var collider in Group.GetComponentsInChildren<Collider>(true))
            { collider.isTrigger = true; collider.enabled = true; }
            Group.IsNetworkObserver = true;
        }
        else Group.transform.SetParent(transform, true);
        Group.name = $"Group_{type}_{size}_View{photonView.ViewID}";
        positions = new Vector3[size];
        rotations = new Quaternion[size];
        animators = new Animator[size];
        speeds = new float[size];
        for (int i = 0; i < size; i++) animators[i] = Group.members[i].GetComponentInChildren<Animator>();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            bool valid = simulating && session != null && session.IsAuthority && Group != null;
            stream.SendNext(valid);
            if (!valid) return;
            for (int i = 0; i < positions.Length; i++)
            {
                stream.SendNext(Group.members[i].transform.position);
                stream.SendNext(Group.members[i].transform.rotation);
                stream.SendNext(animators[i] != null ? animators[i].GetFloat("Speed") : 0f);
            }
        }
        else
        {
            if (!(bool)stream.ReceiveNext()) return;
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = (Vector3)stream.ReceiveNext();
                rotations[i] = (Quaternion)stream.ReceiveNext();
                speeds[i] = (float)stream.ReceiveNext();
            }
            receivedPose = true;
        }
    }

    private void Update()
    {
        if (simulating)
        {
            if (session == null || !session.IsAuthority) return;
            if (Group == null)
            {
                ReadyForInteraction = false;
                if (!serviceStarted) PhotonNetwork.Destroy(gameObject);
                return;
            }
            if (!IsPreService(Group.state)) serviceStarted = true;
            ReadyForInteraction = !serviceStarted && Group.state == CustomerGroup.GroupState.Waiting
                && interactionLine != null && interactionLine.GetFrontOfLine() == Group && Group.CanBeGreeted();
            if (!serviceStarted && (Time.unscaledTime >= nextSnapshot || phase != Group.state))
            {
                phase = Group.state;
                nextSnapshot = Time.unscaledTime + 0.5f;
                photonView.RPC(nameof(ReceiveQueue), RpcTarget.Others, (int)phase,
                    Group.QueueDestination, Group.QueuePatience01, Group.QueuePatienceVisible, ReadyForInteraction);
            }
        }
        if (simulating || !receivedPose || Group == null) return;
        float blend = 1f - Mathf.Exp(-15f * Time.deltaTime);
        for (int i = 0; i < positions.Length; i++)
        {
            var member = Group.members[i].transform;
            member.position = Vector3.Lerp(member.position, positions[i], blend);
            member.rotation = Quaternion.Slerp(member.rotation, rotations[i], blend);
            if (animators[i] != null) animators[i].SetFloat("Speed", speeds[i]);
        }
        Group.PresentObservedQueue(phase, queueDestination, patience, showPatience);
    }

    private static bool IsPreService(CustomerGroup.GroupState value) =>
        value == CustomerGroup.GroupState.Spawning || value == CustomerGroup.GroupState.WalkingToLobby
        || value == CustomerGroup.GroupState.Waiting || value == CustomerGroup.GroupState.AngryLeft
        || value == CustomerGroup.GroupState.UnhappyLeft || value == CustomerGroup.GroupState.Leaving;

    [PunRPC]
    private void ReceiveQueue(int value, Vector3 destination, float progress, bool visible, bool ready, PhotonMessageInfo info)
    {
        if (session == null || !session.IsMultiplayerSession || info.Sender != PhotonNetwork.MasterClient || simulating) return;
        var receivedPhase = (CustomerGroup.GroupState)value;
        if (!IsPreService(receivedPhase)) return;
        phase = receivedPhase;
        queueDestination = destination;
        patience = Mathf.Clamp01(progress);
        showPatience = visible;
        ReadyForInteraction = ready;
    }
}
