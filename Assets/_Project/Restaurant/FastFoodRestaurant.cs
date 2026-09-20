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

    [Header("Stations and entrance (authored in Lobby2)")]
    [SerializeField] private FastFoodServiceStation[] serviceStations;
    [SerializeField] private Transform[] entranceRoute;
    [SerializeField, Min(5f)] private float entranceTravelTimeout = 60f;
    private readonly Dictionary<CustomerGroup, FastFoodServiceStation> stationOwners = new();
    public IReadOnlyList<FastFoodServiceStation> ServiceStations => serviceStations;
    public FastFoodServiceStation StationFor(CustomerGroup group) => group != null &&
        stationOwners.TryGetValue(group, out var station) ? station : null;
    private FastFoodServiceStation ChooseStation(bool countersOnly)
    {
        FastFoodServiceStation chosen = null;
        if (serviceStations == null) return null;
        // Shortest queue; rotate equal-length choices rather than favouring counter one.
        int start = Random.Range(0, serviceStations.Length);
        for (int i = 0; i < serviceStations.Length; i++)
        {
            var station = serviceStations[(start + i) % serviceStations.Length];
            if (station == null || !station.isActiveAndEnabled || station.Queue == null ||
                station.Flow == null || (countersOnly && station.IsKiosk)) continue;
            if (chosen == null || station.Queue.Count < chosen.Queue.Count) chosen = station;
        }
        return chosen;
    }
    public void TransferToCounter(CustomerGroup group)
    {
        var previous = StationFor(group);
        if (previous == null || !previous.IsKiosk || !CanSettle(group)) return;
        var target = ChooseStation(true);
        if (target == null) { group.FailFastFoodService("No payment counter is available."); return; }
        previous.Queue.Remove(group);
        previous.Flow.ForceRelease(group);
        stationOwners[group] = target;
        target.Queue.Enqueue(group);
    }
    private IEnumerator AdmitCustomer(CustomerGroup group)
    {
        if (entranceRoute != null) foreach (var point in entranceRoute)
        {
            if (group == null || group.state == CustomerGroup.GroupState.Leaving ||
                group.state == CustomerGroup.GroupState.UnhappyLeft || group.state == CustomerGroup.GroupState.AngryLeft) yield break;
            if (point == null) continue;
            group.MoveToTakeoutPoint(point.position);
            float deadline = Time.time + entranceTravelTimeout;
            while (group != null && !group.HasReachedTakeoutPoint(point.position, 1.25f) && Time.time < deadline)
                yield return null;
            if (group == null) yield break;
            if (!group.HasReachedTakeoutPoint(point.position, 1.25f))
            { group.FailFastFoodService("The entrance could not be reached."); yield break; }
        }
        var station = ChooseStation(false);
        if (station == null) { group.FailFastFoodService("No order station is available."); yield break; }
        stationOwners[group] = station;
        station.Queue.Enqueue(group);
    }

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
    private readonly HashSet<CustomerGroup> customers = new();
    private readonly HashSet<int> readyOrders = new();
    private uint serviceGeneration;
    private bool acceptingCustomers = true;
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

    public bool ValidateServiceSetup(out string problem)
    {
        problem = null;
        if (counterQueue == null || counterFlow == null || kitchen == null || cashierApproach == null ||
            customerPickupPoint == null || customerExit == null || waitingPoints == null || waitingPoints.Length == 0)
            problem = "Assign all Fast Food service and waiting-point references.";
        else if (serviceStations == null || serviceStations.Length != 4 || entranceRoute == null || entranceRoute.Length < 2)
            problem = "Author two counters, two kiosks and the left entrance route.";
        else if (!counterQueue.ValidateAuthoring(out problem)) return false;
        else if (diningTables == null || diningTables.Length == 0) problem = "Register the dining tables.";
        else
        {
            var unique = new HashSet<Booth>();
            foreach (var booth in diningTables)
            {
                if (booth == null || !unique.Add(booth) || booth.gameObject.scene != gameObject.scene ||
                    !booth.TryGetComponent<FastFoodTable>(out var table))
                { problem = "Every dining table must be registered once and belong to Lobby2."; break; }
                if (!table.ValidateAuthoring(out problem)) break;
            }
            foreach (var point in waitingPoints)
                if (point == null) { problem = "A paid waiting point is missing."; break; }
        }
        if (problem == null)
        {
            int kiosks = 0;
            var queues = new HashSet<TakeoutQueueManager>();
            var flows = new HashSet<TakeoutFlowManager>();
            foreach (var station in serviceStations)
            {
                if (station == null || station.gameObject.scene != gameObject.scene || station.Queue == null ||
                    station.Flow == null || station.StaffApproach == null || !queues.Add(station.Queue) || !flows.Add(station.Flow))
                { problem = "Each station needs a unique queue and flow and an authored approach."; break; }
                if (station.IsKiosk) kiosks++;
                if (!station.Queue.ValidateAuthoring(out problem)) break;
            }
            if (problem == null && kiosks != 2) problem = "Use two kiosks and two cashier counters.";
            foreach (var point in entranceRoute)
                if (point == null) { problem = "An entrance route point is missing."; break; }
        }
        return problem == null;
    }

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
        if (group == null || customers.Contains(group)) return true;
        if (!acceptingCustomers) { Destroy(group.gameObject); return true; }
        if (counterQueue == null || counterFlow == null || kitchen == null)
        {
            Debug.LogError("Lobby2 Fast Food Services has missing scene references.", this);
            group.FailFastFoodService("Counter service is unavailable.");
            return true;
        }
        group.ConfigureFastFood(this, Random.value >= takeoutChance);
        customers.Add(group);
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
        StartCoroutine(AdmitCustomer(group));
        return true;
    }

    public bool CanSettle(CustomerGroup group) => Operational && group != null && group.FastFood == this
        && acceptingCustomers && StationFor(group)?.Queue.CurrentFront == group
        && !group.FastFoodPaid && !settled.Contains(group) && StationFor(group)?.Flow.ActiveGroup == group
        && StationFor(group).Flow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForPayment
        && group.CurrentTakeoutQueueState == CustomerGroup.TakeoutQueueState.AtOrderPoint
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
        StationFor(group)?.Queue.Remove(group); // Detach without sending this paid customer to the exit.
        StationFor(group)?.Flow.ForceRelease(group);
        paid.Add(group);
        group.FastFoodSelfPickup = group.FastFoodDineIn && Random.value < selfPickupChance;
        PlaceInWaitingArea(group);
        if (!group.FastFoodDineIn) SubmitKitchen(group);
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
        customers.RemoveWhere(group => group == null);
        settled.RemoveWhere(group => group == null);
        refunded.RemoveWhere(group => group == null);
        readyBags.RemoveWhere(group => group == null || !paid.Contains(group));
        foreach (var group in new List<CustomerGroup>(waitingSlots.Keys))
            if (group == null || !paid.Contains(group) || group.assignedBooth != null) waitingSlots.Remove(group);
        foreach (var group in new List<CustomerGroup>(cardChoices.Keys))
            if (group == null || group.FastFoodPaid) cardChoices.Remove(group);
        foreach (var group in new List<CustomerGroup>(stationOwners.Keys))
            if (group == null) stationOwners.Remove(group);
        // Oldest compatible paid group gets the next seat. Reserve synchronously.
        foreach (CustomerGroup group in paid.ToArray())
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
        StationFor(group)?.Queue.Remove(group);
        StationFor(group)?.Flow.ForceRelease(group);
        OrderChecklistUI.Instance?.DismissUnavailableOrder(group);
        CashierRegisterUI.Instance?.DismissFastFoodPayment(group);
        readyWait.Remove(group);
        if (currentCard != null && currentCard.TargetGroup == group)
        {
            CardPaymentUI.Instance?.DismissFastFoodPayment(group);
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
        if (group == null || group.FastFood != this || !group.FastFoodPaid || group.FastFoodWasServed ||
            group.currentOrderNumber != order || !paid.Contains(group) || !readyOrders.Add(order)) return;
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
        if (readyBags.Add(group)) StartCoroutine(CollectTakeaway(group));
        return true;
    }

    private IEnumerator CollectTakeaway(CustomerGroup group)
    {
        // BagReady runs during kitchen spawning. Resolve the completed item next frame.
        yield return null;
        var bag = System.Array.Find(FindObjectsByType<TakeoutBagInteractable>(FindObjectsSortMode.None),
            item => item.TargetGroup == group);
        if (bag == null || customerPickupPoint == null) yield break;
        group.MoveToTakeoutPoint(customerPickupPoint.position);
        float deadline = Time.time + pickupTravelTimeout;
        while (group != null && bag != null && !group.HasReachedTakeoutPoint(customerPickupPoint.position, 1.5f)
            && Time.time < deadline) yield return null;
        if (group != null && bag != null && IsBagReady(group) &&
            group.HasReachedTakeoutPoint(customerPickupPoint.position, 1.5f))
        {
            // Respect an in-flight manager pickup. Cancellation releases the claim
            // and permits the waiting customer to finish collecting their order.
            while (group != null && bag != null && IsBagReady(group) && RestaurantTaskClaim.IsClaimedByPlayer(bag))
                yield return null;
            if (group != null && bag != null && IsBagReady(group)) bag.TryCustomerCollect(group);
        }
        // A blocked route leaves the normal manager pickup/delivery available.
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
        (StationFor(group)?.Queue ?? counterQueue).ReleaseGroup(group);
    }

    public void FinishClosing()
    {
        if (!Operational || MultiplayerDayBridge.IsActive) return;
        acceptingCustomers = false;
        // The existing closing grace period is over. Settle failed, unserved
        // prepaid orders before the daily finance result is captured.
        foreach (var group in new List<CustomerGroup>(customers))
            if (group != null && !group.FastFoodWasServed
                && group.state != CustomerGroup.GroupState.Leaving
                && group.state != CustomerGroup.GroupState.UnhappyLeft
                && group.state != CustomerGroup.GroupState.AngryLeft)
                group.FailFastFoodService("The restaurant closed before this order was served.");
        ResetServiceObjects();
    }

    public void RequestPaymentAfterReview(CustomerGroup group)
    {
        if (!CanSettle(group) || !RestaurantTaskClaim.TryClaimPlayer(group)) return;
        StartCoroutine(OpenAfterReview(group, serviceGeneration));
    }
    public void RequestCounterPayment(CustomerGroup group)
    {
        if (!CanSettle(group)) return;
        var mover = RoleManager.Instance?.GetActivePlayerMovement();
        var cashierApproach = StationFor(group)?.StaffApproach;
        if (mover == null || cashierApproach == null) return;
        if (!HygieneManager.HandsEmpty(mover) || !RestaurantTaskClaim.TryClaimPlayer(group)) return;
        uint generation = serviceGeneration;
        mover.UI_MoveToAction(cashierApproach, 2.25f, () =>
        {
            if (generation == serviceGeneration && !OpenCounterPayment(group)) CancelCounterPayment(group);
        }, () => CancelCounterPayment(group));
    }
    private IEnumerator OpenAfterReview(CustomerGroup group, uint generation)
    {
        // The notepad must close before registering the cashier as the active modal.
        yield return null;
        if (generation != serviceGeneration) yield break;
        if (!CanSettle(group) || !OpenCounterPayment(group)) CancelCounterPayment(group);
    }
    public bool OpenCounterPayment(CustomerGroup group)
    {
        if (!CanSettle(group)) return false;
        var cashierApproach = StationFor(group)?.StaffApproach;
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

    public void BeginServiceDay()
    {
        if (!Operational || MultiplayerDayBridge.IsActive) return;
        ResetServiceObjects();
        acceptingCustomers = true;
    }

    private void ResetServiceObjects()
    {
        acceptingCustomers = false;
        serviceGeneration++;
        StopAllCoroutines();
        // Stopped kitchen coroutines may run completion/failure callbacks. They
        // must see no pending customers while the old day is being dismantled.
        paid.Clear();
        // Stop jobs before destroying their targets, and restrict teardown to Lobby2.
        foreach (var staff in SceneObjects<AutonomousStaffBot>()) staff.ResetFastFoodDay();
        foreach (var trolley in SceneObjects<BotTrolleyCarrier>()) trolley.ResetFastFoodDay();
        foreach (var mover in SceneObjects<PlayerMovement>())
            if (mover.Agent != null) mover.CancelLockedTask();
        foreach (var group in SceneObjects<CustomerGroup>())
        {
            group.ServiceOutcomeReported -= OnOutcome;
            OrderChecklistUI.Instance?.DismissUnavailableOrder(group);
            CashierRegisterUI.Instance?.DismissFastFoodPayment(group);
            CardPaymentUI.Instance?.DismissFastFoodPayment(group);
            group.StopAllCoroutines();
            RestaurantTaskClaim.Complete(group);
            group.gameObject.SetActive(false);
            Destroy(group.gameObject);
        }
        foreach (var hands in SceneObjects<WaiterHands>())
        { hands.ClearTicket(); hands.ClearBill(); hands.ClearMoney(); hands.ClearTray(); }
        foreach (var hands in SceneObjects<BusserHands>()) hands.ClearTray();
        if (TakeoutBagInteractable.HeldBag != null && TakeoutBagInteractable.HeldBag.gameObject.scene == gameObject.scene)
            TakeoutBagInteractable.ClearHeldBag(false);
        DestroyItems<FoodTray>(); DestroyItems<TakeoutBagInteractable>(); DestroyItems<MoneyPickup>();
        if (diningTables != null)
            foreach (var table in diningTables)
                if (table != null) table.ResetFastFoodDay();
        if (serviceStations != null) foreach (var station in serviceStations)
        { station?.Queue?.ResetFastFoodDay(); station?.Flow?.ResetFastFoodDay(); }
        stationOwners.Clear();
        kitchen?.ResetFastFoodDay();
        customers.Clear(); readyBags.Clear(); readyOrders.Clear(); settled.Clear();
        waitingSlots.Clear(); cardChoices.Clear(); refunded.Clear(); readyWait.Clear();
        currentCard = null; nextSeatCheck = 0f;
    }

    private IEnumerable<T> SceneObjects<T>() where T : Component
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var item in root.GetComponentsInChildren<T>(true)) yield return item;
    }

    private void DestroyItems<T>() where T : Component
    {
        foreach (var item in SceneObjects<T>())
        {
            RestaurantTaskClaim.Complete(item);
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }
    }
}
