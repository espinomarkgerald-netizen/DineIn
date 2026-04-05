using UnityEngine;

/// <summary>
/// Tutorial-only driver that keeps the arrow pointing at the most urgently needed
/// action object during PracticeGameplay phases (Days 1–4).
///
/// Runs after TutorialArrowManager so its calls win during practice phases.
/// Does NOT modify any main gameplay script — only reads public state.
/// Day 5 mastery arrow is handled by TutorialArrowManager directly.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class TutorialPracticeArrowDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private TutorialArrowManager arrowManager;

    [Header("Scene Targets – Waiter / Host")]
    [SerializeField] private Transform orderSubmitTarget;
    [SerializeField] private Transform cashierBillTarget;

    [Header("Scene Targets – Cashier")]
    [SerializeField] private Transform cashierCounterTarget;
    [SerializeField] private Transform cashierWaitSpotTarget;

    [Header("Scene Targets – Busser")]
    [SerializeField] private Transform busserSinkTarget;

    [Header("Timing")]
    [SerializeField] private float arrowRefreshInterval = 0.3f;

    private float refreshTimer;

    private void Awake()
    {
        if (tutorialManager == null)
            tutorialManager = GetComponent<TutorialManager>();

        if (arrowManager == null)
            arrowManager = GetComponent<TutorialArrowManager>();
    }

    private void Update()
    {
        if (tutorialManager == null || !tutorialManager.TutorialStarted)
            return;

        if (tutorialManager.CurrentPhase != TutorialManager.TutorialPhase.PracticeGameplay)
        {
            // Not in practice — do nothing. Other drivers / TutorialArrowManager own the arrow.
            return;
        }

        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f)
            return;

        refreshTimer = Mathf.Max(0.05f, arrowRefreshInterval);

        Transform target = ResolvePracticeTarget(tutorialManager.CurrentDay);

        if (target != null)
            PointArrow(target);
        else
            HideArrow();
    }

    // -------------------------------------------------------------------------
    // Target resolution per day
    // -------------------------------------------------------------------------

    private Transform ResolvePracticeTarget(TutorialManager.TutorialDay day)
    {
        switch (day)
        {
            case TutorialManager.TutorialDay.Day1Host:
                return ResolveDay1HostTarget();

            case TutorialManager.TutorialDay.Day2Waiter:
                return ResolveDay2WaiterTarget();

            case TutorialManager.TutorialDay.Day3Cashier:
                return ResolveDay3CashierTarget();

            case TutorialManager.TutorialDay.Day4Busser:
                return ResolveDay4BusserTarget();

            default:
                return null;
        }
    }

    /// <summary>
    /// Day 1 Host practice: point at the first ungreeted group → if all greeted, point at the
    /// nearest available booth for a greeted-but-unseated group → otherwise hide.
    /// </summary>
    private Transform ResolveDay1HostTarget()
    {
        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        CustomerGroup ungreeted = null;
        CustomerGroup needsTable = null;

        for (int i = 0; i < groups.Length; i++)
        {
            CustomerGroup g = groups[i];
            if (g == null || !IsActiveGroup(g))
                continue;

            if (!g.hasBeenGreeted)
            {
                ungreeted = g;
                break;
            }

            if (g.hasBeenGreeted && g.assignedBooth == null && needsTable == null)
                needsTable = g;
        }

        if (ungreeted != null)
            return ungreeted.UIAnchor != null ? ungreeted.UIAnchor : ungreeted.transform;

        if (needsTable != null)
            return needsTable.UIAnchor != null ? needsTable.UIAnchor : needsTable.transform;

        return null;
    }

    /// <summary>
    /// Day 2 Waiter practice: walk through the waiter priority chain and return
    /// the most-urgent target across all active groups.
    /// Priority: ReadyToOrder > OrderTaken > NeedsBill > has money on table.
    /// </summary>
    private Transform ResolveDay2WaiterTarget()
    {
        // If waiter is holding money, point at the cashier handoff spot.
        WaiterHands hands = WaiterHands.Instance;
        if (hands != null && hands.HasMoney && cashierBillTarget != null)
            return cashierBillTarget;

        // If waiter is holding a bill, point at the active group.
        if (hands != null && hands.HasBill)
        {
            CustomerGroup active = tutorialManager.ActiveTutorialGroup;
            if (active != null)
                return active.UIAnchor != null ? active.UIAnchor : active.transform;
        }

        // If waiter is holding a tray, point at the active group.
        if (hands != null && hands.HasTray)
        {
            CustomerGroup active = tutorialManager.ActiveTutorialGroup;
            if (active != null)
                return active.UIAnchor != null ? active.UIAnchor : active.transform;
        }

        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        CustomerGroup readyToOrder = null;
        CustomerGroup orderTaken = null;
        CustomerGroup needsBill = null;

        for (int i = 0; i < groups.Length; i++)
        {
            CustomerGroup g = groups[i];
            if (g == null || !IsActiveGroup(g))
                continue;

            switch (g.state)
            {
                case CustomerGroup.GroupState.ReadyToOrder:
                    if (readyToOrder == null) readyToOrder = g;
                    break;

                case CustomerGroup.GroupState.WaitingToOrder:
                    if (readyToOrder == null) readyToOrder = g;
                    break;

                case CustomerGroup.GroupState.OrderTaken:
                    if (orderTaken == null) orderTaken = g;
                    break;

                case CustomerGroup.GroupState.NeedsBill:
                    if (needsBill == null) needsBill = g;
                    break;
            }
        }

        // Priority: someone ready to order > order sent to kitchen > bill needed.
        CustomerGroup priority = readyToOrder ?? orderTaken ?? needsBill;

        if (priority != null)
        {
            if (priority.state == CustomerGroup.GroupState.OrderTaken && orderSubmitTarget != null)
                return orderSubmitTarget;

            return priority.UIAnchor != null ? priority.UIAnchor : priority.transform;
        }

        // Check for a food tray ready for delivery.
        FoodTray tray = FindDeliveryTray();
        if (tray != null)
            return tray.transform;

        return null;
    }

    /// <summary>
    /// Day 3 Cashier practice: if POS is open point at the counter; otherwise point at wait spot.
    /// </summary>
    private Transform ResolveDay3CashierTarget()
    {
        CashierRegisterUI register = CashierRegisterUI.Instance;

        if (register != null && register.IsOpen)
            return cashierCounterTarget;

        return cashierWaitSpotTarget;
    }

    /// <summary>
    /// Day 4 Busser practice: if holding a tray → sink; otherwise → active dirty tray.
    /// </summary>
    private Transform ResolveDay4BusserTarget()
    {
        BusserHands hands = BusserHands.Instance;
        if (hands != null && hands.HasTray)
            return busserSinkTarget;

        FoodTray active = tutorialManager.ActiveDirtyTray;
        if (active != null)
            return active.transform;

        // Fallback: scan for any pickup-ready cleanup tray.
        FoodTray[] all = FindObjectsByType<FoodTray>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            FoodTray t = all[i];
            if (t == null)
                continue;

            FoodTrayInteractable interactable = t.GetComponentInChildren<FoodTrayInteractable>(false);
            if (interactable != null && interactable.IsCleanupPickable)
                return t.transform;
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static bool IsActiveGroup(CustomerGroup g)
    {
        if (g == null || !g.gameObject.activeInHierarchy)
            return false;

        switch (g.state)
        {
            case CustomerGroup.GroupState.Leaving:
            case CustomerGroup.GroupState.AngryLeft:
            case CustomerGroup.GroupState.UnhappyLeft:
                return false;
        }

        return true;
    }

    private static FoodTray FindDeliveryTray()
    {
        FoodTray[] all = FindObjectsByType<FoodTray>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;

            // A tray with a valid group reference that is not yet in cleanup state is a delivery tray.
            FoodTrayInteractable interactable = all[i].GetComponentInChildren<FoodTrayInteractable>(false);
            if (interactable != null && !interactable.IsCleanupPickable)
                return all[i];
        }

        return null;
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
}
