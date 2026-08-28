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
    [Tooltip("How often dynamic restaurant objects are refreshed for bot task searches.")]
    [SerializeField, Min(0.2f)] private float sceneQueryRefreshSeconds = 0.5f;
    [SerializeField, Min(0f)] private float managerReactionSeconds = 1f;
    [SerializeField, Min(6f)] private float paymentTravelTimeoutSeconds = 20f;

    [Header("Customer Clearance")]
    [SerializeField, Min(1f)] private float hostCustomerClearance = 2.2f;
    [SerializeField, Min(1f)] private float takeoutWaiterClearance = 3f;

    [Header("Waiter Service Distance")]
    [SerializeField, Min(0.35f)] private float boothServiceDistance = 2.75f;
    [SerializeField, Min(0.35f)] private float counterServiceDistance = 1.75f;
    [SerializeField, Min(0.35f)] private float pickupServiceDistance = 1.75f;
    [SerializeField, Min(0.35f)] private float takeoutServiceDistance = 1.75f;

    [Header("Crowd Navigation Priorities")]
    [Tooltip("Lower values receive right of way. Keep role values separated to prevent face-to-face deadlocks.")]
    [SerializeField, Range(0, 99)] private int hostAvoidancePriority = 24;
    [SerializeField, Range(0, 99)] private int waiterAvoidancePriority = 32;
    [SerializeField, Range(0, 99)] private int busserAvoidancePriority = 42;

    [Header("Trolley Parking")]
    [Tooltip("Editable scene point near the prepared-food counter. Falls back to WaiterTrolleyParkingPoint by name.")]
    [SerializeField] private Transform waiterTrolleyParkingPoint;
    [Tooltip("Editable scene point near the sink. Falls back to BusserTrolleyParkingPoint by name.")]
    [SerializeField] private Transform busserTrolleyParkingPoint;

    [Header("Trolley Batching")]
    [Tooltip("How briefly the waiter waits for a second prepared tray before using the normal one-tray route.")]
    [SerializeField, Min(0f)] private float waiterTrolleyBatchGraceSeconds = 0.75f;
    [Tooltip("How briefly the busser waits for a second dirty tray before using the normal one-tray route.")]
    [SerializeField, Min(0f)] private float busserTrolleyBatchGraceSeconds = 1.25f;
    [Tooltip("After an unreachable trolley route, temporarily use normal single-tray service before trying the trolley again. This prevents a moved parking point from blocking the role.")]
    [SerializeField, Min(0.5f)] private float trolleyRouteRetrySeconds = 4f;

    private AutonomousStaffBot host;
    private AutonomousStaffBot waiter;
    private AutonomousStaffBot busser;
    private BotTrolleyCarrier waiterTrolley;
    private BotTrolleyCarrier busserTrolley;
    private GameObject hostObject;
    private GameObject waiterObject;
    private GameObject cashierObject;
    private GameObject busserObject;
    private KitchenWorkerBot[] kitchenWorkers = System.Array.Empty<KitchenWorkerBot>();
    private EmployeeManager employeeManager;
    private EquipmentManager equipmentManager;
    private RoleManager roleManager;
    private LobbyLineManager lineManager;
    private KitchenManager kitchenManager;
    private BillManager billManager;
    private TakeoutFlowManager takeoutFlow;
    private SinkInteractable sink;
    private CashierRegisterUI cashierRegister;
    private Transform cashierStation;
    private Booth[] cachedBooths = System.Array.Empty<Booth>();
    private CustomerGroup[] cachedGroups = System.Array.Empty<CustomerGroup>();
    private TakeoutBagInteractable[] cachedTakeoutBags = System.Array.Empty<TakeoutBagInteractable>();
    private FoodTray[] cachedFoodTrays = System.Array.Empty<FoodTray>();
    private MoneyPickup[] cachedPayments = System.Array.Empty<MoneyPickup>();
    private float nextSceneQueryTime;
    private FoodTray waiterBatchGraceTray;
    private float waiterBatchGraceUntil;
    private FoodTray busserBatchGraceTray;
    private float busserBatchGraceUntil;
    private float waiterTrolleyRetryAfter;
    private float busserTrolleyRetryAfter;
    private readonly List<FoodTray> activeWaiterTrolleyBatch = new List<FoodTray>();
    private readonly List<FoodTray> activeBusserTrolleyBatch = new List<FoodTray>();
    private readonly HashSet<string> trolleyDiagnostics = new HashSet<string>();

    private void Awake()
    {
        roleManager = FindFirstObjectByType<RoleManager>(FindObjectsInactive.Include);
        DisableManualRoleControl();
    }

    private IEnumerator Start()
    {
        // This component is added during GameDayManager.Awake. Repeat the shutdown
        // after every scene Awake has run so RoleManager cannot re-enable controls.
        DisableManualRoleControl();

        // Equipment purchases are save-backed. Do not decide that a trolley is
        // unpurchased during the one-frame bootstrap window before save apply.
        GameSaveManager saveManager = GameSaveManager.Instance;
        while (saveManager != null &&
               (!saveManager.HasCompletedInitialLoad || saveManager.IsApplyingSave))
        {
            yield return null;
        }

        ResolveSceneReferences();
        yield return ServiceLoop();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        CancelActiveTrolleyBatch(waiterTrolley, waiter, activeWaiterTrolleyBatch);
        CancelActiveTrolleyBatch(busserTrolley, busser, activeBusserTrolleyBatch);
    }

    private void OnDestroy()
    {
        if (employeeManager != null)
            employeeManager.AssignmentsChanged -= RefreshStaffAssignments;

        if (equipmentManager != null)
            equipmentManager.PurchasesChanged -= RefreshStaffAssignments;

        if (takeoutFlow != null)
            takeoutFlow.SetAutomatedService(false);

        CancelActiveTrolleyBatch(waiterTrolley, waiter, activeWaiterTrolleyBatch);
        CancelActiveTrolleyBatch(busserTrolley, busser, activeBusserTrolleyBatch);
        ShutdownTrolley(waiterTrolley, waiter);
        ShutdownTrolley(busserTrolley, busser);
    }

    private void ResolveSceneReferences()
    {
        lineManager = FindFirstObjectByType<LobbyLineManager>();
        kitchenManager = FindFirstObjectByType<KitchenManager>();
        billManager = FindFirstObjectByType<BillManager>();
        takeoutFlow = FindFirstObjectByType<TakeoutFlowManager>();
        sink = FindFirstObjectByType<SinkInteractable>();
        cashierRegister = FindFirstObjectByType<CashierRegisterUI>(FindObjectsInactive.Include);
        hostObject = ResolveRoleObject(roleManager != null ? roleManager.host : null, "Host");
        waiterObject = ResolveRoleObject(roleManager != null ? roleManager.waiter : null, "Waiter");
        cashierObject = ResolveRoleObject(roleManager != null ? roleManager.cashier : null, "Cashier");
        busserObject = ResolveRoleObject(roleManager != null ? roleManager.busser : null, "Busser");
        kitchenWorkers = FindObjectsByType<KitchenWorkerBot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        CashierBoothInteractable cashierBooth = FindFirstObjectByType<CashierBoothInteractable>();
        cashierStation = cashierBooth != null
            ? cashierBooth.StandPoint
            : FindStation(cashierObject != null ? cashierObject.transform : null, "CashierStation");

        if (waiterTrolleyParkingPoint == null)
            waiterTrolleyParkingPoint = FindStation(null, "WaiterTrolleyParkingPoint");
        if (busserTrolleyParkingPoint == null)
            busserTrolleyParkingPoint = FindStation(null, "BusserTrolleyParkingPoint");

        RefreshSceneQueryCache(true);
        BindEmployeeAssignments();
        BindEquipmentPurchases();
        RefreshStaffAssignments();
    }

    private void BindEmployeeAssignments()
    {
        if (employeeManager == EmployeeManager.Instance)
            return;

        if (employeeManager != null)
            employeeManager.AssignmentsChanged -= RefreshStaffAssignments;

        employeeManager = EmployeeManager.Instance;
        if (employeeManager != null)
            employeeManager.AssignmentsChanged += RefreshStaffAssignments;
    }

    private void BindEquipmentPurchases()
    {
        EquipmentManager current = EquipmentManager.Instance;
        if (equipmentManager == current)
            return;

        if (equipmentManager != null)
            equipmentManager.PurchasesChanged -= RefreshStaffAssignments;

        equipmentManager = current;
        if (equipmentManager != null)
            equipmentManager.PurchasesChanged += RefreshStaffAssignments;
    }

    private void RefreshStaffAssignments()
    {
        BindEmployeeAssignments();
        BindEquipmentPurchases();

        // Disabling a role stops its coroutine immediately. Release every
        // trolley claim first so no tray or pickup bubble remains locked.
        if (waiter != null && !IsAssigned(EmployeeRole.Waiter))
            CancelActiveTrolleyBatch(waiterTrolley, waiter, activeWaiterTrolleyBatch);
        if (busser != null && !IsAssigned(EmployeeRole.Busser))
            CancelActiveTrolleyBatch(busserTrolley, busser, activeBusserTrolleyBatch);

        host = ConfigureLobbyBot(
            hostObject,
            EmployeeRole.Host,
            FindStation(null, "HostHomePoint"),
            hostAvoidancePriority);
        waiter = ConfigureLobbyBot(
            waiterObject,
            EmployeeRole.Waiter,
            FindStation(null, "WaiterHomePoint"),
            waiterAvoidancePriority);
        busser = ConfigureLobbyBot(
            busserObject,
            EmployeeRole.Busser,
            FindStation(null, "BusserHomePoint"),
            busserAvoidancePriority);

        waiterTrolley = ConfigureTrolley(
            waiterTrolley,
            waiterObject,
            EquipmentUpgradeEffect.WaiterTrolley,
            "Upgrades/WaiterTrolley",
            ResolveTrolleyParkingPoint(EquipmentUpgradeEffect.WaiterTrolley));
        busserTrolley = ConfigureTrolley(
            busserTrolley,
            busserObject,
            EquipmentUpgradeEffect.BusserTrolley,
            "Upgrades/BusserTrolley",
            ResolveTrolleyParkingPoint(EquipmentUpgradeEffect.BusserTrolley));

        SetRoleObjectActive(cashierObject, IsAssigned(EmployeeRole.Cashier));
        if (cashierObject != null && cashierObject.activeSelf)
            KeepCharacterStationary(cashierObject);

        for (int i = 0; i < kitchenWorkers.Length; i++)
        {
            KitchenWorkerBot worker = kitchenWorkers[i];
            if (worker != null)
                SetRoleObjectActive(worker.gameObject, IsAssigned(worker.EmployeeRole));
        }

        takeoutFlow?.SetAutomatedService(waiter != null);
        ConfigureIdlePresentation();
    }

    private BotTrolleyCarrier ConfigureTrolley(
        BotTrolleyCarrier existing,
        GameObject roleObject,
        EquipmentUpgradeEffect effect,
        string resourcesPath,
        Transform parkingPoint)
    {
        existing = ResolveAuthoritativeTrolley(existing, effect);
        bool purchased = EquipmentUpgradeService.IsPurchased(effect);
        if (!purchased)
        {
            if (existing != null)
            {
                existing.ReleaseAllForRetry(roleObject != null ? roleObject.transform.position : existing.transform.position);
                existing.EndUse();
                existing.SetVisible(false);
                if (existing.IsRuntimeOwned)
                    Destroy(existing.gameObject);
            }
            LogTrolleyDiagnosticOnce(effect, "not purchased; no trolley will be shown");
            return null;
        }

        // A purchased trolley is a physical restaurant upgrade, so keep it
        // visible at its editable parking point even when that role is not
        // currently staffed. Task batching still requires an active bot.
        if (existing != null && (roleObject == null || !roleObject.activeSelf) && existing.IsInUse)
        {
            existing.ReleaseAllForRetry(existing.transform.position);
            existing.EndUse(true);
        }

        if (existing == null)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab == null)
            {
                Debug.LogError($"[LobbyAutonomousService] Missing editable trolley prefab at Resources/{resourcesPath}.", this);
                return null;
            }

            GameObject instance = Instantiate(prefab);
            instance.name = prefab.name;
            existing = instance.GetComponent<BotTrolleyCarrier>();
            if (existing == null)
            {
                Debug.LogError($"[LobbyAutonomousService] {prefab.name} needs BotTrolleyCarrier on its prefab root.", prefab);
                Destroy(instance);
                return null;
            }
            existing.MarkRuntimeOwned();
        }

        existing.ConfigureRuntime(
            effect,
            EquipmentUpgradeService.GetCarryCapacity(effect),
            parkingPoint);
        if (!existing.IsConfigured)
        {
            Debug.LogError(
                $"[LobbyAutonomousService] {existing.name} is not usable: {existing.ConfigurationProblem}.",
                existing);
            existing.SetVisible(false);
            if (existing.IsRuntimeOwned)
                Destroy(existing.gameObject);
            return null;
        }
        existing.SetVisible(true);
        HideDuplicateTrolleys(effect, existing);
        LogTrolleyDiagnosticOnce(
            effect,
            $"ready at {parkingPoint.name}; role active={roleObject != null && roleObject.activeSelf}; " +
            $"capacity={existing.Capacity}");
        return existing;
    }

    private BotTrolleyCarrier ResolveAuthoritativeTrolley(
        BotTrolleyCarrier current,
        EquipmentUpgradeEffect effect)
    {
        BotTrolleyCarrier[] candidates = FindObjectsByType<BotTrolleyCarrier>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        BotTrolleyCarrier selected = current != null && current.Effect == effect
            ? current
            : null;

        // Prefer an explicitly authored scene object over creating another copy.
        if (selected == null)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                BotTrolleyCarrier candidate = candidates[i];
                if (candidate == null || candidate.Effect != effect ||
                    candidate.gameObject.scene != gameObject.scene)
                    continue;

                if (selected == null || (selected.IsRuntimeOwned && !candidate.IsRuntimeOwned))
                    selected = candidate;
            }
        }

        HideDuplicateTrolleys(effect, selected);
        return selected;
    }

    private void HideDuplicateTrolleys(
        EquipmentUpgradeEffect effect,
        BotTrolleyCarrier authoritative)
    {
        BotTrolleyCarrier[] candidates = FindObjectsByType<BotTrolleyCarrier>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        int duplicateCount = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            BotTrolleyCarrier candidate = candidates[i];
            if (candidate == null || candidate == authoritative ||
                candidate.Effect != effect || candidate.gameObject.scene != gameObject.scene)
                continue;

            duplicateCount++;
            candidate.ReleaseAllForRetry(candidate.transform.position);
            candidate.SetVisible(false);
            if (candidate.IsRuntimeOwned)
                Destroy(candidate.gameObject);
        }

        if (duplicateCount > 0)
        {
            LogTrolleyDiagnosticOnce(
                effect,
                $"found {duplicateCount} duplicate scene instance(s); only one authoritative trolley is active");
        }
    }

    private void LogTrolleyDiagnosticOnce(
        EquipmentUpgradeEffect effect,
        string message)
    {
        string key = effect + ":" + message;
        if (trolleyDiagnostics.Add(key))
            Debug.Log($"[LobbyAutonomousService] {effect}: {message}.", this);
    }

    private AutonomousStaffBot ConfigureLobbyBot(
        GameObject roleObject,
        EmployeeRole role,
        Transform homePoint,
        int avoidancePriority)
    {
        bool assigned = IsAssigned(role);
        SetRoleObjectActive(roleObject, assigned);
        if (!assigned || roleObject == null)
            return null;

        AutonomousStaffBot bot = AddBot(roleObject, homePoint, avoidancePriority);
        bot?.ConfigurePerformance(employeeManager.GetAssignedEmployee(role));
        return bot;
    }

    private bool IsAssigned(EmployeeRole role) =>
        employeeManager != null && employeeManager.GetAssignedEmployee(role) != null;

    private static void SetRoleObjectActive(GameObject roleObject, bool active)
    {
        if (roleObject != null && roleObject.activeSelf != active)
            roleObject.SetActive(active);
    }

    private void DisableManualRoleControl()
    {
        if (roleManager != null)
            roleManager.DisablePlayerRoleControl();

        PlayerMovement[] movements = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < movements.Length; i++)
        {
            if (movements[i].GetComponent<ManagerPlayer>() != null)
            {
                movements[i].enabled = true;
                movements[i].SetPlayerControlled(true);
                continue;
            }

            movements[i].enabled = false;
        }

        RoleBasedAssignController[] assigners = FindObjectsByType<RoleBasedAssignController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < assigners.Length; i++)
            assigners[i].enabled = assigners[i].GetComponent<ManagerPlayer>() != null;

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
        Booth[] booths = cachedBooths;
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
            if (employeeManager != EmployeeManager.Instance ||
                equipmentManager != EquipmentManager.Instance)
                RefreshStaffAssignments();

            if (GameDayManager.Instance != null && GameDayManager.Instance.ServiceActive)
            {
                RefreshSceneQueryCache(false);
                TryStartHostTask();
                TryStartWaiterTask();
                TryStartBusserTask();
            }

            yield return wait;
        }
    }

    private void RefreshSceneQueryCache(bool force)
    {
        if (!force && Time.unscaledTime < nextSceneQueryTime)
            return;

        nextSceneQueryTime = Time.unscaledTime + Mathf.Max(0.2f, sceneQueryRefreshSeconds);
        cachedBooths = FindObjectsByType<Booth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        cachedGroups = FindObjectsByType<CustomerGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        cachedTakeoutBags = FindObjectsByType<TakeoutBagInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        cachedFoodTrays = FindObjectsByType<FoodTray>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        cachedPayments = FindObjectsByType<MoneyPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    private void TryStartHostTask()
    {
        if (host == null || host.IsBusy || lineManager == null)
            return;

        CustomerGroup group = lineManager.GetFrontOfLine();
        if (group == null || group.HasBeenAssigned || group.state != CustomerGroup.GroupState.Waiting)
            return;

        if (!RestaurantTaskClaim.TryClaimBot(group, host, managerReactionSeconds))
            return;

        if (!group.TryClaimReceptionForBot())
        {
            RestaurantTaskClaim.ReleaseBot(group, host);
            return;
        }

        CustomerGreetBubbleSpawner.Instance?.Hide();

        Booth booth = FindAvailableBooth(group.Size);
        if (booth != null)
            host.StartTask(RunReceptionTask(group, host, SeatGroup(group, booth)));
        else
        {
            group.ReleaseBotReceptionTask();
            RestaurantTaskClaim.ReleaseBot(group, host);
        }
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

        // A seated group that has already finished its meal should not be
        // starved by newly arriving takeout work, orders, or prepared trays.
        // Reserve its payment first so the booth can finish and turn over.
        MoneyPickup payment = FindPendingPaymentPickup();
        if (payment != null)
        {
            TryStartClaimedTask(waiter, payment, DeliverPaymentToCashier(payment));

            // A pending payment has the highest waiter priority. During the
            // manager's short reaction window, reserve the waiter instead of
            // starting a lower-priority order that can starve this payment.
            return;
        }

        TakeoutBagInteractable takeoutBag = FindReadyTakeoutBag();
        if (takeoutBag != null &&
            TryStartClaimedTask(waiter, takeoutBag, DeliverTakeoutBag(takeoutBag)))
        {
            return;
        }

        CustomerGroup takeoutPaymentGroup = FindTakeoutPaymentTarget();
        if (takeoutPaymentGroup != null &&
            TryStartClaimedTask(waiter, takeoutPaymentGroup, CompleteTakeoutPaymentAtCashier(takeoutPaymentGroup)))
        {
            return;
        }

        CustomerGroup takeoutOrderGroup = FindTakeoutOrderTarget();
        if (takeoutOrderGroup != null &&
            TryStartClaimedTask(waiter, takeoutOrderGroup, TakeTakeoutOrder(takeoutOrderGroup)))
        {
            return;
        }

        if (TryStartWaiterTrolleyBatch())
            return;

        FoodTray readyTray = FindReadyDeliveryTray();
        if (ShouldWaitForWaiterTrolleyBatch(readyTray))
            return;
        if (readyTray != null &&
            TryStartClaimedTask(waiter, readyTray, DeliverFood(readyTray)))
        {
            return;
        }

        CustomerGroup billGroup = FindBillDeliveryTarget();
        if (billGroup != null &&
            TryStartClaimedTask(waiter, billGroup, DeliverBill(billGroup)))
        {
            return;
        }

        CustomerGroup orderGroup = FindGroupInState(CustomerGroup.GroupState.ReadyToOrder);
        if (orderGroup != null &&
            RestaurantTaskClaim.TryClaimBot(orderGroup, waiter, managerReactionSeconds))
        {
            SetTaskUiClaimed(orderGroup, true);
            waiter.StartTask(RunClaimedTask(orderGroup, waiter, TakeOrder(orderGroup)));
        }
    }

    private void TryStartBusserTask()
    {
        if (busser == null || busser.IsBusy || BusserHands.Instance == null)
            return;

        if (TryStartBusserTrolleyBatch())
            return;

        FoodTray tray = FindCleanupTray();
        if (ShouldWaitForBusserTrolleyBatch(tray))
            return;
        if (tray != null && sink != null &&
            TryStartClaimedTask(busser, tray, CleanTrayAtSink(tray)))
        {
            return;
        }

        Booth dirtyBooth = FindDirtyBooth();
        if (dirtyBooth != null)
            TryStartClaimedTask(busser, dirtyBooth, CleanBooth(dirtyBooth));
    }

    private bool TryStartWaiterTrolleyBatch()
    {
        if (Time.time < waiterTrolleyRetryAfter)
            return false;

        if (waiter == null || waiterTrolley == null || !waiterTrolley.IsConfigured ||
            waiterTrolley.IsInUse || waiterTrolley.Count > 0 ||
            !AreWaiterHandsFree(WaiterHands.Instance))
            return false;

        List<FoodTray> candidates = new List<FoodTray>();
        FoodTray[] trays = cachedFoodTrays;
        for (int i = 0; i < trays.Length && candidates.Count < waiterTrolley.Capacity; i++)
        {
            FoodTray tray = trays[i];
            FoodTrayInteractable interactable = tray != null
                ? tray.GetComponent<FoodTrayInteractable>()
                : null;
            if (tray == null || interactable == null || !interactable.IsDeliveryPickable ||
                tray.TargetGroup == null ||
                tray.TargetGroup.state != CustomerGroup.GroupState.OrderTaken ||
                !RestaurantTaskClaim.CanBotStart(tray, managerReactionSeconds))
                continue;

            candidates.Add(tray);
        }

        if (candidates.Count < waiterTrolley.MinimumBatchSize)
            return false;

        if (candidates.Count == 1 && ShouldWaitForWaiterTrolleyBatch(candidates[0]))
            return false;

        List<FoodTray> batch = new List<FoodTray>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            FoodTray tray = candidates[i];
            if (!RestaurantTaskClaim.TryClaimBot(tray, waiter, managerReactionSeconds))
                continue;
            SetTaskUiClaimed(tray, true);
            batch.Add(tray);
        }

        if (batch.Count < waiterTrolley.MinimumBatchSize)
        {
            for (int i = 0; i < batch.Count; i++)
                ReleaseBatchClaim(batch[i], waiter);
            return false;
        }

        TrackActiveBatch(activeWaiterTrolleyBatch, batch);
        waiter.StartTask(DeliverFoodBatch(batch));
        ClearWaiterBatchGrace();
        return true;
    }

    private bool TryStartBusserTrolleyBatch()
    {
        if (Time.time < busserTrolleyRetryAfter)
            return false;

        if (busser == null || sink == null || busserTrolley == null ||
            !busserTrolley.IsConfigured ||
            busserTrolley.IsInUse || busserTrolley.Count > 0 ||
            (BusserHands.Instance != null && BusserHands.Instance.HasTray))
            return false;

        List<FoodTray> candidates = new List<FoodTray>();
        FoodTray[] trays = cachedFoodTrays;
        for (int i = 0; i < trays.Length && candidates.Count < busserTrolley.Capacity; i++)
        {
            FoodTray tray = trays[i];
            if (!IsCleanupTrayReady(tray) ||
                !RestaurantTaskClaim.CanBotStart(tray, managerReactionSeconds))
                continue;

            candidates.Add(tray);
        }

        if (candidates.Count < busserTrolley.MinimumBatchSize)
            return false;

        if (candidates.Count == 1 && ShouldWaitForBusserTrolleyBatch(candidates[0]))
            return false;

        List<FoodTray> batch = new List<FoodTray>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            FoodTray tray = candidates[i];
            if (!RestaurantTaskClaim.TryClaimBot(tray, busser, managerReactionSeconds))
                continue;
            SetTaskUiClaimed(tray, true);
            batch.Add(tray);
        }

        if (batch.Count < busserTrolley.MinimumBatchSize)
        {
            for (int i = 0; i < batch.Count; i++)
                ReleaseBatchClaim(batch[i], busser);
            return false;
        }

        TrackActiveBatch(activeBusserTrolleyBatch, batch);
        busser.StartTask(CleanTrayBatchAtSink(batch));
        ClearBusserBatchGrace();
        return true;
    }

    private bool ShouldWaitForWaiterTrolleyBatch(FoodTray tray)
    {
        if (Time.time < waiterTrolleyRetryAfter)
        {
            ClearWaiterBatchGrace();
            return false;
        }

        if (tray == null || waiterTrolley == null || !waiterTrolley.IsConfigured)
        {
            ClearWaiterBatchGrace();
            return false;
        }

        if (waiterBatchGraceTray != tray)
        {
            waiterBatchGraceTray = tray;
            waiterBatchGraceUntil = Time.time + Mathf.Max(0f, waiterTrolleyBatchGraceSeconds);
        }

        if (Time.time < waiterBatchGraceUntil)
            return true;

        return false;
    }

    private bool ShouldWaitForBusserTrolleyBatch(FoodTray tray)
    {
        if (Time.time < busserTrolleyRetryAfter)
        {
            ClearBusserBatchGrace();
            return false;
        }

        if (tray == null || busserTrolley == null || !busserTrolley.IsConfigured)
        {
            ClearBusserBatchGrace();
            return false;
        }

        if (busserBatchGraceTray != tray)
        {
            busserBatchGraceTray = tray;
            busserBatchGraceUntil = Time.time + Mathf.Max(0f, busserTrolleyBatchGraceSeconds);
        }

        if (Time.time < busserBatchGraceUntil)
            return true;

        return false;
    }

    private void ClearWaiterBatchGrace()
    {
        waiterBatchGraceTray = null;
        waiterBatchGraceUntil = 0f;
    }

    private void ClearBusserBatchGrace()
    {
        busserBatchGraceTray = null;
        busserBatchGraceUntil = 0f;
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

        if (group == null || booth == null || !group.CanBeSeated || !booth.IsAvailableFor(group.Size))
            yield break;

        yield return host.MoveTo(booth.GetNavigableApproachPosition());
        yield return host.FaceTowards(GetGroupCenter(group));
        yield return host.WorkFor(greetingSeconds);

        if (group != null && booth != null && group.CanBeSeated && booth.IsAvailableFor(group.Size))
            group.AssignToBooth(booth);
    }

    private IEnumerator RunClaimedTask(
        UnityEngine.Object target,
        AutonomousStaffBot owner,
        IEnumerator task)
    {
        yield return task;

        // A player can begin reviewing an order while a waiter coroutine that
        // was already moving toward the table is winding down. Preserve the
        // player's lock instead of letting bot cleanup re-open the bubble and
        // remove the active player claim.
        if (target is CustomerGroup reviewingGroup && reviewingGroup.IsPlayerReviewingOrder)
        {
            RestaurantTaskClaim.ReleaseBot(target, owner);
            yield break;
        }

        SetTaskUiClaimed(target, false);
        RestaurantTaskClaim.Complete(target);
    }

    private IEnumerator RunReceptionTask(
        CustomerGroup group,
        AutonomousStaffBot owner,
        IEnumerator task)
    {
        yield return task;

        if (group != null)
            group.CompleteReceptionTask();
        RestaurantTaskClaim.Complete(group);
    }

    private bool TryStartClaimedTask(
        AutonomousStaffBot owner,
        UnityEngine.Object target,
        IEnumerator task)
    {
        if (!RestaurantTaskClaim.TryClaimBot(target, owner, managerReactionSeconds))
            return false;

        SetTaskUiClaimed(target, true);
        owner.StartTask(RunClaimedTask(target, owner, task));
        return true;
    }

    private static void SetTaskUiClaimed(UnityEngine.Object target, bool claimed)
    {
        if (target is CustomerGroup group)
        {
            if (group.state == CustomerGroup.GroupState.ReadyToOrder)
                group.SetOrderTaskClaimedByStaff(claimed);
            if (group.state == CustomerGroup.GroupState.NeedsBill)
                group.SetBillTaskClaimedByStaff(claimed);
            return;
        }

        if (target is FoodTray tray)
        {
            tray.GetComponent<FoodTrayInteractable>()?.SetClaimedByStaff(claimed);
            return;
        }

        if (target is MoneyPickup payment)
        {
            payment.SetClaimedByStaff(claimed);
            return;
        }

        if (target is TakeoutBagInteractable bag)
            bag.SetClaimedByStaff(claimed);
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

        if (group == null || group.state != CustomerGroup.GroupState.ReadyToOrder ||
            group.IsPlayerReviewingOrder || WaiterHands.Instance == null)
            yield break;

        if (!group.TakeOrderFromWaiter(group.chosenFood, group.chosenDrink))
            yield break;
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

        if (!group.TakeOrderFromWaiter(group.chosenFood, group.chosenDrink) ||
            group == null || group.state != CustomerGroup.GroupState.OrderTaken)
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

        CashierRegisterUI register = ResolveCashierRegister();
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

            bag.TryPickupForStaff(WaiterHands.Instance);
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

        if (tray == null || group == null || group.assignedBooth == null ||
            group.state != CustomerGroup.GroupState.OrderTaken ||
            !group.HasConfirmedOrder || group.IsPlayerReviewingOrder ||
            hands.holdingTray != tray)
        {
            if (hands.holdingTray == tray)
                hands.DisposeTray(true);
            waiter.SetCarrying(false);
            yield break;
        }

        Transform dropPoint = FindTableFoodSpawn(group.assignedBooth);
        if (dropPoint == null || !hands.TryDeliverTrayTo(group, false))
            yield break;

        WaiterHands.AttachKeepingWorldScale(
            tray.transform,
            dropPoint,
            Vector3.zero,
            Quaternion.identity);
        WaiterHands.SetAllColliders(tray.gameObject, true);

        tray.GetComponent<FoodTrayInteractable>()?.NotifyDeliveredToTable();
        group.ReceiveFoodFromWaiter(tray.DeliveredContents, tray);
        waiter.SetCarrying(false);
    }

    private IEnumerator DeliverFoodBatch(List<FoodTray> batch)
    {
        BotTrolleyCarrier trolley = waiterTrolley;
        if (waiter == null || trolley == null || batch == null || batch.Count == 0)
            yield break;

        if (!trolley.TryGetParkingApproachPosition(
                waiter.transform.position,
                out Vector3 approachPosition))
        {
            DeferWaiterTrolleyRoute(trolley, batch);
            yield break;
        }

        yield return waiter.MoveWithin(
            approachPosition,
            trolley.ParkingApproachDistance,
            0.75f);
        if (!waiter.LastMoveSucceeded || !trolley.BeginUse(waiter))
        {
            DeferWaiterTrolleyRoute(trolley, batch);
            yield break;
        }
        waiterTrolleyRetryAfter = 0f;

        for (int i = 0; i < batch.Count; i++)
        {
            FoodTray tray = batch[i];
            if (!IsDeliveryTrayReady(tray))
            {
                ReleaseBatchClaim(tray, waiter);
                continue;
            }

            yield return waiter.MoveWithin(tray.transform.position, pickupServiceDistance, 2f);
            if (!waiter.LastMoveSucceeded || tray == null || !trolley.TryAttach(tray))
            {
                ReleaseBatchClaim(tray, waiter);
                continue;
            }

            yield return waiter.WorkFor(Mathf.Min(0.3f, counterServiceSeconds));
        }

        for (int i = 0; i < batch.Count; i++)
        {
            FoodTray tray = batch[i];
            if (tray == null || !trolley.Contains(tray))
                continue;

            CustomerGroup group = tray.TargetGroup;
            if (!IsDeliveryTrayReady(tray) || group.assignedBooth == null)
            {
                trolley.Dispose(tray);
                continue;
            }

            yield return waiter.MoveWithin(
                group.assignedBooth.GetNavigableApproachPosition(),
                boothServiceDistance);
            if (!waiter.LastMoveSucceeded || !IsDeliveryTrayReady(tray))
            {
                trolley.TryReleaseForRetry(tray, waiter.transform.position);
                ReleaseBatchClaim(tray, waiter);
                continue;
            }

            yield return waiter.FaceTowards(GetGroupCenter(group));
            yield return waiter.WorkFor(tableServiceSeconds);

            Transform dropPoint = FindTableFoodSpawn(group.assignedBooth);
            if (dropPoint == null || !trolley.TryDetach(tray, dropPoint))
            {
                trolley.TryReleaseForRetry(tray, waiter.transform.position);
                ReleaseBatchClaim(tray, waiter);
                continue;
            }

            group.assignedBooth.ClearMenuBook();
            RestaurantTaskClaim.Complete(tray);
            tray.GetComponent<FoodTrayInteractable>()?.NotifyDeliveredToTable();
            group.ReceiveFoodFromWaiter(tray.DeliveredContents, tray);
        }

        trolley.ReleaseAllForRetry(waiter.transform.position);
        ReleaseBatchClaims(batch, waiter);
        yield return ReturnTrolleyToParking(trolley, waiter);
        activeWaiterTrolleyBatch.Clear();
    }

    private IEnumerator CleanTrayBatchAtSink(List<FoodTray> batch)
    {
        BotTrolleyCarrier trolley = busserTrolley;
        if (busser == null || trolley == null || sink == null || batch == null || batch.Count == 0)
            yield break;

        if (!trolley.TryGetParkingApproachPosition(
                busser.transform.position,
                out Vector3 approachPosition))
        {
            DeferBusserTrolleyRoute(trolley, batch);
            yield break;
        }

        yield return busser.MoveWithin(
            approachPosition,
            trolley.ParkingApproachDistance,
            0.75f);
        if (!busser.LastMoveSucceeded || !trolley.BeginUse(busser))
        {
            DeferBusserTrolleyRoute(trolley, batch);
            yield break;
        }
        busserTrolleyRetryAfter = 0f;

        for (int i = 0; i < batch.Count; i++)
        {
            FoodTray tray = batch[i];
            Booth sourceBooth = GetCleanupSourceBooth(tray);
            if (!IsCleanupTrayReady(tray) || sourceBooth == null)
            {
                ReleaseBatchClaim(tray, busser);
                continue;
            }

            yield return busser.MoveTo(sourceBooth.GetNavigableApproachPosition());
            if (!busser.LastMoveSucceeded || tray == null ||
                !trolley.TryAttach(tray))
            {
                ReleaseBatchClaim(tray, busser);
                continue;
            }

            yield return busser.FaceTowards(sourceBooth.transform.position);
            yield return busser.WorkFor(Mathf.Min(0.3f, tableServiceSeconds));
        }

        if (trolley.Count > 0)
        {
            yield return busser.MoveTo(sink.StandPoint);
            if (busser.LastMoveSucceeded)
            {
                yield return busser.WorkFor(cleaningSeconds + 0.2f * (trolley.Count - 1));
                for (int i = 0; i < batch.Count; i++)
                {
                    FoodTray tray = batch[i];
                    if (tray == null || !trolley.Contains(tray))
                        continue;
                    trolley.Dispose(tray);
                    GameDayManager.Instance?.RegisterTrayCleaned();
                }
            }
            else
            {
                for (int i = 0; i < batch.Count; i++)
                {
                    FoodTray tray = batch[i];
                    if (tray == null || !trolley.Contains(tray))
                        continue;
                    trolley.TryReleaseForRetry(tray, busser.transform.position);
                    ReleaseBatchClaim(tray, busser);
                }
            }
        }

        trolley.ReleaseAllForRetry(busser.transform.position);
        ReleaseBatchClaims(batch, busser);
        yield return ReturnTrolleyToParking(trolley, busser);
        activeBusserTrolleyBatch.Clear();
    }

    private static bool IsDeliveryTrayReady(FoodTray tray)
    {
        if (tray == null || tray.TargetGroup == null)
            return false;
        FoodTrayInteractable interactable = tray.GetComponent<FoodTrayInteractable>();
        CustomerGroup group = tray.TargetGroup;
        return interactable != null && interactable.IsDeliveryPickable &&
               group.state == CustomerGroup.GroupState.OrderTaken &&
               group.HasConfirmedOrder && !group.IsPlayerReviewingOrder;
    }

    private static bool IsCleanupTrayReady(FoodTray tray)
    {
        if (tray == null)
            return false;
        FoodTrayInteractable interactable = tray.GetComponent<FoodTrayInteractable>();
        if (interactable == null || !interactable.IsCleanupPickable)
            return false;
        CustomerGroup group = tray.TargetGroup;
        return group == null || group.state == CustomerGroup.GroupState.Leaving ||
               group.state == CustomerGroup.GroupState.AngryLeft ||
               group.state == CustomerGroup.GroupState.UnhappyLeft;
    }

    private static Booth GetCleanupSourceBooth(FoodTray tray)
    {
        if (tray == null)
            return null;
        Booth booth = tray.GetComponentInParent<Booth>();
        return booth != null ? booth : tray.TargetGroup != null ? tray.TargetGroup.assignedBooth : null;
    }

    private void ReleaseBatchClaim(FoodTray tray, AutonomousStaffBot owner)
    {
        if (tray == null || owner == null)
            return;
        SetTaskUiClaimed(tray, false);
        RestaurantTaskClaim.ReleaseBot(tray, owner);
    }

    private void ReleaseBatchClaims(IList<FoodTray> batch, AutonomousStaffBot owner)
    {
        if (batch == null)
            return;
        for (int i = 0; i < batch.Count; i++)
            ReleaseBatchClaim(batch[i], owner);
    }

    private static void TrackActiveBatch(List<FoodTray> destination, IList<FoodTray> source)
    {
        destination.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                destination.Add(source[i]);
        }
    }

    private void CancelActiveTrolleyBatch(
        BotTrolleyCarrier trolley,
        AutonomousStaffBot owner,
        List<FoodTray> batch)
    {
        if (trolley != null)
        {
            Vector3 releasePosition = owner != null
                ? owner.transform.position
                : trolley.transform.position;
            trolley.ReleaseAllForRetry(releasePosition);
            trolley.EndUse(true);
        }

        ReleaseBatchClaims(batch, owner);
        batch.Clear();
    }

    private void DeferWaiterTrolleyRoute(
        BotTrolleyCarrier trolley,
        List<FoodTray> batch)
    {
        if (trolley != null && trolley.IsInUse)
            trolley.EndUse(true);
        ReleaseBatchClaims(batch, waiter);
        activeWaiterTrolleyBatch.Clear();
        ClearWaiterBatchGrace();
        waiterTrolleyRetryAfter = Time.time + Mathf.Max(0.5f, trolleyRouteRetrySeconds);
        LogTrolleyDiagnosticOnce(
            EquipmentUpgradeEffect.WaiterTrolley,
            "parking approach is unreachable; temporarily using normal single-tray service");
    }

    private void DeferBusserTrolleyRoute(
        BotTrolleyCarrier trolley,
        List<FoodTray> batch)
    {
        if (trolley != null && trolley.IsInUse)
            trolley.EndUse(true);
        ReleaseBatchClaims(batch, busser);
        activeBusserTrolleyBatch.Clear();
        ClearBusserBatchGrace();
        busserTrolleyRetryAfter = Time.time + Mathf.Max(0.5f, trolleyRouteRetrySeconds);
        LogTrolleyDiagnosticOnce(
            EquipmentUpgradeEffect.BusserTrolley,
            "parking approach is unreachable; temporarily using normal single-tray service");
    }

    private IEnumerator ReturnTrolleyToParking(
        BotTrolleyCarrier trolley,
        AutonomousStaffBot owner)
    {
        if (trolley == null)
            yield break;

        if (owner != null && owner.isActiveAndEnabled)
        {
            if (trolley.TryGetParkingApproachPosition(
                    owner.transform.position,
                    out Vector3 approachPosition))
            {
                yield return owner.MoveWithin(
                    approachPosition,
                    trolley.ParkingApproachDistance,
                    0.75f);
            }
        }

        // Parking is authoritative even if congestion prevented the last few
        // centimetres of movement. This avoids abandoned tools between batches.
        trolley.EndUse(true);
    }

    private IEnumerator DeliverBill(CustomerGroup group)
    {
        WaiterHands hands = WaiterHands.Instance;
        if (group == null || group.assignedBooth == null || hands == null ||
            (hands.HasBill && hands.holdingBillFor != group))
            yield break;

        if (!hands.HasBill)
        {
            // A waiter must acknowledge the table in person before requesting
            // its bill. This prevents state from advancing while the bot is
            // still across the restaurant or stuck on a path.
            yield return waiter.MoveWithin(
                group.assignedBooth.GetNavigableApproachPosition(),
                boothServiceDistance);
            if (!waiter.LastMoveSucceeded || group == null ||
                group.state != CustomerGroup.GroupState.NeedsBill)
                yield break;

            yield return waiter.FaceTowards(GetGroupCenter(group));
            yield return waiter.WorkFor(tableServiceSeconds);

            if (group == null || group.state != CustomerGroup.GroupState.NeedsBill)
                yield break;

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
            hands.ClearBill();
        }

        waiter.SetCarrying(false);
    }

    private IEnumerator DeliverPaymentToCashier(MoneyPickup payment)
    {
        CustomerGroup group = payment != null ? payment.TargetGroup : null;

        // Card payments are completed at the table. They never enter the cash
        // inventory, carrying, or cashier-counter workflow.
        if (payment != null && payment.IsCardPayment)
        {
            yield return CompleteCardPaymentAtTable(payment);
            yield break;
        }

        WaiterHands hands = WaiterHands.Instance;
        CashierRegisterUI register = ResolveCashierRegister();

        if (payment == null || group == null || hands == null || register == null || hands.HasMoney ||
            group.state != CustomerGroup.GroupState.NeedsBill || group.assignedBooth == null)
            yield break;

        Booth paymentBooth = group.assignedBooth;
        yield return waiter.MoveWithin(
            paymentBooth.GetNavigableApproachPosition(),
            boothServiceDistance,
            -1f,
            paymentTravelTimeoutSeconds);
        if (!waiter.LastMoveSucceeded)
        {
            // Dynamic customer congestion can occasionally keep the waiter a
            // fraction outside the booth interaction radius even with a valid
            // NavMesh path. Finish this already-claimed payment after the long
            // timeout so it cannot block the table forever.
            hands.PickupMoney(payment);
            if (hands.HasMoney && hands.HeldMoney == payment)
            {
                TryCompleteHeldPayment(
                    hands,
                    register,
                    "payment booth was not reachable before the timeout");
            }

            yield break;
        }

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

    private IEnumerator CompleteCardPaymentAtTable(MoneyPickup payment)
    {
        CustomerGroup group = payment != null ? payment.TargetGroup : null;
        if (waiter == null || payment == null || group == null ||
            group.state != CustomerGroup.GroupState.NeedsBill ||
            group.assignedBooth == null)
        {
            yield break;
        }

        yield return waiter.MoveWithin(
            group.assignedBooth.GetNavigableApproachPosition(),
            boothServiceDistance,
            -1f,
            paymentTravelTimeoutSeconds);

        if (waiter.LastMoveSucceeded)
        {
            yield return waiter.FaceTowards(GetGroupCenter(group));
            yield return waiter.WorkFor(tableServiceSeconds);
        }

        if (payment == null || group == null ||
            group.state != CustomerGroup.GroupState.NeedsBill)
        {
            yield break;
        }

        if (!payment.CompleteCardPayment())
        {
            Debug.LogWarning(
                $"[LobbyAutonomousService] Card payment for {group.name} could not be completed at the table.",
                payment);
        }
    }

    private IEnumerator CompleteHeldPaymentAtCashier(WaiterHands hands, CashierRegisterUI register = null)
    {
        if (hands == null || !hands.HasMoney || hands.holdingMoneyFor == null)
            yield break;

        if (register == null)
            register = ResolveCashierRegister();

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
                2f,
                paymentTravelTimeoutSeconds);
            if (!waiter.LastMoveSucceeded)
            {
                TryCompleteHeldPayment(
                    hands,
                    register,
                    "cashier stand point was not reachable before the timeout");
                yield break;
            }
        }

        yield return waiter.WorkFor(counterServiceSeconds);

        TryCompleteHeldPayment(hands, register, null);

        waiter.SetCarrying(hands.HasMoney);
    }

    private bool TryCompleteHeldPayment(
        WaiterHands hands,
        CashierRegisterUI register,
        string fallbackReason)
    {
        if (hands == null || register == null || !hands.HasMoney ||
            hands.holdingMoneyFor == null)
        {
            return false;
        }

        CustomerGroup paidGroup = hands.holdingMoneyFor;
        if (!register.CompleteAutomatedPayment(paidGroup))
            return false;

        hands.ClearMoney();
        waiter.SetCarrying(false);

        if (!string.IsNullOrEmpty(fallbackReason))
        {
            Debug.LogWarning(
                $"[LobbyAutonomousService] Completed {paidGroup.name}'s held payment " +
                $"through fallback because the {fallbackReason}.",
                this);
        }

        return true;
    }

    private CashierRegisterUI ResolveCashierRegister()
    {
        if (cashierRegister == null)
        {
            cashierRegister = CashierRegisterUI.Instance != null
                ? CashierRegisterUI.Instance
                : FindFirstObjectByType<CashierRegisterUI>(FindObjectsInactive.Include);
        }

        return cashierRegister;
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
        Booth[] booths = cachedBooths;
        for (int i = 0; i < booths.Length; i++)
        {
            if (booths[i] != null && booths[i].IsAvailableFor(groupSize))
                return booths[i];
        }

        return null;
    }

    private CustomerGroup FindGroupInState(CustomerGroup.GroupState state)
    {
        CustomerGroup[] groups = cachedGroups;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && !groups[i].IsTakeout && groups[i].state == state &&
                !groups[i].IsPlayerReviewingOrder &&
                RestaurantTaskClaim.CanBotStart(groups[i], managerReactionSeconds))
                return groups[i];
        }

        return null;
    }

    private CustomerGroup FindBillDeliveryTarget()
    {
        CustomerGroup[] groups = cachedGroups;
        for (int i = 0; i < groups.Length; i++)
        {
            CustomerGroup group = groups[i];
            if (group != null && !group.IsTakeout && group.state == CustomerGroup.GroupState.NeedsBill &&
                !group.HasReceivedBill &&
                RestaurantTaskClaim.CanBotStart(group, managerReactionSeconds))
                return group;
        }

        return null;
    }

    private CustomerGroup FindTakeoutOrderTarget()
    {
        CustomerGroup group = takeoutFlow != null ? takeoutFlow.ActiveGroup : null;
        return IsTakeoutOrderReady(group) && !group.IsPlayerReviewingOrder &&
               RestaurantTaskClaim.CanBotStart(group, managerReactionSeconds)
            ? group
            : null;
    }

    private CustomerGroup FindTakeoutPaymentTarget()
    {
        CustomerGroup group = takeoutFlow != null ? takeoutFlow.ActiveGroup : null;
        return IsTakeoutPaymentReady(group) &&
               RestaurantTaskClaim.CanBotStart(group, managerReactionSeconds)
            ? group
            : null;
    }

    private TakeoutBagInteractable FindReadyTakeoutBag()
    {
        CustomerGroup target = takeoutFlow != null ? takeoutFlow.ActiveGroup : null;
        if (!IsTakeoutBagDeliveryReady(target))
            return null;

        TakeoutBagInteractable[] bags = cachedTakeoutBags;
        for (int i = 0; i < bags.Length; i++)
        {
            if (bags[i] != null && bags[i].TargetGroup == target &&
                RestaurantTaskClaim.CanBotStart(bags[i], managerReactionSeconds))
                return bags[i];
        }

        return null;
    }

    private FoodTray FindReadyDeliveryTray()
    {
        if (WaiterHands.Instance != null &&
            (WaiterHands.Instance.HasTray || WaiterHands.Instance.HasBill || WaiterHands.Instance.HasMoney))
            return null;

        FoodTray[] trays = cachedFoodTrays;
        for (int i = 0; i < trays.Length; i++)
        {
            FoodTray tray = trays[i];
            FoodTrayInteractable interactable = tray != null
                ? tray.GetComponent<FoodTrayInteractable>()
                : null;
            if (tray != null && interactable != null && interactable.IsDeliveryPickable &&
                tray.TargetGroup != null &&
                tray.TargetGroup.state == CustomerGroup.GroupState.OrderTaken &&
                RestaurantTaskClaim.CanBotStart(tray, managerReactionSeconds))
                return tray;
        }

        return null;
    }

    private FoodTray FindCleanupTray()
    {
        if (BusserHands.Instance != null && BusserHands.Instance.HasTray)
            return null;

        FoodTray[] trays = cachedFoodTrays;
        for (int i = 0; i < trays.Length; i++)
        {
            FoodTray tray = trays[i];
            if (tray == null)
                continue;

            FoodTrayInteractable interactable = tray.GetComponent<FoodTrayInteractable>();
            if (interactable == null || !interactable.IsCleanupPickable)
                continue;

            CustomerGroup group = tray.TargetGroup;
            if (group == null || group.state == CustomerGroup.GroupState.Leaving ||
                group.state == CustomerGroup.GroupState.AngryLeft || group.state == CustomerGroup.GroupState.UnhappyLeft)
            {
                if (RestaurantTaskClaim.CanBotStart(tray, managerReactionSeconds))
                    return tray;
            }
        }

        return null;
    }

    private MoneyPickup FindPendingPaymentPickup()
    {
        MoneyPickup[] payments = cachedPayments;
        for (int i = 0; i < payments.Length; i++)
        {
            MoneyPickup payment = payments[i];
            if (payment != null && payment.IsAvailableForBotCollection &&
                !RestaurantTaskClaim.IsClaimedByPlayer(payment) &&
                !RestaurantTaskClaim.IsClaimedByBot(payment))
            {
                return payment;
            }
        }

        return null;
    }

    private Booth FindDirtyBooth()
    {
        Booth[] booths = cachedBooths;
        for (int i = 0; i < booths.Length; i++)
        {
            if (booths[i] != null && booths[i].CanCleanMessNow &&
                RestaurantTaskClaim.CanBotStart(booths[i], managerReactionSeconds))
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

    private Transform ResolveTrolleyParkingPoint(EquipmentUpgradeEffect effect)
    {
        if (effect == EquipmentUpgradeEffect.WaiterTrolley)
        {
            if (waiterTrolleyParkingPoint != null)
                return waiterTrolleyParkingPoint;

            if (kitchenManager != null && kitchenManager.traySpawnPoints != null)
            {
                for (int i = 0; i < kitchenManager.traySpawnPoints.Length; i++)
                {
                    if (kitchenManager.traySpawnPoints[i] != null)
                        return kitchenManager.traySpawnPoints[i];
                }
            }

            return FindStation(null, "WaiterHomePoint");
        }

        if (busserTrolleyParkingPoint != null)
            return busserTrolleyParkingPoint;
        if (sink != null && sink.StandPoint != null)
            return sink.StandPoint;
        return FindStation(null, "BusserHomePoint");
    }

    private static void ShutdownTrolley(
        BotTrolleyCarrier trolley,
        AutonomousStaffBot owner)
    {
        if (trolley == null)
            return;

        Vector3 releasePosition = owner != null
            ? owner.transform.position
            : trolley.transform.position;
        trolley.ReleaseAllForRetry(releasePosition);
        trolley.EndUse(true);
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

        // AutonomousStaffBot is the sole movement/animation owner for staff.
        // Leaving PlayerMovement enabled makes both components write the agent
        // and Animator every frame, causing stutter and animation flicker.
        PlayerMovement legacyPlayerMovement = roleObject.GetComponent<PlayerMovement>();
        if (legacyPlayerMovement != null)
            legacyPlayerMovement.enabled = false;

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
