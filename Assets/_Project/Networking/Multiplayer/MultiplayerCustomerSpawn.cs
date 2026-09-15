using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

// Network wrapper around the real group/members, not a second customer state machine.
public partial class MultiplayerCustomerSpawn : MonoBehaviourPun, IPunInstantiateMagicCallback, IPunObservable
{
    [SerializeField] private CustomerGroup groupPrefab;
    [SerializeField] private CustomerAgent greenPrefab;
    [SerializeField] private CustomerAgent pinkPrefab;
    [SerializeField] private CustomerAgent bluePrefab;
    public CustomerGroup Group { get; private set; }
    public bool ReadyForInteraction { get; private set; }
    private Vector3[] positions;
    private Quaternion[] rotations;
    private MultiplayerPoseBuffer[] poseHistory;
    private bool receivedPose;
    private bool simulating;
    private LobbyLineManager interactionLine;
    private MultiplayerSessionManager session;
    private Animator[] animators;
    private float[] speeds;
    private float[] playbackSpeeds;
    private bool[] sitting;
    private static readonly int SittingParameter = Animator.StringToHash("IsSitting");
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private float nextSnapshot;
    public enum BillStage { None, Printing, Printed, Carried, Complete, PaymentAvailable, Paid }
    public BillStage CurrentBillStage { get; private set; }
    public int BillOwnerActor { get; private set; }
    private int billSnapshotOrder;
    private Vector3 billPosition;
    private Quaternion billRotation;

    private bool serviceStarted;
    private bool paidDepartureStarted, paidDepartureFinished, despawnRequested;

    internal void NotifyPaidDepartureFinished(CustomerGroup group)
    {
        if (simulating && session != null && session.IsAuthority && paidDepartureStarted
            && group == Group && group.MultiplayerPaymentComplete && group.state == CustomerGroup.GroupState.Leaving)
            paidDepartureFinished = true;
    }
    private bool eatingStarted, eatingComplete;
    public double EatingStartedAt { get; private set; }
    public float EatingDuration { get; private set; }

    private void LateUpdate()
    {
        if (!simulating && session != null && session.IsMultiplayerSession && Group != null
            && (Group.state == CustomerGroup.GroupState.Eating || Group.state == CustomerGroup.GroupState.NeedsBill) && eatingStarted)
            Group.PresentObservedEating(!eatingComplete && Group.state == CustomerGroup.GroupState.Eating);
    }

    private void ReceiveEating(double startedAt, float duration, bool complete, PhotonMessageInfo info)
    {
        if (simulating || session == null || !session.IsMultiplayerSession || Group == null
            || info.Sender != PhotonNetwork.MasterClient
            || (phase != CustomerGroup.GroupState.Eating && phase != CustomerGroup.GroupState.NeedsBill)) return;
        eatingStarted = true;
        EatingStartedAt = startedAt;
        EatingDuration = duration;
        eatingComplete |= complete;
    }
    private CustomerGroup.GroupState phase;
    public void PresentTakeoutPhase(int value)
    { if (!simulating && Group != null && Group.IsTakeout) phase = (CustomerGroup.GroupState)value; }
    private Vector3 queueDestination;
    private float patience;
    private bool showPatience;
    private string generatedOrderJson;
    public bool HasGeneratedOrder => generatedOrderJson != null;
    public int CarrierActorNumber { get; private set; }
    public bool CarrierNeedsRecovery { get; private set; }

    public bool HasLocalDeliveryGuidance
    {
        get
        {
            if (!isActiveAndEnabled || session == null || !session.IsMultiplayerSession
                || CarrierActorNumber <= 0 || CarrierActorNumber != session.LocalActorNumber
                || CarrierNeedsRecovery || Group == null || !Group.gameObject.activeInHierarchy || Group.IsTakeout
                || Group.state != CustomerGroup.GroupState.OrderTaken || Group.assignedBooth == null)
                return false;
            var manager = session.LocalManager;
            var hands = manager != null ? manager.GetComponent<WaiterHands>() : null;
            var tray = hands != null ? hands.holdingTray : null;
            return tray != null && tray.gameObject.activeInHierarchy && tray.TargetGroup == Group
                && tray.orderNumber == Group.currentOrderNumber;
        }
    }

    private void OnDisable()
    {
        if (Group != null) Group.ProjectLocalDeliveryGuidance(false);
    }

