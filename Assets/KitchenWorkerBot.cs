using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class KitchenWorkerBot : MonoBehaviour
{
    [SerializeField] private Transform[] workPoints;
    [SerializeField] private float waitAtPoint = 2f;
    [SerializeField] private bool loop = true;

    private NavMeshAgent agent;
    private int currentIndex;
    private Coroutine routine;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        if (routine == null)
            routine = StartCoroutine(WorkLoop());
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator WorkLoop()
    {
        if (workPoints == null || workPoints.Length == 0 || agent == null)
            yield break;

        currentIndex = Mathf.Clamp(currentIndex, 0, workPoints.Length - 1);

        while (true)
        {
            Transform target = workPoints[currentIndex];

            if (target != null)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);

                while (agent.pathPending)
                    yield return null;

                while (agent.remainingDistance > agent.stoppingDistance)
                    yield return null;

                agent.isStopped = true;
                yield return new WaitForSeconds(waitAtPoint);
            }

            currentIndex++;

            if (currentIndex >= workPoints.Length)
            {
                if (!loop)
                    yield break;

                currentIndex = 0;
            }
        }
    }
}