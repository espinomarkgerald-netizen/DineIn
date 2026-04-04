using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TakeoutCounterInteractable : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TakeoutQueueManager queueManager;

    [Header("Rules")]
    [SerializeField] private bool requireFrontAtOrderPoint = true;
    [SerializeField] private bool blockWhileServing = true;

    [Header("Debug / Testing")]
    [SerializeField] private bool autoCompleteOnInteract = true;
    [SerializeField] private float autoCompleteDelay = 1.5f;

    [Header("Events")]
    [SerializeField] private UnityEvent onServiceStarted;
    [SerializeField] private UnityEvent onServiceCompleted;
    [SerializeField] private UnityEvent onServiceRejected;

    private CustomerGroup servingGroup;
    private Coroutine autoCompleteRoutine;

    public CustomerGroup CurrentFront => queueManager != null ? queueManager.CurrentFront : null;
    public CustomerGroup ServingGroup => servingGroup;
    public bool IsServing => servingGroup != null;

    public bool CanServeCurrentFront()
    {
        if (queueManager == null)
            return false;

        if (blockWhileServing && servingGroup != null)
            return false;

        CustomerGroup front = queueManager.CurrentFront;
        if (front == null)
            return false;

        if (requireFrontAtOrderPoint &&
            front.CurrentTakeoutQueueState != CustomerGroup.TakeoutQueueState.AtOrderPoint)
            return false;

        return true;
    }

    public void Interact()
    {
        if (!CanServeCurrentFront())
        {
            onServiceRejected?.Invoke();
            return;
        }

        BeginServingCurrentFront();

        if (autoCompleteOnInteract)
        {
            if (autoCompleteRoutine != null)
                StopCoroutine(autoCompleteRoutine);

            autoCompleteRoutine = StartCoroutine(AutoCompleteRoutine());
        }
    }

    public void BeginServingCurrentFront()
    {
        if (!CanServeCurrentFront())
        {
            onServiceRejected?.Invoke();
            return;
        }

        servingGroup = queueManager.CurrentFront;
        onServiceStarted?.Invoke();
    }

    public void CompleteServingCurrentFront()
    {
        if (queueManager == null)
            return;

        CustomerGroup front = queueManager.CurrentFront;

        if (servingGroup == null)
            servingGroup = front;

        if (servingGroup == null)
            return;

        if (front != servingGroup)
        {
            ClearServingState();
            return;
        }

        queueManager.ReleaseFrontFromOrderPoint();
        ClearServingState();
        onServiceCompleted?.Invoke();
    }

    public void CancelServingCurrentFront()
    {
        if (autoCompleteRoutine != null)
        {
            StopCoroutine(autoCompleteRoutine);
            autoCompleteRoutine = null;
        }

        servingGroup = null;
    }

    private IEnumerator AutoCompleteRoutine()
    {
        yield return new WaitForSeconds(autoCompleteDelay);
        autoCompleteRoutine = null;
        CompleteServingCurrentFront();
    }

    private void ClearServingState()
    {
        if (autoCompleteRoutine != null)
        {
            StopCoroutine(autoCompleteRoutine);
            autoCompleteRoutine = null;
        }

        servingGroup = null;
    }
}