    public bool CommitTrayPickup(int actor, WaiterHands hands)
    {
        if (!simulating || session == null || !session.IsAuthority || CarrierActorNumber != 0 || Group == null) return false;
        var kitchen = MultiplayerWorldRegistry.Kitchen;
        if (kitchen == null || !kitchen.CarryPreparedTray(Group, hands)) return false;
        CarrierActorNumber = actor;
        PublishAssignment();
        return true;
    }

    public bool CommitServe(WaiterHands hands, FoodTray tray, Transform drop)
    {
        if (!simulating || session == null || !session.IsAuthority || Group == null
            || Group.state != CustomerGroup.GroupState.OrderTaken || CarrierActorNumber == 0
            || CarrierNeedsRecovery || hands == null || hands.holdingTray != tray || tray == null || drop == null) return false;
        // Close ownership before the existing hand-change notification can re-enter interaction.
        int carrier = CarrierActorNumber;
        CarrierActorNumber = 0;
        tray.NetworkCarryLocked = false;
        bool released = hands.TryDeliverTrayTo(Group, destroyTrayObject: false);
        tray.NetworkCarryLocked = true;
        if (!released) { CarrierActorNumber = carrier; return false; }
        CarrierNeedsRecovery = false;
        PlaceServedTray(tray, drop);
        tray.GetComponent<FoodTrayInteractable>()?.NotifyDeliveredToTable();
        Group.PauseBeforeEating = true;
        Group.ReceiveFoodFromWaiter(tray.DeliveredContents, tray);
        PublishAssignment();
        return true;
    }

    private static void PlaceServedTray(FoodTray tray, Transform drop)
    {
        WaiterHands.AttachKeepingWorldScale(tray.transform, drop, Vector3.zero, Quaternion.identity);
        tray.gameObject.SetActive(true);
        WaiterHands.SetAllColliders(tray.gameObject, true);
    }



