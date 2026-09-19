using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Authored single-player Lobby2 service coordinator. No runtime bootstrap.</summary>
[DefaultExecutionOrder(-100)]
public sealed class FastFoodRestaurant : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private TakeoutQueueManager counterQueue;
    [SerializeField] private TakeoutFlowManager counterFlow;
    [SerializeField] private KitchenManager kitchen;
    [SerializeField] private Booth[] diningTables;
    [SerializeField] private Transform[] waitingPoints;
    [SerializeField] private Transform customerPickupPoint;
    [SerializeField] private Transform cashierApproach;
    [SerializeField] private Transform customerExit;
    [SerializeField] private MoneyPickup cardPaymentPrefab;
    [Tooltip("The Lobby2 surface that owns the authored navigation bake. Keep it on its original transform.")]
    [SerializeField] private Unity.AI.Navigation.NavMeshSurface navigationSurface;

    [Header("Customer choices")]
    [SerializeField, Range(0f, 1f)] private float takeoutChance = .3f;
    [SerializeField, Range(0f, 1f)] private float selfPickupChance = .5f;
    [Header("Timing (gameplay seconds)")]
    [SerializeField, Min(.1f)] private float seatCheckInterval = .5f;
    [SerializeField, Min(5f)] private float pickupTravelTimeout = 20f;
    [Tooltip("Maximum wait after the food is ready. Waiting for a seat or kitchen cleaning does not use this time.")]
    [SerializeField, Min(30f)] private float readyFoodWaitSeconds = 120f;
    [SerializeField] private Vector3 customerTrayOffset = new Vector3(0f, .85f, .4f);
    [SerializeField, Min(1f)] private float waitingSpacing = 2.5f;

    private readonly List<CustomerGroup> paid = new();
    private readonly HashSet<CustomerGroup> readyBags = new();
    private readonly HashSet<CustomerGroup> settled = new();
    private readonly Dictionary<CustomerGroup, int> waitingSlots = new();
    private readonly Dictionary<CustomerGroup, bool> cardChoices = new();
    private readonly HashSet<CustomerGroup> refunded = new();
    private readonly Dictionary<CustomerGroup, float> readyWait = new();
    private MoneyPickup currentCard;
    private FastFoodTable[] furniture;
    private float nextSeatCheck;
    public bool Operational => isActiveAndEnabled && gameObject.scene.name == "Lobby2";
    public Transform CustomerExit => customerExit;
    public Transform CashierApproach => cashierApproach;
    public Transform PickupApproach => customerPickupPoint;
    public Unity.AI.Navigation.NavMeshSurface NavigationSurface => navigationSurface != null
        ? navigationSurface : GetComponent<Unity.AI.Navigation.NavMeshSurface>();
    public bool HasNavigation => NavigationSurface != null && NavigationSurface.isActiveAndEnabled
        && NavigationSurface.navMeshData != null
        && cashierApproach != null && customerPickupPoint != null
        && NavMesh.SamplePosition(cashierApproach.position, out _, 1f, NavMesh.AllAreas)
        && NavMesh.SamplePosition(customerPickupPoint.position, out _, 1f, NavMesh.AllAreas);
    public int WaitingForSeatCount => paid.FindAll(g => g != null && g.FastFoodDineIn && g.assignedBooth == null).Count;

    public static FastFoodRestaurant For(Component component)
    {
        if (component == null || component.gameObject.scene.name != "Lobby2") return null;
        // Lookup happens on spawn, not each frame. Scope to the spawning scene.
        foreach (var root in component.gameObject.scene.GetRootGameObjects())
            foreach (var restaurant in root.GetComponentsInChildren<FastFoodRestaurant>())
                if (restaurant.Operational) return restaurant;
        return null;
    }

    private void OnEnable()
    {
        if (kitchen != null) kitchen.OrderFinished += OnKitchenFinished;
        furniture = GetComponentsInChildren<FastFoodTable>(true);
    }
    private void OnDisable()
    {
        if (kitchen != null) kitchen.OrderFinished -= OnKitchenFinished;
        StopAllCoroutines();
    }

    public bool Route(CustomerGroup group)
    {
        if (!Operational) return false;
        if (counterQueue == null || counterFlow == null || kitchen == null)
        {
            Debug.LogError("Lobby2 Fast Food Services has missing scene references.", this);
            group.FailFastFoodService("Counter service is unavailable.");
            return true;
        }
        group.ConfigureFastFood(this, Random.value >= takeoutChance);
        group.ServiceOutcomeReported += OnOutcome;
        group.state = CustomerGroup.GroupState.Waiting;
        if (group.GetComponent<TakeoutCustomerInteractable>() == null)
            group.gameObject.AddComponent<TakeoutCustomerInteractable>();
        if (group.GetComponent<Collider>() == null)
        {
            var collider = group.gameObject.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * .9f;
            collider.radius = .45f;
            collider.height = 1.8f;
            collider.isTrigger = true;
        }
        counterQueue.Enqueue(group);
        return true;
    }

    public bool CanSettle(CustomerGroup group) => Operational && group != null && group.FastFood == this
        && !group.FastFoodPaid && !settled.Contains(group) && counterFlow.ActiveGroup == group
        && counterFlow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForPayment
        && group.HasConfirmedOrder && group.state == CustomerGroup.GroupState.OrderTaken;

    // Called BEFORE finance callbacks; closes the payment against reentrant clicks.
    public bool ReserveSettlement(CustomerGroup group)
    {
        if (!CanSettle(group)) return false;
        return settled.Add(group);
    }

    public void PaymentCompleted(CustomerGroup group)
    {
        if (group == null || group.FastFood != this || !settled.Contains(group) || !group.CommitFastFoodPayment()) return;
        counterQueue.Remove(group); // Detach without sending this paid customer to the exit.
        counterFlow.ForceRelease(group);
        paid.Add(group);
        group.FastFoodSelfPickup = group.FastFoodDineIn && Random.value < selfPickupChance;
        PlaceInWaitingArea(group);
        SubmitKitchen(group);
    }

    private void PlaceInWaitingArea(CustomerGroup group)
    {
        int slot = 0;
        while (waitingSlots.ContainsValue(slot)) slot++;
        waitingSlots[group] = slot;
        Transform point = waitingPoints != null && waitingPoints.Length > 0
            ? waitingPoints[slot % waitingPoints.Length] : customerPickupPoint;
        if (point == null) return;
        int overflow = waitingPoints != null && waitingPoints.Length > 0 ? slot / waitingPoints.Length : slot;
        group.MoveToTakeoutPoint(point.position - point.forward * overflow * waitingSpacing);
    }

    private void Update()
    {
        if (!Operational || Time.time < nextSeatCheck) return;
        nextSeatCheck = Time.time + seatCheckInterval;
        foreach (var group in new List<CustomerGroup>(readyWait.Keys))
        {
            if (group == null || group.FastFoodWasServed || !paid.Contains(group))
            { readyWait.Remove(group); continue; }
            if (HygieneManager.KitchenPaused) continue;
            readyWait[group] += seatCheckInterval;
            if (readyWait[group] >= readyFoodWaitSeconds)
            {
                readyWait.Remove(group);
                group.FailFastFoodService("The prepared order was not delivered in time.");
            }
        }
        paid.RemoveAll(group => group == null || group.state == CustomerGroup.GroupState.Leaving
            || group.state == CustomerGroup.GroupState.UnhappyLeft || group.state == CustomerGroup.GroupState.AngryLeft);
        settled.RemoveWhere(group => group == null);
        refunded.RemoveWhere(group => group == null);
        readyBags.RemoveWhere(group => group == null || !paid.Contains(group));
        foreach (var group in new List<CustomerGroup>(waitingSlots.Keys))
            if (group == null || !paid.Contains(group) || group.assignedBooth != null) waitingSlots.Remove(group);
        foreach (var group in new List<CustomerGroup>(cardChoices.Keys))
            if (group == null || group.FastFoodPaid) cardChoices.Remove(group);
        // Oldest compatible paid group gets the next seat. Reserve synchronously.
        foreach (CustomerGroup group in paid)
        {
            if (!group.CanChooseFastFoodSeat) continue;
            if (diningTables == null) continue;
            int start = diningTables.Length == 0 ? 0 : Random.Range(0, diningTables.Length);
            for (int i = 0; i < diningTables.Length; i++)
            {
                Booth table = diningTables[(start + i) % diningTables.Length];
                if (table == null || !table.gameObject.activeInHierarchy || !table.IsAvailableFor(group.Size)
                    || table.NeedsSurfaceCleaning || table.NetworkTrayPoint == null) continue;
                bool complete = true;
                for (int seat = 0; seat < group.Size; seat++) complete &= table.GetSeat(seat) != null;
                if (!complete) continue;
                group.ChooseFastFoodSeat(table);
                break;
            }
        }
    }

    public void OnSeated(CustomerGroup group)
    {
        if (group != null && group.FastFood == this && group.FastFoodPaid) SubmitKitchen(group);
    }
    private void OnOutcome(CustomerGroup group, CustomerGroup.FinalResult result)
    {
        if (group == null) return;
        readyWait.Remove(group);
        if (currentCard != null && currentCard.TargetGroup == group)
        {
            CardPaymentUI.Instance?.CancelPayment();
            Destroy(currentCard.gameObject);
            currentCard = null;
        }
        if (group != null && group.FastFoodPaid && result != CustomerGroup.FinalResult.Happy
            && !group.FastFoodWasServed && refunded.Add(group))
        {
            DailyFinanceBridge.Instance?.ApplyRefund(group.GetCurrentOrderTotal(), "Fast Food unserved order refund");
            GameDayManager.Instance?.RefreshRevenueUI();
            FoodTray tray = kitchen.GetPreparedResult(group.currentOrderNumber);
            if (tray != null) Destroy(tray.gameObject);
            foreach (var bag in FindObjectsByType<TakeoutBagInteractable>(FindObjectsSortMode.None))
                if (bag.TargetGroup == group) Destroy(bag.gameObject);
        }
    }
    private void SubmitKitchen(CustomerGroup group)
    {
        if (kitchen.HasAcceptedOrder(group.currentOrderNumber)) return;
        if (kitchen.ProcessOrder(group)) GameDayManager.Instance?.RegisterOrderProcessed();
        else group.FailFastFoodService("The kitchen could not accept this order.");
    }
    private void OnKitchenFinished(CustomerGroup group, int order, bool success)
    {
        if (group == null || group.FastFood != this || !group.FastFoodPaid) return;
        if (!success) { group.FailFastFoodService("The kitchen could not prepare this order."); return; }
        readyWait[group] = 0f;
        if (group.FastFoodDineIn && group.FastFoodSelfPickup)
        {
            FoodTray tray = kitchen.GetPreparedResult(order);
            if (tray != null && customerPickupPoint != null)
                group.StartCoroutine(group.CollectFastFoodTray(tray, customerPickupPoint,
                    pickupTravelTimeout, customerTrayOffset));
        }
    }

    public bool BagReady(CustomerGroup group)
    {
        if (group == null || group.FastFood != this || !group.FastFoodPaid || group.FastFoodDineIn || !paid.Contains(group)) return false;
        readyBags.Add(group);
        return true;
    }
    public Booth DiningSurface(Renderer renderer)
    {
        if (furniture == null) return null;
        foreach (var table in furniture)
            if (table != null && table.Contains(renderer)) return table.GetComponent<Booth>();
        return null;
    }
    public bool IsBagReady(CustomerGroup group) => group != null && readyBags.Contains(group);
    public void BagDelivered(CustomerGroup group)
    {
        if (!readyBags.Remove(group)) return;
        paid.Remove(group);
        waitingSlots.Remove(group);
        counterQueue.ReleaseGroup(group);
    }

    public void FinishClosing()
    {
        // The existing closing grace period is over. Settle failed, unserved
        // prepaid orders before the daily finance result is captured.
        foreach (var group in paid.ToArray())
            if (group != null && !group.FastFoodWasServed
                && group.state != CustomerGroup.GroupState.Leaving
                && group.state != CustomerGroup.GroupState.UnhappyLeft
                && group.state != CustomerGroup.GroupState.AngryLeft)
                group.FailFastFoodService("The restaurant closed before this order was served.");
    }

    public void RequestPaymentAfterReview(CustomerGroup group) => StartCoroutine(OpenAfterReview(group));
    public void RequestCounterPayment(CustomerGroup group)
    {
        if (!CanSettle(group)) return;
        var mover = RoleManager.Instance?.GetActivePlayerMovement();
        if (mover == null || cashierApproach == null) return;
        mover.UI_MoveToAction(cashierApproach, 2.25f, () => OpenCounterPayment(group));
    }
    private IEnumerator OpenAfterReview(CustomerGroup group)
    {
        // The notepad must close before registering the cashier as the active modal.
        yield return null;
        if (CanSettle(group)) OpenCounterPayment(group);
    }
    public bool OpenCounterPayment(CustomerGroup group)
    {
        if (!CanSettle(group)) return false;
        var register = CashierRegisterUI.ResolveInstance();
        if (register == null || register.IsOpen) return false;
        var cardUI = CardPaymentUI.Instance;
        if (cardUI != null && cardUI.IsOpen) return false;
        if (!RestaurantTaskClaim.TryClaimPlayer(group)) return false;
        if (!cardChoices.TryGetValue(group, out bool useCard))
            cardChoices[group] = useCard = CardPaymentService.ShouldUseCardPayment();
        int total = group.GetCurrentOrderTotal();
        if (useCard && cardPaymentPrefab != null)
        {
            var ui = CardPaymentUI.Instance != null ? CardPaymentUI.Instance : FindFirstObjectByType<CardPaymentUI>(FindObjectsInactive.Include);
            if (ui != null)
            {
                if (currentCard == null || currentCard.TargetGroup != group)
                {
                    if (currentCard != null) Destroy(currentCard.gameObject);
                    currentCard = Instantiate(cardPaymentPrefab, cashierApproach != null ? cashierApproach : transform);
                    currentCard.Init(group, total, cashierApproach, cardPayment: true);
                }
                bool opened = ui.Open(currentCard);
                if (!opened) CancelCounterPayment(group);
                return opened;
            }
        }
        register.OpenForPayment(group, TakeoutFlowManager.GetPaymentDenomination(total), total);
        if (!register.IsOpen) CancelCounterPayment(group);
        return register.IsOpen;
    }

    public void CancelCounterPayment(CustomerGroup group)
    {
        if (group != null && group.FastFood == this && !group.FastFoodPaid)
            RestaurantTaskClaim.ReleasePlayer(group);
    }
}
