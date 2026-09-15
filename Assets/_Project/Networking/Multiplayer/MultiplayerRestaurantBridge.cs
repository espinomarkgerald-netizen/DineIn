using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Transports existing manager data and invokes existing operations; owns no restaurant rules.
public sealed partial class MultiplayerRestaurantBridge : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte CommandEvent = 173, ReplyEvent = 174, SnapshotRequestEvent = 175;
    private const string SnapshotKey = "restaurant.management.v2";
    [Serializable] private sealed class Command
    { public string run, id, operation, target; public int day, value; }
    [Serializable] private sealed class Reply { public string id; public bool accepted; }
    [Serializable] public sealed class Snapshot
    {
        public string run;
        public int revision, phase;
        public bool slotsLocked, economyOnly;
        public GameSaveData data;
        public DailyObjectiveManager.NetworkState objectives;
        public int[] finance;
    }
    [Serializable] private sealed class StaffState
    { public List<EmployeeSaveEntry> employees; public int next, processed; public bool unseen; }
    private MultiplayerSessionManager session;
    private int revision, receivedRevision, economyRevision;
    private float nextPublish, retryAt, requestUntil;
    private bool committing;
    private Command pending;
    private readonly Dictionary<string, bool> receipts = new();
    private string previousPayload, staffSignature, equipmentSignature, menuSignature, polishSignature, inventorySignature;
    public bool HasState { get; private set; }
    public static event Action StateChanged;
    public static bool IsActive => MultiplayerSessionManager.Instance != null && !MultiplayerProgressionContext.RestorationInProgress;
    public static MultiplayerRestaurantBridge Active => MultiplayerSessionManager.Instance?.GetComponent<MultiplayerRestaurantBridge>();
    public static bool Committing => Active != null && Active.committing;
    public static bool CanEdit => !IsActive || (MultiplayerSessionManager.Instance.CanAct && Active != null && Active.pending == null
        && GameDayManager.Instance != null && !GameDayManager.Instance.ServiceActive && !GameDayManager.Instance.HasDayResults);
    public static bool IsObserver => IsActive && !MultiplayerSessionManager.Instance.IsAuthority;
    private void Awake() => session = GetComponent<MultiplayerSessionManager>();
    public int NextRevision() => ++revision;
    public void RunSystemAction(Action operation)
    {
        bool previous = committing;
        committing = true;
        try { operation(); } finally { committing = previous; managementDirty = true; }
    }

    private void Update()
    {
        SubscribeChanges();
        if (!session.IsConnected || !MultiplayerProgressionContext.LocallyPrepared) return;
        if (session.IsAuthority && Time.unscaledTime >= nextPublish) Publish();
        else if (!session.IsHostConnection && !HasState) ReadSnapshot();
        if (pending == null) return;
        if (!session.CanAct || Time.unscaledTime > requestUntil)
        { pending = null; WarningSlideUI.Instance?.Show("Restaurant request timed out. Check the shared state before retrying."); StateChanged?.Invoke(); }
        else if (Time.unscaledTime >= retryAt) SendPending();
    }
    public static bool Request(string operation, string target = null, int value = 0)
    {
        var bridge = Active;
        if (bridge == null || !CanEdit) return false;
        bridge.pending = new Command { run = bridge.session.RunId, day = GameFlowManager.Instance.CurrentDay,
            id = Guid.NewGuid().ToString("N"), operation = operation, target = target, value = value };
        bridge.requestUntil = Time.unscaledTime + 8f;
        bridge.SendPending();
        return true;
    }
    private void SendPending()
    {
        retryAt = Time.unscaledTime + 1f;
        if (session.IsAuthority) Handle(session.LocalActorNumber, pending);
        else PhotonNetwork.RaiseEvent(CommandEvent, JsonUtility.ToJson(pending),
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    private void Handle(int actor, Command command)
    {
        if (!session.IsAuthority || command == null || !session.ValidActor(actor) || command.run != session.RunId
            || command.day != GameFlowManager.Instance.CurrentDay || !Guid.TryParseExact(command.id, "N", out _)) return;
        string key = actor + ":" + command.id;
        if (!receipts.TryGetValue(key, out bool accepted))
        {
            accepted = false;
            if (MultiplayerProgressionContext.Ready && !GameDayManager.Instance.ServiceActive && !GameDayManager.Instance.HasDayResults)
            {
                committing = true;
                try { accepted = Execute(command); }
                finally { committing = false; }
            }
            receipts[key] = accepted;
            // Receipt lifetime spans this day; old-day commands are rejected by the day token.
            Publish(true);
        }
        var reply = new Reply { id = command.id, accepted = accepted };
        if (actor == session.LocalActorNumber) Receive(reply);
        else PhotonNetwork.RaiseEvent(ReplyEvent, JsonUtility.ToJson(reply),
            new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }
    private bool Execute(Command command)
    {
        var employees = EmployeeManager.Instance;
        var employee = employees?.allEmployees.Find(e => e != null && e.EmployeeID == command.target);
        var product = MenuCatalog.Default?.FindProduct(command.target);
        switch (command.operation)
        {
            case "hire": return employee != null && employees.HireApplicant(employee);
            case "fire": return employee != null && employees.FireEmployee(employee);
            case "decline": return employee != null && employees.DeclineApplicant(employee);
            case "assign": return employee != null && employees.AssignEmployeeForDay(employee);
            case "unassign": return employee != null && employees.UnassignEmployeeForDay(employee);
            case "equipment":
                var equipment = EquipmentManager.Instance?.AllEquipment.Find(e => e != null && e.itemID == command.target);
                return equipment != null && equipment.dayToUnlock <= GameFlowManager.Instance.ProgressionDay
                    && EquipmentManager.Instance.Purchase(equipment.itemID);
            case "menu": return product != null && MenuAvailabilityManager.Instance.SetProductAvailable(product, command.value != 0);
            case "price": return product != null && command.value >= 0 && MenuAvailabilityManager.Instance.SetProductPrice(product, command.value);
            case "newspaper": CasualDiningPolishManager.Instance?.MarkCurrentIssueViewed(); return true;
            default: return false;
        }
    }
    private void Receive(Reply reply)
    {
        if (reply == null || pending == null || reply.id != pending.id) return;
        pending = null;
        if (!reply.accepted) WarningSlideUI.Instance?.Show("That restaurant choice is no longer available. The shared state has been refreshed.");
        StateChanged?.Invoke();
    }
    public void ResetDay()
    {
        pending = null; receipts.Clear(); previousPayload = null; managementDirty = economyDirty = true;
        staffSignature = equipmentSignature = menuSignature = polishSignature = inventorySignature = null;
    }
    public Snapshot Capture()
    {
        var data = GameSaveManager.Instance.CaptureRuntimeState();
        return new Snapshot { run = session.RunId, revision = revision,
            phase = (int)GameFlowManager.Instance.CurrentRestaurantSessionState,
            slotsLocked = EmployeeManager.Instance != null && EmployeeManager.Instance.SlotsLocked,
            data = data, objectives = DailyObjectiveManager.Instance?.CaptureNetworkState(),
            finance = DailyFinanceBridge.Instance?.CaptureNetworkState() };
    }
    public void Publish(bool force = false)
    {
        if (!session.IsHostConnection || !MultiplayerProgressionContext.LocallyPrepared || GameSaveManager.Instance == null) return;
        nextPublish = Time.unscaledTime + 0.1f;
        if (!force && !managementDirty && !economyDirty) return;
        bool full = force || managementDirty || !HasState;
        using var measurement = MultiplayerDiagnostics.EconomyState.Auto();
        var snapshot = full ? Capture() : CaptureEconomy();
        managementDirty = economyDirty = false;
        snapshot.revision = 0;
        string payload = JsonUtility.ToJson(snapshot);
        if (!force && previousPayload == payload) return;
        previousPayload = payload;
        snapshot.revision = NextRevision();
        HasState = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [full ? SnapshotKey : EconomyKey] = JsonUtility.ToJson(snapshot) });
        if (full) StateChanged?.Invoke();
    }
    public void RequestSnapshot()
    {
        HasState = false;
        receivedRevision = 0; economyRevision = 0;
        if (!session.IsConnected) return;
        ReadSnapshot();
        PhotonNetwork.RaiseEvent(SnapshotRequestEvent, null,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    private void ReadSnapshot()
    {
        if (PhotonNetwork.CurrentRoom?.CustomProperties[SnapshotKey] is not string json || json.Length > 1048576) return;
        try { Apply(JsonUtility.FromJson<Snapshot>(json)); }
        catch (ArgumentException) { Debug.LogWarning("[Multiplayer] Invalid restaurant snapshot."); }
    }
    public void Apply(Snapshot snapshot)
    {
        if (session.IsHostConnection || !MultiplayerProgressionContext.LocallyPrepared || snapshot?.data == null
            || snapshot.run != session.RunId || (!snapshot.economyOnly && snapshot.revision <= receivedRevision) || snapshot.data.currentDay < GameFlowManager.Instance.CurrentDay) return;
        if (snapshot.economyOnly)
        {
            if (!HasState || snapshot.data.currentDay != GameFlowManager.Instance.CurrentDay || snapshot.revision <= economyRevision) return;
            ApplyEconomy(snapshot.data, snapshot.revision);
            AlienApprovalManager.Instance?.ApplyMultiplayerApproval(snapshot.data.approval);
            DailyObjectiveManager.Instance?.ApplyNetworkState(snapshot.objectives);
            DailyFinanceBridge.Instance?.ApplyNetworkState(snapshot.finance);
            return;
        }
        bool newDay = GameFlowManager.Instance.CurrentDay != snapshot.data.currentDay;
        if (newDay) { GameDayManager.Instance?.PrepareNextMultiplayerDay(); ResetDay(); }
        receivedRevision = snapshot.revision;
        HasState = true;
        GameFlowManager.Instance.ApplyMultiplayerState(snapshot.data.currentDay, snapshot.data.campaignCompleted,
            (GameFlowManager.RestaurantSessionState)snapshot.phase);
        if (snapshot.revision > economyRevision)
        {
            ApplyEconomy(snapshot.data, snapshot.revision);
            AlienApprovalManager.Instance?.ApplyMultiplayerApproval(snapshot.data.approval);
            DailyObjectiveManager.Instance?.ApplyNetworkState(snapshot.objectives);
            DailyFinanceBridge.Instance?.ApplyNetworkState(snapshot.finance);
        }
        UnlockManager.Instance?.ApplySaveData(snapshot.data);
        string staff = JsonUtility.ToJson(new StaffState { employees = snapshot.data.employees,
            next = snapshot.data.employeeApplicantNextRefreshDay, processed = snapshot.data.employeeApplicantLastProcessedDay,
            unseen = snapshot.data.employeeApplicantsUnseen });
        bool changed = newDay;
        if (staff != staffSignature)
        { staffSignature = staff; EmployeeManager.Instance?.ApplySaveData(snapshot.data); changed = true; }
        EmployeeManager.Instance?.ApplyMultiplayerSlotLock(snapshot.slotsLocked);
        string equipment = string.Join("|", snapshot.data.purchasedEquipmentIDs);
        if (equipment != equipmentSignature)
        {
            equipmentSignature = equipment;
            EquipmentManager.Instance?.ApplySaveData(snapshot.data);
            foreach (var link in FindObjectsByType<EquipmentLink>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                link.gameObject.SetActive(EquipmentManager.Instance.Purchased(link.itemID));
            changed = true;
        }
        var menuData = new GameSaveData { disabledMenuProductIDs = snapshot.data.disabledMenuProductIDs,
            menuPriceOverrides = snapshot.data.menuPriceOverrides };
        string menu = JsonUtility.ToJson(menuData);
        if (menu != menuSignature) { menuSignature = menu; MenuAvailabilityManager.Instance?.ApplySaveData(snapshot.data); changed = true; }
        var source = snapshot.data;
        var polishData = new GameSaveData
        {
            saveSchemaVersion = source.saveSchemaVersion, polishPreparedDay = source.polishPreparedDay,
            polishLastFinalizedDay = source.polishLastFinalizedDay, polishDayStartApproval = source.polishDayStartApproval,
            polishDayStartMoney = source.polishDayStartMoney, restaurantRatingScore = source.restaurantRatingScore,
            supplierMarketGeneratedDay = source.supplierMarketGeneratedDay, lastDailyRestaurantSnapshot = source.lastDailyRestaurantSnapshot,
            supplierPrices = source.supplierPrices, restaurantRatingHistory = source.restaurantRatingHistory,
            restaurantReviews = source.restaurantReviews, newspaperTemplateHistory = source.newspaperTemplateHistory,
            newspaperIssues = source.newspaperIssues
        };
        string polish = JsonUtility.ToJson(polishData);
        if (polish != polishSignature)
        { polishSignature = polish; CasualDiningPolishManager.Instance?.ApplySaveData(source); changed = true; }
        ReadEconomy();
        session.SnapshotRestored();
        if (changed) StateChanged?.Invoke();
    }
    public void ApplyEconomy(GameSaveData data, int stamp)
    {
        if (data == null || stamp <= economyRevision) return;
        economyRevision = stamp;
        string inventory = JsonUtility.ToJson(new GameSaveData
        {
            saveSchemaVersion = data.saveSchemaVersion, inventorySystemVersion = data.inventorySystemVersion,
            discardedUnitsToday = data.discardedUnitsToday, inventoryStocks = data.inventoryStocks,
            inventoryStockBatches = data.inventoryStockBatches
        });
        if (inventory != inventorySignature)
        { inventorySignature = inventory; InventoryManager.Instance?.ApplySaveData(data); }
        MoneyManager.Instance?.ApplySaveData(data);
    }
    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (session.IsHostConnection) return;
        if (changed.ContainsKey(SnapshotKey)) ReadSnapshot();
        if (changed.ContainsKey(EconomyKey)) ReadEconomy();
    }
    public void OnEvent(EventData ev)
    {
        if (!session.IsConnected) return;
        if (ev.Code == SnapshotRequestEvent && session.IsAuthority && session.ValidActor(ev.Sender)) { Publish(true); return; }
        if (ev.CustomData is not string json || json.Length > 4096) return;
        try
        {
            if (ev.Code == CommandEvent && session.IsAuthority) Handle(ev.Sender, JsonUtility.FromJson<Command>(json));
            else if (ev.Code == ReplyEvent && ev.Sender == session.Run?.hostActor) Receive(JsonUtility.FromJson<Reply>(json));
        }
        catch (ArgumentException) { Debug.LogWarning("[Multiplayer] Invalid management request."); }
    }
}
