using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SimplePlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    private NavMeshAgent agent;
    private Camera cam;

    [Header("Animation")]
    [SerializeField] private string speedParam = "Speed";

    [Header("Facing")]
    [SerializeField] private bool rotateToMovement = true;
    [SerializeField] private float rotationSpeed = 12f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        agent.updateRotation = false;
    }

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        HandleInput();
        UpdateAnimator();
        HandleRotation();
    }

    void HandleInput()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.point);
        }
    }

    void UpdateAnimator()
    {
        if (animator == null)
            return;

        float speed = agent.velocity.magnitude;
        animator.SetFloat(speedParam, speed);
    }

    void HandleRotation()
    {
        if (!rotateToMovement)
            return;

        Vector3 vel = agent.velocity;
        vel.y = 0f;

        if (vel.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(vel);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }
}