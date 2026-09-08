using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Scene-owned transport over the existing inventory/order ledger. No campaign persistence.
public sealed class MultiplayerRestockBridge : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte RequestEvent = 171, ReplyEvent = 172;
    private const string SnapshotKey = "Restaurant:Restock";
    [Serializable] private sealed class Request
    {
        public int sequence;
        public string operation, restaurant, shelf;
        public string[] items;
        public int[] quantities;
        public int column, row;
        public bool wrongStorageConfirmed;
    }
    [Serializable] private sealed class Receipt
    {
        public int actor, sequence;
        public bool accepted;
        public string message;
    }
    [Serializable] private sealed class Snapshot
    {
        public int revision, owner;
        public GameSaveData data = new GameSaveData();
        public List<Receipt> receipts = new List<Receipt>();
    }
    [Serializable] private sealed class Reply
    {
        public Receipt receipt;
        public string snapshot;
    }
    [Serializable] private sealed class ShelfLayout { public Shelf[] shelves; }
    [Serializable] private sealed class Shelf
    {
        public string id;
        public RestockStorageType storage;
        public int columns, rows;
    }

    private MultiplayerSessionManager session;
    private ShelfLayout layout;
    private Snapshot state = new Snapshot();
    private string published;
    private string displayedLedger;
    private bool initialized, committing;
    private int sequence;
    private Request pending;
    private Action<bool, string> completion;
    private float nextPublish, nextRetry;
    private readonly HashSet<string> seenNotices = new HashSet<string>();

    public static bool IsActive => MultiplayerSessionManager.Instance != null
        && MultiplayerSessionManager.Instance.IsMultiplayerSession;
    public static MultiplayerRestockBridge Active => IsActive
        ? MultiplayerSessionManager.Instance.GetComponent<MultiplayerRestockBridge>() : null;
    public static bool CanCommit => Active != null && Active.session.IsAuthority && Active.committing;
    public static bool ObserveOnly => IsActive && !MultiplayerSessionManager.Instance.IsAuthority;
    public static bool CanUsePayload => !IsActive || (Active != null
        && Active.state.owner == Active.session.LocalActorNumber);
    public static bool RequestPending => IsActive && (Active == null || Active.pending != null);

    private ManagementComputerController computer;
    private ManagementComputerController Computer
    {
        get
        {
            if (computer == null) computer = FindFirstObjectByType<ManagementComputerController>();
            return computer;
        }
    }
    private RestaurantStorageConfig Storage => Computer?.RestockStorage;
    private IReadOnlyList<ItemData> Items => MultiplayerProgressionContext.Catalog?.Ingredients;

    private void Awake()
    {
        session = GetComponent<MultiplayerSessionManager>();
        // Export of RestockScene + DryRoomShelf's authored StableShelfId/cell layout.
        // Authority validates this even when only a remote client loads the local view.
        var asset = Resources.Load<TextAsset>("MultiplayerRestockShelves");
        if (asset != null) layout = JsonUtility.FromJson<ShelfLayout>(asset.text);
    }

    private void Update()
    {
        if (!IsActive || !MultiplayerProgressionContext.Ready) return;
        if (!initialized)
        {
            initialized = true;
            ReadSnapshot();
            if (session.IsAuthority) Publish();
        }
        if (session.IsAuthority && Time.unscaledTime >= nextPublish) Publish();
        if (pending != null && Time.unscaledTime >= nextRetry) SendPending();
    }

    public void RequestOrder(IReadOnlyList<RestockCartLine> cart, Action<bool, string> done)
    {
        var request = new Request { operation = "order", items = new string[cart.Count],
            quantities = new int[cart.Count] };
        for (int i = 0; i < cart.Count; i++)
        {
            request.items[i] = cart[i].item.StableItemId;
            request.quantities[i] = cart[i].quantity;
        }
        Submit(request, done);
    }

    public void RequestCollect(Action<bool, string> done) => Submit(new Request { operation = "collect" }, done);
    public void RequestEntry(Action<bool, string> done) => Submit(new Request { operation = "enter" }, done);
    public void Release() => Submit(new Request { operation = "release" }, null);
    public void RequestPlace(ItemData item, ShelfGrid grid, int column, int row, Action<bool, string> done)
        => Submit(new Request { operation = "place", items = new[] { item.StableItemId },
            shelf = grid.StableShelfId, column = column, row = row,
            wrongStorageConfirmed = grid.StorageType != item.requiredStorage }, done);

    private void Submit(Request request, Action<bool, string> done)
    {
        if (!IsActive || !initialized || session.LocalManager == null || pending != null)
        {
            done?.Invoke(false, "Restock is waiting for the restaurant authority. Please try again.");
            return;
        }
        request.sequence = ++sequence;
        request.restaurant = Storage?.RestaurantID;
        pending = request;
        completion = done;
        SendPending();
    }

    private void SendPending()
    {
        nextRetry = Time.unscaledTime + 2f;
        if (session.IsAuthority) HandleRequest(session.LocalActorNumber, pending);
        else PhotonNetwork.RaiseEvent(RequestEvent, JsonUtility.ToJson(pending),
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData ev)
    {
        if (!IsActive || !initialized || !(ev.CustomData is string json) || json.Length > 131072) return;
        if (ev.Code == RequestEvent && session.IsAuthority)
        {
            if (json.Length > 16384) return;
            try { HandleRequest(ev.Sender, JsonUtility.FromJson<Request>(json)); }
            catch (ArgumentException) { Debug.LogWarning("[MultiplayerRestock] Invalid request data."); }
        }
        else if (ev.Code == ReplyEvent && ev.Sender == PhotonNetwork.MasterClient.ActorNumber)
            ReceiveReply(JsonUtility.FromJson<Reply>(json));
    }

    private void HandleRequest(int actor, Request request)
    {
        if (request == null || request.sequence <= 0 || !session.IsAuthority || !MultiplayerProgressionContext.Ready) return;
        if (!PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out var player) || player.IsInactive
            || !session.TryGetManager(actor, out var manager) || !manager.activeInHierarchy) return;
        Receipt previous = state.receipts.Find(r => r.actor == actor);
        if (previous != null && request.sequence <= previous.sequence)
        {
            if (request.sequence == previous.sequence) SendReply(actor, previous);
            return;
        }
        string message = "Restock request could not be completed.";
        bool accepted = false;
        committing = true;
        try
        {
            if (Storage == null || request.restaurant != Storage.RestaurantID)
                message = "The restaurant storage configuration does not match.";
            else if (request.operation == "order") accepted = Order(request, out message);
            else if (request.operation == "release")
            {
                accepted = state.owner == 0 || state.owner == actor;
                if (accepted) state.owner = 0;
            }
            else if (state.owner != 0 && state.owner != actor)
                message = "Another Player is restocking. Wait for them to leave the stock room.";
            else if (request.operation == "enter")
            {
                state.owner = actor;
                accepted = true;
            }
            else if (request.operation == "collect")
            {
                accepted = RestockOrderManager.Instance.CollectDeliveredOrders();
                if (accepted) state.owner = actor;
                message = accepted ? "ORDER COLLECTED" : "That delivery has already been collected or is not ready.";
            }
            else if (request.operation == "place" && state.owner == actor)
                accepted = Place(request, out message);
        }
        finally { committing = false; }
        var receipt = new Receipt { actor = actor, sequence = request.sequence, accepted = accepted, message = message };
        if (previous != null) state.receipts.Remove(previous);
        state.receipts.Add(receipt);
        Publish();
        SendReply(actor, receipt);
    }

    private bool Order(Request request, out string message)
    {
        message = "Invalid cart or insufficient restaurant storage/money.";
        if (Items == null || InventoryManager.Instance == null || request.items == null
            || request.quantities == null || request.items.Length == 0 || request.items.Length > Items.Count
            || request.items.Length != request.quantities.Length) return false;
        var cart = new List<RestockCartLine>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        for (int i = 0; i < request.items.Length; i++)
        {
            ItemData item = FindItem(request.items[i]);
            int quantity = request.quantities[i];
            if (item == null || !IsUnlocked(item) || item.worldContainerPrefab == null || quantity <= 0
                || quantity > Storage.GetCapacity(item.requiredStorage) || !ids.Add(item.StableItemId)) return false;
            total += (long)CasualDiningPolishManager.GetCurrentBoxCostOrBase(item) * quantity;
            cart.Add(new RestockCartLine { item = item, quantity = quantity });
        }
        if (total <= 0 || total > int.MaxValue || MoneyManager.Instance == null
            || !MoneyManager.Instance.HasEnough((int)total)) return false;
        foreach (RestockStorageType type in Enum.GetValues(typeof(RestockStorageType)))
        {
            int used = RestockOrderManager.Instance.GetReservedContainers(type, Items);
            foreach (ItemData item in Items)
                if (item != null && item.requiredStorage == type)
                    used += Mathf.CeilToInt(InventoryManager.Instance.GetStock(item.itemType) / (float)Mathf.Max(1, item.unitsPerBox));
            foreach (RestockCartLine line in cart)
                if (line.item.requiredStorage == type) used += line.quantity;
            if (used > Storage.GetCapacity(type)) return false;
        }
        bool accepted = Computer != null && Computer.ConfirmRestockOrderOnAuthority(cart);
        if (accepted) message = "Order placed. The containers are reserved for delivery.";
        return accepted;
    }

    private bool Place(Request request, out string message)
    {
        message = "The shelf cell or delivered box is no longer available.";
        ItemData item = request.items != null && request.items.Length == 1 ? FindItem(request.items[0]) : null;
        Shelf shelf = layout?.shelves == null ? null : Array.Find(layout.shelves, s => s.id == request.shelf);
        var orders = RestockOrderManager.Instance;
        var inventory = InventoryManager.Instance;
        if (item == null || item.worldContainerPrefab == null || shelf == null || inventory == null
            || request.column < 0 || request.column >= shelf.columns || request.row < 0 || request.row >= shelf.rows
            || orders.GetHotbarContainers(item) <= 0
            || (shelf.storage != item.requiredStorage && !request.wrongStorageConfirmed)) return false;
        int occupied = 0;
        foreach (var entry in orders.StoredContainers)
        {
            if (entry == null || !inventory.TryGetBatch(entry.stockBatchID, out var batch) || batch.unitsRemaining <= 0) continue;
            if (entry.shelfID == request.shelf && entry.column == request.column && entry.row == request.row) return false;
            if (entry.storageType == shelf.storage) occupied++;
        }
        if (occupied >= Storage.GetCapacity(shelf.storage)) return false;
        // Include starter stock and partially consumed batches, not just visible delivered boxes.
        var capacityData = new GameSaveData();
        inventory.FillSaveData(capacityData);
        int used = 0;
        foreach (var batch in capacityData.inventoryStockBatches)
        {
            if (batch.currentStorage != shelf.storage || batch.unitsRemaining <= 0) continue;
            ItemData storedItem = null;
            foreach (var candidate in Items)
                if (candidate != null && candidate.itemType == batch.itemType) { storedItem = candidate; break; }
            if (storedItem != null)
                used += Mathf.CeilToInt(batch.unitsRemaining / (float)Mathf.Max(1, storedItem.unitsPerBox));
        }
        if (used >= Storage.GetCapacity(shelf.storage)) return false;
        if (!orders.TryStoreOneContainer(item, shelf.storage, out message, out string batchID, out _)) return false;
        orders.RegisterAuthoritativeContainer(new RestockStoredContainerSaveData
        {
            containerID = Guid.NewGuid().ToString("N"), stockBatchID = batchID,
            itemID = item.StableItemId, itemType = item.itemType, shelfID = shelf.id,
            column = request.column, row = request.row, storageType = shelf.storage,
            wrongStorage = shelf.storage != item.requiredStorage,
            rotationY = item.worldContainerPrefab.transform.eulerAngles.y
        });
        return true;
    }

    private bool IsUnlocked(ItemData item)
    {
        int day = int.MaxValue;
        var catalog = MultiplayerProgressionContext.Catalog;
        foreach (var product in catalog.Products)
        {
            if (product == null || product.ingredients == null) continue;
            foreach (var ingredient in product.ingredients)
                if (ingredient?.item == item) day = Math.Min(day, Math.Max(1, product.dayToUnlock));
        }
        if (day == int.MaxValue) day = Math.Max(1, item.dayToUnlock);
        return day <= MultiplayerProgressionContext.CurrentDay
            || (UnlockManager.Instance != null && UnlockManager.Instance.IsIngredientUnlocked(item));
    }

    private ItemData FindItem(string id)
    {
        if (Items != null)
            foreach (var item in Items)
                if (item != null && item.StableItemId == id) return item;
        return null;
    }

    private void Publish()
    {
        nextPublish = Time.unscaledTime + 0.5f;
        var data = new GameSaveData { currentDay = MultiplayerProgressionContext.CurrentDay };
        InventoryManager.Instance?.FillSaveData(data);
        RestockOrderManager.Instance?.FillSaveData(data);
        data.money = MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0;
        // Notice acknowledgement is local UI state, never another player's acknowledgement.
        foreach (var order in data.restockOrders) order.deliveryNoticeShown = false;
        state.data = data;
        string json = JsonUtility.ToJson(state);
        if (json == published) return;
        state.revision++;
        published = JsonUtility.ToJson(state);
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [SnapshotKey] = published });
        RefreshViewIfChanged();
    }

    private void SendReply(int actor, Receipt receipt)
    {
        var reply = new Reply { receipt = receipt, snapshot = published };
        if (actor == session.LocalActorNumber) ReceiveReply(reply);
        else PhotonNetwork.RaiseEvent(ReplyEvent, JsonUtility.ToJson(reply),
            new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }

    private void ReceiveReply(Reply reply)
    {
        if (reply?.receipt == null) return;
        if (!session.IsAuthority) ApplySnapshot(reply.snapshot);
        if (pending == null || reply.receipt.sequence != pending.sequence) return;
        var done = completion;
        pending = null;
        completion = null;
        done?.Invoke(reply.receipt.accepted, reply.receipt.message);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (initialized && !session.IsAuthority && changed.ContainsKey(SnapshotKey)) ReadSnapshot();
    }
    private void ReadSnapshot()
    {
        if (PhotonNetwork.CurrentRoom?.CustomProperties[SnapshotKey] is string json) ApplySnapshot(json);
    }
    private void ApplySnapshot(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        Snapshot incoming = JsonUtility.FromJson<Snapshot>(json);
        if (incoming?.data == null || incoming.revision <= state.revision) return;
        foreach (var order in RestockOrderManager.Instance.Orders)
            if (order.deliveryNoticeShown) seenNotices.Add(order.orderID);
        foreach (var order in incoming.data.restockOrders)
            order.deliveryNoticeShown = seenNotices.Contains(order.orderID);
        state = incoming;
        var ownReceipt = state.receipts.Find(r => r.actor == session.LocalActorNumber);
        if (ownReceipt != null) sequence = Math.Max(sequence, ownReceipt.sequence);
        InventoryManager.Instance.ApplySaveData(state.data);
        MoneyManager.Instance?.ApplyMultiplayerRestockBalance(state.data.money);
        string ledger = LedgerSignature();
        if (ledger != displayedLedger)
            RestockOrderManager.Instance.ApplyMultiplayerSnapshot(state.data);
        RefreshViewIfChanged();
    }

    private string LedgerSignature()
    {
        // Stock consumption and receipt acknowledgements must not cancel a local hotbar drag.
        var data = new GameSaveData { restockOrders = state.data.restockOrders,
            restockStoredContainers = state.data.restockStoredContainers };
        data = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(data));
        foreach (var order in data.restockOrders) order.deliveryNoticeShown = false;
        return state.owner + ":" + JsonUtility.ToJson(data);
    }

    private void RefreshViewIfChanged()
    {
        string ledger = LedgerSignature();
        if (ledger == displayedLedger) return;
        displayedLedger = ledger;
        RestockFlowCoordinator.Instance?.RefreshMultiplayerRestockView();
    }

    public override void OnPlayerLeftRoom(Player player)
    {
        if (!session.IsAuthority) return;
        if (state.owner == player.ActorNumber) state.owner = 0;
        Publish();
    }
    public override void OnMasterClientSwitched(Player player)
    {
        if (!initialized) return;
        ReadSnapshot();
        if (session.IsAuthority)
        {
            if (!PhotonNetwork.CurrentRoom.Players.ContainsKey(state.owner)) state.owner = 0;
            published = null;
            Publish();
        }
        if (pending != null) SendPending();
    }
    public override void OnLeftRoom() => CancelLocal();
    public override void OnDisconnected(DisconnectCause cause) => CancelLocal();
    private void OnDestroy() => CancelLocal();
    private void CancelLocal()
    {
        initialized = false;
        var done = completion;
        completion = null;
        pending = null;
        done?.Invoke(false, "The multiplayer restaurant session ended.");
        RestockFlowCoordinator.Instance?.CloseMultiplayerRestockView();
    }
}
