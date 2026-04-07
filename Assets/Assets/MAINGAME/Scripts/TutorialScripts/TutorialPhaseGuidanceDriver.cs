using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class TutorialPhaseGuidanceDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private TutorialArrowManager arrowManager;
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private TutorialRoleHighlight roleHighlight;

    [Header("Scene Targets - Cashier")]
    [SerializeField] private Transform cashierCounterTarget;
    [SerializeField] private Transform cashierWaitSpotTarget;

    [Header("Scene Targets - Waiter")]
    [SerializeField] private Transform notepadTarget;
    [SerializeField] private Transform orderSubmitTarget;

    [Header("Scene Targets - Busser")]
    [SerializeField] private Transform busserSinkTarget;

    [Header("Timing")]
    [SerializeField] private float phaseEntryHintDuration = 3.5f;
    [SerializeField] private float busserArrowRefreshInterval = 0.2f;

    private float busserRefreshTimer;

    private void Awake()
    {
        if (tutorialManager == null)
            tutorialManager = GetComponent<TutorialManager>();

        if (arrowManager == null)
            arrowManager = GetComponent<TutorialArrowManager>();

        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<TutorialDialogueUI>(FindObjectsInactive.Include);

        if (roleHighlight == null)
            roleHighlight = FindFirstObjectByType<TutorialRoleHighlight>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (tutorialManager == null || !tutorialManager.TutorialStarted)
            return;

        // Day 4 Busser CleanTray is owned by TutorialManager / TutorialArrowManager.
        if (ShouldSkipBusserCleanTrayOwnership())
            return;

        if (tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.CleanTray)
        {
            busserRefreshTimer -= Time.deltaTime;
            if (busserRefreshTimer <= 0f)
            {
                busserRefreshTimer = Mathf.Max(0.05f, busserArrowRefreshInterval);
                UpdateCleanTrayArrow();
            }
        }
    }

    public void OnPhaseEntered(TutorialManager.TutorialPhase phase)
    {
        if (tutorialManager == null || !tutorialManager.TutorialStarted)
            return;

        Debug.Log($"[TutorialGuidance] OnPhaseEntered: {phase}");

        // Day 4 Busser CleanTray: do not own arrow control.
        if (ShouldSkipBusserCleanTrayOwnership(phase))
        {
            arrowManager?.EndExternalControl("TutorialPhaseGuidanceDriver:skip-day4-cleantray");

            string day4Hint = BuildPhaseEntryHint(phase, tutorialManager.ActiveTutorialGroup);
            if (!string.IsNullOrWhiteSpace(day4Hint))
                SendHint(day4Hint);

            UpdateRoleHighlight(phase);
            return;
        }

        if (phase == TutorialManager.TutorialPhase.CleanTray)
            arrowManager?.BeginExternalControl("TutorialPhaseGuidanceDriver");
        else
            arrowManager?.EndExternalControl("TutorialPhaseGuidanceDriver");

        busserRefreshTimer = 0f;
        UpdateRoleHighlight(phase);
        FirePhaseEntryGuidance(phase);
    }

    public void OnActiveTutorialGroupChanged(CustomerGroup group)
    {
        if (tutorialManager == null || !tutorialManager.TutorialStarted)
            return;

        TutorialManager.TutorialPhase phase = tutorialManager.CurrentPhase;

        if (ShouldSkipBusserCleanTrayOwnership(phase))
            return;

        if (IsWaiterOwnedPhase(phase))
            return;

        Transform target = ResolveFirstTargetForPhase(phase, group);
        if (target != null)
            PointArrow(target);
    }

    private void UpdateCleanTrayArrow()
    {
        if (ShouldSkipBusserCleanTrayOwnership())
            return;

        BusserHands hands = BusserHands.Instance;

        if (hands != null && hands.HasTray)
        {
            Debug.Log("[TutorialGuidance] CleanTray - busser HasTray=true, pointing to sink: " +
                      (busserSinkTarget != null ? busserSinkTarget.name : "NULL"));

            if (busserSinkTarget != null)
                PointArrow(busserSinkTarget);

            return;
        }

        FoodTray active = tutorialManager.ActiveDirtyTray;

        Debug.Log($"[TutorialGuidance] CleanTray - HasTray=false  ActiveDirtyTray={(active != null ? active.name : "NULL")}  BusserHands.Instance={(hands != null ? "OK" : "NULL")}");

        if (active != null)
        {
            PointArrow(active.transform);
            return;
        }

        HideArrow();
    }

    private void FirePhaseEntryGuidance(TutorialManager.TutorialPhase phase)
    {
        if (IsWaiterOwnedPhase(phase))
            return;

        CustomerGroup group = tutorialManager.ActiveTutorialGroup;
        Transform target = ResolveFirstTargetForPhase(phase, group);

        if (target != null)
        {
            PointArrow(target);
        }
        else if (phase != TutorialManager.TutorialPhase.CleanTray)
        {
            HideArrow();
        }

        string hint = BuildPhaseEntryHint(phase, group);
        if (!string.IsNullOrWhiteSpace(hint))
            SendHint(hint);
    }

    private Transform ResolveFirstTargetForPhase(TutorialManager.TutorialPhase phase, CustomerGroup group)
    {
        switch (phase)
        {
            case TutorialManager.TutorialPhase.GreetCustomer:
                if (group != null)
                    return group.UIAnchor != null ? group.UIAnchor : group.transform;
                return null;

            case TutorialManager.TutorialPhase.AssignTable:
                if (group != null)
                    return group.UIAnchor != null ? group.UIAnchor : group.transform;
                return null;

            case TutorialManager.TutorialPhase.TakeOrder:
                if (group != null)
                    return group.UIAnchor != null ? group.UIAnchor : group.transform;
                return null;

            case TutorialManager.TutorialPhase.ConfirmOrder:
                return notepadTarget;

            case TutorialManager.TutorialPhase.SubmitOrder:
                return orderSubmitTarget;

            case TutorialManager.TutorialPhase.CashierWaitForMoney:
                return cashierWaitSpotTarget;

            case TutorialManager.TutorialPhase.CashierProcessPayment:
                return cashierCounterTarget;

            case TutorialManager.TutorialPhase.CleanTray:
            {
                FoodTray tray = tutorialManager.ActiveDirtyTray;
                if (tray != null)
                    return tray.transform;
                return null;
            }

            case TutorialManager.TutorialPhase.PracticeGameplay:
                return null;

            case TutorialManager.TutorialPhase.AllTogetherGameplay:
                return null;

            default:
                return null;
        }
    }

    private string BuildPhaseEntryHint(TutorialManager.TutorialPhase phase, CustomerGroup group)
    {
        switch (phase)
        {
            case TutorialManager.TutorialPhase.GreetCustomer:
                return "A customer group has arrived. Go greet them now.";

            case TutorialManager.TutorialPhase.AssignTable:
                return "Now assign them to an empty table. Tap the correct booth.";

            case TutorialManager.TutorialPhase.TakeOrder:
                return "Walk up to the table and tap the order bubble when it appears.";

            case TutorialManager.TutorialPhase.ConfirmOrder:
                return "Match the food and drink shown at the top of the notepad, then confirm.";

            case TutorialManager.TutorialPhase.SubmitOrder:
                return "Order confirmed. Bring it to the order counter now.";

            case TutorialManager.TutorialPhase.CashierWaitForMoney:
                return "Stand by the cashier booth. A waiter will bring you money soon.";

            case TutorialManager.TutorialPhase.CashierProcessPayment:
                return "Money has arrived. Open the POS and process the payment.";

            case TutorialManager.TutorialPhase.CleanTray:
                return "Find the dirty tray on the table and pick it up. Then bring it to the sink.";

            case TutorialManager.TutorialPhase.PracticeGameplay:
                return string.Empty;

            case TutorialManager.TutorialPhase.AllTogetherGameplay:
                return "All roles are active. Switch between Host, Waiter, Cashier, and Busser to keep the lobby running.";

            default:
                return string.Empty;
        }
    }

    private void UpdateRoleHighlight(TutorialManager.TutorialPhase phase)
    {
        if (roleHighlight == null)
            return;

        switch (phase)
        {
            case TutorialManager.TutorialPhase.None:
            case TutorialManager.TutorialPhase.Intro:
            case TutorialManager.TutorialPhase.PracticeGameplay:
            case TutorialManager.TutorialPhase.AllTogetherGameplay:
            case TutorialManager.TutorialPhase.Complete:
                roleHighlight.Hide();
                break;
        }
    }

    private void PointArrow(Transform target)
    {
        if (arrowManager != null && target != null)
            arrowManager.PointToTransform(target, "TutorialPhaseGuidanceDriver");
    }

    private void HideArrow()
    {
        if (arrowManager != null)
            arrowManager.ForceHide("TutorialPhaseGuidanceDriver");
    }

    private void SendHint(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (dialogueUI != null)
            dialogueUI.ShowAuto("Manager", message, phaseEntryHintDuration);
        else
            Debug.Log("[TutorialPhaseGuidanceDriver] " + message);
    }

    private bool ShouldSkipBusserCleanTrayOwnership()
    {
        if (tutorialManager == null)
            return false;

        return ShouldSkipBusserCleanTrayOwnership(tutorialManager.CurrentPhase);
    }

    private bool ShouldSkipBusserCleanTrayOwnership(TutorialManager.TutorialPhase phase)
    {
        if (tutorialManager == null)
            return false;

        return tutorialManager.CurrentDay == TutorialManager.TutorialDay.Day4Busser &&
               phase == TutorialManager.TutorialPhase.CleanTray;
    }

    private static bool IsWaiterOwnedPhase(TutorialManager.TutorialPhase phase)
    {
        switch (phase)
        {
            case TutorialManager.TutorialPhase.ServeFood:
            case TutorialManager.TutorialPhase.PickupBill:
            case TutorialManager.TutorialPhase.DeliverBill:
            case TutorialManager.TutorialPhase.PickupMoney:
            case TutorialManager.TutorialPhase.CollectPayment:
                return true;
        }

        return false;
    }
}