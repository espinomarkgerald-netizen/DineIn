using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private float animationSmooth = 10f;

    private NavMeshAgent agent;
    private float smoothedSpeed;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    void Update()
    {
        if (agent == null || animator == null) return;

        // Horizontal movement only
        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        float targetSpeed = velocity.magnitude;

        // Smooth to avoid flicker near stopping distance
        smoothedSpeed = Mathf.Lerp(
            smoothedSpeed,
            targetSpeed,
            Time.deltaTime * animationSmooth
        );

        animator.SetFloat(speedParam, smoothedSpeed);
    }
}
