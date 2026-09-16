using UnityEngine;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

// Claims and authoritative greeting/booth assignment.
public partial class MultiplayerCustomerInteractionBridge : MonoBehaviour, IOnEventCallback
{
    private const byte GreetRequestEvent = 185;
    private const byte SeatRequestEvent = 186, SeatResultEvent = 187;
    private RoleBasedAssignController seatController;
    private bool seatPending;
    private int seatApproachVersion;
    private MultiplayerSessionManager session;
    private MultiplayerTaskClaims claims;
    private MultiplayerCustomerSpawn target;
    private Camera cameraForPopup;
    private string taskId;
    private bool pending, cancelled;
    private PlayerMovement greetMover;
    private IInteractable greetApproach;
    private bool greetApproached;
    private bool greetWaiting;
    private long greetLease;
    private float greetReplyUntil;
    private readonly System.Collections.Generic.HashSet<string> greetingRequests = new();
    private FoodTray pickupTarget;
    private string pickupTaskId;
    private bool pickupPending, pickupCancelled;
    private PlayerMovement pickupMover;
    private IInteractable pickupApproach;
    private int pickupApproachVersion, pickupSlot;
    private bool carryPending;
    public bool AwaitingLocalAction => pendingReviewedOrder != null || greetWaiting || seatPending
        || carryPending || pendingFoodAction != null || billTransitionPending;
    public bool ReviewingLocalOrder => !orderCancelled && orderTarget != null
        && orderTarget.Group != null && orderTarget.Group.IsPlayerReviewingOrder;
    private string requestedTaskId;
    private System.Action beginGrantedTask;
    private const byte CarryRequestEvent = 192, CarryRejectedEvent = 193;

    private const byte ServeRequestEvent = 194, ServeRejectedEvent = 195;

