using Photon.Pun;
using UnityEngine;

// Presentation only: the existing mover and task claims always take precedence.
public sealed class MultiplayerAvatarEmote : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private string layerName = "Multiplayer Emotes";
    [SerializeField, Min(0.01f)] private float blendSeconds = 0.12f;
    [SerializeField, Min(0.01f)] private float movementThreshold = 0.08f;
    private PhotonView view;
    private int layer = -1;
    private float remaining, weight, speed;
    private Vector3 previousPosition;
    public Transform BubbleAnchor => animator != null && animator.isHuman
        ? animator.GetBoneTransform(HumanBodyBones.Head) ?? transform : transform;
    public bool CanPlay
    {
        get
        {
            var session = MultiplayerSessionManager.Instance;
            if (layer < 0 || animator == null || !animator.isActiveAndEnabled || session == null || !session.CanAct ||
                view == null || !session.ValidActor(view.OwnerActorNr) || Time.timeScale <= 0f || speed > movementThreshold) return false;
            var agent = movement != null ? movement.Agent : null;
            if (agent != null && agent.enabled && agent.isOnNavMesh &&
                (agent.pathPending || agent.hasPath || agent.velocity.sqrMagnitude > movementThreshold * movementThreshold)) return false;
            return MultiplayerActorActivity.Read(gameObject).phase == MultiplayerActorActivity.Phase.Idle;
        }
    }
    private void Awake()
    {
        view = GetComponent<PhotonView>();
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator != null) layer = animator.GetLayerIndex(layerName);
    }
    private void OnEnable() { previousPosition = transform.position; speed = 0f; }
    public void Play(MultiplayerChatCatalog.Entry entry, double sentAt)
    {
        float age = Mathf.Max(0f, (float)(PhotonNetwork.Time - sentAt));
        if (entry?.clip == null || string.IsNullOrEmpty(entry.animatorState) || !CanPlay) return;
        float duration = Mathf.Min(entry.duration, entry.clip.length);
        int state = Animator.StringToHash(layerName + "." + entry.animatorState);
        if (age >= duration || !animator.HasState(layer, state)) return;
        remaining = duration - age;
        animator.Play(state, layer, age / Mathf.Max(0.01f, entry.clip.length));
    }
    private void LateUpdate()
    {
        Vector3 delta = transform.position - previousPosition; delta.y = 0f;
        previousPosition = transform.position;
        speed = delta.magnitude / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
        if (layer < 0 || remaining <= 0f && weight <= 0f) return;
        if (!CanPlay) remaining = 0f;
        else remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        float targetWeight = Mathf.Clamp01(remaining / blendSeconds);
        weight = Mathf.MoveTowards(weight, targetWeight, Time.unscaledDeltaTime / blendSeconds);
        animator.SetLayerWeight(layer, weight);
    }
    private void OnDisable()
    {
        remaining = weight = 0f;
        if (animator != null && layer >= 0) animator.SetLayerWeight(layer, 0f);
    }
}
