using System;
using IEnumerator = System.Collections.IEnumerator;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Request/receipt transport for the existing takeout, cash/card and complaint systems.
// No client supplies prices, rewards, order contents or customer outcomes.
public sealed partial class MultiplayerServiceActions : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte RequestEvent = 176, ReplyEvent = 177;
    private const string StateKey = "restaurant.service.v3";
    [Serializable] private sealed class Command
    {
        public MultiplayerActionContext context;
        public int value;
        public string id => context?.actionId;
        public string operation => context?.operation;
        public int view => context != null ? context.customerView : 0;
    }
    [Serializable] private sealed class Reply
    {
        public MultiplayerActionResult result;
        public int amount, total, value;
        public string id => result?.context?.actionId;
        public string operation => result?.context?.operation;
        public int view => result?.context != null ? result.context.customerView : 0;
        public long revision => result != null ? result.revision : 0;
        public bool accepted => result != null && result.Accepted;
    }
    [Serializable] public sealed class GroupState
    {
        public int view, phase, queue, orderNumber, moneyAmount, owner, bagOwner;
        public string moneyHolder, bagHolder;
        public bool confirmed, paid, billDelivered, hasMoney, card, hasBag;
        public CustomerGroup.SimpleOrder order, submitted;
        public Vector3 bagPosition;
        public Quaternion bagRotation;
    }
    [Serializable] private sealed class Snapshot
    {
        public string run;
        public int day, revision, takeoutView, takeoutPhase;
        public List<GroupState> groups = new();
        public ManagerComplaintSystem.NetworkState complaint;
    }
    private sealed class Reservation { public int actor, order; public long lease; public bool registerOpen; }
    private MultiplayerSessionManager session;
    private int day, revision, received, appliedObjects;
    private long localReservationRevision;
    private float nextPublish, retryAt, requestUntil, nextObservedApply, cardOpenUntil;
    private string previous, lastReadPayload;
    private Command pending;
    private CustomerGroup cancelAfterPending;
    private Reply deferredCard;
    private Reply deferredPickup;
    private float pickupAttachUntil;
    private bool cashierAfterPickup;
    public bool? CardPaymentResult { get; private set; }
    private Snapshot observed;
    private string approachTask;
    private PlayerMovement approachMover;
    private IInteractable approachTarget;
    private int approachVersion;
    private readonly Dictionary<string, Reply> receipts = new();
    private readonly Dictionary<string, Command> executing = new();
    private readonly Dictionary<int, Reservation> reservations = new();
    private readonly Dictionary<int, MoneyPickup> observedMoney = new();
    private readonly Dictionary<int, TakeoutBagInteractable> observedBags = new();
    public static bool IsActive => MultiplayerRestaurantBridge.IsActive;
    public static MultiplayerServiceActions Active => MultiplayerSessionManager.Instance?.GetComponent<MultiplayerServiceActions>();
    public int PaymentOwner(int view)
    {
        if (view <= 0) return 0;
        if (session.IsAuthority) return reservations.TryGetValue(view, out var owner) ? owner.actor : 0;
        if (observed?.groups != null) foreach (var group in observed.groups) if (group.view == view) return group.owner;
        return 0;
    }
    public TakeoutBagInteractable LocalBag
    {
        get
        {
            foreach (var bag in MultiplayerWorldRegistry.All<TakeoutBagInteractable>())
                if (bag.NetworkOwner == session.LocalActorNumber) return bag;
            return null;
        }
    }
    private void Awake() => session = GetComponent<MultiplayerSessionManager>();
    private static int ViewId(CustomerGroup group) => group != null ? group.GetComponentInParent<MultiplayerCustomerSpawn>()?.photonView.ViewID ?? 0 : 0;
    public static CustomerGroup Resolve(int view) => PhotonView.Find(view)?.GetComponent<MultiplayerCustomerSpawn>()?.Group;
    private static MoneyPickup Money(CustomerGroup group)
    {
        foreach (var money in MultiplayerWorldRegistry.All<MoneyPickup>())
            if (money.gameObject.activeInHierarchy && money.TargetGroup == group && money.OrderNumber == group.currentOrderNumber) return money;
        return null;
    }
    private static TakeoutBagInteractable Bag(CustomerGroup group)
    {
        foreach (var bag in MultiplayerWorldRegistry.All<TakeoutBagInteractable>())
            if (bag.TargetGroup == group) return bag;
        return null;
    }
    public static bool IsReserved(UnityEngine.Object target)
    {
        if (!IsActive || Active == null) return false;
        if (target is FoodTray tray) return Active.dirtyOwners.ContainsKey(tray.orderNumber);
        CustomerGroup group = target is CustomerGroup customer ? customer
            : target is MoneyPickup money ? money.TargetGroup : target is TakeoutBagInteractable bag ? bag.TargetGroup : null;
        return group != null && (Active.reservations.ContainsKey(ViewId(group)) || Bag(group)?.NetworkOwner > 0);
    }
    public static bool CanCollect(MoneyPickup money) => Active != null && Active.session.CanAct
        && Active.deferredPickup == null
        && money != null && money.IsAvailableForCollection && WaiterHands.ActivePlayerHands != null
        && !WaiterHands.ActivePlayerHands.HasMoney && !WaiterHands.ActivePlayerHands.HasTray
        && !WaiterHands.ActivePlayerHands.HasBill && !WaiterHands.ActivePlayerHands.HasTicket
        && BusserHands.ActivePlayerHands?.HasTray != true && Active.LocalBag == null;
    public static bool CanUseCashier => Active != null && Active.session.CanAct
        && (WaiterHands.ActivePlayerHands?.HasMoney == true || Active.deferredPickup != null);
    public static bool CanStartCashierOpen => CanUseCashier && Active.pending == null
        && Active.deferredCard == null && Active.cancelAfterPending == null;
    public static bool RequestCashierOpen(PlayerMovement mover)
    {
        var active = Active;
        if (active == null || !active.session.CanAct || mover == null || mover.gameObject != active.session.LocalManager) return false;
        var hands = mover.GetComponent<WaiterHands>();
        if (hands != null && hands.HasMoney) return Send("cash_open", hands.holdingMoneyFor);
        if (active.deferredPickup == null) return false;
        // The player already clicked this station. Retain that input while the
        // acknowledged item attachment catches up; snapshots alone never open it.
        active.cashierAfterPickup = true;
        return true;
    }
    public static void Approach(string operation, CustomerGroup group, Transform stand, float radius)
    {
        var active = Active;
        var mover = active != null ? active.session.LocalManager?.GetComponent<PlayerMovement>() : null;
        if (mover == null || stand == null || group == null || active.pending != null) return;
        if (operation == "money_pickup" || operation == "card_open")
        { active.ApproachClaimed($"Customer:{ViewId(group)}:Payment", mover, stand, radius, () => Send(operation, group)); return; }
        mover.UI_MoveToAction(stand, radius, () => Send(operation, group), () => { });
    }
    private void ApproachClaimed(string task, PlayerMovement mover, Transform stand, float radius, Action arrived)
    {
        if (approachTask == task && approachMover != null && ReferenceEquals(approachMover.CurrentTarget, approachTarget)) return;
        var claims = session.GetComponent<MultiplayerTaskClaims>();
        session.GetComponent<MultiplayerCustomerInteractionBridge>().RequestServiceTask(task, () =>
        {
            int version = ++approachVersion;
            approachTask = task; approachMover = mover; approachTarget = null;
            long lease = claims.GetLease(task);
            void Release()
            {
                if (version != approachVersion) return;
                approachTask = null; approachMover = null; approachTarget = null;
                if (claims.GetLease(task) == lease && claims.IsClaimedBy(task, session.LocalActorNumber)) claims.Release(task);
            }
            if (!session.CanAct || mover == null || stand == null) { Release(); return; }
            bool started = mover.UI_MoveToAction(stand, radius, () =>
            {
                if (version != approachVersion) return;
                approachTask = null; approachMover = null; approachTarget = null;
                if (claims.GetLease(task) == lease && claims.IsClaimedBy(task, session.LocalActorNumber)) arrived();
            }, Release);
            if (!started) Release(); else if (approachTask == task) approachTarget = mover.CurrentTarget;
        });
    }
    public static bool Send(string operation, CustomerGroup group, int value = 0)
    {
        if (group == null || ViewId(group) <= 0) return false;
        return SendRequest(operation, ViewId(group), value);
    }
    private static bool SendRequest(string operation, int view, int value)
    {
        var active = Active;
        if (active == null || !active.session.CanAct) return false;
        if (active.pending != null || active.deferredCard != null)
        {
            if (operation == "payment_cancel" && (active.pending?.view == view || active.deferredCard?.view == view))
            { active.cancelAfterPending = Resolve(view); return true; }
            return false;
        }
        if (operation == "card_confirm") active.CardPaymentResult = null;
        var group = Resolve(view);
        string task = operation.StartsWith("cleanup_", StringComparison.Ordinal) ? $"Order:{value}:Cleanup"
            : IsPayment(operation) && group?.IsTakeout != true ? $"Customer:{view}:Payment" : null;
        var context = MultiplayerActionContext.Capture(active.session, group, operation, task, ExpectedStage(operation));
        context.expectedRevision = active.session.IsAuthority ? active.revision : active.received;
        if (operation.StartsWith("cleanup_", StringComparison.Ordinal)) context.orderNumber = value;
        active.pending = new Command { context = context, value = value };
        active.requestUntil = Time.unscaledTime + 8f;
        active.Transmit();
        return true;
    }
    private void Transmit()
    {
        retryAt = Time.unscaledTime + 1f;
        if (session.IsAuthority) Handle(pending, session.LocalActorNumber);
        else MultiplayerWire.Raise(RequestEvent, JsonUtility.ToJson(pending),
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    private void Update()
    {
        if (!session.IsConnected || !MultiplayerProgressionContext.Ready || session.Ended) return;
        int current = GameFlowManager.Instance.CurrentDay;
        if (day != current) { ResetDay(); day = current; }
        if (pending != null)
        {
            if (!session.CanAct || Time.unscaledTime > requestUntil)
            {
                if (pending.operation == "card_confirm") CardPaymentResult = false;
                pending = null; WarningSlideUI.Instance?.Show("Service request timed out. Check the customer before retrying.");
                TrySendQueuedCancellation();
            }
            else if (Time.unscaledTime >= retryAt) Transmit();
        }
        if (session.IsAuthority && Time.unscaledTime >= nextPublish)
        {
            RecoverDirtyTrays();
            foreach (int key in new List<int>(reservations.Keys))
            {
                var entry = reservations[key];
                var group = Resolve(key);
                var claims = session.GetComponent<MultiplayerTaskClaims>();
                bool claimValid = group != null && (group.IsTakeout || claims.IsClaimedBy($"Customer:{key}:Payment", entry.actor)
                    && claims.GetLease($"Customer:{key}:Payment") == entry.lease);
                if (group == null || group.currentOrderNumber != entry.order || group.MultiplayerPaymentComplete
                    || !session.ValidActor(entry.actor) || !claimValid)
                    ReleaseReservation(key, false);
            }
            foreach (var bag in MultiplayerWorldRegistry.All<TakeoutBagInteractable>())
                if (bag.NetworkOwner > 0 && !session.ValidActor(bag.NetworkOwner))
                    bag.PresentNetworkOwner(null, 0, bag.PickupPosition, bag.PickupRotation);
            Publish();
        }
        if (!session.IsHostConnection && Time.unscaledTime >= nextObservedApply)
        {
            nextObservedApply = Time.unscaledTime + 0.2f;
            ReadSnapshot();
            ApplyObserved(); // Newly instantiated customers/players may arrive after the property.
        }
        TryOpenCard();
        TryFinishPickupPresentation();
    }
    public void ResetDay()
    {
        approachVersion++; approachTask = null; approachMover = null; approachTarget = null;
        pending = null; deferredCard = deferredPickup = null; cancelAfterPending = null; cashierAfterPickup = false;
        CardPaymentResult = null; received = appliedObjects = 0;
        StopAllCoroutines(); executing.Clear();
        receipts.Clear(); reservations.Clear(); dirtyOwners.Clear(); previous = null; observed = null; lastReadPayload = null;
        CashierRegisterUI.Instance?.Hide();
        CardPaymentUI.Instance?.CloseNetworkPayment();
        foreach (var money in observedMoney.Values) RemoveObservedMoney(money);
        foreach (var bag in observedBags.Values) if (bag != null) Destroy(bag.gameObject);
        observedMoney.Clear(); observedBags.Clear();
    }
    private static bool Near(GameObject player, Transform point, float radius = 2f) => point != null && Near(player, point.position, radius);
    private static bool Near(GameObject player, Vector3 point, float radius = 2f)
    { if (player == null) return false; var delta = NetworkPlayerMovementSync.AuthorityPosition(player) - point; delta.y = 0; return delta.sqrMagnitude <= (radius + 0.35f) * (radius + 0.35f); }
    private bool Reserve(int view, int actor, CustomerGroup group, UnityEngine.Object target)
    {
        if (reservations.TryGetValue(view, out var existing)) return existing.actor == actor && existing.order == group.currentOrderNumber
            && (group.IsTakeout || existing.lease == session.GetComponent<MultiplayerTaskClaims>().GetLease($"Customer:{view}:Payment"));
        if (target is MoneyPickup && !session.GetComponent<MultiplayerTaskClaims>().IsClaimedBy($"Customer:{view}:Payment", actor)) return false;
        foreach (var reservation in reservations.Values) if (reservation.actor == actor) return false;
        if (target is not MoneyPickup && RestaurantTaskClaim.IsClaimedByBot(group)) return false;
        if (RestaurantTaskClaim.IsClaimedByBot(target)
            && !RestaurantTaskClaim.TryTakeOver(RestaurantTaskClaim.GetMultiplayerTaskId(target))) return false;
        reservations[view] = new Reservation { actor = actor, order = group.currentOrderNumber,
            lease = session.GetComponent<MultiplayerTaskClaims>().GetLease($"Customer:{view}:Payment") };
        return true;
    }
    private void Handle(Command request, int actor)
    {
        if (!session.IsAuthority || request?.context == null || !request.context.MatchesSession(session, actor)) return;
        string key = actor + ":" + request.id;
        if (receipts.TryGetValue(key, out var cached))
        {
            if (cached.result.context.MatchesAction(request.context) && cached.value == request.value) SendReply(cached, actor);
            return;
        }
        if (executing.ContainsKey(key)) return;
        executing[key] = request;
        StartCoroutine(ExecuteWhenArrived(request, actor, key));
    }

    private IEnumerator ExecuteWhenArrived(Command request, int actor, string key)
    {
        var reply = new Reply { result = new MultiplayerActionResult { context = request.context, outcome = MultiplayerActionOutcome.Unavailable }, value = request.value };
        float until = Time.unscaledTime + 0.4f;
        while (true)
        {
            var outcome = Validate(request, actor, out var player, out var group);
            if (outcome != MultiplayerActionOutcome.Success) { reply.result.outcome = outcome; break; }
            if (!TryArrivalPoint(request, group, out var point, out float radius) || Near(player, point, radius))
            {
                bool success = request.operation.StartsWith("cleanup_", StringComparison.Ordinal) ? ExecuteCleanup(request, actor, player)
                    : group != null && Execute(request, actor, player, group, reply);
                if (success) reply.result.outcome = MultiplayerActionOutcome.Success;
                break;
            }
            if (Time.unscaledTime >= until) { reply.result.outcome = MultiplayerActionOutcome.Unreachable; break; }
            yield return null;
        }
        executing.Remove(key);
        if (!request.context.MatchesSession(session, actor)) yield break;
        if (!reply.accepted) ReleaseFailedAcquisition(request, actor);
        receipts[key] = reply;
        Publish();
        reply.result.revision = revision;
        SendReply(reply, actor);
    }

    private void SendReply(Reply reply, int actor)
    {
        if (actor == session.LocalActorNumber) Receive(reply);
        else MultiplayerWire.Raise(ReplyEvent, JsonUtility.ToJson(reply), new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }

    private static bool IsPayment(string operation) => operation == "money_pickup" || operation == "card_open"
        || operation == "cash_open" || operation == "cash_confirm" || operation == "card_confirm" || operation == "payment_cancel";

    private static string ExpectedStage(string operation) => operation switch
    {
        "money_pickup" or "card_open" => "payment.available",
        "cash_open" => "payment.carried",
        "cash_confirm" => "payment.cash-register",
        "card_confirm" => "payment.card-terminal",
        "payment_cancel" => "payment.reserved",
        "cleanup_pickup" => "cleanup.available",
        "cleanup_wash" => "cleanup.carried",
        "takeout_payment" => "takeout.waiting-payment",
        _ => "service." + operation
    };

    private MultiplayerActionOutcome Validate(Command request, int actor, out GameObject player, out CustomerGroup group)
    {
        player = null; group = Resolve(request.view);
        var context = request.context;
        if (!session.IsAuthority || !context.MatchesSession(session, actor) || GameDayManager.Instance?.ServiceActive != true)
            return MultiplayerActionOutcome.Cancelled;
        if (!session.TryGetManager(actor, out player)) return MultiplayerActionOutcome.Unavailable;
        if (context.expectedStage != ExpectedStage(request.operation) || context.expectedRevision > revision)
            return MultiplayerActionOutcome.StaleStage;
        var claims = session.GetComponent<MultiplayerTaskClaims>();
        if (request.operation.StartsWith("cleanup_", StringComparison.Ordinal))
        {
            if (context.orderNumber != request.value || context.customerView != 0) return MultiplayerActionOutcome.WrongTarget;
            string task = $"Order:{request.value}:Cleanup";
            if (!claims.IsClaimedBy(task, actor)) return MultiplayerActionOutcome.AnotherOwner;
            if (context.lease <= 0 || claims.GetLease(task) != context.lease) return MultiplayerActionOutcome.StaleStage;
            return MultiplayerActionOutcome.Success;
        }
        if (!context.MatchesOrder(group)) return MultiplayerActionOutcome.WrongTarget;
        if (!IsPayment(request.operation)) return MultiplayerActionOutcome.Success;
        if (group.MultiplayerPaymentComplete) return MultiplayerActionOutcome.StaleStage;
        if (!group.IsTakeout)
        {
            string task = $"Customer:{request.view}:Payment";
            if (!claims.IsClaimedBy(task, actor)) return MultiplayerActionOutcome.AnotherOwner;
            if (context.lease <= 0 || claims.GetLease(task) != context.lease) return MultiplayerActionOutcome.StaleStage;
        }
        if (reservations.TryGetValue(request.view, out var reservation))
        {
            if (reservation.actor != actor) return MultiplayerActionOutcome.AnotherOwner;
            if (reservation.order != context.orderNumber || reservation.lease != context.lease) return MultiplayerActionOutcome.StaleStage;
        }
        else if (request.operation == "cash_confirm" || request.operation == "card_confirm" || request.operation == "payment_cancel")
            return MultiplayerActionOutcome.StaleStage;
        return MultiplayerActionOutcome.Success;
    }

    private bool TryArrivalPoint(Command request, CustomerGroup group, out Vector3 point, out float radius)
    {
        Transform stand = null;
        radius = 2f;
        switch (request.operation)
        {
            case "money_pickup": case "card_open": case "card_confirm":
                var money = group != null ? Money(group) : null;
                if (money != null) { stand = money.StandPoint; radius = money.GetInteractRadius(); }
                break;
            case "cash_open": case "cash_confirm": case "takeout_payment":
                stand = FindFirstObjectByType<CashierBoothInteractable>()?.StandPoint;
                break;
            case "cleanup_pickup":
                var pickup = FindTray(request.value)?.GetComponent<FoodTrayInteractable>();
                if (pickup != null) { stand = pickup.StandPoint; radius = pickup.GetInteractRadius(); }
                break;
            case "cleanup_wash":
                var sink = FindFirstObjectByType<SinkInteractable>();
                if (sink != null) { stand = sink.StandPoint; radius = sink.GetInteractRadius(); }
                break;
        }
        point = stand != null ? stand.position : Vector3.zero;
        return stand != null;
    }

    private void ReleaseFailedAcquisition(Command request, int actor)
    {
        string task = request.operation == "money_pickup" || request.operation == "card_open" ? $"Customer:{request.view}:Payment"
            : request.operation == "cleanup_pickup" ? $"Order:{request.value}:Cleanup" : null;
        if (task == null || reservations.ContainsKey(request.view)
            || request.operation == "cleanup_pickup" && dirtyOwners.ContainsKey(request.value)) return;
        var claims = session.GetComponent<MultiplayerTaskClaims>();
        if (claims.IsClaimedBy(task, actor) && claims.GetLease(task) == request.context.lease)
            claims.CompleteOnAuthority(task, actor);
    }

    private static bool Reject(Reply reply, MultiplayerActionOutcome outcome, string message = null)
    { reply.result.outcome = outcome; reply.result.message = message; return false; }

    private bool Execute(Command request, int actor, GameObject player, CustomerGroup group, Reply reply)
    {
        var money = Money(group);
        var bag = Bag(group);
        var hands = player.GetComponent<WaiterHands>();
        if (hands == null) return false;
        bool owned = reservations.TryGetValue(request.view, out var reservation) && reservation.actor == actor;
        var cashier = FindFirstObjectByType<CashierBoothInteractable>();
        var flow = TakeoutFlowManager.Instance;
        bool takeoutPayment = flow != null && flow.ActiveGroup == group && flow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForPayment;
        switch (request.operation)
        {
            case "complaint_open": return ManagerComplaintSystem.Instance != null && ManagerComplaintSystem.Instance.OpenNetworkComplaint(group, actor);
            case "complaint_choice": return ManagerComplaintSystem.Instance != null && ManagerComplaintSystem.Instance.ResolveNetworkComplaint(group, actor, request.value);
            case "takeout_order":
                return group.IsTakeout && flow != null && flow.ActiveGroup == group && flow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForOrder
                    && Near(player, group.GetCurrentWorldCenter()) && !RestaurantTaskClaim.IsClaimedByBot(group)
                    && group.TakeOrderFromWaiter(group.chosenFood, group.chosenDrink);
            case "bag_pickup":
                return bag != null && Near(player, bag.transform) && LocalBagFor(actor) == null && bag.NetworkPickup(hands, actor);
            case "bag_deliver":
                return bag != null && Near(player, group.GetCurrentWorldCenter(), 1.4f) && bag.NetworkDeliver(group, actor);
            case "money_pickup":
            case "card_open":
                if (money == null || !money.IsAvailableForCollection) return Reject(reply, MultiplayerActionOutcome.StaleStage);
                if (money.IsCardPayment != (request.operation == "card_open")) return Reject(reply, MultiplayerActionOutcome.WrongTarget);
                if (!Near(player, money.StandPoint, money.GetInteractRadius())) return Reject(reply, MultiplayerActionOutcome.Unreachable);
                if (hands.HasMoney || hands.HasTray || hands.HasBill || hands.HasTicket || LocalBagFor(actor) != null
                    || player.GetComponent<BusserHands>()?.HasTray == true) return Reject(reply, MultiplayerActionOutcome.OccupiedHands);
                if (!Reserve(request.view, actor, group, money)) return Reject(reply, MultiplayerActionOutcome.AnotherOwner);
                if (!money.IsCardPayment) hands.PickupMoney(money);
                if (money.IsCardPayment || hands.HeldMoney == money) return true;
                ReleaseReservation(request.view, false);
                return Reject(reply, MultiplayerActionOutcome.Unavailable);
            case "cash_open":
            case "takeout_payment":
                if (cashier == null) return Reject(reply, MultiplayerActionOutcome.Unavailable);
                if (!Near(player, cashier.StandPoint)) return Reject(reply, MultiplayerActionOutcome.Unreachable);
                if (group.MultiplayerPaymentComplete) return Reject(reply, MultiplayerActionOutcome.StaleStage);
                if (hands.HasTray || hands.HasBill || hands.HasTicket || player.GetComponent<BusserHands>()?.HasTray == true || LocalBagFor(actor) != null)
                    return Reject(reply, MultiplayerActionOutcome.OccupiedHands);
                if (!takeoutPayment && (money == null || money.IsCardPayment || hands.HeldMoney != money)) return Reject(reply, MultiplayerActionOutcome.WrongTarget);
                if (!Reserve(request.view, actor, group, money)) return Reject(reply, MultiplayerActionOutcome.AnotherOwner);
                reply.total = group.GetCurrentOrderTotal();
                reply.amount = takeoutPayment ? TakeoutFlowManager.GetPaymentDenomination(reply.total) : money.Amount;
                reservations[request.view].registerOpen = true;
                return true;
            case "cash_confirm":
            case "card_confirm":
                if (!owned) return Reject(reply, MultiplayerActionOutcome.AnotherOwner);
                if (group.MultiplayerPaymentComplete) return Reject(reply, MultiplayerActionOutcome.StaleStage);
                var paymentRegister = CashierRegisterUI.ResolveInstance();
                if (paymentRegister == null) return Reject(reply, MultiplayerActionOutcome.Unavailable);
                if (request.operation == "cash_confirm")
                {
                    if (!reservation.registerOpen) return Reject(reply, MultiplayerActionOutcome.StaleStage);
                    if (cashier == null || !Near(player, cashier.StandPoint)) return Reject(reply, MultiplayerActionOutcome.Unreachable);
                    int amount = takeoutPayment ? TakeoutFlowManager.GetPaymentDenomination(group.GetCurrentOrderTotal())
                        : hands.HeldMoney == money && money != null ? money.Amount : -1;
                    if (amount < 0) return Reject(reply, MultiplayerActionOutcome.WrongTarget);
                    if (request.value != amount - group.GetCurrentOrderTotal())
                        return Reject(reply, MultiplayerActionOutcome.WrongTarget, "Enter the exact change shown by the payment amount and bill total.");
                }
                else
                {
                    if (money == null || !money.IsCardPayment || !money.IsAvailableForCollection) return Reject(reply, MultiplayerActionOutcome.StaleStage);
                    if (!Near(player, money.StandPoint, money.GetInteractRadius())) return Reject(reply, MultiplayerActionOutcome.Unreachable);
                }
                if (!paymentRegister.CompleteAutomatedPayment(group)) return false;
                reservations.Remove(request.view);
                session.GetComponent<MultiplayerTaskClaims>().CompleteOnAuthority($"Customer:{request.view}:Payment", actor);
                if (hands.holdingMoneyFor == group) hands.ClearMoney();
                else if (money != null) RemoveObservedMoney(money);
                return true;
            case "payment_cancel":
                if (!owned) return false;
                ReleaseReservation(request.view, true);
                return true;
            default: return false;
        }
    }
    private TakeoutBagInteractable LocalBagFor(int actor)
    { foreach (var bag in MultiplayerWorldRegistry.All<TakeoutBagInteractable>()) if (bag.NetworkOwner == actor) return bag; return null; }
    private void ReleaseReservation(int view, bool abandoned)
    {
        if (!reservations.TryGetValue(view, out var reservation)) return;
        reservations.Remove(view);
        var claims = session.GetComponent<MultiplayerTaskClaims>();
        string task = $"Customer:{view}:Payment";
        if (claims.IsClaimedBy(task, reservation.actor) && claims.GetLease(task) == reservation.lease)
            claims.CompleteOnAuthority(task, reservation.actor);
        if (reservation.actor == session.LocalActorNumber)
        {
            CashierRegisterUI.Instance?.CloseNetworkPayment(Resolve(view));
            CardPaymentUI.Instance?.CloseNetworkPayment(Resolve(view));
        }
        if (abandoned && reservation.registerOpen) GameDayManager.Instance?.RegisterCashError();
        var group = Resolve(view);
        if (group == null) return;
        var money = Money(group);
        if (money != null && money.OrderNumber == reservation.order && money.IsPickedUp && !group.MultiplayerPaymentComplete)
        {
            if (money.PresentAtPickup()) group.PresentPaymentBubble(money);
        }
    }
    public void ReleasePaymentClaim(int view) { if (session.IsAuthority) ReleaseReservation(view, false); }

    private void Receive(Reply reply)
    {
        if (reply?.result?.context == null || pending?.context == null || reply.result.context.actor != session.LocalActorNumber
            || !reply.result.context.MatchesAction(pending.context) || reply.value != pending.value
            || reply.result.context.run != session.RunId || reply.result.context.day != GameFlowManager.Instance.CurrentDay) return;
        pending = null;
        var group = Resolve(reply.view);
        if (reply.operation == "card_confirm") CardPaymentResult = reply.accepted;
        if (!reply.accepted)
        {
            WarningSlideUI.Instance?.Show(string.IsNullOrEmpty(reply.result.message) ? MultiplayerActionFeedback.Message(reply.result.outcome) : reply.result.message);
            TrySendQueuedCancellation();
            return;
        }
        localReservationRevision = Math.Max(localReservationRevision, reply.revision);
        if (reply.operation == "cash_open" || reply.operation == "takeout_payment")
        {
            var register = CashierRegisterUI.ResolveInstance();
            if (register != null) register.OpenForPayment(group, reply.amount, reply.total);
            else WarningSlideUI.Instance?.Show("The cash register is unavailable. Select the cashier to try again.");
        }
        else if (reply.operation == "card_open")
        {
            deferredCard = reply; cardOpenUntil = Time.unscaledTime + 8f; TryOpenCard();
        }
        else if (reply.operation == "cash_confirm" || reply.operation == "payment_cancel")
            CashierRegisterUI.Instance?.CloseNetworkPayment(group);
        else if (reply.operation == "money_pickup")
        { deferredPickup = reply; pickupAttachUntil = Time.unscaledTime + 8f; TryFinishPickupPresentation(); }
        if (reply.operation == "cash_confirm" || reply.operation == "card_confirm" || reply.operation == "payment_cancel")
        { cancelAfterPending = null; deferredPickup = null; cashierAfterPickup = false; }
        TrySendQueuedCancellation();
    }

    private void TryFinishPickupPresentation()
    {
        if (deferredPickup == null || pending != null || cancelAfterPending != null) return;
        var context = deferredPickup.result.context;
        var group = Resolve(context.customerView);
        var hands = session.LocalManager?.GetComponent<WaiterHands>();
        if (!session.CanAct || !context.MatchesOrder(group) || group.MultiplayerPaymentComplete)
        { deferredPickup = null; cashierAfterPickup = false; return; }
        if (hands != null && hands.HeldMoney != null && hands.HeldMoney.TargetGroup == group && hands.HeldMoney.OrderNumber == context.orderNumber)
        {
            bool openCashier = cashierAfterPickup;
            deferredPickup = null; cashierAfterPickup = false;
            if (openCashier) Send("cash_open", group);
            else WarningSlideUI.Instance?.Show("Payment collected. Take it to the cashier.");
            return;
        }
        if (Time.unscaledTime >= pickupAttachUntil)
        {
            deferredPickup = null; cashierAfterPickup = false;
            appliedObjects = 0; nextObservedApply = 0;
            WarningSlideUI.Instance?.Show("Payment is still synchronizing. Select the cashier when it appears in your hands.");
        }
    }

    private void TrySendQueuedCancellation()
    {
        if (cancelAfterPending == null || pending != null) return;
        var group = cancelAfterPending;
        cancelAfterPending = null;
        deferredCard = null;
        if (!group.MultiplayerPaymentComplete) Send("payment_cancel", group);
    }
    private void TryOpenCard()
    {
        if (deferredCard == null) return;
        if (cancelAfterPending != null) { TrySendQueuedCancellation(); return; }
        var group = Resolve(deferredCard.view);
        var money = group != null ? Money(group) : null;
        var ui = FindFirstObjectByType<CardPaymentUI>(FindObjectsInactive.Include);
        if (session.CanAct && deferredCard.result.context.MatchesOrder(group) && money != null && ui != null && ui.Open(money)) { deferredCard = null; return; }
        if (!session.CanAct || Time.unscaledTime >= cardOpenUntil)
        { deferredCard = null; if (group != null) Send("payment_cancel", group); }
    }
    private static string Holder(Transform item)
    {
        var hands = item != null ? item.GetComponentInParent<WaiterHands>(true) : null;
        if (hands == null) return string.Empty;
        return MultiplayerWorldRegistry.HolderId(hands);
    }
    private WaiterHands ResolveHolder(string holder)
    {
        return MultiplayerWorldRegistry.ResolveHolder(holder);
    }
    private static void RemoveObservedMoney(MoneyPickup money)
    {
        if (money == null) return;
        money.GetComponentInParent<WaiterHands>(true)?.DetachMoneyPickup(money);
        money.TargetGroup?.assignedBooth?.GetComponent<BoothMoneySpawner>()?.ForgetObservedMoney(money);
        money.gameObject.SetActive(false);
        Destroy(money.gameObject);
    }
    private void Publish()
    {
        if (!session.IsAuthority) return;
        nextPublish = Time.unscaledTime + 0.3f;
        var flow = TakeoutFlowManager.Instance;
        var snapshot = new Snapshot { run = session.RunId, day = GameFlowManager.Instance.CurrentDay,
            takeoutView = ViewId(flow?.ActiveGroup), takeoutPhase = (int)(flow?.CurrentPhase ?? TakeoutFlowManager.TakeoutPhase.None),
            complaint = ManagerComplaintSystem.Instance?.CaptureNetworkState() };
        foreach (var customer in MultiplayerWorldRegistry.All<MultiplayerCustomerSpawn>())
        {
            var group = customer.Group;
            if (group == null) continue;
            var money = Money(group); var bag = Bag(group);
            snapshot.groups.Add(new GroupState { view = customer.photonView.ViewID, phase = (int)group.state,
                queue = (int)group.CurrentTakeoutQueueState, orderNumber = group.currentOrderNumber, order = group.IsTakeout ? group.currentOrder : null,
                submitted = group.IsTakeout ? group.submittedOrder : null, confirmed = group.HasConfirmedOrder, paid = group.MultiplayerPaymentComplete,
                billDelivered = group.HasReceivedBill, hasMoney = money != null, moneyAmount = money != null ? money.Amount : 0,
                card = money != null && money.IsCardPayment, moneyHolder = Holder(money?.transform),
                owner = reservations.TryGetValue(customer.photonView.ViewID, out var owner) ? owner.actor : 0,
                hasBag = bag != null, bagOwner = bag != null ? bag.NetworkOwner : 0, bagHolder = Holder(bag?.transform),
                bagPosition = bag != null ? bag.PickupPosition : Vector3.zero, bagRotation = bag != null ? bag.PickupRotation : Quaternion.identity });
        }
        snapshot.groups.Sort((a, b) => a.view.CompareTo(b.view));
        string payload = JsonUtility.ToJson(snapshot);
        if (payload == previous) return;
        previous = payload; snapshot.revision = ++revision;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [StateKey] = JsonUtility.ToJson(snapshot) });
    }
    private void ReadSnapshot()
    {
        if (PhotonNetwork.CurrentRoom?.CustomProperties[StateKey] is not string json || json.Length > 262144 || json == lastReadPayload) return;
        try
        {
            var state = JsonUtility.FromJson<Snapshot>(json);
            if (state?.groups == null || state.run != session.RunId || state.day != GameFlowManager.Instance.CurrentDay || state.revision <= received) return;
            received = state.revision; observed = state; lastReadPayload = json;
        }
        catch (ArgumentException) { Debug.LogWarning("[Multiplayer] Invalid service snapshot."); }
    }
    private void ApplyObserved()
    {
        if (observed == null || observed.day != GameFlowManager.Instance.CurrentDay || observed.revision <= appliedObjects) return;
        bool unresolved = false;
        var present = new HashSet<int>();
        foreach (var state in observed.groups)
        {
            if (state == null) continue;
            present.Add(state.view);
            var group = Resolve(state.view);
            if (group == null) { unresolved = true; continue; }
            if (!group.IsTakeout && state.orderNumber != group.currentOrderNumber) { unresolved = true; continue; }
            group.PresentServiceState(state);
            if (state.owner != session.LocalActorNumber && !state.paid && observed.revision >= localReservationRevision)
            {
                CashierRegisterUI.Instance?.CloseNetworkPayment(group);
                CardPaymentUI.Instance?.CloseNetworkPayment(group);
            }
            observedMoney.TryGetValue(state.view, out var money);
            if (money != null && (money.TargetGroup != group || money.OrderNumber != state.orderNumber))
            { RemoveObservedMoney(money); observedMoney.Remove(state.view); money = null; }
            if (!state.hasMoney || state.paid || group.MultiplayerPaymentComplete)
            {
                RemoveObservedMoney(money);
                observedMoney.Remove(state.view);
                if (state.paid) CashierRegisterUI.Instance?.CloseNetworkPayment(group);
            }
            else
            {
                var holder = ResolveHolder(state.moneyHolder);
                if (!string.IsNullOrEmpty(state.moneyHolder) && holder == null) { unresolved = true; continue; }
                if (money == null && group.assignedBooth != null)
                {
                    money = group.assignedBooth.GetComponent<BoothMoneySpawner>()?.SpawnMoney(group, state.moneyAmount, group.assignedBooth.approachPoint, state.card);
                    observedMoney[state.view] = money;
                }
                if (money != null)
                {
                    if (holder != null) holder.PresentMoneyPickup(money);
                    else if (money.PresentAtPickup()) group.PresentPaymentBubble(money);
                    else unresolved = true;
                }
                else unresolved = true;
            }
            observedBags.TryGetValue(state.view, out var bag);
            if (!state.hasBag)
            { if (bag != null) Destroy(bag.gameObject); observedBags.Remove(state.view); }
            else
            {
                var holder = ResolveHolder(state.bagHolder);
                if (!string.IsNullOrEmpty(state.bagHolder) && holder == null) { unresolved = true; continue; }
                if (bag == null) { bag = MultiplayerWorldRegistry.Kitchen?.PresentTakeoutBag(group, state.bagPosition, state.bagRotation); observedBags[state.view] = bag; }
                bag?.PresentNetworkOwner(holder, state.bagOwner, state.bagPosition, state.bagRotation);
                if (bag == null) unresolved = true;
            }
        }
        foreach (int key in new List<int>(observedMoney.Keys)) if (!present.Contains(key)) { RemoveObservedMoney(observedMoney[key]); observedMoney.Remove(key); }
        foreach (int key in new List<int>(observedBags.Keys)) if (!present.Contains(key)) { if (observedBags[key] != null) Destroy(observedBags[key].gameObject); observedBags.Remove(key); }
        TakeoutFlowManager.Instance?.PresentNetworkState(Resolve(observed.takeoutView), observed.takeoutPhase);
        ManagerComplaintSystem.EnsureInstance()?.ApplyNetworkState(observed.complaint);
        if (observed.takeoutView > 0 && Resolve(observed.takeoutView) == null) unresolved = true;
        if (!unresolved) appliedObjects = observed.revision;
    }
    public static bool CanUseTakeout(CustomerGroup group)
    {
        if (Active == null || !Active.session.CanAct || group == null || !group.IsTakeout) return false;
        var flow = TakeoutFlowManager.Instance;
        return Active.LocalBag?.TargetGroup == group || flow != null && flow.ActiveGroup == group
            && (flow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForOrder || flow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForPayment);
    }
    public static void UseTakeout(CustomerGroup group)
    {
        if (!CanUseTakeout(group)) return;
        if (Active.LocalBag?.TargetGroup == group) { Send("bag_deliver", group); return; }
        if (TakeoutFlowManager.Instance.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForOrder) Send("takeout_order", group);
        else
        {
            var cashier = FindFirstObjectByType<CashierBoothInteractable>();
            if (cashier != null) Approach("takeout_payment", group, cashier.StandPoint, 1.5f);
        }
    }
    public static void ApproachTakeout(CustomerGroup group)
    {
        if (!CanUseTakeout(group)) return;
        var point = group.GetComponent<TakeoutCustomerInteractable>()?.StandPoint;
        var mover = Active.session.LocalManager?.GetComponent<PlayerMovement>();
        if (point == null || mover == null) return;
        point.position = group.GetCurrentWorldCenter();
        mover.UI_MoveToAction(point, 1.4f, () => UseTakeout(group), () => { });
    }
    public void OnEvent(EventData ev)
    {
        if ((ev.Code != RequestEvent && ev.Code != ReplyEvent) || !MultiplayerWire.TryRead(ev, out var payload)
            || payload is not string json || json.Length > 4096) return;
        try
        {
            if (ev.Code == RequestEvent && session.IsAuthority) Handle(JsonUtility.FromJson<Command>(json), ev.Sender);
            else if (ev.Code == ReplyEvent && ev.Sender == session.Run?.hostActor) Receive(JsonUtility.FromJson<Reply>(json));
        }
        catch (ArgumentException) { Debug.LogWarning("[Multiplayer] Invalid service request."); }
    }
}
