using UnityEngine;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

// Claims and authoritative greeting/booth assignment.
public class MultiplayerCustomerInteractionBridge : MonoBehaviour, IOnEventCallback
{
    private const byte GreetRequestEvent = 185;
    private const byte SeatRequestEvent = 186, SeatResultEvent = 187;
    private RoleBasedAssignController seatController;
    private bool seatPending;
    private MultiplayerSessionManager session;
    private MultiplayerTaskClaims claims;
    private MultiplayerCustomerSpawn target;
    private Camera cameraForPopup;
    private string taskId;
    private bool pending, cancelled;
    private PlayerMovement greetMover;
    private IInteractable greetApproach;
    private bool greetApproached;
    private FoodTray pickupTarget;
    private string pickupTaskId;
    private bool pickupPending, pickupCancelled;
    private const byte CarryRequestEvent = 192, CarryRejectedEvent = 193;

    private const byte ServeRequestEvent = 194, ServeRejectedEvent = 195;

    public static bool CanAttemptServe(CustomerGroup group)
    {
        var session = MultiplayerSessionManager.Instance;
        var hands = session != null && session.LocalManager != null
            ? session.LocalManager.GetComponent<WaiterHands>() : null;
        return ReviewIsMultiplayer && group != null && group.state == CustomerGroup.GroupState.OrderTaken
            && hands != null && hands.HasTray && hands.holdingTray.NetworkCarryLocked;
    }

