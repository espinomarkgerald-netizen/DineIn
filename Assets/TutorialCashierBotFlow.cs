using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class TutorialCashierBotFlow : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform cashierPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float arriveDistance = 0.35f;

    [Header("Timing")]
    [SerializeField] private float firstDeliveryDelay = 1f;
    [SerializeField] private float repeatDeliveryDelay = 1.2f;

    [Header("Visuals")]
    [SerializeField] private GameObject cashVisual;

    private bool guidedDeliverySent;
    private bool deliveryActive;
    private bool waitingForProcessing;
    private Coroutine queuedDeliveryRoutine;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (spawnPoint != null)
            SnapToPoint(spawnPoint);

        SetCashVisual(false);
    }

    private void Update()
    {
        TutorialManager tm = TutorialManager.Instance;
        bool cashierDayActive =
            tm != null &&
            tm.TutorialStarted &&
            tm.CurrentDay == TutorialManager.TutorialDay.Day3Cashier;

        if (!cashierDayActive)
        {
            ResetBotState(true);
            return;
        }

        if (!guidedDeliverySent &&
            !deliveryActive &&
            !waitingForProcessing &&
            tm.CurrentPhase == TutorialManager.TutorialPhase.CashierWaitForMoney)
        {
            guidedDeliverySent = true;
            QueueDelivery(firstDeliveryDelay);
        }

        if (!deliveryActive)
            return;

        if (!HasArrived())
            return;

        deliveryActive = false;
        waitingForProcessing = true;

        if (agent != null)
            agent.ResetPath();

        SetCashVisual(false);

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnMoneyGivenToCashier(null);
    }

    public void NotifyPaymentProcessed()
    {
        if (!waitingForProcessing)
            return;

        StartCoroutine(HandlePaymentProcessed());
    }

    private IEnumerator HandlePaymentProcessed()
    {
        waitingForProcessing = false;

        if (exitPoint != null)
            MoveTo(exitPoint);

        yield return new WaitForSeconds(repeatDeliveryDelay);

        TutorialManager tm = TutorialManager.Instance;
        if (tm == null || !tm.TutorialStarted)
            yield break;

        if (tm.CurrentDay != TutorialManager.TutorialDay.Day3Cashier)
            yield break;

        if (tm.CurrentPhase == TutorialManager.TutorialPhase.Complete)
            yield break;

        if (tm.CurrentPhase == TutorialManager.TutorialPhase.PracticeGameplay)
            QueueDelivery(0f);
    }

    private void QueueDelivery(float delay)
    {
        if (queuedDeliveryRoutine != null)
            StopCoroutine(queuedDeliveryRoutine);

        queuedDeliveryRoutine = StartCoroutine(BeginDeliveryAfterDelay(delay));
    }

    private IEnumerator BeginDeliveryAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        TutorialManager tm = TutorialManager.Instance;
        if (tm == null || !tm.TutorialStarted)
            yield break;

        if (tm.CurrentDay != TutorialManager.TutorialDay.Day3Cashier)
            yield break;

        if (tm.CurrentPhase == TutorialManager.TutorialPhase.Complete)
            yield break;

        if (spawnPoint != null)
            SnapToPoint(spawnPoint);

        SetCashVisual(true);
        MoveTo(cashierPoint);
        deliveryActive = true;
    }

    private void MoveTo(Transform target)
    {
        if (target == null)
            return;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
            return;
        }

        transform.position = target.position;
    }

    private bool HasArrived()
    {
        if (cashierPoint == null)
            return false;

        if (agent != null && agent.enabled)
        {
            if (agent.pathPending)
                return false;

            return agent.remainingDistance <= arriveDistance;
        }

        return Vector3.Distance(transform.position, cashierPoint.position) <= arriveDistance;
    }

    private void SnapToPoint(Transform point)
    {
        if (point == null)
            return;

        if (agent != null && agent.enabled)
        {
            agent.Warp(point.position);
            return;
        }

        transform.position = point.position;
    }

    private void SetCashVisual(bool value)
    {
        if (cashVisual != null)
            cashVisual.SetActive(value);
    }

    private void ResetBotState(bool fullReset)
    {
        if (queuedDeliveryRoutine != null)
        {
            StopCoroutine(queuedDeliveryRoutine);
            queuedDeliveryRoutine = null;
        }

        deliveryActive = false;
        waitingForProcessing = false;
        SetCashVisual(false);

        if (agent != null && agent.enabled)
            agent.ResetPath();

        if (fullReset)
        {
            guidedDeliverySent = false;

            if (spawnPoint != null)
                SnapToPoint(spawnPoint);
        }
    }
}