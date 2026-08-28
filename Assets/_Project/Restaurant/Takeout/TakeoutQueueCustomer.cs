using UnityEngine;
using UnityEngine.AI;

public class TakeoutQueueCustomer : MonoBehaviour
{
    public enum QueueState
    {
        None,
        WalkingToQueueSlot,
        WaitingInQueue,
        WalkingToOrderPoint,
        AtOrderPoint
    }

    [Header("Refs")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform positionRoot;

    [Header("Debug")]
    [SerializeField] private bool isTakeout;
    [SerializeField] private QueueState queueState = QueueState.None;

    public bool IsTakeout => isTakeout;
    public QueueState CurrentState => queueState;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent == null)
            agent = GetComponentInChildren<NavMeshAgent>();

        if (agent != null)
            CrowdNavigationAgent.Ensure(agent.gameObject, true);

        if (positionRoot == null)
            positionRoot = transform;
    }

    public void SetTakeout(bool value)
    {
        isTakeout = value;
    }

    public void SetQueueState(QueueState value)
    {
        queueState = value;
    }

    public void MoveToPoint(Vector3 worldPoint)
    {
        if (agent == null)
            return;

        agent.SetDestination(worldPoint);
    }

    public bool HasReachedPoint(Vector3 worldPoint, float threshold = 0.6f)
    {
        Vector3 a = positionRoot.position;
        Vector3 b = worldPoint;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b) <= threshold;
    }
}