    public static bool TryServe(CustomerGroup group, PlayerMovement mover = null)
    {
        if (!ReviewIsMultiplayer) return false;
        var session = MultiplayerSessionManager.Instance;
        if (!CanAttemptServe(group) || (mover != null && mover.gameObject != session.LocalManager)) return true;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        var customer = group.GetComponentInParent<MultiplayerCustomerSpawn>();
        if (bridge == null || !bridge.isActiveAndEnabled || customer == null) return true;
        int order = session.LocalManager.GetComponent<WaiterHands>().holdingTray.orderNumber;
        if (session.IsAuthority) bridge.HandleServe(customer.photonView.ViewID, order, session.LocalActorNumber, session.LocalActorNumber);
        else PhotonNetwork.RaiseEvent(ServeRequestEvent,
            new object[] { customer.photonView.ViewID, order, session.LocalActorNumber },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        return true;
    }

    public static Transform ServePoint(CustomerGroup group, out Transform stand, out float radius)
    {
        stand = null;
        radius = 0f;
        if (group == null || group.assignedBooth == null) return null;
        var boothDelivery = group.assignedBooth.GetComponentInChildren<BoothDeliverInteractable>(true);
        if (boothDelivery != null && boothDelivery.DeliveryPoint != null)
        {
            stand = boothDelivery.StandPoint;
            radius = boothDelivery.GetInteractRadius();
            return boothDelivery.DeliveryPoint;
        }
        var delivery = group.assignedBooth.GetComponentInChildren<CustomerDeliverInteractable>(true);
        if (delivery == null) return null;
        stand = delivery.StandPoint;
        radius = delivery.GetInteractRadius();
        return delivery.DeliveryPoint;
    }

    private static bool SameProducts(System.Collections.Generic.IReadOnlyList<string> left,
        System.Collections.Generic.IReadOnlyList<string> right)
    {
        if (left == null || right == null || left.Count == 0 || left.Count != right.Count) return false;
        var a = new System.Collections.Generic.List<string>(left);
        var b = new System.Collections.Generic.List<string>(right);
        a.Sort(System.StringComparer.Ordinal);
        b.Sort(System.StringComparer.Ordinal);
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static bool CanReceiveContents(FoodTray tray, CustomerGroup group)
    {
        var catalog = MenuCatalog.Default;
        if (catalog == null) return false;
        var contents = tray.DeliveredContents;
        foreach (var item in contents)
            if (!string.IsNullOrWhiteSpace(item) &&
                (item.IndexOf("burnt", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 item.IndexOf("burned", System.StringComparison.OrdinalIgnoreCase) >= 0)) return false;
        // Validate the same name-to-product conversion used by the existing receipt method.
        return SameProducts(catalog.GetProductIds(catalog.ResolveProducts(contents)), group.submittedOrder.productIds);
    }

    private void HandleServe(int viewId, int order, int actor, int sender)
    {
        if (!session.IsAuthority || actor != sender) return;
        var view = PhotonView.Find(viewId);
        var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
        var group = customer != null ? customer.Group : null;
        var kitchen = FindFirstObjectByType<KitchenManager>();
        var tray = kitchen != null ? kitchen.GetPreparedResult(order) : null;
        var drop = ServePoint(group, out var stand, out float radius);
        bool accepted = PhotonNetwork.CurrentRoom.Players.TryGetValue(sender, out var player) && !player.IsInactive
            && session.TryGetManager(sender, out var manager) && manager != null && manager.activeInHierarchy
            && customer != null && customer.CarrierActorNumber == sender && !customer.CarrierNeedsRecovery
            && group != null && group.gameObject.activeInHierarchy && !group.IsNetworkObserver
            && group.state == CustomerGroup.GroupState.OrderTaken && group.HasConfirmedOrder && !group.IsPlayerReviewingOrder
            && group.currentOrderNumber == order && tray != null && tray.orderNumber == order && tray.TargetGroup == group
            && tray.NetworkCarryLocked && !tray.ContainsBurntFood
            && group.submittedOrder != null && group.currentOrder != null
            && SameProducts(tray.DeliveredProductIds, group.submittedOrder.productIds)
            && SameProducts(tray.DeliveredProductIds, group.currentOrder.productIds) && CanReceiveContents(tray, group)
            && group.assignedBooth != null && group.assignedBooth.CurrentGroup == group
            && drop != null && stand != null
            && Vector2.Distance(new Vector2(manager.transform.position.x, manager.transform.position.z),
                new Vector2(stand.position.x, stand.position.z)) <= radius
            && manager.GetComponent<WaiterHands>() is WaiterHands hands && hands.holdingTray == tray
            && customer.CommitServe(hands, tray, drop);
        if (accepted) return;
        if (sender == session.LocalActorNumber) WarningSlideUI.Instance?.Show("Cannot serve here. Carry the matching order to its table.");
        else PhotonNetwork.RaiseEvent(ServeRejectedEvent, order,
            new RaiseEventOptions { TargetActors = new[] { sender } }, SendOptions.SendReliable);
    }

    private void RequestCarry()
    {
        var customer = pickupTarget != null && pickupTarget.TargetGroup != null
            ? pickupTarget.TargetGroup.GetComponentInParent<MultiplayerCustomerSpawn>() : null;
        if (customer == null) return;
        int viewId = customer.photonView.ViewID;
        if (session.IsAuthority) HandleCarry(viewId, pickupTarget.orderNumber, session.LocalActorNumber, session.LocalActorNumber);
        else PhotonNetwork.RaiseEvent(CarryRequestEvent,
            new object[] { viewId, pickupTarget.orderNumber, session.LocalActorNumber },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    private void HandleCarry(int viewId, int order, int actor, int sender)
    {
        if (!session.IsAuthority || actor != sender) return;
        var view = PhotonView.Find(viewId);
        var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
        string id = $"Order:{order}:Pickup";
        bool accepted = PhotonNetwork.CurrentRoom.Players.TryGetValue(sender, out var player) && !player.IsInactive
            && session.TryGetManager(sender, out var manager) && claims.IsClaimedBy(id, sender)
            && customer != null && customer.Group != null && customer.Group.currentOrderNumber == order
            && customer.CarrierActorNumber == 0 && customer.CommitTrayPickup(sender, manager.GetComponent<WaiterHands>());
        if (accepted) claims.CompleteOnAuthority(id, sender);
        else if (sender == session.LocalActorNumber) WarningSlideUI.Instance?.Show("This tray or your hands are no longer available for pickup.");
        else PhotonNetwork.RaiseEvent(CarryRejectedEvent, order,
            new RaiseEventOptions { TargetActors = new[] { sender } }, SendOptions.SendReliable);
    }

    public static bool CanClaimPreparedTray(FoodTray tray)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession || session.LocalManager == null || tray == null) return false;
        var kitchen = FindFirstObjectByType<KitchenManager>();
        return kitchen != null && kitchen.TryGetPreparedTray(tray.orderNumber, out var registered) && registered == tray;
    }

    public static bool TryClaimPreparedTray(FoodTray tray, PlayerMovement mover = null)
    {
        if (!ReviewIsMultiplayer) return false;
        var session = MultiplayerSessionManager.Instance;
        if (!CanClaimPreparedTray(tray) || (mover != null && mover.gameObject != session.LocalManager)) return true;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        if (bridge == null || bridge.pickupPending) return true;
        if (bridge.pickupTarget == tray && bridge.claims.IsClaimedBy(bridge.pickupTaskId, session.LocalActorNumber))
        {
            bridge.RequestCarry();
            return true;
        }
        bridge.CancelPickup();
        bridge.Cancel();
        bridge.CancelOrder();
        bridge.pickupTarget = tray;
        bridge.pickupTaskId = $"Order:{tray.orderNumber}:Pickup";
        bridge.pickupCancelled = false;
        bridge.pickupPending = true;
        if (!bridge.claims.RequestClaim(bridge.pickupTaskId))
        { bridge.pickupPending = false; bridge.CancelPickup(); }
        return true;
    }

    private void CancelPickup()
    {
        pickupCancelled = true;
        if (pickupTaskId != null && claims != null && claims.IsClaimedBy(pickupTaskId, session.LocalActorNumber))
            claims.Release(pickupTaskId);
        if (!pickupPending) { pickupTaskId = null; pickupTarget = null; }
    }
    private MultiplayerCustomerSpawn orderTarget;
    private string orderTaskId;
    private bool orderPending, orderCancelled;
    private const byte ReviewRequestEvent = 188, ReviewRejectedEvent = 189;
    private const byte ConfirmRequestEvent = 190, ConfirmRejectedEvent = 191;

    public static bool TryConfirmOrder(CustomerGroup group)
    {
        if (!ReviewIsMultiplayer) return false;
        if (!CanOpenReview(group)) return true;
        var bridge = MultiplayerSessionManager.Instance.GetComponent<MultiplayerCustomerInteractionBridge>();
        int viewId = bridge.orderTarget.photonView.ViewID;
        if (bridge.session.IsAuthority)
            bridge.HandleConfirm(viewId, bridge.session.LocalActorNumber, bridge.session.LocalActorNumber, group.currentOrderNumber);
        else PhotonNetwork.RaiseEvent(ConfirmRequestEvent,
            new object[] { viewId, bridge.session.LocalActorNumber, group.currentOrderNumber },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        return true;
    }

    private void HandleConfirm(int viewId, int actor, int sender, int expectedOrder)
    {
        if (!session.IsAuthority || actor != sender) return;
        var view = PhotonView.Find(viewId);
        var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
        var group = customer != null ? customer.Group : null;
        if (group != null && group.HasConfirmedOrder) return; // Duplicate success: never repeat stock or kitchen work.
        string id = $"Customer:{viewId}:Order";
        string error = "This order is no longer available for confirmation.";
        bool valid = PhotonNetwork.CurrentRoom.Players.TryGetValue(sender, out var player) && !player.IsInactive
            && session.TryGetManager(sender, out _) && claims.IsClaimedBy(id, sender)
            && group != null && !group.IsNetworkObserver && !group.IsTakeout
            && group.state == CustomerGroup.GroupState.ReadyToOrder && group.IsPlayerReviewingOrder
            && customer.ReviewActor == sender && customer.HasGeneratedOrder
            && group.currentOrderNumber >= 0 && expectedOrder == group.currentOrderNumber
            && group.currentOrder != null && group.currentOrder.lines.Count > 0;
        if (valid)
        {
            var kitchen = FindFirstObjectByType<KitchenManager>();
            var catalog = MenuCatalog.Default;
            var products = catalog != null ? catalog.ResolveProducts(group.currentOrder.productIds) : null;
            valid = kitchen != null && kitchen.CanAcceptOrderNumber(group.currentOrderNumber)
                && products != null && products.Count > 0
                && products.Count == group.currentOrder.productIds.Count;
            error = "The kitchen or order products are unavailable.";
            if (valid && LobbyStockBridge.Instance != null)
            {
                error = "One or more products in this order are no longer available.";
                valid = LobbyStockBridge.Instance.HasOrderStock(products)
                    && LobbyStockBridge.Instance.TryUseOrderStock(products);
            }
            if (valid)
            {
                group.submittedOrder = JsonUtility.FromJson<CustomerGroup.SimpleOrder>(JsonUtility.ToJson(group.currentOrder));
                group.ConfirmPlayerReviewedOrder(group.chosenFood, group.chosenDrink);
                customer.ReviewActor = 0;
                RestaurantTaskClaim.Complete(group);
                // Register one real kitchen order, stopping before cooking/tray creation.
                if (!kitchen.ProcessOrder(group, pauseAfterAcceptance: true))
                    Debug.LogError($"Kitchen rejected confirmed order {group.currentOrderNumber}.");
                customer.PublishAssignment();
                claims.CompleteOnAuthority(id, sender);
                if (sender == session.LocalActorNumber) CancelOrder();
                return;
            }
        }
        if (sender == session.LocalActorNumber) WarningSlideUI.Instance?.Show(error);
        else PhotonNetwork.RaiseEvent(ConfirmRejectedEvent, new object[] { viewId, error },
            new RaiseEventOptions { TargetActors = new[] { sender } }, SendOptions.SendReliable);
    }
    private OrderChecklistUI reviewUI;
    public static bool ReviewIsMultiplayer => MultiplayerSessionManager.Instance != null
        && MultiplayerSessionManager.Instance.IsMultiplayerSession;

    public static bool CanOpenReview(CustomerGroup group)
    {
        if (!ReviewIsMultiplayer) return true;
        var bridge = MultiplayerSessionManager.Instance.GetComponent<MultiplayerCustomerInteractionBridge>();
        return bridge != null && !bridge.orderCancelled && !bridge.orderPending
            && bridge.orderTarget != null && bridge.orderTarget.Group == group && group.IsPlayerReviewingOrder
            && bridge.orderTarget.HasGeneratedOrder && bridge.session.LocalManager != null
            && bridge.orderTarget.ReviewActor == bridge.session.LocalActorNumber
            && bridge.claims.IsClaimedBy(bridge.orderTaskId, bridge.session.LocalActorNumber);
    }

    public static bool CloseReview(CustomerGroup group)
    {
        if (!ReviewIsMultiplayer) return false;
        var bridge = MultiplayerSessionManager.Instance.GetComponent<MultiplayerCustomerInteractionBridge>();
        if (bridge != null && bridge.orderTarget != null && bridge.orderTarget.Group == group)
        {
            bridge.reviewUI = null;
            bridge.CancelOrder();
        }
        return true;
    }

    public static void ObserveReview(CustomerGroup group)
    {
        if (!CanOpenReview(group) || !ReviewIsMultiplayer) return;
        var bridge = MultiplayerSessionManager.Instance.GetComponent<MultiplayerCustomerInteractionBridge>();
        if (bridge.reviewUI != null) return;
        bridge.reviewUI = OrderChecklistUI.Instance != null ? OrderChecklistUI.Instance
            : FindFirstObjectByType<OrderChecklistUI>(FindObjectsInactive.Include);
        if (bridge.reviewUI == null) { bridge.CancelOrder(); return; }
        bridge.reviewUI.Open(group);
    }

    private void RequestReview()
    {
        if (orderTarget == null) return;
        int view = orderTarget.photonView.ViewID;
        if (session.IsAuthority) HandleReview(view, session.LocalActorNumber, session.LocalActorNumber);
        else PhotonNetwork.RaiseEvent(ReviewRequestEvent, new object[] { view, session.LocalActorNumber },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    private void HandleReview(int viewId, int actor, int sender)
    {
        if (!session.IsAuthority || actor != sender) return;
        var view = PhotonView.Find(viewId);
        var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
        bool valid = PhotonNetwork.CurrentRoom.Players.TryGetValue(sender, out var player) && !player.IsInactive
            && session.TryGetManager(sender, out _) && claims.IsClaimedBy($"Customer:{viewId}:Order", sender)
            && customer != null && customer.HasGeneratedOrder && customer.Group != null
            && !customer.Group.IsNetworkObserver && customer.Group.state == CustomerGroup.GroupState.ReadyToOrder
            && customer.Group.currentOrder != null && customer.Group.currentOrder.lines.Count > 0;
        if (valid && customer.Group.BeginPlayerOrderReview())
        {
            customer.ReviewActor = sender;
            customer.PublishAssignment();
            ObserveReview(customer.Group);
        }
        else if (sender == session.LocalActorNumber) CancelOrder();
        else PhotonNetwork.RaiseEvent(ReviewRejectedEvent, viewId,
            new RaiseEventOptions { TargetActors = new[] { sender } }, SendOptions.SendReliable);
    }

    private CustomerGroup billTarget;
    private string billTaskId;
    private bool billPending, billCancelled;
    private const byte BillFlowEvent = 199;
    private PlayerMovement billMover;
    private IInteractable billApproach;
    private BillPaper localBill;
    private bool billTransitionPending;
    private CustomerGroup settlingBill;
    public static bool CanSettleBill(CustomerGroup group)
    {
        var s = MultiplayerSessionManager.Instance;
        return group != null && s != null && s.IsMultiplayerSession && s.IsAuthority
            && s.GetComponent<MultiplayerCustomerInteractionBridge>()?.settlingBill == group;
    }
    private readonly System.Collections.Generic.HashSet<int> printingBills = new System.Collections.Generic.HashSet<int>();

    public static bool CanRetrieveBill(BillPaper paper)
    {
        var s = MultiplayerSessionManager.Instance;
        var bridge = s != null ? s.GetComponent<MultiplayerCustomerInteractionBridge>() : null;
        return bridge != null && paper != null && bridge.localBill == paper && !bridge.billCancelled
            && !bridge.billTransitionPending && bridge.billMover == null && s.LocalManager != null
            && s.LocalManager.GetComponent<WaiterHands>() is WaiterHands hands && !hands.HasBill
            && bridge.claims.IsClaimedBy(bridge.billTaskId, s.LocalActorNumber);
    }

    public static bool TryRetrieveBill(BillPaper paper)
    {
        if (!ReviewIsMultiplayer) return false;
        if (CanRetrieveBill(paper))
            MultiplayerSessionManager.Instance.GetComponent<MultiplayerCustomerInteractionBridge>()
                .MoveForBill(paper.StandPoint, paper.GetInteractRadius(), 2);
        return true;
    }

    private void BeginBillApproach()
    {
        var booth = billTarget != null ? billTarget.assignedBooth : null;
        MoveForBill(booth != null ? booth.approachPoint != null ? booth.approachPoint : booth.transform : null, 2.75f, 0);
    }

    private void MoveForBill(Transform stand, float radius, int stage)
    {
        var mover = session.LocalManager != null ? session.LocalManager.GetComponent<PlayerMovement>() : null;
        if (mover == null || !mover.isActiveAndEnabled || stand == null || billTarget == null) { CancelBill(); return; }
        billMover = mover;
        bool started = mover.UI_MoveToAction(stand, radius, () =>
        {
            billMover = null;
            billApproach = null;
            if (billCancelled || billTarget == null || session.LocalManager != mover.gameObject
                || !claims.IsClaimedBy(billTaskId, session.LocalActorNumber)) { CancelBill(); return; }
            billTransitionPending = true;
            int view = billTarget.GetComponentInParent<MultiplayerCustomerSpawn>().photonView.ViewID;
            if (session.IsAuthority) HandleBillFlow(view, stage, session.LocalActorNumber);
            else if (!PhotonNetwork.RaiseEvent(BillFlowEvent, new object[] { view, stage },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable)) CancelBill();
        }, () => { billMover = null; billApproach = null; CancelBill(); });
        if (started) billApproach = mover.CurrentTarget;
        else CancelBill();
    }

    private void HandleBillFlow(int viewId, int stage, int actor)
    {
        var view = PhotonView.Find(viewId);
        var group = view != null ? view.GetComponent<MultiplayerCustomerSpawn>()?.Group : null;
        var manager = BillManager.Instance;
        if (!session.IsAuthority || group == null || group.IsNetworkObserver || manager == null
            || group.state != CustomerGroup.GroupState.NeedsBill || group.HasReceivedBill
            || !PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out var player) || player.IsInactive
            || !claims.IsClaimedBy($"Customer:{viewId}:Bill", actor)
            || !session.TryGetManager(actor, out var owner) || owner == null)
        { ReplyBill(viewId, 4, actor, null); return; }
        var hands = owner.GetComponent<WaiterHands>();
        var booth = group.assignedBooth;
        if (stage == 6)
        {
            var held = hands != null ? hands.GetComponentInChildren<BillPaper>(true) : null;
            var delivery = booth != null ? booth.approachPoint != null ? booth.approachPoint : booth.transform : null;
            var register = CashierRegisterUI.Instance;
            if (held == null || !held.Matches(group) || hands.holdingBillFor != group
                || hands.BillHoldPoint == null || !held.transform.IsChildOf(hands.BillHoldPoint)
                || delivery == null || register == null || group.MultiplayerPaymentComplete
                || Vector2.Distance(new Vector2(owner.transform.position.x, owner.transform.position.z),
                    new Vector2(delivery.position.x, delivery.position.z)) > 2.75f)
            { ReplyBill(viewId, 4, actor, null); return; }
            bool paid;
            settlingBill = group;
            try { paid = register.CompleteAutomatedPayment(group); }
            finally { settlingBill = null; }
            if (!paid) { ReplyBill(viewId, 4, actor, null); return; }
            hands.ClearBill();
            view.GetComponent<MultiplayerCustomerSpawn>().PublishAssignment();
            ReplyBill(viewId, 5, actor, null);
            claims.CompleteOnAuthority($"Customer:{viewId}:Bill", actor);
            return;
        }
        var paper = manager.FindBillForGroup(group);
        Transform stand = stage == 0 ? (booth != null ? booth.approachPoint != null ? booth.approachPoint : booth.transform : null)
            : paper != null ? paper.StandPoint : null;
        float radius = stage == 0 ? 2.75f : paper != null ? paper.GetInteractRadius() : 0f;
        if (hands == null || hands.HasBill || stand == null ||
            Vector2.Distance(new Vector2(owner.transform.position.x, owner.transform.position.z),
                new Vector2(stand.position.x, stand.position.z)) > radius)
        { ReplyBill(viewId, 4, actor, null); return; }
        if (stage == 0)
        {
            if (printingBills.Add(viewId)) StartCoroutine(PrintClaimedBill(group, viewId, actor));
        }
        else if (stage == 2 && paper != null)
        {
            hands.PickupBillPaper(paper);
            ReplyBill(viewId, hands.holdingBillFor == group ? 3 : 4, actor, paper);
        }
    }

    private System.Collections.IEnumerator PrintClaimedBill(CustomerGroup group, int viewId, int actor)
    {
        BillPaper paper = null;
        var manager = BillManager.Instance;
        // Exact printing entry called by RequestBillFromCashier; leave its
        // multiplayer payment barrier untouched.
        manager.RequestBill(group);
        float deadline = Time.unscaledTime + 30f;
        while (group != null && claims.IsClaimedBy($"Customer:{viewId}:Bill", actor)
            && group.state == CustomerGroup.GroupState.NeedsBill && session.IsAuthority)
        {
            if (paper == null && manager != null) paper = manager.FindBillForGroup(group);
            if (paper != null) break;
            if (Time.unscaledTime >= deadline) break;
            yield return null;
        }
        if (paper != null && group != null && session.IsAuthority
            && claims.IsClaimedBy($"Customer:{viewId}:Bill", actor))
        {
            ReplyBill(viewId, 1, actor, paper);
            while (group != null && session.IsAuthority && group.state == CustomerGroup.GroupState.NeedsBill
                && claims.IsClaimedBy($"Customer:{viewId}:Bill", actor)) yield return null;
        }
        else ReplyBill(viewId, 4, actor, null);
        if (session.TryGetManager(actor, out var owner) && owner != null
            && owner.GetComponent<WaiterHands>() is WaiterHands hands && hands.holdingBillFor == group)
            hands.ClearBill();
        if (paper != null && (group == null || !group.MultiplayerPaymentComplete)) Destroy(paper.gameObject);
        printingBills.Remove(viewId);
    }

    private void ReplyBill(int view, int stage, int actor, BillPaper paper)
    {
        Vector3 position = paper != null ? paper.transform.position : Vector3.zero;
        Quaternion rotation = paper != null ? paper.transform.rotation : Quaternion.identity;
        if (actor == session.LocalActorNumber) ObserveBill(view, stage, position, rotation, paper);
        else PhotonNetwork.RaiseEvent(BillFlowEvent, new object[] { view, stage, position, rotation },
            new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }

    private void ObserveBill(int view, int stage, Vector3 position, Quaternion rotation, BillPaper paper = null)
    {
        if (stage == 5 && billTaskId == $"Customer:{view}:Bill")
        {
            billTarget?.PresentObservedPayment(true);
            billTaskId = null; // Authority completed the claim; never send another release.
            CancelBill();
            return;
        }
        if (billCancelled || billTarget == null || billTaskId != $"Customer:{view}:Bill"
            || !claims.IsClaimedBy(billTaskId, session.LocalActorNumber)) return;
        billTransitionPending = false;
        if (stage == 4) { WarningSlideUI.Instance?.Show("Bill request failed. Please try again."); CancelBill(); return; }
        if (stage == 1)
        {
            localBill = paper != null ? paper : BillManager.Instance?.PresentMultiplayerBill(billTarget, position, rotation);
            if (localBill == null) CancelBill();
        }
        else if (stage == 3 && session.LocalManager != null)
        {
            var hands = session.LocalManager.GetComponent<WaiterHands>();
            if (hands != null && !hands.HasBill && localBill != null) hands.PickupBillPaper(localBill);
            WarningSlideUI.Instance?.Show("Bill retrieved. Select its customer's bill bubble to deliver and settle payment.");
        }
    }

    public static bool TryHandleBill(CustomerGroup group)
    {
        if (!ReviewIsMultiplayer) return false;
        var session = MultiplayerSessionManager.Instance;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        var customer = group != null ? group.GetComponentInParent<MultiplayerCustomerSpawn>() : null;
        if (bridge == null || customer == null || session.LocalManager == null
            || group.state != CustomerGroup.GroupState.NeedsBill || group.HasReceivedBill) return true;
        if (bridge.billPending) return true;
        var hands = session.LocalManager.GetComponent<WaiterHands>();
        if (hands != null && hands.HasBill && hands.holdingBillFor != group)
        { WarningSlideUI.Instance?.Show("This bill belongs to another customer."); return true; }
        if (bridge.billTarget == group && bridge.claims.IsClaimedBy(bridge.billTaskId, session.LocalActorNumber))
        {
            if (hands != null && hands.HasBill && !bridge.billTransitionPending && bridge.billMover == null)
            {
                var booth = group.assignedBooth;
                bridge.MoveForBill(booth != null ? booth.approachPoint != null ? booth.approachPoint : booth.transform : null, 2.75f, 6);
            }
            return true;
        }
        bridge.CancelBill();
        bridge.billTarget = group;
        bridge.billTaskId = $"Customer:{customer.photonView.ViewID}:Bill";
        bridge.billCancelled = false;
        bridge.billPending = true;
        if (!bridge.claims.RequestClaim(bridge.billTaskId))
        { bridge.billPending = false; bridge.CancelBill(); }
        return true;
    }

    private void CancelBill()
    {
        billCancelled = true;
        billTransitionPending = false;
        var mover = billMover;
        var approach = billApproach;
        billMover = null;
        billApproach = null;
        if (mover != null && approach != null && session.LocalManager == mover.gameObject
            && ReferenceEquals(mover.CurrentTarget, approach)) mover.CancelLockedTask();
        if (session.LocalManager != null && session.LocalManager.GetComponent<WaiterHands>() is WaiterHands hands
            && billTarget != null && hands.holdingBillFor == billTarget) hands.ClearBill();
        if (!session.IsAuthority && localBill != null && (billTarget == null || !billTarget.MultiplayerPaymentComplete))
            Destroy(localBill.gameObject);
        localBill = null;
        if (billTaskId != null && claims != null && claims.IsClaimedBy(billTaskId, session.LocalActorNumber))
            claims.Release(billTaskId);
        // Keep cancelled in-flight requests so a late grant is released.
        if (!billPending) { billTaskId = null; billTarget = null; }
    }

    public static bool TryHandleOrder(CustomerGroup group)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession) return false;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        bridge?.CancelPickup();
        var customer = group != null ? group.GetComponentInParent<MultiplayerCustomerSpawn>() : null;
        if (bridge == null || customer == null || session.LocalManager == null
            || group.state != CustomerGroup.GroupState.ReadyToOrder || group.HasConfirmedOrder) return true;
        if (bridge.orderPending) return true;
        if (bridge.orderTarget == customer && bridge.claims.IsClaimedBy(bridge.orderTaskId, session.LocalActorNumber))
        {
            bridge.RequestReview();
            return true;
        }
        bridge.CancelOrder();
        bridge.orderTarget = customer;
        bridge.orderTaskId = $"Customer:{customer.photonView.ViewID}:Order";
        bridge.orderCancelled = false;
        bridge.orderPending = true;
        if (!bridge.claims.RequestClaim(bridge.orderTaskId))
        {
            bridge.orderPending = false;
            bridge.CancelOrder();
        }
        return true;
    }

    private void CancelOrder()
    {
        orderCancelled = true;
        var closingUI = reviewUI;
        reviewUI = null;
        if (closingUI != null) closingUI.Close();
        if (orderTaskId != null && claims != null && claims.IsClaimedBy(orderTaskId, session.LocalActorNumber))
            claims.Release(orderTaskId);
        // Keep cancelled in-flight requests until a late grant can be released.
        if (!orderPending) { orderTaskId = null; orderTarget = null; }
    }

    public static bool TryHandle(RoleBasedAssignController controller, CustomerGroup group, Camera camera)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession) return false;
        if (session.LocalManager != controller.gameObject) return true;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        if (bridge == null) bridge = session.gameObject.AddComponent<MultiplayerCustomerInteractionBridge>();
        bridge.Begin(group.GetComponentInParent<MultiplayerCustomerSpawn>(), camera);
        return true;
    }

    public static bool TryHandleGreetAction(CustomerGroup group)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession) return false;
        if (group == null) return true;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        var customer = group.GetComponentInParent<MultiplayerCustomerSpawn>();
        if (bridge == null || customer == null || bridge.target != customer || bridge.pending
            || bridge.cancelled || !bridge.greetApproached || session.LocalManager == null
            || !bridge.claims.IsClaimedBy(bridge.taskId, session.LocalActorNumber)) return true;
        if (group.hasBeenGreeted)
        {
            bridge.seatController = session.LocalManager.GetComponent<RoleBasedAssignController>();
            bridge.seatController?.BeginAssignFromBubble(group);
            return true;
        }
        if (session.IsAuthority)
            bridge.HandleGreet(customer.photonView.ViewID, session.LocalActorNumber, session.LocalActorNumber);
        else
            PhotonNetwork.RaiseEvent(GreetRequestEvent,
                new object[] { customer.photonView.ViewID, session.LocalActorNumber },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        return true;
    }

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == BillFlowEvent && session != null && session.IsMultiplayerSession
            && photonEvent.CustomData is object[] bill && bill.Length >= 2
            && bill[0] is int billView && bill[1] is int billStage)
        {
            if (session.IsAuthority && bill.Length == 2 && (billStage == 0 || billStage == 2 || billStage == 6))
                HandleBillFlow(billView, billStage, photonEvent.Sender);
            else if (bill.Length == 4 && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber
                && bill[2] is Vector3 billPosition && bill[3] is Quaternion billRotation)
                ObserveBill(billView, billStage, billPosition, billRotation);
            return;
        }
        if (session == null || !session.IsMultiplayerSession) return;
        if (photonEvent.Code == ServeRequestEvent && session.IsAuthority
            && photonEvent.CustomData is object[] serve && serve.Length == 3
            && serve[0] is int serveView && serve[1] is int serveOrder && serve[2] is int serveActor)
        { HandleServe(serveView, serveOrder, serveActor, photonEvent.Sender); return; }
        if (photonEvent.Code == ServeRejectedEvent && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber)
        { WarningSlideUI.Instance?.Show("Cannot serve here. Carry the matching order to its table."); return; }
        if (photonEvent.Code == CarryRequestEvent && session.IsAuthority
            && photonEvent.CustomData is object[] carry && carry.Length == 3
            && carry[0] is int carryView && carry[1] is int carryOrder && carry[2] is int carryActor)
        { HandleCarry(carryView, carryOrder, carryActor, photonEvent.Sender); return; }
        if (photonEvent.Code == CarryRejectedEvent && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber
            && photonEvent.CustomData is int rejectedCarry && pickupTarget != null && pickupTarget.orderNumber == rejectedCarry)
        { WarningSlideUI.Instance?.Show("This tray or your hands are no longer available for pickup."); return; }
        if (photonEvent.Code == ConfirmRequestEvent && session.IsAuthority
            && photonEvent.CustomData is object[] confirm && confirm.Length == 3
            && confirm[0] is int confirmView && confirm[1] is int confirmActor && confirm[2] is int orderNumber)
        { HandleConfirm(confirmView, confirmActor, photonEvent.Sender, orderNumber); return; }
        if (photonEvent.Code == ConfirmRejectedEvent && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber
            && photonEvent.CustomData is object[] rejection && rejection.Length == 2
            && rejection[0] is int rejectedOrderView && rejection[1] is string warning
            && orderTarget != null && orderTarget.photonView.ViewID == rejectedOrderView)
        { WarningSlideUI.Instance?.Show(warning); return; }
        if (photonEvent.Code == ReviewRejectedEvent && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber
            && photonEvent.CustomData is int rejectedView && orderTarget != null && orderTarget.photonView.ViewID == rejectedView)
        { CancelOrder(); return; }
        if (photonEvent.Code == ReviewRequestEvent && session.IsAuthority
            && photonEvent.CustomData is object[] review && review.Length == 2
            && review[0] is int reviewView && review[1] is int reviewActor)
        { HandleReview(reviewView, reviewActor, photonEvent.Sender); return; }
        if (photonEvent.Code == SeatResultEvent && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber
            && photonEvent.CustomData is object[] result && result.Length == 2
            && result[0] is int resultView && result[1] is bool accepted)
        {
            FinishSeat(resultView, accepted);
            return;
        }
        if (!session.IsAuthority) return;
        if (photonEvent.Code == SeatRequestEvent && photonEvent.CustomData is object[] seat && seat.Length == 3
            && seat[0] is int seatView && seat[1] is int seatActor && seat[2] is string boothId)
        {
            HandleSeat(seatView, seatActor, photonEvent.Sender, boothId);
            return;
        }
        if (photonEvent.Code != GreetRequestEvent) return;
        if (photonEvent.CustomData is object[] data && data.Length == 2
            && data[0] is int viewId && data[1] is int actor)
            HandleGreet(viewId, actor, photonEvent.Sender);
    }

    private void HandleGreet(int viewId, int actor, int sender)
    {
        if (!session.IsAuthority || actor != sender
            || !PhotonNetwork.CurrentRoom.Players.TryGetValue(sender, out var player) || player.IsInactive
            || !session.TryGetManager(sender, out _)
            || !claims.IsClaimedBy($"Customer:{viewId}:GreetSeat", sender)) return;
        var view = PhotonView.Find(viewId);
        var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
        var group = customer != null ? customer.Group : null;
        var line = FindFirstObjectByType<LobbyLineManager>();
        if (group == null || group.IsNetworkObserver || group.hasBeenGreeted
            || group.state != CustomerGroup.GroupState.Waiting || !group.CanBeGreeted()
            || line == null || line.GetFrontOfLine() != group) return;
        group.MarkGreeted();
        customer.PublishGreeted();
        RefreshOwnedPopup(group);
    }

    public static void RefreshOwnedPopup(CustomerGroup group)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession) return;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        if (bridge == null || bridge.cancelled || bridge.pending || bridge.target == null
            || !bridge.greetApproached
            || bridge.target.Group != group || session.LocalManager == null
            || !bridge.claims.IsClaimedBy(bridge.taskId, session.LocalActorNumber)) return;
        HostSpeechBubbleSpawner.Instance?.HideImmediate();
        CustomerGreetBubbleSpawner.Instance?.SetVisibleAndRefresh(group, true);
    }

    public static bool CanSelectBooth(RoleBasedAssignController controller, CustomerGroup group)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession) return true;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        return bridge != null && session.LocalManager == controller.gameObject
            && !bridge.cancelled && !bridge.pending && !bridge.seatPending
            && bridge.greetApproached
            && bridge.target != null && bridge.target.Group == group && group.hasBeenGreeted
            && !group.HasBeenAssigned && bridge.target.ReadyForInteraction
            && bridge.claims.IsClaimedBy(bridge.taskId, session.LocalActorNumber);
    }

    public static bool TryHandleSeat(RoleBasedAssignController controller, CustomerGroup group, Booth booth)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession) return false;
        if (!CanSelectBooth(controller, group) || booth == null) return true;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        bridge.seatController = controller;
        bridge.seatPending = true;
        int viewId = bridge.target.photonView.ViewID;
        string boothId = BoothIdentity(booth);
        if (session.IsAuthority) bridge.HandleSeat(viewId, session.LocalActorNumber, session.LocalActorNumber, boothId);
        else if (!PhotonNetwork.RaiseEvent(SeatRequestEvent,
            new object[] { viewId, session.LocalActorNumber, boothId },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable))
            bridge.FinishSeat(viewId, false);
        return true;
    }

    // Authored scene hierarchy names are shared by clients; ambiguous paths fail closed.
    public static string BoothIdentity(Booth booth)
    {
        string path = "";
        for (var node = booth.transform; node != null; node = node.parent)
            path = node.name.Length + ":" + node.name + "/" + path;
        return booth.gameObject.scene.path + ":" + path;
    }

    public static Booth ResolveBooth(string id)
    {
        Booth result = null;
        foreach (var booth in FindObjectsByType<Booth>(FindObjectsSortMode.None))
        {
            if (BoothIdentity(booth) != id) continue;
            if (result != null) return null;
            result = booth;
        }
        return result;
    }

    private void HandleSeat(int viewId, int actor, int sender, string boothId)
    {
        if (!session.IsAuthority || actor != sender
            || !PhotonNetwork.CurrentRoom.Players.TryGetValue(sender, out var player) || player.IsInactive) return;
        string id = $"Customer:{viewId}:GreetSeat";
        var view = PhotonView.Find(viewId);
        var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
        var group = customer != null ? customer.Group : null;
        var booth = ResolveBooth(boothId);
        var line = FindFirstObjectByType<LobbyLineManager>();
        bool accepted = session.TryGetManager(sender, out _) && claims.IsClaimedBy(id, sender)
            && group != null && !group.IsNetworkObserver && group.hasBeenGreeted && !group.HasBeenAssigned
            && group.state == CustomerGroup.GroupState.Waiting && group.CanBeSeated
            && line != null && line.GetFrontOfLine() == group
            && booth != null && booth.IsAvailableFor(group.Size);
        if (accepted)
        {
            for (int i = 0; i < group.Size; i++)
                if (booth.GetSeat(i) == null) accepted = false;
        }
        if (accepted)
        {
            group.PauseAfterSeating = true;
            group.AssignToBooth(booth);
            group.CompleteReceptionTask();
            RestaurantTaskClaim.Complete(group);
            customer.PublishAssignment();
            claims.CompleteOnAuthority(id, sender);
        }
        if (sender == session.LocalActorNumber) FinishSeat(viewId, accepted);
        else PhotonNetwork.RaiseEvent(SeatResultEvent, new object[] { viewId, accepted },
            new RaiseEventOptions { TargetActors = new[] { sender } }, SendOptions.SendReliable);
    }

    private void FinishSeat(int viewId, bool accepted)
    {
        if (target == null || target.photonView.ViewID != viewId) return;
        if (!accepted) WarningSlideUI.Instance?.Show("That table assignment was rejected. Select the customer to try again.");
        Cancel();
    }

    private void Awake()
    {
        session = GetComponent<MultiplayerSessionManager>();
        claims = GetComponent<MultiplayerTaskClaims>();
        claims.ClaimResult += OnResult;
    }

    private void Begin(MultiplayerCustomerSpawn customer, Camera camera)
    {
        CancelPickup();
        if (pending) return;
        Cancel();
        if (customer == null || !customer.ReadyForInteraction) return;
        target = customer;
        taskId = $"Customer:{customer.photonView.ViewID}:GreetSeat";
        cameraForPopup = camera;
        cancelled = false;
        pending = true;
        if (!claims.RequestClaim(taskId)) { pending = false; Cancel(); }
    }

    private void OnResult(string id, bool accepted)
    {
        if (billPending && id == billTaskId)
        {
            billPending = false;
            if (!accepted)
            {
                billTaskId = null;
                billTarget = null;
                WarningSlideUI.Instance?.Show("That bill task is unavailable or already claimed.");
            }
            else if (billCancelled || session.LocalManager == null || billTarget == null
                || billTarget.state != CustomerGroup.GroupState.NeedsBill || billTarget.HasReceivedBill) CancelBill();
            else if (claims.IsClaimedBy(id, session.LocalActorNumber)) BeginBillApproach();
            return;
        }
        if (pickupPending && id == pickupTaskId)
        {
            pickupPending = false;
            if (!accepted)
            {
                pickupTaskId = null;
                pickupTarget = null;
                WarningSlideUI.Instance?.Show("That pickup task is unavailable or already claimed.");
            }
            else if (pickupCancelled || !CanClaimPreparedTray(pickupTarget)) CancelPickup();
            else WarningSlideUI.Instance?.Show("Pickup task claimed. Interact with the tray again to pick it up. Escape or right-click cancels.");
            return;
        }
        if (orderPending && id == orderTaskId)
        {
            orderPending = false;
            if (!accepted)
            {
                orderTaskId = null;
                orderTarget = null;
                WarningSlideUI.Instance?.Show("That order task is unavailable or already claimed.");
            }
            else if (orderCancelled || orderTarget == null || session.LocalManager == null
                || orderTarget.Group == null || orderTarget.Group.state != CustomerGroup.GroupState.ReadyToOrder)
                CancelOrder();
            else RequestReview();
            return;
        }
        if (!pending || id != taskId) return;
        pending = false;
        if (!accepted) { taskId = null; target = null; return; }
        if (cancelled || target == null || !target.ReadyForInteraction || session.LocalManager == null)
        { Cancel(); return; }
        if (claims.IsClaimedBy(id, session.LocalActorNumber))
            BeginGreetApproach();
    }

    private void BeginGreetApproach()
    {
        var mover = session.LocalManager.GetComponent<PlayerMovement>();
        var stand = CustomerGreetBubbleUI.FindClosestCustomer(target.Group, session.LocalManager.transform.position);
        if (mover == null || !mover.isActiveAndEnabled || stand == null) { Cancel(); return; }

        var customer = target;
        string claimedTask = taskId;
        greetMover = mover;
        CustomerGreetBubbleSpawner.Instance?.Hide();
        bool started = mover.UI_MoveToAction(stand, CustomerGreetBubbleUI.GreetingInteractRadius,
            () =>
            {
                greetMover = null;
                greetApproach = null;
                if (cancelled || target != customer || taskId != claimedTask || customer == null
                    || !customer.ReadyForInteraction || !session.IsMultiplayerSession
                    || session.LocalManager != mover.gameObject
                    || !claims.IsClaimedBy(claimedTask, session.LocalActorNumber))
                { Cancel(); return; }
                greetApproached = true;
                CustomerGreetBubbleSpawner.Instance?.Show(customer.Group, cameraForPopup);
            },
            () =>
            {
                // Movement already handles stopping/replacement; do not cancel
                // whatever interaction may replace this deferred action.
                greetMover = null;
                greetApproach = null;
                Cancel();
            });
        if (started) greetApproach = mover.CurrentTarget;
        else Cancel();
    }

    private void Update()
    {
        if (billTarget != null && billTarget.MultiplayerPaymentComplete)
        {
            billTaskId = null;
            CancelBill();
        }
        if (billApproach != null && (billMover == null || !billMover.isActiveAndEnabled
            || session.LocalManager != billMover.gameObject || !billApproach.CanInteract()
            || !ReferenceEquals(billMover.CurrentTarget, billApproach))) CancelBill();
        if (billTaskId != null && (!session.IsMultiplayerSession || session.LocalManager == null
            || billTarget == null || billTarget.state != CustomerGroup.GroupState.NeedsBill || billTarget.HasReceivedBill
            || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)
            || (!billPending && !claims.IsClaimedBy(billTaskId, session.LocalActorNumber)))) CancelBill();
        if (pickupTaskId != null && (!CanClaimPreparedTray(pickupTarget)
            || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)
            || (!pickupPending && !claims.IsClaimedBy(pickupTaskId, session.LocalActorNumber)))) CancelPickup();
        if (orderTaskId != null && (!session.IsMultiplayerSession || session.LocalManager == null
            || orderTarget == null || orderTarget.Group == null
            || orderTarget.Group.state != CustomerGroup.GroupState.ReadyToOrder || orderTarget.Group.HasConfirmedOrder
            || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)
            || (!orderPending && !claims.IsClaimedBy(orderTaskId, session.LocalActorNumber)))) CancelOrder();
        if (taskId == null) return;
        if (greetApproach != null && (greetMover == null || !greetMover.isActiveAndEnabled
            || session.LocalManager != greetMover.gameObject || !greetApproach.CanInteract()
            || !ReferenceEquals(greetMover.CurrentTarget, greetApproach)))
        { Cancel(); return; }
        if (!session.IsMultiplayerSession || session.LocalManager == null || target == null
            || !target.ReadyForInteraction || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)
            || (!pending && !claims.IsClaimedBy(taskId, session.LocalActorNumber))) Cancel();
    }

    public void Cancel()
    {
        cancelled = true;
        greetApproached = false;
        var mover = greetMover;
        var approach = greetApproach;
        greetMover = null;
        greetApproach = null;
        if (mover != null && approach != null && session.LocalManager == mover.gameObject
            && ReferenceEquals(mover.CurrentTarget, approach))
            mover.CancelLockedTask();
        seatPending = false;
        if (seatController != null) seatController.ClearBoothSelection(target != null ? target.Group : null);
        seatController = null;
        if (taskId == null) return;
        CustomerGreetBubbleSpawner.Instance?.Hide();
        if (claims.IsClaimedBy(taskId, session.LocalActorNumber)) claims.Release(taskId);
        // Retain a cancelled request until its response so a late grant is released.
        if (!pending) { taskId = null; target = null; }
    }

    private void OnDestroy()
    {
        CancelBill();
        CancelPickup();
        CancelOrder();
        Cancel();
        if (claims != null) claims.ClaimResult -= OnResult;
    }
}
