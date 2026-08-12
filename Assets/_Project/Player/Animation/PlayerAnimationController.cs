using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives the Speed animator float from NavMeshAgent velocity for the local player only.
/// On remote players this component disables itself; animation is handled by
/// <see cref="NetworkPlayerMovementSync"/> which applies synced parameters directly.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private float animationSmooth = 10f;

    private NavMeshAgent agent;
    private PhotonView photonView;
    private float smoothedSpeed;
    private bool hasSpeed;
    private bool hasIsMoving;

    private void Awake()
    {
        agent      = GetComponent<NavMeshAgent>();
        photonView = GetComponent<PhotonView>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == speedParam && parameter.type == AnimatorControllerParameterType.Float)
                    hasSpeed = true;
                else if (parameter.name == "IsMoving" && parameter.type == AnimatorControllerParameterType.Bool)
                    hasIsMoving = true;
            }
        }
    }

    private void Start()
    {
        // Remote players have animation driven by NetworkPlayerMovementSync; disable this component.
        if (photonView != null && PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (agent == null || animator == null) return;

        // Horizontal movement only.
        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        float targetSpeed = velocity.magnitude;

        // Smooth to avoid flicker near stopping distance.
        smoothedSpeed = Mathf.Lerp(
            smoothedSpeed,
            targetSpeed,
            Time.deltaTime * animationSmooth
        );

        if (hasSpeed)
            animator.SetFloat(speedParam, smoothedSpeed);
        if (hasIsMoving)
            animator.SetBool("IsMoving", targetSpeed > 0.01f);
    }
}
