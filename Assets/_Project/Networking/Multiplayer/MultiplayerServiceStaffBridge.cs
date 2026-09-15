using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AI;

// Reuses the four scene roots. No bot Photon player, prefab instantiation, or task replication.
public sealed class MultiplayerServiceStaffBridge : MonoBehaviour, IOnEventCallback
{
    private const byte StaffSnapshot = 198;
    private static MultiplayerServiceStaffBridge instance;
    private MultiplayerSessionManager session;
    private MultiplayerStaffRosterController roster;
    private readonly GameObject[] roots = new GameObject[4];
    private readonly MultiplayerAnimatorState[] animators = new MultiplayerAnimatorState[4];
    private readonly MultiplayerPoseBuffer[] poses = new MultiplayerPoseBuffer[4];
    private readonly bool[] received = new bool[4];
    private readonly bool[] visible = new bool[4];
    private float nextSend, nextReliable;
    private int sequence, lastSequence = -1, lastSender;
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Moving = Animator.StringToHash("IsMoving");
    private static readonly int Carrying = Animator.StringToHash("IsCarrying");

    public static bool CanSimulate => !MultiplayerDayBridge.IsActive
        || MultiplayerSessionManager.Instance.IsAuthority;

    public static bool AllowRole(GameObject root, bool requested)
    {
        if (!MultiplayerDayBridge.IsActive || instance == null || root == null) return requested;
        for (int i = 0; i < instance.roots.Length; i++)
            if (instance.roots[i] == root)
                return requested && instance.roster.IsRoleAvailableForAI((MultiplayerStaffRosterController.ServiceRole)i);
        return requested; // Kitchen and unrelated objects retain their original policy.
    }

    private void Awake()
    {
        session = GetComponent<MultiplayerSessionManager>();
        roster = GetComponent<MultiplayerStaffRosterController>();
        if (session == null || !session.IsMultiplayerSession || roster == null || !roster.IsInitialized) return;
        instance = this;
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i] = roster.GetStaffRoot((MultiplayerStaffRosterController.ServiceRole)i);
            if (root == null) continue;
            animators[i] = new MultiplayerAnimatorState(root);
            poses[i] = new MultiplayerPoseBuffer();
            // These scene prefabs also carry legacy human/PUN components.
            foreach (var setter in root.GetComponentsInChildren<PlayerNameTagSetter>(true)) setter.enabled = false;
            foreach (var customization in root.GetComponentsInChildren<ApplyCustomizationOnSpawn>(true)) customization.enabled = false;
            foreach (var setup in root.GetComponentsInChildren<PlayerSetup>(true)) setup.enabled = false;
            foreach (var sync in root.GetComponentsInChildren<PhotonTransformView>(true)) sync.enabled = false;
            foreach (var view in root.GetComponentsInChildren<PhotonView>(true)) view.Synchronization = ViewSynchronization.Off;
            GuardControls(root);
            SetRoleName(i);
        }
    }

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);
    private void OnDestroy() { if (instance == this) instance = null; }

    private void GuardControls(GameObject root)
    {
        var movement = root.GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;
        var assignment = root.GetComponent<RoleBasedAssignController>();
        if (assignment != null) assignment.enabled = false;
        if (session.IsAuthority) return;
        var bot = root.GetComponent<AutonomousStaffBot>();
        if (bot != null && bot.enabled) bot.enabled = false;
        var agent = root.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        var crowd = root.GetComponent<CrowdNavigationAgent>();
        if (crowd != null) crowd.enabled = false;
        var animationDriver = root.GetComponent<PlayerAnimationController>();
        if (animationDriver != null) animationDriver.enabled = false;
        var playerSync = root.GetComponent<NetworkPlayerMovementSync>();
        if (playerSync != null) playerSync.enabled = false;
        var animator = root.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.applyRootMotion = false;
    }

    private void SetRoleName(int role)
    {
        foreach (var tag in roots[role].GetComponentsInChildren<NameTagBillboard>(true))
            tag.SetName(((MultiplayerStaffRosterController.ServiceRole)role).ToString());
    }

    private void LateUpdate()
    {
        if (session == null || !session.IsMultiplayerSession || roster == null || !roster.IsInitialized) return;
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null) continue;
            bool available = roster.IsRoleAvailableForAI((MultiplayerStaffRosterController.ServiceRole)i);
            if (!available) root.SetActive(false);
            else if (!session.IsAuthority)
            {
                root.SetActive(received[i] && visible[i]);
                if (received[i])
                {
                    if (poses[i].Read(PhotonNetwork.Time - 0.1d, out var position, out var rotation))
                        root.transform.SetPositionAndRotation(position, rotation);
                }
            }
        }
        if (!session.IsAuthority || Time.unscaledTime < nextSend) return;
        nextSend = Time.unscaledTime + 0.1f;
        bool reliable = Time.unscaledTime >= nextReliable;
        if (reliable) nextReliable = Time.unscaledTime + 0.5f;
        var states = new object[4];
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null) continue;
            var animator = animators[i];
            states[i] = new object[] { i, root.activeInHierarchy, root.transform.position, root.transform.rotation,
                animator.Float(Speed), animator.Bool(Moving), animator.Bool(Carrying) };
        }
        MultiplayerWire.Raise(StaffSnapshot, new object[] { ++sequence, states, PhotonNetwork.Time },
            new Photon.Realtime.RaiseEventOptions { Receivers = Photon.Realtime.ReceiverGroup.Others },
            reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable);
    }

    public void OnEvent(EventData data)
    {
        if (!MultiplayerWire.TryRead(data, out var payload)) return;
        if (session == null || !session.IsMultiplayerSession || session.IsAuthority || data.Code != StaffSnapshot
            || data.Sender != PhotonNetwork.MasterClient.ActorNumber || payload is not object[] packet
            || packet.Length != 3 || packet[0] is not int tick || packet[1] is not object[] states || packet[2] is not double at) return;
        if (lastSender != data.Sender) { lastSender = data.Sender; lastSequence = -1; }
        if (tick <= lastSequence) return;
        lastSequence = tick;
        foreach (var value in states)
        {
            if (value is not object[] state || state.Length != 7 || state[0] is not int role || role < 0 || role >= 4
                || state[1] is not bool active || state[2] is not Vector3 position || state[3] is not Quaternion rotation
                || state[4] is not float speed || state[5] is not bool moving || state[6] is not bool carrying
                || roots[role] == null) continue;
            visible[role] = active && roster.IsRoleAvailableForAI((MultiplayerStaffRosterController.ServiceRole)role);
            poses[role].Add(at, position, rotation);
            if (!received[role]) roots[role].transform.SetPositionAndRotation(position, rotation);
            received[role] = true;
            var animator = animators[role];
            animator.Set(Speed, speed); animator.Set(Moving, moving); animator.Set(Carrying, carrying);
        }
    }
}
