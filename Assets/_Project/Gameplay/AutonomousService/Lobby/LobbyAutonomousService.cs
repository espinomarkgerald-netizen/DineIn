using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Casual Dining's temporary no-player runtime. It assigns service tasks to the
/// moving staff while CustomerGroup, KitchenManager, BillManager, and the existing
/// hands components retain ownership of restaurant state. The cashier stays put.
/// </summary>
[DefaultExecutionOrder(-100)]
public class LobbyAutonomousService : MonoBehaviour
{
    [Header("Task Timing")]
    [SerializeField] private float greetingSeconds = 0.8f;
    [SerializeField] private float tableServiceSeconds = 0.75f;
    [SerializeField] private float counterServiceSeconds = 0.7f;
    [SerializeField] private float cleaningSeconds = 1.2f;
    [SerializeField] private float idlePollSeconds = 0.2f;

    [Header("Customer Clearance")]
    [SerializeField, Min(1f)] private float hostCustomerClearance = 2.2f;
    [SerializeField, Min(1f)] private float takeoutWaiterClearance = 3f;

    [Header("Waiter Service Distance")]
    [SerializeField, Min(0.35f)] private float boothServiceDistance = 2.75f;
    [SerializeField, Min(0.35f)] private float counterServiceDistance = 1.75f;
    [SerializeField, Min(0.35f)] private float pickupServiceDistance = 1.75f;
    [SerializeField, Min(0.35f)] private float takeoutServiceDistance = 1.75f;

    private AutonomousStaffBot host;
    private AutonomousStaffBot waiter;
    private AutonomousStaffBot busser;
    private RoleManager roleManager;
    private LobbyLineManager lineManager;
    private KitchenManager kitchenManager;
    private BillManager billManager;
    private TakeoutFlowManager takeoutFlow;
    private SinkInteractable sink;
    private Transform cashierStation;
    private readonly HashSet<CustomerGroup> billDeliveredGroups = new HashSet<CustomerGroup>();

    private void Awake()
    {
        roleManager = FindFirstObjectByType<RoleManager>(FindObjectsInactive.Include);
        DisableManualRoleControl();
    }

    private void Start()
    {
        // This component is added during GameDayManager.Awake. Repeat the shutdown
        // after every scene Awake has run so RoleManager cannot re-enable controls.
        DisableManualRoleControl();
        ResolveSceneReferences();
        StartCoroutine(ServiceLoop());
    }

    private void OnDestroy()
    {
        if (takeoutFlow != null)
            takeoutFlow.SetAutomatedService(false);
    }

    private void ResolveSceneReferences()
    {
        lineManager = FindFirstObjectByType<LobbyLineManager>();
        kitchenManager = FindFirstObjectByType<KitchenManager>();
        billManager = FindFirstObjectByType<BillManager>();
        takeoutFlow = FindFirstObjectByType<TakeoutFlowManager>();
        sink = FindFirstObjectByType<SinkInteractable>();
        takeoutFlow?.SetAutomatedService(true);

        GameObject hostObject = ResolveRoleObject(roleManager != null ? roleManager.host : null, "Host");
        GameObject waiterObject = ResolveRoleObject(roleManager != null ? roleManager.waiter : null, "Waiter");
        GameObject cashierObject = ResolveRoleObject(roleManager != null ? roleManager.cashier : null, "Cashier");
        GameObject busserObject = ResolveRoleObject(roleManager != null ? roleManager.busser : null, "Busser");

        host = AddBot(hostObject, FindStation(null, "HostHomePoint"), 30);
        waiter = AddBot(waiterObject, FindStation(null, "WaiterHomePoint"), 80);
        busser = AddBot(busserObject, FindStation(null, "BusserHomePoint"), 50);
        CashierBoothInteractable cashierBooth = FindFirstObjectByType<CashierBoothInteractable>();
        cashierStation = cashierBooth != null
            ? cashierBooth.StandPoint
            : FindStation(cashierObject != null ? cashierObject.transform : null, "CashierStation");
        KeepCharacterStationary(cashierObject);
        ConfigureIdlePresentation();

        if (host == null || waiter == null || cashierObject == null || busser == null)
            Debug.LogError("[LobbyAutonomousService] Lobby1 role references must contain one Host, Waiter, Cashier, and Busser.", this);
    }

    private void DisableManualRoleControl()
    {
        if (roleManager != null)
            roleManager.DisablePlayerRoleControl();

        PlayerMovement[] movements = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < movements.Length; i++)
            movements[i].enabled = false;

