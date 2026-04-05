using UnityEngine;

/// <summary>
/// Tutorial-only driver that fires a contextual hint and points the arrow at the first
/// required action object every time a new guided phase begins.
/// Also manages TutorialRoleHighlight visibility during active gameplay phases.
/// During the CleanTray guided phase it continuously tracks whether the busser is
/// already holding a tray and redirects the arrow to the sink accordingly.
///
/// Lives on the TutorialManager GameObject.
/// Does NOT modify any main gameplay script — only reads public state and calls
/// TutorialArrowManager / TutorialDialogueUI / TutorialRoleHighlight.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class TutorialPhaseGuidanceDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private TutorialArrowManager arrowManager;
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private TutorialRoleHighlight roleHighlight;

    [Header("Scene Targets – Cashier")]
    [SerializeField] private Transform cashierCounterTarget;
    [SerializeField] private Transform cashierWaitSpotTarget;

    [Header("Scene Targets – Waiter")]
    [SerializeField] private Transform notepadTarget;
    [SerializeField] private Transform orderSubmitTarget;

    [Header("Scene Targets – Busser")]
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

        // During the guided CleanTray phase the arrow must switch dynamically:
        // tray not yet picked up → point at the dirty tray.
        // tray picked up         → point at the sink.
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

    /// <summary>
    /// Called by TutorialManager.SetPhase() immediately after the phase is changed.
    /// Fires a phase-entry hint and points the arrow at the first action object.
    /// </summary>
    public void OnPhaseEntered(TutorialManager.TutorialPhase phase)
    {
        if (tutorialManager == null || !tutorialManager.TutorialStarted)
            return;

        busserRefreshTimer = 0f; // force immediate arrow update on next Update tick
        UpdateRoleHighlight(phase);
        FirePhaseEntryGuidance(phase);
    }

    /// <summary>
    /// Called by TutorialManager.RefreshRuntimeTargets() when activeTutorialGroup changes.
    /// Refreshes the arrow immediately so the player is never left without a target.
    /// </summary>
    public void OnActiveTutorialGroupChanged(CustomerGroup group)
    {
        if (tutorialManager == null || !tutorialManager.TutorialStarted)
            return;

        TutorialManager.TutorialPhase phase = tutorialManager.CurrentPhase;

        // Waiter phases ServeFood→CollectPayment are fully owned by TutorialWaiterGuidedDialogue.
        if (IsWaiterOwnedPhase(phase))
            return;

        // Re-point the arrow at the newly active group if the phase expects one.
        Transform target = ResolveFirstTargetForPhase(phase, group);
        if (target != null)
            PointArrow(target);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Continuously called during CleanTray to redirect the arrow based on
    /// whether the busser is currently holding a tray.
    /// </summary>
    private void UpdateCleanTrayArrow()
    {
        BusserHands hands = BusserHands.Instance;

        if (hands != null && hands.HasTray)
        {
            // Busser picked up the tray — point at the sink.
            if (busserSinkTarget != null)
                PointArrow(busserSinkTarget);
            return;
        }

        // Busser does not have a tray yet — keep pointing at the active dirty tray.
        FoodTray active = tutorialManager.ActiveDirtyTray;
        if (active != null)
        {
            PointArrow(active.transform);
            return;
        }

        HideArrow();
    }

    private void FirePhaseEntryGuidance(TutorialManager.TutorialPhase phase)
    {
        // Waiter phases ServeFood→CollectPayment already handled by TutorialWaiterGuidedDialogue.
        if (IsWaiterOwnedPhase(phase))
            return;

        CustomerGroup group = tutorialManager.ActiveTutorialGroup;
        Transform target = ResolveFirstTargetForPhase(phase, group);

        if (target != null)
            PointArrow(target);
        else
            HideArrow();

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
                // Phase entry: initially point at the dirty tray.
                // The Update() loop takes over from here and switches to the sink when needed.
                FoodTray tray = tutorialManager.ActiveDirtyTray;
                if (tray != null)
                    return tray.transform;
                return null;

            case TutorialManager.TutorialPhase.PracticeGameplay:
                // Practice arrow is handled by TutorialPracticeArrowDriver.
                return null;

            case TutorialManager.TutorialPhase.AllTogetherGameplay:
                // Mastery arrow is handled by TutorialArrowManager.
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
            arrowManager.PointToTransform(target);
    }

    private void HideArrow()
    {
        if (arrowManager != null)
            arrowManager.ForceHide();
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

    /// <summary>
    /// Returns true for the waiter phases fully owned by TutorialWaiterGuidedDialogue.
    /// This driver must not override the arrow for those phases.
    /// </summary>
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