    private void PresentCarrier()
    {
        if (CarrierActorNumber == 0 || Group == null || session == null || !session.IsConnected || session.Ended) return;
        bool found = session.TryGetManager(CarrierActorNumber, out var manager);
        if (simulating && !session.ValidActor(CarrierActorNumber)) CarrierNeedsRecovery = true;
        if (simulating && CarrierNeedsRecovery && MultiplayerWorldRegistry.Kitchen?.ReturnDisconnectedTray(Group) == true)
        { CarrierActorNumber = 0; CarrierNeedsRecovery = false; PublishAssignment(); return; }
        var hands = found && !CarrierNeedsRecovery ? manager.GetComponent<WaiterHands>() : null;
        if (hands != null && hands.holdingTray != null && hands.holdingTray.TargetGroup == Group
            && hands.holdingTray.orderNumber == Group.currentOrderNumber) return;
        MultiplayerWorldRegistry.Kitchen?.PresentCarriedTray(Group, hands);
    }
    public int ReviewActor { get; set; }
    private int generatedOrderNumber;
    private int[] generatedLegacyChoices;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        MultiplayerWorldRegistry.Track(this);
        session = MultiplayerSessionManager.Instance;
        // Authority must receive greet requests even before it interacts locally.
        if (session != null && session.GetComponent<MultiplayerCustomerInteractionBridge>() == null)
            session.gameObject.AddComponent<MultiplayerCustomerInteractionBridge>();
        var bridge = session != null ? session.GetComponent<MultiplayerCustomerSpawnBridge>() : null;
        Group = bridge != null ? bridge.PendingGroup : null;
        simulating = Group != null && session.IsAuthority;
        if (simulating) interactionLine = FindFirstObjectByType<LobbyLineManager>();
        var data = photonView.InstantiationData;
        var type = (CustomerGroup.CustomerType)(int)data[0];
        int size = (int)data[1];
        if (!simulating)
        {
            Group = Instantiate(groupPrefab, transform.position, transform.rotation, transform);
            Group.members.Clear();
            Group.SetCustomerType(type);
            Group.SetServiceType((bool)data[2] ? CustomerGroup.ServiceType.Takeout : CustomerGroup.ServiceType.DineIn);
            CustomerAgent prefab = type == CustomerGroup.CustomerType.Pink ? pinkPrefab
                : type == CustomerGroup.CustomerType.Blue ? bluePrefab : greenPrefab;
            if (prefab == null) prefab = greenPrefab;
            for (int i = 0; i < size; i++)
            {
                var member = Instantiate(prefab,
                    transform.position + new Vector3(i % 2 * 0.6f, 0, i / 2 * 0.6f),
                    Quaternion.identity, Group.transform);
                Group.members.Add(member);
            }
            // Observation only: no local navigation, timers, interaction, or customer scripts.
            foreach (var behaviour in Group.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var agent in Group.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
            foreach (var collider in Group.GetComponentsInChildren<Collider>(true))
            { collider.isTrigger = true; collider.enabled = true; }
            Group.IsNetworkObserver = true;
        }
        else Group.transform.SetParent(transform, true);
        Group.name = $"Group_{type}_{size}_View{photonView.ViewID}";
        positions = new Vector3[size];
        rotations = new Quaternion[size];
        animators = new Animator[size];
        speeds = new float[size];
        playbackSpeeds = new float[size];
        sitting = new bool[size];
        poseHistory = new MultiplayerPoseBuffer[size];
        for (int i = 0; i < size; i++)
        {
            poseHistory[i] = new MultiplayerPoseBuffer();
            // Use the same Animator that CustomerAgent actually drives.
            animators[i] = Group.members[i].MovementAnimator;
            if (!simulating && animators[i] != null) animators[i].enabled = true;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            bool valid = simulating && session != null && session.IsAuthority && Group != null;
            stream.SendNext(valid);
            if (!valid) return;
            for (int i = 0; i < positions.Length; i++)
            {
                stream.SendNext(Group.members[i].transform.position);
                stream.SendNext(Group.members[i].transform.rotation);
                stream.SendNext(animators[i] != null ? animators[i].GetFloat(SpeedParameter) : 0f);
                // The authority's procedural driver varies playback rate per customer.
                stream.SendNext(animators[i] != null ? animators[i].speed : 1f);
                stream.SendNext(animators[i] != null && animators[i].GetBool(SittingParameter));
            }
        }
        else
        {
            if (!(bool)stream.ReceiveNext()) return;
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = (Vector3)stream.ReceiveNext();
                rotations[i] = (Quaternion)stream.ReceiveNext();
                speeds[i] = (float)stream.ReceiveNext();
                playbackSpeeds[i] = (float)stream.ReceiveNext();
                sitting[i] = (bool)stream.ReceiveNext();
                poseHistory[i].Add(info.SentServerTime, positions[i], rotations[i]);
            }
            receivedPose = true;
        }
    }

    private void Update()
    {
        if (simulating) PresentCarrier();
        ApplyPendingServiceState();
        Group?.ReconcileMultiplayerBubbles(this);
        if (Group != null && billSnapshotOrder == Group.currentOrderNumber && session != null && session.IsMultiplayerSession)
            session.GetComponent<MultiplayerCustomerInteractionBridge>()?.ProjectBillState(
                this, CurrentBillStage, BillOwnerActor, billPosition, billRotation);
        if (Group != null) Group.ProjectLocalDeliveryGuidance(HasLocalDeliveryGuidance);
        if (simulating)
        {
            if (session == null || !session.IsAuthority) return;
            if (Group == null)
            {
                ReadyForInteraction = false;
                if (!despawnRequested)
                {
                    despawnRequested = true;
                    PhotonNetwork.Destroy(gameObject);
                }
                return;
            }
            if (!paidDepartureStarted && Group.TryStartMultiplayerDeparture())
            {
                paidDepartureStarted = true;
                PublishAssignment();
            }
            if (!IsPreService(Group.state)) serviceStarted = true;
            if (Group.state == CustomerGroup.GroupState.Eating && Group.PauseBeforeEating)
                Group.ResumePausedEating();
            if (Group.MultiplayerEatingComplete) Group.ResumePausedNeedsBill();
            if (Group.IsPlayerReviewingOrder && session.GetComponent<MultiplayerTaskClaims>()
                .GetOwner($"Customer:{photonView.ViewID}:Order") != ReviewActor)
            {
                Group.EndPlayerOrderReview();
                ReviewActor = 0;
                PublishAssignment();
            }
            if (Group.PauseAfterSeating && Group.state == CustomerGroup.GroupState.Seated)
                Group.ResumePausedOrderReadiness();
            if (Group.state == CustomerGroup.GroupState.ReadyToOrder && Group.PauseAfterSeating) Group.GeneratePausedOrderOnce();
            if (Group.currentOrderNumber >= 0 && Group.currentOrder != null && Group.currentOrder.lines.Count > 0
                && (generatedOrderJson == null || generatedOrderNumber != Group.currentOrderNumber))
            {
                generatedOrderJson = JsonUtility.ToJson(Group.currentOrder);
                generatedOrderNumber = Group.currentOrderNumber;
                generatedLegacyChoices = new[] { (int)Group.chosenFood, (int)Group.chosenDrink,
                    (int)Group.confirmedFood, (int)Group.confirmedDrink };
            }
            ReadyForInteraction = !serviceStarted && Group.state == CustomerGroup.GroupState.Waiting
                && interactionLine != null && interactionLine.GetFrontOfLine() == Group && Group.CanBeGreeted();
            if (Group.HasBeenAssigned && (Time.unscaledTime >= nextSnapshot || phase != Group.state))
            {
                phase = Group.state;
                nextSnapshot = Time.unscaledTime + 0.1f;
                PublishAssignment();
            }
            if (!serviceStarted && (Time.unscaledTime >= nextSnapshot || phase != Group.state))
            {
                phase = Group.state;
                nextSnapshot = Time.unscaledTime + 0.1f;
                PublishServiceState();
            }
        }
        if (simulating || !receivedPose || Group == null) return;
        for (int i = 0; i < positions.Length; i++)
        {
            var member = Group.members[i].transform;
            if (poseHistory[i].Read(PhotonNetwork.Time - 0.1d, out var position, out var rotation))
                member.SetPositionAndRotation(position, rotation);
            if (animators[i] != null)
            {
                animators[i].SetFloat(SpeedParameter, speeds[i]);
                animators[i].speed = playbackSpeeds[i];
                animators[i].SetBool(SittingParameter, sitting[i]);
            }
        }
        Group.PresentObservedQueue(phase, queueDestination, patience, showPatience);
    }

    public void FinishClosing()
    {
        if (session == null || !session.IsAuthority) return;
        Group?.FinishMultiplayerClosing();
        PhotonNetwork.Destroy(gameObject);
    }

    private static bool IsPreService(CustomerGroup.GroupState value) =>
        value == CustomerGroup.GroupState.Spawning || value == CustomerGroup.GroupState.WalkingToLobby
        || value == CustomerGroup.GroupState.Waiting || value == CustomerGroup.GroupState.AngryLeft
        || value == CustomerGroup.GroupState.UnhappyLeft || value == CustomerGroup.GroupState.Leaving;

    public void PublishGreeted() => PublishServiceState();
    public void PublishAssignment() => PublishServiceState();





    private void ReceiveKitchenEntry(int orderNumber, float startedAt, float preparationDelay,
        float cookDuration, float predictedReadyAt, bool isPaused, bool awaitingSpawn, int state,
        float authorityNow, double sentAt, PhotonMessageInfo info)
    {
        if (session == null || !session.IsMultiplayerSession || simulating
            || info.Sender != PhotonNetwork.MasterClient || Group == null || !Group.IsNetworkObserver
            || !HasGeneratedOrder || Group.state != CustomerGroup.GroupState.OrderTaken
            || !Group.HasConfirmedOrder || Group.currentOrderNumber != orderNumber) return;
        var kitchen = MultiplayerWorldRegistry.Kitchen;
        // Translate the authority's Unity clock into this client's clock, including transit time.
        float clockOffset = Time.time - authorityNow
            - (float)System.Math.Max(0d, PhotonNetwork.Time - sentAt) * Time.timeScale;
        if (kitchen != null) kitchen.PresentObservedAcceptedOrder(Group, orderNumber,
            startedAt + clockOffset, preparationDelay, cookDuration, predictedReadyAt + clockOffset,
            isPaused, awaitingSpawn, (KitchenManager.ForecastState)state);
    }

    private void ReceiveGeneratedOrder(string json, int orderNumber, int[] legacyChoices, PhotonMessageInfo info)
    {
        if (session == null || !session.IsMultiplayerSession || simulating || Group == null
            || !Group.IsNetworkObserver || info.Sender != PhotonNetwork.MasterClient
            || (generatedOrderJson != null && generatedOrderNumber == orderNumber) || string.IsNullOrEmpty(json)
            || legacyChoices == null || legacyChoices.Length != 4) return;
        var order = JsonUtility.FromJson<CustomerGroup.SimpleOrder>(json);
        if (order == null || order.lines == null || order.productIds == null || order.contents == null) return;
        // Preserve the authority's exact snapshot. Existing MenuCatalog ID lookups resolve assets
        // when needed; resolving here would replace authoritative prices with local prices.
        Group.currentOrder = order;
        Group.ResetObservedOrder(orderNumber);
        eatingStarted = false; eatingComplete = false;
        Group.currentOrderNumber = orderNumber;
        Group.chosenFood = (CustomerGroup.FoodType)legacyChoices[0];
        Group.chosenDrink = (CustomerGroup.DrinkType)legacyChoices[1];
        Group.confirmedFood = (CustomerGroup.FoodType)legacyChoices[2];
        Group.confirmedDrink = (CustomerGroup.DrinkType)legacyChoices[3];
        generatedOrderJson = json;
        generatedOrderNumber = orderNumber;
        generatedLegacyChoices = legacyChoices;
        ApplyObservedConfirmation();
        MultiplayerCustomerInteractionBridge.ObserveReview(Group);
    }

    private void ReceiveAssignment(string boothId, int value, bool reviewing, int reviewActor,
        string thought, int mood, int result, bool complaintPending, bool paid, PhotonMessageInfo info)
    {
        if (session == null || !session.IsMultiplayerSession || simulating
            || info.Sender != PhotonNetwork.MasterClient || Group == null) return;
        var assignedPhase = (CustomerGroup.GroupState)value;
        if (assignedPhase != CustomerGroup.GroupState.WalkingToBooth && assignedPhase != CustomerGroup.GroupState.Seated
            && assignedPhase != CustomerGroup.GroupState.WaitingToOrder && assignedPhase != CustomerGroup.GroupState.ReadyToOrder
            && assignedPhase != CustomerGroup.GroupState.OrderTaken && assignedPhase != CustomerGroup.GroupState.Eating
            && assignedPhase != CustomerGroup.GroupState.NeedsBill
            && assignedPhase != CustomerGroup.GroupState.AngryLeft && assignedPhase != CustomerGroup.GroupState.UnhappyLeft
            && assignedPhase != CustomerGroup.GroupState.Leaving) return;
        var booth = MultiplayerCustomerInteractionBridge.ResolveBooth(boothId);
        if (booth == null) return;
        Group.PresentObservedOutcome(thought, mood, result, complaintPending);
        phase = assignedPhase;
        ReadyForInteraction = false;
        showPatience = false;
        if (!Group.hasBeenGreeted) Group.MarkGreeted();
        Group.PresentObservedAssignment(booth, phase);
        Group.PresentObservedPayment(paid);
        ReviewActor = reviewing ? reviewActor : 0;
        if (reviewing) Group.BeginPlayerOrderReview();
        else Group.EndPlayerOrderReview();
        ApplyObservedConfirmation();
        MultiplayerCustomerInteractionBridge.ObserveReview(Group);
    }

    private void ApplyObservedConfirmation()
    {
        if (simulating || Group == null || !Group.IsNetworkObserver || (phase != CustomerGroup.GroupState.OrderTaken
            && phase != CustomerGroup.GroupState.Eating && phase != CustomerGroup.GroupState.NeedsBill)) return;
        Group.ClearOrderBubble();
        Group.EndPlayerOrderReview();
        ReviewActor = 0;
        if (!HasGeneratedOrder || Group.HasConfirmedOrder) return;
        Group.submittedOrder = JsonUtility.FromJson<CustomerGroup.SimpleOrder>(generatedOrderJson);
        Group.ConfirmOrder(Group.chosenFood, Group.chosenDrink);
    }

    private void ReceiveQueue(int value, Vector3 destination, float progress, bool visible, bool ready, bool greeted,
        string thought, int mood, int result, bool complaintPending, PhotonMessageInfo info)
    {
        if (session == null || !session.IsMultiplayerSession || info.Sender != PhotonNetwork.MasterClient || simulating) return;
        if (Group != null && Group.HasBeenAssigned) return;
        var receivedPhase = (CustomerGroup.GroupState)value;
        if (!IsPreService(receivedPhase)) return;
        Group?.PresentObservedOutcome(thought, mood, result, complaintPending);
        phase = receivedPhase;
        queueDestination = destination;
        patience = Mathf.Clamp01(progress);
        showPatience = visible;
        ReadyForInteraction = ready;
        if (Group != null && greeted && !Group.hasBeenGreeted)
        {
            Group.MarkGreeted();
            MultiplayerCustomerInteractionBridge.RefreshOwnedPopup(Group);
        }
    }
}
