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
    private readonly Dictionary<CustomerGroup, FastFoodServiceStation> stationOwners = new();
    public IReadOnlyList<FastFoodServiceStation> ServiceStations => serviceStations;
    public FastFoodServiceStation StationFor(CustomerGroup group) => group != null &&
        stationOwners.TryGetValue(group, out var station) ? station : null;
    [SerializeField, Min(10f)] private float seatWaitSeconds = 120f;
    [SerializeField, Min(10f)] private float closingServiceSeconds = 30f;
    [SerializeField, Min(5f)] private float closingExitSeconds = 20f;
    [SerializeField] private Transform[] customerPickupSlots;
    private readonly Dictionary<CustomerGroup, Transform> pickupReservations = new();
    public bool HasPaidWaitingSpace => waitingPoints != null && waitingSlots.Count < waitingPoints.Length;
    public Transform ReservedPickupFor(CustomerGroup group) => group != null && pickupReservations.TryGetValue(group, out var point) ? point : null;
    public bool TryReservePickup(CustomerGroup group, out Transform point)
    {
        point = ReservedPickupFor(group);
        if (point != null) return true;
        if (group == null || group.FastFood != this || group.IsFastFoodLeaving || customerPickupSlots == null) return false;
        foreach (var slot in customerPickupSlots)
            if (slot != null && !pickupReservations.ContainsValue(slot))
            { pickupReservations[group] = point = slot; return true; }
        return false;
    }
    public void ReleasePickup(CustomerGroup group) { if (!ReferenceEquals(group, null)) pickupReservations.Remove(group); }
    private readonly Dictionary<CustomerGroup, float> seatWait = new();
    private bool serviceAvailable;
    public float ClosingServiceSeconds => closingServiceSeconds;
    public float ClosingExitSeconds => closingExitSeconds;
    public int ActiveCustomerCount
    {
        get { int count = 0; foreach (var g in customers) if (g != null && g.isActiveAndEnabled) count++; return count; }
    }
    private int StationLoad(FastFoodServiceStation station)
    {
        int count = 0;
        foreach (var pair in stationOwners)
            if (pair.Value == station && pair.Key != null && !pair.Key.FastFoodPaid && !pair.Key.IsFastFoodLeaving) count++;
        return count;
    }
    public bool CanAdmitCustomer => acceptingCustomers && ChooseStation(false) != null;

    private FastFoodServiceStation ChooseStation(bool countersOnly)
    {
        FastFoodServiceStation chosen = null;
        if (serviceStations == null) return null;
        // Shortest queue; rotate equal-length choices rather than favouring counter one.
        int start = Random.Range(0, serviceStations.Length);
        for (int i = 0; i < serviceStations.Length; i++)
        {
            var station = serviceStations[(start + i) % serviceStations.Length];
            if (station == null || !station.isActiveAndEnabled || !station.IsUnlocked || station.Queue == null ||
                station.Flow == null || (countersOnly && station.IsKiosk)) continue;
            int load = StationLoad(station);
            if (load >= 1 + station.Queue.QueuePoints.Count) continue;
            if (chosen == null || load < StationLoad(chosen)) chosen = station;
        }
        return chosen;
    }
    [Header("Customer choices")]
    [SerializeField, Range(0f, 1f)] private float takeoutChance = .3f;
    [Tooltip("Non-pink dine-in groups requesting table delivery. Pink dine-in groups always request delivery.")]
    [SerializeField, Range(0f, 1f)] private float tableDeliveryChance = .3f;
    [Header("Timing (gameplay seconds)")]
    [SerializeField, Min(.1f)] private float seatCheckInterval = .5f;
    [SerializeField, Min(5f)] private float pickupTravelTimeout = 20f;
    [Tooltip("Maximum wait after the food is ready. Waiting for a seat or kitchen cleaning does not use this time.")]
    [SerializeField, Min(30f)] private float readyFoodWaitSeconds = 120f;
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
    public string ServiceSummary
    {
        get
        {
            if (!serviceAvailable) return "Prepare the menu, stock and staff at the office computer.";
            var lines = new System.Text.StringBuilder();
            int counter = 0, kiosk = 0;
            foreach (var station in serviceStations)
            {
                if (station == null || station.Queue == null) continue;
                string label = station.IsKiosk ? "Kiosk " + (++kiosk) : "Counter " + (++counter);
                if (!station.IsUnlocked) { lines.Append(label).AppendLine(": Locked - Computer > Equipment"); continue; }
                lines.Append(label).Append(": ").Append(StationLoad(station)).Append(" / ")
                    .Append(1 + station.Queue.QueuePoints.Count).Append("   ");
                if (station.IsKiosk) lines.Append(Mathf.RoundToInt(station.ServiceProgress * 100f)).Append("%");
                lines.AppendLine();
            }
            lines.Append("Finding seats: ").Append(WaitingForSeatCount).Append(" | Collecting: ").Append(pickupReservations.Count);
            return lines.ToString();
        }
    }
    public int WaitingForSeatCount => paid.FindAll(g => g != null && g.FastFoodDineIn && g.assignedBooth == null).Count;

    public bool ValidateServiceSetup(out string problem)
    {
        problem = null;
        if (counterQueue == null || counterFlow == null || kitchen == null || cashierApproach == null ||
            customerPickupPoint == null || customerExit == null || waitingPoints == null || waitingPoints.Length == 0)
            problem = "Assign all Fast Food service and waiting-point references.";
        else if (serviceStations == null || serviceStations.Length != 4)
            problem = "Author two counters and two kiosks with indoor queues.";
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
                if (station.CompanionPoints == null || station.CompanionPoints.Length != 3 ||
                    System.Array.Exists(station.CompanionPoints, point => point == null))
                { problem = "Each station needs three authored companion waiting points."; break; }
                if (station.IsKiosk) kiosks++;
                if (!station.Queue.ValidateAuthoring(out problem)) break;
            }
            if (problem == null && kiosks != 2) problem = "Use two kiosks and two cashier counters.";
        }
        if (problem == null)
        {
            foreach (var points in new[] { customerPickupSlots })
            {
                if (points == null || points.Length == 0) { problem = "Author the customer pickup spaces."; break; }
                var unique = new HashSet<Transform>();
                foreach (var point in points)
                    if (point == null || point.gameObject.scene != gameObject.scene || !unique.Add(point))
                    { problem = "Waiting and pickup anchors must be distinct and belong to Lobby2."; break; }
            }
        }
        if (problem == null)
        {
            var bindings = GetComponent<FastFoodLobbyAuthoring>();
            if (bindings == null) problem = "Assign the Lobby2 room computer and storage bindings.";
            else bindings.ValidateAuthoring(out problem);
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
        StartCoroutine(BindProgressionVisibility());
    }
    private EquipmentManager visibilityEquipment;

    private IEnumerator BindProgressionVisibility()
    {
        // The coordinator stays active so purchased, inactive children can return.
        while (EquipmentManager.Instance == null ||
               GameSaveManager.Instance != null && !GameSaveManager.Instance.HasCompletedInitialLoad)
            yield return null;
        visibilityEquipment = EquipmentManager.Instance;
        visibilityEquipment.PurchasesChanged += RefreshProgressionVisibility;
        RefreshProgressionVisibility();
    }

    public void RefreshProgressionVisibility()
    {
        if (FastFoodProgressionSettings.Current == null) return;
        if (diningTables != null)
            foreach (var booth in diningTables)
            {
                if (booth == null) continue;
                var table = booth.GetComponent<FastFoodTable>();
                if (table != null) booth.gameObject.SetActive(table.AvailableSeats > 0);
            }
        if (serviceStations != null)
            foreach (var station in serviceStations)
                if (station != null) station.gameObject.SetActive(station.IsUnlocked);
    }
    private void OnDisable()
    {
        if (kitchen != null) kitchen.OrderFinished -= OnKitchenFinished;
        if (visibilityEquipment != null) visibilityEquipment.PurchasesChanged -= RefreshProgressionVisibility;
        visibilityEquipment = null;
        StopAllCoroutines();
    }

    internal bool ShouldSpawnTakeout(float roll) => roll <
        (FastFoodProgressionSettings.Current != null ? FastFoodProgressionSettings.Current.takeoutChance : takeoutChance);

    public bool Route(CustomerGroup group, bool? dineIn = null)
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
        var station = ChooseStation(false);
        // The spawner checks capacity before creating a group; guard direct callers too.
        if (station == null) { Destroy(group.gameObject); return true; }
        stationOwners[group] = station;
        group.ConfigureFastFood(this, dineIn ?? !ShouldSpawnTakeout(Random.value));
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
        // Walk straight to the reserved indoor slot; NavMesh handles the doorway.
        station.Queue.Enqueue(group);
        return true;
    }

    public bool CanSettle(CustomerGroup group) => Operational && group != null && group.FastFood == this
        && serviceAvailable && HasPaidWaitingSpace && StationFor(group)?.Queue.CurrentFront == group
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
        group.FastFoodSelfPickup = group.FastFoodDineIn &&
            group.CurrentCustomerType != CustomerGroup.CustomerType.Pink && Random.value >=
            (FastFoodProgressionSettings.Current != null ? FastFoodProgressionSettings.Current.tableDeliveryChance : tableDeliveryChance);
        PlaceInWaitingArea(group);
        if (group.FastFoodDineIn) seatWait[group] = 0f;
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
        group.MoveToTakeoutPoint(point.position - point.forward * overflow * waitingSpacing,
            point.forward, 1.1f, 1f);
    }

    private void Update()
    {
        if (!Operational || Time.time < nextSeatCheck) return;
        nextSeatCheck = Time.time + seatCheckInterval;
        foreach (var group in new List<CustomerGroup>(readyWait.Keys))
        {
            if (group == null || group.HasReceivedCurrentFastFoodOrder || !paid.Contains(group))
            { readyWait.Remove(group); continue; }
            if (HygieneManager.KitchenPaused) continue;
            if (group.CanCollectFastFoodTray)
            {
                var prepared = kitchen.GetPreparedResult(group.currentOrderNumber);
                if (prepared != null && prepared.GetComponent<FoodTrayInteractable>()?.IsDeliveryPickable == true &&
                    TryReservePickup(group, out var pickup))
                    group.StartCoroutine(group.CollectFastFoodTray(prepared, pickup, pickupTravelTimeout));
            }
            // Starting the coroutine can synchronously reject an invalid order and remove it.
            if (!readyWait.ContainsKey(group)) continue;
            // Seating has its own deadline; an active pickup has bounded travel deadlines.
            if (group.FastFoodAwaitingSeat || group.IsCollectingFastFoodTray) continue;
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
        foreach (var group in new List<CustomerGroup>(pickupReservations.Keys))
            if (group == null || group.IsFastFoodLeaving) pickupReservations.Remove(group);
        foreach (var group in new List<CustomerGroup>(stationOwners.Keys))
        {
            if (group == null) { stationOwners.Remove(group); continue; }
            var station = stationOwners[group];
            if (!group.FastFoodPaid && !group.IsFastFoodLeaving &&
                (station == null || !station.isActiveAndEnabled || station.Queue == null || station.Flow == null))
                group.FailFastFoodService("The ordering station is no longer available.");
        }
        foreach (var group in new List<CustomerGroup>(seatWait.Keys))
        {
            if (group == null || !paid.Contains(group) || !group.FastFoodAwaitingSeat) { seatWait.Remove(group); continue; }
            seatWait[group] += seatCheckInterval;
            if (seatWait[group] >= seatWaitSeconds)
            { seatWait.Remove(group); group.FailFastFoodService("A suitable table was not available in time."); }
        }
        // A prepaid remake reuses the kitchen and never creates a waiter ticket or second bill.
        foreach (var group in paid.ToArray())
            if (group != null && group.NeedsFastFoodRemake) group.TryConfirmFastFoodRemake();
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
        if (group == null || group.FastFood != this || !group.FastFoodPaid) return;
        seatWait.Remove(group);
        // Cooking was already accepted at payment. Kitchen releases its prepared result after seating.
    }
    private void OnOutcome(CustomerGroup group, CustomerGroup.FinalResult result)
    {
        if (group == null) return;
        StationFor(group)?.Queue.Remove(group);
        StationFor(group)?.Flow.ForceRelease(group);
        OrderChecklistUI.Instance?.DismissUnavailableOrder(group);
        CashierRegisterUI.Instance?.DismissFastFoodPayment(group);
        readyWait.Remove(group);
        seatWait.Remove(group);
        ReleasePickup(group);
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
    public void SubmitKitchen(CustomerGroup group)
    {
        if (group == null || group.FastFood != this || !group.FastFoodPaid || group.IsFastFoodLeaving) return;
        if (kitchen.HasAcceptedOrder(group.currentOrderNumber)) return;
        if (kitchen.ProcessOrder(group)) GameDayManager.Instance?.RegisterOrderProcessed();
        else group.FailFastFoodService("The kitchen could not accept this order.");
    }
    private void OnKitchenFinished(CustomerGroup group, int order, bool success)
    {
        if (group == null || group.FastFood != this || !group.FastFoodPaid || group.HasReceivedCurrentFastFoodOrder ||
            group.currentOrderNumber != order || !paid.Contains(group) || !readyOrders.Add(order)) return;
        if (!success) { group.FailFastFoodService("The kitchen could not prepare this order."); return; }
        readyWait[group] = 0f;
        // Update is the single scheduler, after the entire party is seated.
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
        Transform pickup = null;
        float claimDeadline = Time.time + readyFoodWaitSeconds;
        while (group != null && !group.IsFastFoodLeaving && !TryReservePickup(group, out pickup) && Time.time < claimDeadline)
            yield return null;
        if (pickup == null || group == null || group.IsFastFoodLeaving) yield break;
        try
        {
            // Takeout parties collect together; leaving a companion in the
            // waiting area also leaves their shared thought bubble between them.
            group.MoveToTakeoutPoint(pickup.position, pickup.forward, 1.1f, 1f);
            float deadline = Time.time + pickupTravelTimeout;
            while (group != null && !group.IsFastFoodLeaving && bag != null &&
                !group.HasReachedTakeoutPoint(pickup.position) && Time.time < deadline) yield return null;
            if (group == null || group.IsFastFoodLeaving || !group.HasReachedTakeoutPoint(pickup.position)) yield break;
            if (group != null && bag != null && IsBagReady(group)) bag.TryCustomerCollect(group);
        }
        finally { ReleasePickup(group); }
        // Ready-food timeout retains bounded failure/refund if the route fails.
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

    public void StopAdmissions()
    {
        acceptingCustomers = false;
    }
    public void BeginClosingExit()
    {
        serviceAvailable = false;
        foreach (var group in new List<CustomerGroup>(customers))
            if (group != null && !group.IsFastFoodLeaving) group.EndFastFoodServiceAtClosing();
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
        serviceAvailable = true;
    }

    private void ResetServiceObjects()
    {
        acceptingCustomers = false;
        serviceAvailable = false;
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
        seatWait.Clear(); pickupReservations.Clear();
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