        RoleBasedAssignController[] assigners = FindObjectsByType<RoleBasedAssignController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < assigners.Length; i++)
            assigners[i].enabled = false;

        HostAssignController[] hostAssigners = FindObjectsByType<HostAssignController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < hostAssigners.Length; i++)
            hostAssigners[i].enabled = false;

        RoleIndicator[] indicators = FindObjectsByType<RoleIndicator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < indicators.Length; i++)
        {
            indicators[i].SetSelected(false);
            indicators[i].enabled = false;
        }
    }

    private void ConfigureIdlePresentation()
    {
        Booth[] booths = FindObjectsByType<Booth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<Transform> boothTargets = new List<Transform>(booths.Length);
        for (int i = 0; i < booths.Length; i++)
        {
            if (booths[i] == null)
                continue;

            boothTargets.Add(booths[i].tableLookTarget != null
                ? booths[i].tableLookTarget
                : booths[i].transform);
        }

        List<Transform> hostTargets = new List<Transform>(boothTargets);
        if (lineManager != null)
            hostTargets.Add(lineManager.transform);

        List<Transform> waiterTargets = new List<Transform>(boothTargets);
        if (kitchenManager != null)
            waiterTargets.Add(kitchenManager.transform);
        if (cashierStation != null)
            waiterTargets.Add(cashierStation);

        List<Transform> busserTargets = new List<Transform>(boothTargets);
        if (sink != null && sink.StandPoint != null)
            busserTargets.Add(sink.StandPoint);

        host?.ConfigureIdlePresentation(hostTargets.ToArray());
        waiter?.ConfigureIdlePresentation(waiterTargets.ToArray());
        busser?.ConfigureIdlePresentation(busserTargets.ToArray());
    }

    private IEnumerator ServiceLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(idlePollSeconds);

        while (true)
        {
            if (GameDayManager.Instance != null && GameDayManager.Instance.ServiceActive)
            {
                TryStartHostTask();
                TryStartWaiterTask();
                TryStartBusserTask();
            }

            yield return wait;
        }
    }

    private void TryStartHostTask()
    {
        if (host == null || host.IsBusy || lineManager == null)
            return;

        CustomerGroup group = lineManager.GetFrontOfLine();
        if (group == null || group.HasBeenAssigned || group.state != CustomerGroup.GroupState.Waiting)
            return;

        Booth booth = FindAvailableBooth(group.Size);
        if (booth != null)
            host.StartTask(SeatGroup(group, booth));
    }

    private void TryStartWaiterTask()
    {
        WaiterHands hands = WaiterHands.Instance;
        if (waiter == null || waiter.IsBusy || hands == null)
            return;

        if (hands.HasMoney)
        {
            waiter.StartTask(CompleteHeldPaymentAtCashier(hands));
            return;
        }

        if (TakeoutBagInteractable.HasHeldBag)
        {
            waiter.StartTask(DeliverTakeoutBag(TakeoutBagInteractable.HeldBag));
            return;
        }

        if (hands.HasTicket)
        {
            CustomerGroup ticketGroup = hands.holdingTicketFor;
            if (ticketGroup != null && !ticketGroup.IsTakeout &&
                ticketGroup.state == CustomerGroup.GroupState.OrderTaken)
            {
                waiter.StartTask(ProcessDineInOrderAtCashier(ticketGroup));
            }
            else
            {
                hands.ClearTicket();
                waiter.SetCarrying(false);
            }

            return;
        }

        if (hands.HasTray)
        {
            waiter.StartTask(DeliverFood(hands.holdingTray));
            return;
        }

        if (hands.HasBill)
        {
            CustomerGroup heldBillGroup = hands.holdingBillFor;
            if (heldBillGroup != null && heldBillGroup.state == CustomerGroup.GroupState.NeedsBill)
                waiter.StartTask(DeliverBill(heldBillGroup));
            else
            {
                hands.ClearBill();
                waiter.SetCarrying(false);
            }

            return;
        }

        if (!AreWaiterHandsFree(hands))
            return;

        TakeoutBagInteractable takeoutBag = FindReadyTakeoutBag();
        if (takeoutBag != null)
        {
            waiter.StartTask(DeliverTakeoutBag(takeoutBag));
            return;
        }

        CustomerGroup takeoutPaymentGroup = FindTakeoutPaymentTarget();
        if (takeoutPaymentGroup != null)
        {
            waiter.StartTask(CompleteTakeoutPaymentAtCashier(takeoutPaymentGroup));
            return;
        }

        CustomerGroup takeoutOrderGroup = FindTakeoutOrderTarget();
        if (takeoutOrderGroup != null)
        {
            waiter.StartTask(TakeTakeoutOrder(takeoutOrderGroup));
            return;
        }

        FoodTray readyTray = FindReadyDeliveryTray();
        if (readyTray != null)
        {
            waiter.StartTask(DeliverFood(readyTray));
            return;
        }

        MoneyPickup payment = FindPaymentPickup();
        if (payment != null)
        {
            waiter.StartTask(DeliverPaymentToCashier(payment));
            return;
        }

        CustomerGroup billGroup = FindBillDeliveryTarget();
        if (billGroup != null)
        {
            waiter.StartTask(DeliverBill(billGroup));
            return;
        }

        CustomerGroup orderGroup = FindGroupInState(CustomerGroup.GroupState.ReadyToOrder);
        if (orderGroup != null)
            waiter.StartTask(TakeOrder(orderGroup));
    }

    private void TryStartBusserTask()
    {
        if (busser == null || busser.IsBusy || BusserHands.Instance == null)
            return;

        FoodTray tray = FindCleanupTray();
        if (tray != null && sink != null)
        {
            busser.StartTask(CleanTrayAtSink(tray));
            return;
        }

        Booth dirtyBooth = FindDirtyBooth();
        if (dirtyBooth != null)
            busser.StartTask(CleanBooth(dirtyBooth));
    }

    private IEnumerator SeatGroup(CustomerGroup group, Booth booth)
    {
        Vector3 groupCenter = GetGroupCenter(group);
        yield return host.MoveTo(GetGroupInteractionPosition(
            group,
            host.transform.position,
            hostCustomerClearance));

        if (!host.LastMoveSucceeded)
            yield break;

        yield return host.FaceTowards(groupCenter);
        yield return host.WorkFor(greetingSeconds);

        if (group == null || booth == null || group.HasBeenAssigned || !booth.IsAvailableFor(group.Size))
            yield break;

        yield return host.MoveTo(booth.GetNavigableApproachPosition());
        yield return host.FaceTowards(GetGroupCenter(group));
        yield return host.WorkFor(greetingSeconds);

        if (group != null && booth != null && !group.HasBeenAssigned && booth.IsAvailableFor(group.Size))
            group.AssignToBooth(booth);
    }

    private IEnumerator TakeOrder(CustomerGroup group)
    {
        if (group == null || group.assignedBooth == null)
            yield break;

        yield return waiter.MoveWithin(
            group.assignedBooth.GetNavigableApproachPosition(),
            boothServiceDistance);
        if (!waiter.LastMoveSucceeded)
            yield break;

        yield return waiter.FaceTowards(GetGroupCenter(group));
        yield return waiter.WorkFor(tableServiceSeconds);

        if (group == null || group.state != CustomerGroup.GroupState.ReadyToOrder || WaiterHands.Instance == null)
            yield break;

        group.TakeOrderFromWaiter(group.chosenFood, group.chosenDrink);
        WaiterHands.Instance.holdingTicketFor = group;
        waiter.SetCarrying(true);

        yield return ProcessDineInOrderAtCashier(group);
    }

    private IEnumerator ProcessDineInOrderAtCashier(CustomerGroup group)
    {
        WaiterHands hands = WaiterHands.Instance;
        if (group == null || hands == null || hands.holdingTicketFor != group ||
            group.state != CustomerGroup.GroupState.OrderTaken)
            yield break;

        if (cashierStation != null)
        {
            yield return waiter.MoveWithin(
                cashierStation.position,
                counterServiceDistance,
                2f);
            if (!waiter.LastMoveSucceeded)
                yield break;
        }

        yield return waiter.WorkFor(counterServiceSeconds);

        if (group != null && group.state == CustomerGroup.GroupState.OrderTaken &&
            hands.holdingTicketFor == group && kitchenManager != null)
        {
            hands.ClearTicket();
            waiter.SetCarrying(false);
            kitchenManager.ProcessOrder(group);
            GameDayManager.Instance?.RegisterOrderProcessed();
        }
    }

    private IEnumerator TakeTakeoutOrder(CustomerGroup group)
    {
        if (!IsTakeoutOrderReady(group) || !AreWaiterHandsFree(WaiterHands.Instance))
            yield break;

        IgnoreWaiterPhysicalCollisions(group);

        yield return waiter.MoveWithin(
            GetGroupInteractionPosition(
                group,
                waiter.transform.position,
                takeoutWaiterClearance),
            takeoutServiceDistance,
            3f);
        if (!waiter.LastMoveSucceeded)
        {
            group?.FailTakeoutService("Waiter could not reach the takeout order point.");
            yield break;
        }

        yield return waiter.FaceTowards(GetGroupCenter(group));
        yield return waiter.WorkFor(tableServiceSeconds);

        WaiterHands hands = WaiterHands.Instance;
        if (!IsTakeoutOrderReady(group) || !AreWaiterHandsFree(hands))
            yield break;

        group.TakeOrderFromWaiter(group.chosenFood, group.chosenDrink);
        if (group == null || group.state != CustomerGroup.GroupState.OrderTaken)
            yield break;

        hands.holdingTicketFor = group;
        waiter.SetCarrying(true);
        yield return CompleteTakeoutPaymentAtCashier(group);
    }

    private IEnumerator CompleteTakeoutPaymentAtCashier(CustomerGroup group)
    {
        WaiterHands hands = WaiterHands.Instance;
        bool carryingTicket = hands != null && hands.holdingTicketFor == group;

        if (!IsTakeoutPaymentReady(group))
        {
            if (carryingTicket)
                hands.ClearTicket();

            waiter.SetCarrying(false);
            yield break;
        }

        if (cashierStation == null)
        {
            if (carryingTicket && hands != null)
                hands.ClearTicket();

            waiter.SetCarrying(false);
            TryCompleteTakeoutPaymentFallback(group, "Cashier stand point is missing.");
            yield break;
        }

        if (cashierStation != null)
        {
            yield return waiter.MoveWithin(
                cashierStation.position,
                counterServiceDistance,
                3f);
            if (!waiter.LastMoveSucceeded)
            {
                if (carryingTicket && hands != null)
                    hands.ClearTicket();

                waiter.SetCarrying(false);
                TryCompleteTakeoutPaymentFallback(group, "Cashier stand point was not reachable.");
                yield break;
            }
        }

        yield return waiter.WorkFor(counterServiceSeconds);

        if (carryingTicket && hands != null)
            hands.ClearTicket();

        waiter.SetCarrying(false);

        CashierRegisterUI register = CashierRegisterUI.Instance;
        if (register == null)
        {
            Debug.LogError("[LobbyAutonomousService] CashierRegisterUI is required for automated takeout payment.", this);
            group?.FailTakeoutService("Cashier register is missing.");
            yield break;
        }

        if (IsTakeoutPaymentReady(group) && !register.CompleteAutomatedPayment(group))
            group.FailTakeoutService("Automated payment validation failed.");
    }

    private void TryCompleteTakeoutPaymentFallback(CustomerGroup group, string reason)
    {
        if (!IsTakeoutPaymentReady(group))
            return;

        CashierRegisterUI register = CashierRegisterUI.Instance;
        if (register != null && register.CompleteAutomatedPayment(group))
        {
            Debug.LogWarning($"[LobbyAutonomousService] Completed {group.name}'s payment through fallback: {reason}", this);
            return;
        }

        group.FailTakeoutService(reason);
    }

    private IEnumerator DeliverTakeoutBag(TakeoutBagInteractable bag)
    {
        CustomerGroup group = bag != null ? bag.TargetGroup : null;
        if (bag == null)
            yield break;

        if (group == null || !group.IsTakeout)
        {
            if (TakeoutBagInteractable.HeldBag == bag)
                TakeoutBagInteractable.ClearHeldBag(true);
            else
                Destroy(bag.gameObject);

            waiter.SetCarrying(false);
            yield break;
        }

        IgnoreWaiterPhysicalCollisions(group);

        if (TakeoutBagInteractable.HeldBag != bag)
        {
            yield return waiter.MoveWithin(
                bag.transform.position,
                pickupServiceDistance,
                3f);
            if (!waiter.LastMoveSucceeded || bag == null)
            {
                AbortTakeoutService(group, bag, "Waiter could not reach the prepared takeout bag.");
                yield break;
            }

            bag.TryPickup();
            if (TakeoutBagInteractable.HeldBag != bag)
            {
                AbortTakeoutService(group, bag, "Waiter could not pick up the prepared takeout bag.");
                yield break;
            }
        }

        waiter.SetCarrying(true);

        if (!IsTakeoutBagDeliveryReady(group))
        {
            TakeoutBagInteractable.ClearHeldBag(true);
            waiter.SetCarrying(false);
            yield break;
        }

        yield return waiter.MoveWithin(
            GetGroupInteractionPosition(
                group,
                waiter.transform.position,
                takeoutWaiterClearance),
            takeoutServiceDistance,
            3f);
        if (!waiter.LastMoveSucceeded || bag == null || group == null)
        {
            AbortTakeoutService(group, bag, "Waiter could not reach the takeout customer for delivery.");
            yield break;
        }

        yield return waiter.FaceTowards(GetGroupCenter(group));
        yield return waiter.WorkFor(tableServiceSeconds);

        if (IsTakeoutBagDeliveryReady(group) && bag.TryDeliverTo(group))
        {
            waiter.SetCarrying(false);
            yield break;
        }

        if (!IsTakeoutBagDeliveryReady(group) && TakeoutBagInteractable.HeldBag == bag)
        {
            TakeoutBagInteractable.ClearHeldBag(true);
            waiter.SetCarrying(false);
            yield break;
        }

        AbortTakeoutService(group, bag, "Takeout bag delivery validation failed.");
    }

    private void AbortTakeoutService(
        CustomerGroup group,
        TakeoutBagInteractable bag,
        string reason)
    {
        if (bag != null)
        {
            if (TakeoutBagInteractable.HeldBag == bag)
                TakeoutBagInteractable.ClearHeldBag(true);
            else
                Destroy(bag.gameObject);
        }

        waiter?.SetCarrying(false);
        group?.FailTakeoutService(reason);
    }

    private IEnumerator DeliverFood(FoodTray tray)
    {
        CustomerGroup group = tray != null ? tray.TargetGroup : null;
        WaiterHands hands = WaiterHands.Instance;
        if (tray == null || group == null || group.assignedBooth == null || hands == null ||
            (hands.HasTray && hands.holdingTray != tray))
            yield break;

        if (!hands.HasTray)
        {
            yield return waiter.MoveWithin(
                tray.transform.position,
                pickupServiceDistance,
                2f);
            if (!waiter.LastMoveSucceeded)
                yield break;

            if (tray == null || group == null || !hands.PickupTray(tray))
                yield break;
        }

        waiter.SetCarrying(true);
        yield return waiter.MoveWithin(
            group.assignedBooth.GetNavigableApproachPosition(),
            boothServiceDistance);
        if (!waiter.LastMoveSucceeded)
            yield break;

        yield return waiter.FaceTowards(GetGroupCenter(group));
        yield return waiter.WorkFor(tableServiceSeconds);

        if (tray == null || group == null || group.assignedBooth == null || hands.holdingTray != tray)
            yield break;

        Transform dropPoint = FindTableFoodSpawn(group.assignedBooth);
        if (dropPoint == null || !hands.TryDeliverTrayTo(group, false))
            yield break;

        tray.transform.SetParent(dropPoint, false);
        tray.transform.localPosition = Vector3.zero;
        tray.transform.localRotation = Quaternion.identity;

        Collider trayCollider = tray.GetComponentInChildren<Collider>(true);
        if (trayCollider != null)
            trayCollider.enabled = true;

        tray.GetComponent<FoodTrayInteractable>()?.NotifyDeliveredToTable();
        group.ReceiveFoodFromWaiter(tray.DeliveredContents);
        waiter.SetCarrying(false);
    }

    private IEnumerator DeliverBill(CustomerGroup group)
    {
        WaiterHands hands = WaiterHands.Instance;
        if (group == null || group.assignedBooth == null || hands == null ||
            (hands.HasBill && hands.holdingBillFor != group))
            yield break;

        if (!hands.HasBill)
        {
            billManager?.RequestBill(group);
            BillPaper bill = billManager != null ? billManager.FindBillForGroup(group) : null;
            while (bill == null && group != null && group.state == CustomerGroup.GroupState.NeedsBill)
            {
                yield return new WaitForSeconds(idlePollSeconds);
                bill = billManager != null ? billManager.FindBillForGroup(group) : null;
            }

            if (bill == null || group == null)
                yield break;

            yield return waiter.MoveWithin(
                bill.StandPoint.position,
                pickupServiceDistance,
                2f);
            if (!waiter.LastMoveSucceeded)
                yield break;

            if (hands.HasBill)
                yield break;

            hands.PickupBillPaper(bill);
            if (!hands.HasBill)
                yield break;
        }

        waiter.SetCarrying(true);
        yield return waiter.MoveWithin(
            group.assignedBooth.GetNavigableApproachPosition(),
            boothServiceDistance);
        if (!waiter.LastMoveSucceeded)
            yield break;

        yield return waiter.FaceTowards(GetGroupCenter(group));
        yield return waiter.WorkFor(tableServiceSeconds);

        if (group != null && group.state == CustomerGroup.GroupState.NeedsBill)
        {
            group.ReceiveBillFromWaiter();
            billDeliveredGroups.Add(group);
            hands.ClearBill();
        }

        waiter.SetCarrying(false);
    }

    private IEnumerator DeliverPaymentToCashier(MoneyPickup payment)
    {
        CustomerGroup group = payment != null ? payment.TargetGroup : null;
        WaiterHands hands = WaiterHands.Instance;
        CashierRegisterUI register = CashierRegisterUI.Instance;

        if (payment == null || group == null || hands == null || register == null || hands.HasMoney ||
            group.state != CustomerGroup.GroupState.NeedsBill || group.assignedBooth == null)
            yield break;

        Booth paymentBooth = group.assignedBooth;
        yield return waiter.MoveWithin(
            paymentBooth.GetNavigableApproachPosition(),
            boothServiceDistance);
        if (!waiter.LastMoveSucceeded)
            yield break;

        yield return waiter.FaceTowards(GetGroupCenter(group));
        yield return waiter.WorkFor(tableServiceSeconds);
        if (payment == null || group == null || hands.HasMoney)
            yield break;

        hands.PickupMoney(payment);
        if (!hands.HasMoney)
            yield break;

        waiter.SetCarrying(true);
        yield return CompleteHeldPaymentAtCashier(hands, register);
    }

    private IEnumerator CompleteHeldPaymentAtCashier(WaiterHands hands, CashierRegisterUI register = null)
    {
        if (hands == null || !hands.HasMoney || hands.holdingMoneyFor == null)
            yield break;

        if (register == null)
            register = CashierRegisterUI.Instance;

        if (register == null)
        {
            Debug.LogError("[LobbyAutonomousService] CashierRegisterUI is required to complete an automated payment.", this);
            yield break;
        }

        waiter.SetCarrying(true);
        if (cashierStation != null)
        {
            yield return waiter.MoveWithin(
                cashierStation.position,
                counterServiceDistance,
                2f);
            if (!waiter.LastMoveSucceeded)
                yield break;
        }

        yield return waiter.WorkFor(counterServiceSeconds);

        CustomerGroup paidGroup = hands.holdingMoneyFor;
        if (register.CompleteAutomatedPayment(paidGroup))
        {
            hands.ClearMoney();
            billDeliveredGroups.Remove(paidGroup);
        }

        waiter.SetCarrying(hands.HasMoney);
    }

    private IEnumerator CleanTrayAtSink(FoodTray tray)
    {
        if (tray == null || BusserHands.Instance == null)
            yield break;

        Booth sourceBooth = tray.GetComponentInParent<Booth>();
        if (sourceBooth == null && tray.TargetGroup != null)
            sourceBooth = tray.TargetGroup.assignedBooth;

        if (sourceBooth == null)
        {
            Debug.LogWarning("[LobbyAutonomousService] Busser cannot collect a tray without its source booth.", tray);
            yield break;
        }

        yield return busser.MoveTo(sourceBooth.GetNavigableApproachPosition());
        if (!busser.LastMoveSucceeded)
            yield break;

        yield return busser.FaceTowards(sourceBooth.transform.position);
        yield return busser.WorkFor(tableServiceSeconds);
        if (tray == null || !BusserHands.Instance.PickupTray(tray))
            yield break;

        busser.SetCarrying(true);
        yield return busser.MoveTo(sink.StandPoint);
        yield return busser.WorkFor(cleaningSeconds);

        BusserHands.Instance.DisposeTray(true);
        GameDayManager.Instance?.RegisterTrayCleaned();
        busser.SetCarrying(false);
    }

    private IEnumerator CleanBooth(Booth booth)
    {
        if (booth == null || !booth.CanCleanMessNow)
            yield break;

        yield return busser.MoveTo(booth.GetNavigableApproachPosition());
        if (!busser.LastMoveSucceeded || booth == null || !booth.CanCleanMessNow)
            yield break;

        Vector3 lookTarget = booth.tableLookTarget != null
            ? booth.tableLookTarget.position
            : booth.transform.position;
        yield return busser.FaceTowards(lookTarget);

        if (!booth.BeginAutomatedMessCleaning())
        {
            Debug.LogWarning($"[LobbyAutonomousService] {booth.name} has no active hold-to-clean UI; mess was left for a retry.", booth);
            yield break;
        }

        while (booth != null && booth.IsDirty && booth.IsAutomatedMessCleaning)
            yield return null;

        if (booth != null && booth.IsDirty)
            booth.CancelAutomatedMessCleaning();
    }

    private Booth FindAvailableBooth(int groupSize)
    {
        Booth[] booths = FindObjectsByType<Booth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < booths.Length; i++)
        {
            if (booths[i] != null && booths[i].IsAvailableFor(groupSize))
                return booths[i];
        }

        return null;
    }

    private CustomerGroup FindGroupInState(CustomerGroup.GroupState state)
    {
        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && !groups[i].IsTakeout && groups[i].state == state)
                return groups[i];
        }

        return null;
    }

    private CustomerGroup FindBillDeliveryTarget()
    {
        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < groups.Length; i++)
        {
            CustomerGroup group = groups[i];
            if (group != null && !group.IsTakeout && group.state == CustomerGroup.GroupState.NeedsBill &&
                !billDeliveredGroups.Contains(group))
                return group;
        }

        return null;
    }

    private CustomerGroup FindTakeoutOrderTarget()
    {
        CustomerGroup group = takeoutFlow != null ? takeoutFlow.ActiveGroup : null;
        return IsTakeoutOrderReady(group) ? group : null;
    }

    private CustomerGroup FindTakeoutPaymentTarget()
    {
        CustomerGroup group = takeoutFlow != null ? takeoutFlow.ActiveGroup : null;
        return IsTakeoutPaymentReady(group) ? group : null;
    }

    private TakeoutBagInteractable FindReadyTakeoutBag()
    {
        CustomerGroup target = takeoutFlow != null ? takeoutFlow.ActiveGroup : null;
        if (!IsTakeoutBagDeliveryReady(target))
            return null;

        TakeoutBagInteractable[] bags = FindObjectsByType<TakeoutBagInteractable>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < bags.Length; i++)
        {
            if (bags[i] != null && bags[i].TargetGroup == target)
                return bags[i];
        }

        return null;
    }

    private FoodTray FindReadyDeliveryTray()
    {
        if (WaiterHands.Instance != null &&
            (WaiterHands.Instance.HasTray || WaiterHands.Instance.HasBill || WaiterHands.Instance.HasMoney))
            return null;

        FoodTray[] trays = FindObjectsByType<FoodTray>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < trays.Length; i++)
        {
            FoodTray tray = trays[i];
            if (tray != null && tray.TargetGroup != null && tray.TargetGroup.state == CustomerGroup.GroupState.OrderTaken)
                return tray;
        }

        return null;
    }

    private FoodTray FindCleanupTray()
    {
        if (BusserHands.Instance != null && BusserHands.Instance.HasTray)
            return null;

        FoodTray[] trays = FindObjectsByType<FoodTray>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < trays.Length; i++)
        {
            FoodTray tray = trays[i];
            if (tray == null)
                continue;

            CustomerGroup group = tray.TargetGroup;
            if (group == null || group.state == CustomerGroup.GroupState.Leaving ||
                group.state == CustomerGroup.GroupState.AngryLeft || group.state == CustomerGroup.GroupState.UnhappyLeft)
                return tray;
        }

        return null;
    }

    private MoneyPickup FindPaymentPickup()
    {
        MoneyPickup[] payments = FindObjectsByType<MoneyPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < payments.Length; i++)
        {
            MoneyPickup payment = payments[i];
            if (payment != null && payment.TargetGroup != null && payment.TargetGroup.state == CustomerGroup.GroupState.NeedsBill)
                return payment;
        }

        return null;
    }

    private Booth FindDirtyBooth()
    {
        Booth[] booths = FindObjectsByType<Booth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < booths.Length; i++)
        {
            if (booths[i] != null && booths[i].CanCleanMessNow)
                return booths[i];
        }

        return null;
    }

    private bool IsTakeoutOrderReady(CustomerGroup group)
    {
        return takeoutFlow != null &&
               group != null &&
               group.IsTakeout &&
               takeoutFlow.ActiveGroup == group &&
               takeoutFlow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForOrder &&
               group.CurrentTakeoutQueueState == CustomerGroup.TakeoutQueueState.AtOrderPoint &&
               group.state == CustomerGroup.GroupState.ReadyToOrder;
    }

    private bool IsTakeoutPaymentReady(CustomerGroup group)
    {
        return takeoutFlow != null &&
               group != null &&
               group.IsTakeout &&
               takeoutFlow.ActiveGroup == group &&
               takeoutFlow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForPayment &&
               group.CurrentTakeoutQueueState == CustomerGroup.TakeoutQueueState.AtOrderPoint;
    }

    private bool IsTakeoutBagDeliveryReady(CustomerGroup group)
    {
        return takeoutFlow != null &&
               group != null &&
               group.IsTakeout &&
               takeoutFlow.ActiveGroup == group &&
               takeoutFlow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForBagDelivery &&
               group.CurrentTakeoutQueueState == CustomerGroup.TakeoutQueueState.AtOrderPoint;
    }

    private static bool AreWaiterHandsFree(WaiterHands hands)
    {
        return hands != null &&
               !hands.HasTicket &&
               !hands.HasTray &&
               !hands.HasBill &&
               !hands.HasMoney &&
               !TakeoutBagInteractable.HasHeldBag;
    }

    private static Transform FindTableFoodSpawn(Booth booth)
    {
        if (booth == null)
            return null;

        Transform[] transforms = booth.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == "TableFoodSpawn")
                return transforms[i];
        }

        return null;
    }

    private static Transform FindStation(Transform fallback, string stationName)
    {
        GameObject station = GameObject.Find(stationName);
        return station != null ? station.transform : fallback;
    }

    private static GameObject ResolveRoleObject(GameObject configuredRole, string fallbackName)
    {
        return configuredRole != null ? configuredRole : GameObject.Find(fallbackName);
    }

    private static AutonomousStaffBot AddBot(
        GameObject roleObject,
        Transform homePoint,
        int avoidancePriority)
    {
        if (roleObject == null)
            return null;

        AutonomousStaffBot bot = roleObject.GetComponent<AutonomousStaffBot>();
        if (bot == null)
            bot = roleObject.AddComponent<AutonomousStaffBot>();

        bot.ConfigureHome(homePoint, avoidancePriority);
        return bot;
    }

    private static void KeepCharacterStationary(GameObject character)
    {
        if (character == null)
            return;

        UnityEngine.AI.NavMeshAgent agent = character.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private static Vector3 GetGroupCenter(CustomerGroup group)
    {
        if (group == null || group.members == null || group.members.Count == 0)
            return group != null ? group.transform.position : Vector3.zero;

        Vector3 total = Vector3.zero;
        int count = 0;

        for (int i = 0; i < group.members.Count; i++)
        {
            CustomerAgent member = group.members[i];
            if (member == null)
                continue;

            total += member.transform.position;
            count++;
        }

        return count > 0 ? total / count : group.transform.position;
    }

    private static Vector3 GetGroupInteractionPosition(
        CustomerGroup group,
        Vector3 staffPosition,
        float clearance)
    {
        Vector3 center = GetGroupCenter(group);
        Vector3 awayFromGroup = staffPosition - center;
        awayFromGroup.y = 0f;

        if (awayFromGroup.sqrMagnitude < 0.0001f)
            awayFromGroup = group != null ? -group.transform.forward : Vector3.back;

        return center + awayFromGroup.normalized * Mathf.Max(1f, clearance);
    }

    private void IgnoreWaiterPhysicalCollisions(CustomerGroup group)
    {
        if (waiter == null || group == null || group.members == null)
            return;

        Collider[] waiterColliders = waiter.GetComponentsInChildren<Collider>(true);
        if (waiterColliders.Length == 0)
            return;

        for (int i = 0; i < group.members.Count; i++)
        {
            CustomerAgent member = group.members[i];
            if (member == null)
                continue;

            Collider[] customerColliders = member.GetComponentsInChildren<Collider>(true);
            for (int waiterIndex = 0; waiterIndex < waiterColliders.Length; waiterIndex++)
            {
                Collider waiterCollider = waiterColliders[waiterIndex];
                if (waiterCollider == null)
                    continue;

                for (int customerIndex = 0; customerIndex < customerColliders.Length; customerIndex++)
                {
                    Collider customerCollider = customerColliders[customerIndex];
                    if (customerCollider != null)
                        Physics.IgnoreCollision(waiterCollider, customerCollider, true);
                }
            }
        }
    }
}
