using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class SimplePlayerMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";

    private Camera cam;

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
        // Mouse
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
                MoveToTarget(hit.point);
        }

        // Touch
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(touchPos);
            if (Physics.Raycast(ray, out RaycastHit hit))
                MoveToTarget(hit.point);
        }
    }

    void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(speedParam, agent.velocity.magnitude);
    }

    void HandleRotation()
    {
        Vector3 vel = agent.velocity;
        vel.y = 0f;
        if (vel.sqrMagnitude < 0.01f) return;
        Quaternion targetRot = Quaternion.LookRotation(vel);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 12f * Time.deltaTime);
    }

    // <--- Make sure this is inside the same class --->
    public void MoveToTarget(Vector3 targetPosition)
    {
        if (agent != null)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPosition);
        }
    }
}