    public static bool CanAttemptServe(CustomerGroup group)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session != null && session.IsMultiplayerSession) WaiterHands.ReconcileMultiplayerHands(session.LocalManager);
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
        if (mover == null)
        {
            ServePoint(group, out var stand, out float radius);
            var localMover = session.LocalManager.GetComponent<PlayerMovement>();
            if (localMover != null && stand != null)
                localMover.UI_MoveToAction(stand, radius, () => TryServe(group, localMover));
            return true;
        }
        var heldTray = session.LocalManager.GetComponent<WaiterHands>().holdingTray;
        if (heldTray == null || heldTray.TargetGroup != group)
        { WarningSlideUI.Instance?.Show(MultiplayerActionFeedback.Message(MultiplayerActionOutcome.WrongTarget)); return true; }
        bridge.SendFoodAction(group, "food_deliver", -1);
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

    private void BeginPickupApproach()
    {
        if (pickupMover != null || carryPending) return;
        var tray = pickupTarget;
        var interaction = tray != null ? tray.GetComponent<FoodTrayInteractable>() : null;
        var customer = tray != null && tray.TargetGroup != null
            ? tray.TargetGroup.GetComponentInParent<MultiplayerCustomerSpawn>() : null;
        var kitchen = MultiplayerWorldRegistry.Kitchen;
        var manager = session.LocalManager;
        var mover = manager != null ? manager.GetComponent<PlayerMovement>() : null;
        if (!claims.IsClaimedBy(pickupTaskId, session.LocalActorNumber)
            || !CanClaimPreparedTray(tray) || interaction == null || interaction.StandPoint == null
            || customer == null || mover == null || !mover.isActiveAndEnabled || kitchen == null
            || !kitchen.TryGetPreparedSlot(tray.orderNumber, out pickupSlot))
        { CancelPickup(); return; }
        int slot = pickupSlot;
        int order = tray.orderNumber;
        string claimedTask = pickupTaskId;
        int version = ++pickupApproachVersion;
        pickupMover = mover;
        bool started = mover.UI_MoveToAction(interaction.StandPoint, interaction.GetInteractRadius(),
            () =>
            {
                if (version != pickupApproachVersion) return;
                pickupMover = null;
                pickupApproach = null;
                if (pickupCancelled || pickupTarget != tray || pickupTaskId != claimedTask
                    || !session.IsMultiplayerSession || session.LocalManager != mover.gameObject
                    || !mover.isActiveAndEnabled || !CanClaimPreparedTray(tray) || customer == null
                    || tray.TargetGroup != customer.Group || tray.orderNumber != order
                    || kitchen == null || !kitchen.TryGetPreparedSlot(order, out int currentSlot) || currentSlot != slot
                    || !claims.IsClaimedBy(claimedTask, session.LocalActorNumber))
                { CancelPickup(); return; }
                carryPending = true;
                RequestCarry();
            },
            () =>
            {
                if (version != pickupApproachVersion) return;
                pickupMover = null;
                pickupApproach = null;
                CancelPickup();
            });
        if (started) pickupApproach = mover.CurrentTarget;
        else if (version == pickupApproachVersion) CancelPickup();
    }

    private void RequestCarry()
    {
        if (pickupTarget != null) SendFoodAction(pickupTarget.TargetGroup, "food_pickup", pickupSlot);
    }

    public static bool CanClaimPreparedTray(FoodTray tray)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession || session.LocalManager == null || tray == null) return false;
        var kitchen = MultiplayerWorldRegistry.Kitchen;
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
            bridge.RetainOwnedTask(bridge.pickupTaskId);
            bridge.BeginPickupApproach();
            return true;
        }
        string requested = $"Order:{tray.orderNumber}:Pickup";
        bridge.RequestTask(requested, () =>
        {
            bridge.pickupTarget = tray;
            bridge.pickupTaskId = requested;
            bridge.pickupCancelled = false;
            bridge.pickupPending = true;
            bridge.OnResult(requested, true);
        });
        return true;
    }

    private void CancelPickup()
    {
        if (RetainCommittedFoodPickup()) return;
        ForgetFoodIntent();
        pickupApproachVersion++;
        carryPending = false;
        pickupCancelled = true;
        var mover = pickupMover;
        var approach = pickupApproach;
        pickupMover = null;
        pickupApproach = null;
        if (mover != null && approach != null && session.LocalManager == mover.gameObject
            && ReferenceEquals(mover.CurrentTarget, approach)) mover.CancelLockedTask();
        if (pickupTaskId != null && claims != null && claims.IsClaimedBy(pickupTaskId, session.LocalActorNumber))
            claims.Release(pickupTaskId);
        if (!pickupPending) { pickupTaskId = null; pickupTarget = null; }
    }
    private MultiplayerCustomerSpawn orderTarget;
    private string orderTaskId;
    private bool orderPending, orderCancelled;
    private PlayerMovement orderMover;
    private IInteractable orderApproach;
    private Booth orderBooth;
    private int orderApproachVersion;
    private bool orderApproached;
    private const byte ReviewRequestEvent = 188, ReviewRejectedEvent = 189;
    private const byte ConfirmRequestEvent = 190, ConfirmRejectedEvent = 191;

    private OrderChecklistUI reviewUI;
    public static bool ReviewIsMultiplayer => MultiplayerSessionManager.Instance != null
        && MultiplayerSessionManager.Instance.IsMultiplayerSession;

    public static bool CanOpenReview(CustomerGroup group)
    {
        if (!ReviewIsMultiplayer) return true;
        var bridge = MultiplayerSessionManager.Instance.GetComponent<MultiplayerCustomerInteractionBridge>();
        return bridge != null && !bridge.orderCancelled && !bridge.orderPending && bridge.orderApproached
            && bridge.orderTarget != null && bridge.orderTarget.Group == group && group.IsPlayerReviewingOrder
            && group.state == CustomerGroup.GroupState.ReadyToOrder && !group.HasConfirmedOrder
            && bridge.orderBooth != null && group.assignedBooth == bridge.orderBooth
            && bridge.orderBooth.CurrentGroup == group
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

    private void BeginOrderApproach()
    {
        var customer = orderTarget;
        var group = customer != null ? customer.Group : null;
        var manager = session.LocalManager;
        var mover = manager != null ? manager.GetComponent<PlayerMovement>() : null;
        var booth = group != null ? group.assignedBooth : null;
        Transform stand = booth != null ? booth.approachPoint != null ? booth.approachPoint : booth.transform : null;
        if (group == null || group.HasConfirmedOrder || !group.HasBeenAssigned || booth == null
            || booth.CurrentGroup != group || mover == null || !mover.isActiveAndEnabled || stand == null
            || !claims.IsClaimedBy(orderTaskId, session.LocalActorNumber))
        { CancelOrder(); return; }
        string claimedTask = orderTaskId;
        int version = ++orderApproachVersion;
        orderApproached = false;
        orderBooth = booth;
        orderMover = mover;
        bool started = mover.UI_MoveToAction(stand, 2.75f,
            () =>
            {
                if (version != orderApproachVersion) return;
                orderMover = null;
                orderApproach = null;
                if (orderCancelled || orderTarget != customer || orderTaskId != claimedTask
                    || customer == null || group == null || customer.Group != group
                    || !session.IsMultiplayerSession || session.LocalManager != mover.gameObject
                    || !mover.isActiveAndEnabled || !group.gameObject.activeInHierarchy
                    || group.state != CustomerGroup.GroupState.ReadyToOrder || group.HasConfirmedOrder
                    || !group.HasBeenAssigned || booth == null || !booth.gameObject.activeInHierarchy
                    || group.assignedBooth != booth || booth.CurrentGroup != group
                    || !claims.IsClaimedBy(claimedTask, session.LocalActorNumber))
                { CancelOrder(); return; }
                orderApproached = true;
                RequestReview();
            },
            () =>
            {
                if (version != orderApproachVersion) return;
                orderMover = null;
                orderApproach = null;
                CancelOrder();
            });
        if (started) orderApproach = mover.CurrentTarget;
        else if (version == orderApproachVersion) CancelOrder();
    }

    private void RequestReview()
    {
        if (orderTarget == null) return;
        int view = orderTarget.photonView.ViewID;
        if (session.IsAuthority) HandleReview(view, session.LocalActorNumber, session.LocalActorNumber);
        else MultiplayerWire.Raise(ReviewRequestEvent, new object[] { view, session.LocalActorNumber },
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
        else MultiplayerWire.Raise(ReviewRejectedEvent, viewId,
            new RaiseEventOptions { TargetActors = new[] { sender } }, SendOptions.SendReliable);
    }

    private CustomerGroup billTarget;
    private string billTaskId;
    private bool billPending, billCancelled;
    private const byte BillFlowEvent = 199;
    private PlayerMovement billMover;
    private IInteractable billApproach;
    private int billApproachVersion;
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

    public static bool LocalBillIsPrinting
    {
        get
        {
            var s = MultiplayerSessionManager.Instance;
            var bridge = s != null ? s.GetComponent<MultiplayerCustomerInteractionBridge>() : null;
            var customer = bridge != null && bridge.billTarget != null
                ? bridge.billTarget.GetComponentInParent<MultiplayerCustomerSpawn>() : null;
            return s != null && s.IsMultiplayerSession && s.LocalManager != null && customer != null
                && customer.Group != null && !customer.Group.MultiplayerPaymentComplete
                && customer.Group.state == CustomerGroup.GroupState.NeedsBill
                && !bridge.billCancelled && customer.CurrentBillStage == MultiplayerCustomerSpawn.BillStage.Printing
                && customer.BillOwnerActor == s.LocalActorNumber
                && bridge.claims.IsClaimedBy(bridge.billTaskId, s.LocalActorNumber);
        }
    }

    public void ProjectBillState(MultiplayerCustomerSpawn customer, MultiplayerCustomerSpawn.BillStage stage,
        int owner, Vector3 position, Quaternion rotation)
    {
        if (customer == null || billCancelled || billTarget != customer.Group || session.LocalManager == null) return;
        if (stage >= MultiplayerCustomerSpawn.BillStage.Complete)
        {
            // Snapshot projection only clears local presentation. Authority owns completion/recovery.
            billTaskId = null; billTarget = null; localBill = null; billCancelled = true;
            billTransitionPending = false; pendingBillAction = null;
            awaitingBillProjection = MultiplayerCustomerSpawn.BillStage.None;
            return;
        }
        if (owner != session.LocalActorNumber || !claims.IsClaimedBy(billTaskId, owner)) return;
        localBill = BillManager.Instance?.FindBillForGroup(billTarget);
        if (localBill == null || awaitingBillProjection == MultiplayerCustomerSpawn.BillStage.None) return;
        var hands = session.LocalManager.GetComponent<WaiterHands>();
        bool ready = BillProjectionReady(hands, billTarget, localBill, stage, awaitingBillProjection);
        if (!ready) return;
        awaitingBillProjection = MultiplayerCustomerSpawn.BillStage.None;
        billTransitionPending = pendingBillAction != null;
    }

    private static bool IsMatchingHeldBill(WaiterHands hands, CustomerGroup group, BillPaper paper) =>
        hands != null && paper != null && group != null && paper.Matches(group) && hands.holdingBillFor == group
        && hands.BillHoldPoint != null && paper.transform.IsChildOf(hands.BillHoldPoint);

    private static bool BillProjectionReady(WaiterHands hands, CustomerGroup group, BillPaper paper,
        MultiplayerCustomerSpawn.BillStage actual, MultiplayerCustomerSpawn.BillStage expected) =>
        paper != null && paper.Matches(group) && actual == expected
        && (expected == MultiplayerCustomerSpawn.BillStage.Carried ? IsMatchingHeldBill(hands, group, paper)
            : expected == MultiplayerCustomerSpawn.BillStage.Printed && paper.GetComponentInParent<WaiterHands>(true) == null);

    public static bool CanRetrieveBill(BillPaper paper)
    {
        var s = MultiplayerSessionManager.Instance;
        var bridge = s != null ? s.GetComponent<MultiplayerCustomerInteractionBridge>() : null;
        if (bridge == null || paper == null || paper.TargetGroup == null || s.LocalManager == null || !s.CanAct) return false;
        var customer = paper.TargetGroup.GetComponentInParent<MultiplayerCustomerSpawn>();
        if (customer == null || paper.TargetGroup.HasReceivedBill) return false;
        int owner = bridge.claims.GetOwner($"Customer:{customer.photonView.ViewID}:Bill");
        return (owner == 0 || owner == s.LocalActorNumber) && !bridge.billPending
            && !bridge.billTransitionPending && bridge.billMover == null
            && s.LocalManager.GetComponent<WaiterHands>() is WaiterHands hands && !hands.HasBill;
    }

    public static bool TryRetrieveBill(BillPaper paper)
    {
        if (!ReviewIsMultiplayer) return false;
        if (CanRetrieveBill(paper))
        {
            var bridge = MultiplayerSessionManager.Instance.GetComponent<MultiplayerCustomerInteractionBridge>();
            if (bridge.billTarget == paper.TargetGroup && bridge.claims.IsClaimedBy(bridge.billTaskId, bridge.session.LocalActorNumber))
                bridge.MoveForBill(paper.StandPoint, paper.GetInteractRadius(), 2);
            else TryHandleBill(paper.TargetGroup);
        }
        return true;
    }

    private void BeginBillApproach()
    {
        localBill = BillManager.Instance?.FindBillForGroup(billTarget);
        var hands = session.LocalManager != null ? session.LocalManager.GetComponent<WaiterHands>() : null;
        var booth = billTarget != null ? billTarget.assignedBooth : null;
        var stand = booth != null ? booth.approachPoint != null ? booth.approachPoint : booth.transform : null;
        if (IsMatchingHeldBill(hands, billTarget, localBill)) { MoveForBill(stand, 2.75f, 6); return; }
        if (localBill != null && localBill.GetComponentInParent<WaiterHands>(true) == null)
        { MoveForBill(localBill.StandPoint, localBill.GetInteractRadius(), 2); return; }
        MoveForBill(stand, 2.75f, 0);
    }

    private void MoveForBill(Transform stand, float radius, int stage)
    {
        if (billMover != null || billTransitionPending) return;
        var mover = session.LocalManager != null ? session.LocalManager.GetComponent<PlayerMovement>() : null;
        if (mover == null || !mover.isActiveAndEnabled || stand == null || billTarget == null) { PauseBillApproach(); return; }
        int version = ++billApproachVersion;
        int expectedOrder = billTarget.currentOrderNumber;
        long expectedLease = claims.GetLease(billTaskId);
        billMover = mover;
        bool started = mover.UI_MoveToAction(stand, radius, () =>
        {
            if (version != billApproachVersion) return;
            billMover = null;
            billApproach = null;
            if (billCancelled || billTarget == null || session.LocalManager != mover.gameObject
                || billTarget.currentOrderNumber != expectedOrder || claims.GetLease(billTaskId) != expectedLease
                || !claims.IsClaimedBy(billTaskId, session.LocalActorNumber)) { CancelBill(); return; }
            billTransitionPending = true;
            int view = billTarget.GetComponentInParent<MultiplayerCustomerSpawn>().photonView.ViewID;
            SendBillAction(view, stage, billTarget.currentOrderNumber);
        }, () => { if (version != billApproachVersion) return; billMover = null; billApproach = null; PauseBillApproach(); });
        if (started) billApproach = mover.CurrentTarget;
        else PauseBillApproach();
    }

    private void HandleBillFlow(int viewId, int stage, int actor)
    {
        var view = PhotonView.Find(viewId);
        var group = view != null ? view.GetComponent<MultiplayerCustomerSpawn>()?.Group : null;
        var manager = BillManager.Instance;
        if (!session.IsAuthority || group == null || group.IsNetworkObserver || manager == null
            || !session.ValidActor(actor) || !session.TryGetManager(actor, out var owner) || owner == null)
        { ReplyBill(viewId, 4, actor, null, MultiplayerActionOutcome.Unavailable); return; }
        if (!claims.IsClaimedBy($"Customer:{viewId}:Bill", actor))
        { ReplyBill(viewId, 4, actor, null, MultiplayerActionOutcome.AnotherOwner); return; }
        if (group.state != CustomerGroup.GroupState.NeedsBill || group.HasReceivedBill || group.MultiplayerPaymentComplete)
        { ReplyBill(viewId, 4, actor, null, MultiplayerActionOutcome.StaleStage); return; }
        var hands = owner.GetComponent<WaiterHands>();
        var booth = group.assignedBooth;
        var paper = manager.FindBillForGroup(group);
        if (stage == 6)
        {
            var delivery = booth != null ? booth.approachPoint != null ? booth.approachPoint : booth.transform : null;
            if (!IsMatchingHeldBill(hands, group, paper))
            { ReplyBill(viewId, 4, actor, null, MultiplayerActionOutcome.WrongTarget); return; }
            if (!BillWithinReach(owner, delivery, 2.75f))
            { ReplyBill(viewId, 4, actor, null, MultiplayerActionOutcome.Unreachable); return; }
            group.ReceiveBillFromWaiter();
            if (!group.HasReceivedBill) { ReplyBill(viewId, 4, actor, null, MultiplayerActionOutcome.StaleStage); return; }
            hands.ClearBill();
            view.GetComponent<MultiplayerCustomerSpawn>().PublishAssignment();
            ReplyBill(viewId, 5, actor, null);
            claims.CompleteOnAuthority($"Customer:{viewId}:Bill", actor);
            return;
        }
        if (hands == null || hands.HasBill || hands.HasMoney || hands.HasTray || hands.HasTicket
            || owner.GetComponent<BusserHands>()?.HasTray == true || owner.GetComponentInChildren<TakeoutBagInteractable>() != null)
        { ReplyBill(viewId, 4, actor, null, MultiplayerActionOutcome.OccupiedHands); return; }
        Transform stand = stage == 0 ? booth != null ? booth.approachPoint != null ? booth.approachPoint : booth.transform : null
            : paper != null ? paper.StandPoint : null;
        float radius = stage == 0 ? 2.75f : paper != null ? paper.GetInteractRadius() : 0f;
        if (!BillWithinReach(owner, stand, radius))
        { ReplyBill(viewId, 4, actor, null, MultiplayerActionOutcome.Unreachable); return; }
        if (stage == 0)
        {
            if (billActions.TryGetValue((actor, viewId), out var action) && printingBills.Add(viewId))
                StartCoroutine(PrintClaimedBill(group, viewId, actor, action, billActionDay));
        }
        else if (stage == 2)
        {
            if (paper == null || paper.GetComponentInParent<WaiterHands>(true) != null)
            { ReplyBill(viewId, 4, actor, null, MultiplayerActionOutcome.AnotherOwner); return; }
            hands.PickupBillPaper(paper);
            view.GetComponent<MultiplayerCustomerSpawn>().PublishAssignment();
            bool pickedUp = IsMatchingHeldBill(hands, group, paper);
            ReplyBill(viewId, pickedUp ? 3 : 4, actor, paper,
                pickedUp ? MultiplayerActionOutcome.Success : MultiplayerActionOutcome.Unavailable);
        }
    }

    private System.Collections.IEnumerator PrintClaimedBill(CustomerGroup group, int viewId, int actor, BillAction action, int day)
    {
        BillPaper paper = null;
        var manager = BillManager.Instance;
        // Reuse the normal printing entry; payment follows bill delivery separately.
        manager.RequestBill(group);
        float deadline = Time.unscaledTime + 30f;
        while (BillActionIsCurrent(actor, action, day) && group != null && claims.IsClaimedBy($"Customer:{viewId}:Bill", actor)
            && group.currentOrderNumber == action.order && group.state == CustomerGroup.GroupState.NeedsBill && session.IsAuthority)
        {
            if (paper == null && manager != null) paper = manager.FindBillForGroup(group);
            if (paper != null) break;
            if (Time.unscaledTime >= deadline) break;
            yield return null;
        }
        // A cancelled/replaced print must not acknowledge a newer pickup/delivery.
        if (!BillActionIsCurrent(actor, action, day)) yield break;
        if (paper != null && group != null && group.currentOrderNumber == action.order && paper.Matches(group) && session.IsAuthority
            && claims.IsClaimedBy($"Customer:{viewId}:Bill", actor))
        {
            ReplyBill(viewId, 1, actor, paper);
        }
        else ReplyBill(viewId, 4, actor, null, MultiplayerActionOutcome.Unavailable);
        printingBills.Remove(viewId);
    }

    private void ReplyBill(int view, int stage, int actor, BillPaper paper,
        MultiplayerActionOutcome outcome = MultiplayerActionOutcome.Success)
    {
        Vector3 position = paper != null ? paper.transform.position : Vector3.zero;
        Quaternion rotation = paper != null ? paper.transform.rotation : Quaternion.identity;
        CompleteBillAction(view, stage, actor, position, rotation, paper, outcome);
    }

    private void ObserveBill(int view, int stage, Vector3 position, Quaternion rotation)
    {
        if (stage == 5 && billTaskId == $"Customer:{view}:Bill")
        {
            billTaskId = null; billTarget = null; localBill = null; billCancelled = true;
            awaitingBillProjection = MultiplayerCustomerSpawn.BillStage.None;
            billTransitionPending = false; return;
        }
        if (billCancelled || billTarget == null || billTaskId != $"Customer:{view}:Bill"
            || !claims.IsClaimedBy(billTaskId, session.LocalActorNumber)) return;
        if (stage != 1 && stage != 3) return;
        // An acknowledgement does not attach an item. Wait for the authoritative
        // projection before enabling the next explicit player action.
        awaitingBillProjection = stage == 3 ? MultiplayerCustomerSpawn.BillStage.Carried : MultiplayerCustomerSpawn.BillStage.Printed;
        billProjectionDeadline = Time.unscaledTime + 8f;
        billTransitionPending = true;
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
        WaiterHands.ReconcileMultiplayerHands(session.LocalManager);
        var hands = session.LocalManager.GetComponent<WaiterHands>();
        if (hands != null && hands.HasBill && hands.holdingBillFor != group)
        { WarningSlideUI.Instance?.Show("This bill belongs to another customer."); return true; }
        if (bridge.billTarget == group && bridge.claims.IsClaimedBy(bridge.billTaskId, session.LocalActorNumber))
        {
            bridge.RetainOwnedTask(bridge.billTaskId);
            if (hands != null && hands.HasBill && !bridge.billTransitionPending && bridge.billMover == null)
            {
                var booth = group.assignedBooth;
                bridge.MoveForBill(booth != null ? booth.approachPoint != null ? booth.approachPoint : booth.transform : null, 2.75f, 6);
            }
            return true;
        }
        string requested = $"Customer:{customer.photonView.ViewID}:Bill";
        bridge.RequestTask(requested, () =>
        {
            bridge.billTarget = group;
            bridge.billTaskId = requested;
            bridge.billCancelled = false;
            bridge.billPending = true;
            bridge.OnResult(requested, true);
        });
        return true;
    }

    private void PauseBillApproach()
    {
        billApproachVersion++;
        var mover = billMover; var approach = billApproach;
        billMover = null; billApproach = null;
        if (mover != null && approach != null && ReferenceEquals(mover.CurrentTarget, approach)) mover.CancelLockedTask();
        var hands = session.LocalManager != null ? session.LocalManager.GetComponent<WaiterHands>() : null;
        var paper = BillManager.Instance?.FindBillForGroup(billTarget);
        if (billTarget != null && claims.IsClaimedBy(billTaskId, session.LocalActorNumber)
            && (IsMatchingHeldBill(hands, billTarget, paper)
                || awaitingBillProjection == MultiplayerCustomerSpawn.BillStage.Carried
                || pendingBillAction?.request.stage == 2 || pendingBillAction?.request.stage == 6))
        {
            billTransitionPending = pendingBillAction != null || awaitingBillProjection != MultiplayerCustomerSpawn.BillStage.None;
            return;
        }
        CancelBill();
    }

    private void CancelBill()
    {
        billApproachVersion++;
        awaitingBillProjection = MultiplayerCustomerSpawn.BillStage.None;
        billCancelled = true;
        billTransitionPending = false;
        var mover = billMover;
        var approach = billApproach;
        billMover = null;
        billApproach = null;
        if (mover != null && approach != null && session.LocalManager == mover.gameObject
            && ReferenceEquals(mover.CurrentTarget, approach)) mover.CancelLockedTask();
        if (session.IsAuthority && billTarget != null && claims.IsClaimedBy(billTaskId, session.LocalActorNumber))
            BillManager.Instance?.ReturnUndeliveredBill(billTarget);
        pendingBillAction = null;
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
        var customer = group != null ? group.GetComponentInParent<MultiplayerCustomerSpawn>() : null;
        if (bridge == null || customer == null || session.LocalManager == null
            || group.state != CustomerGroup.GroupState.ReadyToOrder || group.HasConfirmedOrder) return true;
        if (bridge.orderPending) return true;
        if (bridge.orderTarget == customer && bridge.claims.IsClaimedBy(bridge.orderTaskId, session.LocalActorNumber))
        {
            bridge.RetainOwnedTask(bridge.orderTaskId);
            return true;
        }
        string requested = $"Customer:{customer.photonView.ViewID}:Order";
        bridge.RequestTask(requested, () =>
        {
            bridge.orderTarget = customer;
            bridge.orderTaskId = requested;
            bridge.orderCancelled = false;
            bridge.orderPending = true;
            bridge.OnResult(requested, true);
        });
        return true;
    }

    private void CancelOrder()
    {
        orderApproachVersion++;
        orderApproached = false;
        orderBooth = null;
        orderCancelled = true;
        var mover = orderMover;
        var approach = orderApproach;
        orderMover = null;
        orderApproach = null;
        if (mover != null && approach != null && session.LocalManager == mover.gameObject
            && ReferenceEquals(mover.CurrentTarget, approach)) mover.CancelLockedTask();
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
            if (bridge.seatController != null && !bridge.seatController.IsSelectingBooth(group))
                bridge.seatController = null;
            return true;
        }
        bridge.RequestGreeting();
        return true;
    }

    internal static bool IsGreetingActionHidden(CustomerGroup group)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession || group == null) return false;
        var customer = group.GetComponentInParent<MultiplayerCustomerSpawn>();
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        if (customer == null || bridge == null || bridge.claims == null || !customer.ReadyForInteraction || group.HasBeenAssigned) return true;
        string id = $"Customer:{customer.photonView.ViewID}:GreetSeat";
        int owner = bridge.claims.GetOwner(id);
        if (owner != 0 && owner != session.LocalActorNumber || bridge.claims.GetBotClaim(id)?.committed == true) return true;
        return bridge.requestedTaskId == id || bridge.target == customer && !bridge.cancelled
            && (bridge.pending || !bridge.greetApproached || bridge.greetWaiting || bridge.seatPending
                || bridge.seatController != null && bridge.seatController.IsSelectingBooth(group));
    }

    private void RequestGreeting()
    {
        if (greetWaiting || target == null || !greetApproached || cancelled) return;
        greetWaiting = true;
        greetLease = claims.GetLease(taskId);
        greetReplyUntil = Time.unscaledTime + 8f;
        CustomerGreetBubbleSpawner.Instance?.Hide();
        if (session.IsAuthority) HandleGreet(target.photonView.ViewID, session.LocalActorNumber, session.LocalActorNumber, greetLease);
        else if (!MultiplayerWire.Raise(GreetRequestEvent, new object[] { target.photonView.ViewID, session.LocalActorNumber, greetLease },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable)) Cancel();
    }

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    public void OnEvent(EventData photonEvent)
    {
        if (!MultiplayerWire.TryRead(photonEvent, out var payload)) return;
        if (photonEvent.Code == BillFlowEvent) { ReceiveBillAction(photonEvent.Sender, payload); return; }
        if (session == null || !session.IsMultiplayerSession) return;
        if ((photonEvent.Code == CarryRequestEvent || photonEvent.Code == ServeRequestEvent) && session.IsAuthority)
        { ReceiveFoodRequest(photonEvent.Sender, payload); return; }
        if (photonEvent.Code == CarryRejectedEvent || photonEvent.Code == ServeRejectedEvent)
        { ReceiveFoodReply(photonEvent.Sender, payload); return; }
        if (photonEvent.Code == ConfirmRequestEvent)
        { ReceiveReviewedOrderCommand(photonEvent.Sender, payload); return; }
        if (photonEvent.Code == ConfirmRejectedEvent)
        { ReceiveReviewedOrderResult(photonEvent.Sender, payload); return; }
        if (photonEvent.Code == ReviewRejectedEvent && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber
            && payload is int rejectedView && orderTarget != null && orderTarget.photonView.ViewID == rejectedView)
        { CancelOrder(); return; }
        if (photonEvent.Code == ReviewRequestEvent && session.IsAuthority
            && payload is object[] review && review.Length == 2
            && review[0] is int reviewView && review[1] is int reviewActor)
        { HandleReview(reviewView, reviewActor, photonEvent.Sender); return; }
        if (photonEvent.Code == GreetRequestEvent && photonEvent.Sender == session.Run?.hostActor
            && payload is object[] greetingResult && greetingResult.Length == 4
            && greetingResult[0] is int greetView && greetingResult[1] is int greetActor
            && greetingResult[2] is long lease && greetingResult[3] is bool greeted)
        { FinishGreeting(greetView, greetActor, lease, greeted); return; }
        if (photonEvent.Code == SeatResultEvent && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber
            && payload is object[] result && result.Length == 2
            && result[0] is int resultView && result[1] is bool accepted)
        {
            FinishSeat(resultView, accepted);
            return;
        }
        if (!session.IsAuthority) return;
        if (photonEvent.Code == SeatRequestEvent && payload is object[] seat && seat.Length == 3
            && seat[0] is int seatView && seat[1] is int seatActor && seat[2] is string boothId)
        {
            HandleSeat(seatView, seatActor, photonEvent.Sender, boothId);
            return;
        }
        if (photonEvent.Code != GreetRequestEvent) return;
        if (payload is object[] data && data.Length == 3
            && data[0] is int viewId && data[1] is int actor && data[2] is long greetStamp)
            HandleGreet(viewId, actor, photonEvent.Sender, greetStamp);
    }

    private void HandleGreet(int viewId, int actor, int sender, long lease)
    {
        if (!session.IsAuthority || actor != sender || !session.ValidActor(sender)) return;
        string key = $"{viewId}:{actor}:{lease}";
        if (!greetingRequests.Add(key)) return;
        StartCoroutine(GreetWhenArrived(viewId, actor, lease, key));
    }

    private System.Collections.IEnumerator GreetWhenArrived(int viewId, int actor, long lease, string key)
    {
        string run = session.RunId;
        int day = GameFlowManager.Instance.CurrentDay;
        float until = Time.unscaledTime + 0.4f;
        bool accepted = false;
        string id = $"Customer:{viewId}:GreetSeat";
        while (session.RunId == run && GameFlowManager.Instance.CurrentDay == day && session.IsAuthority && session.ValidActor(actor))
        {
            var customer = PhotonView.Find(viewId)?.GetComponent<MultiplayerCustomerSpawn>();
            var group = customer != null ? customer.Group : null;
            var line = FindFirstObjectByType<LobbyLineManager>();
            if (!claims.IsClaimedBy(id, actor) || claims.GetLease(id) != lease || lease <= 0
                || !session.TryGetManager(actor, out var manager) || group == null || group.IsNetworkObserver
                || group.state != CustomerGroup.GroupState.Waiting || !group.CanBeGreeted()
                || line == null || line.GetFrontOfLine() != group) break;
            var position = NetworkPlayerMovementSync.AuthorityPosition(manager);
            var stand = CustomerGreetBubbleUI.FindClosestCustomer(group, position);
            if (stand == null) break;
            var delta = position - stand.position; delta.y = 0;
            float radius = CustomerGreetBubbleUI.GreetingInteractRadius + 0.35f;
            if (delta.sqrMagnitude <= radius * radius)
            {
                accepted = true;
                if (!group.hasBeenGreeted) { group.MarkGreeted(); customer.PublishGreeted(); }
                break;
            }
            if (Time.unscaledTime >= until) break;
            yield return null;
        }
        greetingRequests.Remove(key);
        if (session.RunId != run || GameFlowManager.Instance.CurrentDay != day) yield break;
        if (actor == session.LocalActorNumber) FinishGreeting(viewId, actor, lease, accepted);
        else MultiplayerWire.Raise(GreetRequestEvent, new object[] { viewId, actor, lease, accepted },
            new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }

    private void FinishGreeting(int view, int actor, long lease, bool accepted)
    {
        if (!greetWaiting || target == null || target.Group == null || target.photonView.ViewID != view || actor != session.LocalActorNumber
            || greetLease != lease || claims.GetLease(taskId) != lease) return;
        greetWaiting = false;
        if (!accepted) { Cancel(); WarningSlideUI.Instance?.Show("This customer cannot be greeted here. Select their current action to try again."); return; }
        target.Group.MarkGreeted(); // Authoritative acknowledgement; same projection as the customer snapshot.
        RefreshOwnedPopup(target.Group);
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
        if (!CanSelectBooth(controller, group)) return true;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        if (!IsValidSeatBooth(group, booth))
        {
            WarningSlideUI.Instance?.Show("That table is not available for this group.");
            return true;
        }
        var mover = session.LocalManager.GetComponent<PlayerMovement>();
        if (mover == null || !mover.isActiveAndEnabled) return true;
        bridge.seatController = controller;
        bridge.seatPending = true;
        var customer = bridge.target;
        string claimedTask = bridge.taskId;
        int version = ++bridge.seatApproachVersion;
        Transform approach = booth.approachPoint != null ? booth.approachPoint : booth.transform;
        BoothAssignArrowManager.Instance?.HideAll();
        CustomerGreetBubbleSpawner.Instance?.Hide();
        bridge.greetMover = mover;
        bool started = mover.UI_MoveToAction(approach, 2.75f,
            () =>
            {
                if (version != bridge.seatApproachVersion) return;
                bridge.greetMover = null;
                bridge.greetApproach = null;
                if (bridge.cancelled || !bridge.seatPending || bridge.target != customer
                    || customer == null || group == null || customer.Group != group || bridge.taskId != claimedTask
                    || !session.IsMultiplayerSession || session.LocalManager != mover.gameObject
                    || !customer.ReadyForInteraction || !group.hasBeenGreeted || group.HasBeenAssigned
                    || !bridge.claims.IsClaimedBy(claimedTask, session.LocalActorNumber))
                { bridge.Cancel(); return; }
                if (!IsValidSeatBooth(group, booth))
                {
                    WarningSlideUI.Instance?.Show("That table is no longer available. Select the customer to try again.");
                    bridge.Cancel();
                    return;
                }
                int viewId = customer.photonView.ViewID;
                string boothId = BoothIdentity(booth);
                if (session.IsAuthority) bridge.HandleSeat(viewId, session.LocalActorNumber, session.LocalActorNumber, boothId);
                else if (!MultiplayerWire.Raise(SeatRequestEvent,
                    new object[] { viewId, session.LocalActorNumber, boothId },
                    new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable))
                    bridge.FinishSeat(viewId, false);
            },
            () =>
            {
                if (version != bridge.seatApproachVersion) return;
                bridge.greetMover = null;
                bridge.greetApproach = null;
                bridge.Cancel();
            });
        if (started) bridge.greetApproach = mover.CurrentTarget;
        else if (version == bridge.seatApproachVersion) bridge.Cancel();
        return true;
    }

    private static bool IsValidSeatBooth(CustomerGroup group, Booth booth)
    {
        if (group == null || !group.CanBeSeated || booth == null || !booth.gameObject.activeInHierarchy
            || booth.CurrentGroup != null || booth.seats == null || booth.seats.Count < group.Size
            || !booth.IsAvailableFor(group.Size)
            || (booth.approachPoint != null && !booth.approachPoint.gameObject.activeInHierarchy)) return false;
        for (int i = 0; i < group.Size; i++)
        {
            var seat = booth.GetSeat(i);
            if (seat == null || !seat.gameObject.activeInHierarchy || SeatAnchor.IsSeatOccupied(seat)) return false;
        }
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
        foreach (var booth in MultiplayerWorldRegistry.All<Booth>())
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
        else MultiplayerWire.Raise(SeatResultEvent, new object[] { viewId, accepted },
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
        claims.OwnerChanged += OnOwnerChanged;
    }

    private void RetainOwnedTask(string id)
    {
        CancelRequestedTask();
        claims.RequestClaim(id); // Fence older intents without restarting the owned task.
    }

    private void RequestTask(string id, System.Action onGranted)
    {
        if (requestedTaskId == id) return;
        requestedTaskId = id;
        beginGrantedTask = onGranted;
        if (!claims.RequestClaim(id))
        {
            requestedTaskId = null;
            beginGrantedTask = null;
        }
    }

    public void RequestServiceTask(string id, System.Action onGranted) => RequestTask(id, onGranted);

    private void CancelRequestedTask()
    {
        string id = requestedTaskId;
        requestedTaskId = null;
        beginGrantedTask = null;
        if (id != null) claims.Release(id);
    }

    private void OnOwnerChanged(string id, int owner)
    {
        if (owner == session.LocalActorNumber) return;
        if (id == taskId) Cancel();
        if (id == orderTaskId) CancelOrder();
        if (id == pickupTaskId) CancelPickup();
        if (id == billTaskId) CancelBill();
    }

    private void Begin(MultiplayerCustomerSpawn customer, Camera camera)
    {
        if (customer == null || !customer.ReadyForInteraction) return;
        if (target == customer && claims.IsClaimedBy(taskId, session.LocalActorNumber))
        {
            RetainOwnedTask(taskId);
            return;
        }
        string requested = $"Customer:{customer.photonView.ViewID}:GreetSeat";
        CustomerGreetBubbleSpawner.Instance?.SetVisibleAndRefresh(customer.Group, false);
        RequestTask(requested, () =>
        {
            target = customer;
            taskId = requested;
            cameraForPopup = camera;
            cancelled = false;
            greetApproached = false;
            pending = true;
            OnResult(requested, true);
        });
    }

    private void OnResult(string id, bool accepted)
    {
        if (id == requestedTaskId)
        {
            var start = beginGrantedTask;
            requestedTaskId = null;
            beginGrantedTask = null;
            if (accepted && claims.IsClaimedBy(id, session.LocalActorNumber)) start?.Invoke();
            else WarningSlideUI.Instance?.Show(claims.LastRejection ?? "The task changed. Select its current action.");
            return;
        }
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
            else BeginPickupApproach();
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
            else BeginOrderApproach();
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
                if (customer.Group.hasBeenGreeted) RefreshOwnedPopup(customer.Group);
                else RequestGreeting();
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
        if (greetWaiting && Time.unscaledTime >= greetReplyUntil)
        { Cancel(); WarningSlideUI.Instance?.Show("Greeting timed out. Select the customer to try again."); }
        TickBillActions();
        TickReviewedOrderSubmission();
        TickFoodActions();
        if (pickupApproach != null && (pickupMover == null || !pickupMover.isActiveAndEnabled
            || session.LocalManager != pickupMover.gameObject || !pickupApproach.CanInteract()
            || !ReferenceEquals(pickupMover.CurrentTarget, pickupApproach))) CancelPickup();
        if (orderApproach != null && (orderMover == null || !orderMover.isActiveAndEnabled
            || session.LocalManager != orderMover.gameObject || !orderApproach.CanInteract()
            || !ReferenceEquals(orderMover.CurrentTarget, orderApproach))) CancelOrder();
        if (requestedTaskId != null && (!session.IsMultiplayerSession || session.LocalManager == null
            || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))) CancelRequestedTask();
        if (billTarget != null && billTarget.MultiplayerPaymentComplete)
        {
            billTaskId = null;
            CancelBill();
        }
        if (billApproach != null && (billMover == null || !billMover.isActiveAndEnabled
            || session.LocalManager != billMover.gameObject || !billApproach.CanInteract()
            || !ReferenceEquals(billMover.CurrentTarget, billApproach))) PauseBillApproach();
        if (billTaskId != null && (!session.IsMultiplayerSession || session.LocalManager == null
            || billTarget == null || billTarget.state != CustomerGroup.GroupState.NeedsBill || billTarget.HasReceivedBill
            || (!billPending && !claims.IsClaimedBy(billTaskId, session.LocalActorNumber)))) CancelBill();
        if (billTaskId != null && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))) PauseBillApproach();
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
        seatApproachVersion++;
        cancelled = true;
        greetWaiting = false;
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
        ForgetFoodIntent();
        pickupCancelled = true;
        CancelRequestedTask();
        CancelBill();
        CancelPickup();
        CancelOrder();
        Cancel();
        if (claims != null) claims.ClaimResult -= OnResult;
        if (claims != null) claims.OwnerChanged -= OnOwnerChanged;
    }
}
