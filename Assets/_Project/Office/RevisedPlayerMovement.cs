using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class SimplePlayerMovement : MonoBehaviour
{
    private System.Action OnArrivalCallback;
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
        CheckArrival();
    }
    void CheckArrival()
    {
        if (agent.pathPending) return;

        // Only trigger when the agent actually reached destination and stopped
        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                agent.ResetPath();

                OnArrivalCallback?.Invoke();
                OnArrivalCallback = null;
            }
        }
    }

    void HandleInput()
    {
        // Mouse input
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
                MoveToTarget(hit.point);
        }

        // Touch input
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            int fingerId = Touchscreen.current.primaryTouch.touchId.ReadValue();

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId))
                return;

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
    public void MoveToTargetAndShowUI(Transform target, GameObject uiToShow)
    {
        MoveToTarget(target.position); // existing movement function
        OnArrivalCallback = () =>
        {
        UIManager.Instance.ShowActiveUI(uiToShow);
        };
    }
}