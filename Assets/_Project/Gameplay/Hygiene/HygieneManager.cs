using System;
using Photon.Pun;
using ExitGames.Client.Photon;
using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed partial class HygieneManager : MonoBehaviourPunCallbacks, Photon.Realtime.IOnEventCallback
{
    private const string StateKey = "restaurant.hygiene.v5";
    public static HygieneManager Instance { get; private set; }
    public static bool DecisionPaused => Instance != null && Instance.State.decisionOpen;
    private static int decisionClosedFrame = -1;
    public static bool InputBlocked => DecisionPaused || decisionClosedFrame == Time.frameCount;
    public static bool KitchenPaused => Instance != null && Instance.State.KitchenPaused;
    public static bool HoldNewCooking => Instance != null && (Instance.State.KitchenPaused || Instance.State.forcedPending);
    public static float CookMultiplier => Instance != null ? Instance.State.CookMultiplier : 1f;
    public HygieneState State { get; private set; } = new HygieneState();
    private static HygieneSettings Settings => HygieneSettings.Current;
    private float lobbyDirtMultiplier => Settings.lobbyDirtMultiplier * Settings.dirtSpeed;
    public float WalkingSpacing => Settings.walkingSpacing;
    public bool CanRecordLobbyActivity => CanRecordActivity && !State.LobbyDirtSuppressed;
    private KitchenManager kitchen;
    private HygieneSurfaceRegistry surfaces;
    private HygieneDialogue dialogue;
    private string lastPayload;
    private float nextSync, nextVisual;
    private bool dirty, pauseOwned;
    private float previousScale = 1f;
    private float pauseStarted;
    private static float accumulatedPause;
    private readonly System.Collections.Generic.Queue<Action> deferredActions = new();
    // Network recovery still runs in real time; service-action deadlines exclude staff decisions.
    public static float ServiceTime => Time.unscaledTime - accumulatedPause
        - (Instance != null && Instance.pauseOwned ? Time.unscaledTime - Instance.pauseStarted : 0f);
    public float CleaningProgress => Authority ? State.Progress : Mathf.Clamp01(1f -
        (State.cleaningRemaining - (float)Math.Max(0d, PhotonNetwork.Time - State.sentAt) * State.clockScale)
        / Mathf.Max(.01f, State.cleaningDuration));
    public static bool Defer(Action action)
    {
        if (!DecisionPaused) return false;
        if (Instance.deferredActions.Count < 256) Instance.deferredActions.Enqueue(action);
        return true; // Bounded queue. Existing reliable action retries handle saturation.
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; accumulatedPause = 0f; decisionClosedFrame = -1; }
    private UnityEngine.Object helpTarget;
    private PlayerMovement helpMover;
    private float helpStarted;
    private bool sceneReady;
    private bool dialogueReady;
    private KitchenCleaningMode lastPresentedMode;
    private sealed class CustomerActivity
    {
        public float nextSample;
        public CustomerGroup.GroupState phase;
        public bool initialized;
    }
    private readonly System.Collections.Generic.Dictionary<CustomerGroup, CustomerActivity> customerActivity = new();
    private float floorRetryAfter;
    public float LobbyCleaningProgress => State.floorRouteTotal > 0
        ? Mathf.Clamp01(Mathf.Min((float)State.floorRouteDone / State.floorRouteTotal,
            State.floorWorkSeconds / HygieneCleaningRoute.WorkSeconds)) : 1f;
    private bool Authority => !MultiplayerDayBridge.IsActive || MultiplayerSessionManager.Instance.IsAuthority;
    private string Run => MultiplayerDayBridge.IsActive ? MultiplayerSessionManager.Instance.RunId : "singleplayer";
    private int Day => GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
    public bool CanRecordActivity => sceneReady && Authority && !State.decisionOpen && GameDayManager.Instance?.ServiceActive == true;

    public static void Ensure(KitchenManager owner)
    {
        if (Instance == null) owner.gameObject.AddComponent<HygieneManager>();
    }
    public void RegisterSurface(HygieneSurface surface) => surfaces?.Register(surface);
    public void RegisterBooth(Booth booth) => surfaces?.Register(booth);
    public void RegisterStation(Component station) => surfaces?.RegisterStation(station);
    public void ResetForShift()
    {
        if (Authority && sceneReady) { ResetState(); Publish(); }
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        kitchen = GetComponent<KitchenManager>();
    }
    private void Start()
    {
        surfaces = new HygieneSurfaceRegistry();
        surfaces.Discover(kitchen);
        foreach (var player in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None)) HygieneWalker.Ensure(player.gameObject);
        foreach (var staff in FindObjectsByType<AutonomousStaffBot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) HygieneWalker.Ensure(staff.gameObject);
        foreach (var customer in FindObjectsByType<CustomerAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None)) HygieneWalker.Ensure(customer.gameObject);
        dialogue = gameObject.AddComponent<HygieneDialogue>();
        dialogueReady = dialogue.Initialize();
        sceneReady = true;
        ResetState();
        if (!Authority) ReadState();
    }
    private void ResetState()
    {
        ReleasePause();
        helpTarget = null; helpMover = null;
        State = new HygieneState { run = Run, day = Day, revision = Authority ? State.revision + 1 : 0 };
        lastPayload = null;
        deferredActions.Clear();
        customerActivity.Clear();
        ResetCleaningRequests();
        dirty = true;
        surfaces?.Refresh(State);
    }
    private void Update()
    {
        if (!sceneReady) return;
        ExpireCleaningRequests();
        if (State.run != Run || State.day != Day) ResetState();
        var session = MultiplayerSessionManager.Instance;
        if (MultiplayerDayBridge.IsActive && (session.Ended || !session.IsConnected))
        {
            CancelLobbyCleaning();
            State.decisionOpen = false;
            ReleasePause();
            dialogue.SetVisible(false);
            deferredActions.Clear();
            return;
        }
        if (Authority)
        {
            while (!State.decisionOpen && deferredActions.Count > 0) deferredActions.Dequeue().Invoke();
            var day = GameDayManager.Instance;
            if (day != null && day.ServiceActive)
            {
                if (State.AdvanceLobbyClock(day.CurrentGameHour)) Changed();
                if (State.Tick(Time.deltaTime)) Changed();
                if (State.forcedPending && kitchen.HygieneActiveCookingCount == 0)
                {
                    State.BeginCleaning(KitchenCleaningMode.Forced);
                    Changed();
                }
                TickHelp();
                TickCleaningRequests();
                // Never cover another decision or interrupt the player's current job.
                if (dialogueReady && !State.decisionOpen && !GameplayUIBlocker.IsBlocked()
                    && Time.timeScale > 0f && ManagerComplaintSystem.Instance?.HasActiveComplaint != true)
                {
                    if (!State.Cleaning && !State.kitchenWarned && State.kitchenDirt >= HygieneState.WarningLevel)
                        OpenDecision(HygieneArea.Kitchen);
                    else if (!State.lobbyWarned && State.lobbyDirt >= HygieneState.WarningLevel && !PlayerBusy())
                        OpenDecision(HygieneArea.Lobby);
                }
            }
            else if (State.decisionOpen || State.Cleaning || State.lobbyAssistance || State.lobbyCleaningRequested)
            {
                State.decisionOpen = false;
                State.cleaningMode = KitchenCleaningMode.None;
                State.lobbyAssistance = false;
                State.lobbyCleaningRequested = false;
                ResetCleaningRequests();
                helpTarget = null; helpMover = null;
                Changed();
            }
            if ((dirty || State.Cleaning) && Time.unscaledTime >= nextSync) Publish();
        }
        else if (Time.unscaledTime >= nextSync)
        {
            nextSync = Time.unscaledTime + 1f;
            ReadState();
        }
        if (Time.unscaledTime >= nextVisual)
        {
            nextVisual = Time.unscaledTime + .2f;
            surfaces.Refresh(State);
            PresentHygieneFeedback();
        }
        if (State.cleaningMode == KitchenCleaningMode.Forced && lastPresentedMode != KitchenCleaningMode.Forced)
            WarningSlideUI.Instance?.Show($"Kitchen cleaning is required. Cooking resumes in {State.cleaningDuration:0.#} seconds.");
        lastPresentedMode = State.cleaningMode;
        dialogue.Present(State, Authority, Choose);
    }
    private void LateUpdate()
    {
        if (State.decisionOpen)
        {
            if (!pauseOwned) { previousScale = Time.timeScale; pauseStarted = Time.unscaledTime; pauseOwned = true; }
            Time.timeScale = 0f;
        }
        else ReleasePause();
    }
    private void ReleasePause()
    {
        if (!pauseOwned) return;
        accumulatedPause += Time.unscaledTime - pauseStarted;
        if (Time.timeScale == 0f) Time.timeScale = previousScale;
        pauseOwned = false;
        decisionClosedFrame = Time.frameCount;
        if (Authority && MultiplayerDayBridge.IsActive)
            MultiplayerSessionManager.Instance.GetComponent<MultiplayerDayBridge>()?.PublishNow();
    }
    private void OpenDecision(HygieneArea area)
    {
        State.decisionOpen = true;
        State.decisionArea = area;
        State.decisionId++;
        if (area == HygieneArea.Kitchen) State.kitchenWarned = true;
        else State.lobbyWarned = true;
        // Pause before publishing the day clock so guests cannot project time through the decision.
        if (!pauseOwned) { previousScale = Time.timeScale; pauseStarted = Time.unscaledTime; pauseOwned = true; }
        Time.timeScale = 0f;
        Changed(); Publish();
        if (MultiplayerDayBridge.IsActive)
            MultiplayerSessionManager.Instance.GetComponent<MultiplayerDayBridge>()?.PublishNow();
    }
    public void Choose(int decisionId, HygieneDecision choice)
    {
        if (!Authority || !State.decisionOpen || State.decisionId != decisionId) return;
        if (State.decisionArea == HygieneArea.Kitchen)
        {
            if (choice == HygieneDecision.CleanNow) State.BeginCleaning(KitchenCleaningMode.Immediate);
            else if (choice == HygieneDecision.CleanWhileCooking) State.BeginCleaning(KitchenCleaningMode.WhileCooking);
            else if (choice == HygieneDecision.ContinueUntilBlocked)
            {
                State.decisionOpen = false;
                State.deferredKitchen = true;
                State.forcedPending = State.kitchenDirt >= 1f;
            }
            else return;
        }
        else
        {
            if (choice != HygieneDecision.HelpClean && choice != HygieneDecision.KeepServing && choice != HygieneDecision.CleanLobby) return;
            State.decisionOpen = false;
            ReleasePause();
            dialogue.SetVisible(false);
            if (choice == HygieneDecision.HelpClean)
            {
                StartHelp();
            }
            if (choice == HygieneDecision.HelpClean || choice == HygieneDecision.CleanLobby) CaptureFloorRoute();
        }
        ReleasePause();
        Changed(); Publish();
    }
    public bool TryGetWorkStation(EmployeeRole role, ref int cursor, Vector3 from, out Transform station, out Vector3 approach)
    {
        station = null; approach = from;
        return CanRecordActivity && surfaces != null && surfaces.TryGetWorkStation(role, ref cursor, from, out station, out approach);
    }
    public void RecordKitchenUse(Component equipment, float amount = -1f)
    {
        if (!CanRecordActivity || State.Cleaning || equipment == null || equipment.gameObject.scene != gameObject.scene) return;
        float value = (amount < 0 ? Settings.dirtPerStationUse : amount) * Settings.kitchenDirtMultiplier * Settings.dirtSpeed;
        if (!surfaces.Use(State, equipment, value) && !surfaces.UseNearest(State, equipment.transform.position, value)) return;
        if (State.deferredKitchen && State.kitchenDirt >= 1f) State.forcedPending = true;
        Changed();
    }
    public void RecordDiningUse(Booth booth, float amount)
    {
        if (!CanRecordLobbyActivity || booth == null || booth.gameObject.scene != gameObject.scene) return;
        surfaces.Register(booth);
        if (surfaces.Use(State, booth, amount * lobbyDirtMultiplier)) Changed();
    }
    public float BoothDirt(Booth booth) => surfaces != null && booth != null ? surfaces.DirtFor(State, booth) : 0f;
    public void CleanDining(Booth booth)
    {
        if (!CanRecordActivity || booth == null) return;
        surfaces.Clean(State, booth);
        if (State.lobbyDirt < .3f) State.lobbyWarned = false;
        Changed();
    }
    public void RecordLobbyIncident(bool dirtySurface, Vector3? location = null)
    {
        if (!Authority || GameDayManager.Instance?.ServiceActive != true) return;
        if (location.HasValue && surfaces != null && surfaces.TryGetFloorPoint(location.Value, out var floor))
        {
            if (dirtySurface) State.AddFloorDirt(floor, Settings.dirtPerLobbyIncident * lobbyDirtMultiplier, spill: true);
            else State.CleanFloor(floor);
        }
        if (State.lobbyDirt < HygieneState.WarningLevel * .5f) State.lobbyWarned = false;
        Changed();
    }
    public void ForgetCustomer(CustomerGroup group) => customerActivity.Remove(group);
    public void RecordCustomerActivity(CustomerGroup group)
    {
        if (!CanRecordActivity
            || group == null || group.members == null || group.gameObject.scene != gameObject.scene) return;
        if (!customerActivity.TryGetValue(group, out var activity))
        {
            activity = new CustomerActivity();
            customerActivity.Add(group, activity);
        }
        if (Time.time < activity.nextSample) return;
        activity.nextSample = Time.time + .5f;
        if (group.assignedBooth != null)
        {
            if (!activity.initialized || activity.phase != group.state)
            {
                float use = group.state == CustomerGroup.GroupState.WaitingToOrder ? Settings.seatingDirt
                    : group.state == CustomerGroup.GroupState.OrderTaken ? Settings.orderDirt
                    : group.state == CustomerGroup.GroupState.Eating ? Settings.mealStartDirt
                    : group.state == CustomerGroup.GroupState.NeedsBill ? Settings.billDirt : 0f;
                if (use > 0f) RecordDiningUse(group.assignedBooth, use);
            }
            if (group.state == CustomerGroup.GroupState.Eating &&
                (!group.PauseBeforeEating || group.MultiplayerEatingStarted && !group.MultiplayerEatingComplete))
            {
                RecordDiningUse(group.assignedBooth, Settings.eatingDirt * Mathf.Max(1, group.members.Count));
                Vector3 approach = group.assignedBooth.GetNavigableApproachPosition();
                if (surfaces.TryGetFloorPoint(approach, out var floor))
                { if (State.AddFloorDirt(floor, Settings.eatingSpillDirt * lobbyDirtMultiplier, spill: true)) Changed(); }
            }
        }
        activity.phase = group.state; activity.initialized = true;
    }
    public bool RecordFootstep(Vector3 point, float yaw, byte color = 0)
    {
        if (!CanRecordLobbyActivity || !surfaces.TryGetFloorPoint(point, out var floor)) return false;
        if (!State.AddFloorDirt(floor, Settings.walkingDirt * Settings.dirtSpeed, yaw, false, color)) return false;
        Changed(); return true;
    }

    public bool TryStartLobbyCleaning(AutonomousStaffBot staff)
    {
        if (!Authority || !State.lobbyCleaningRequested || State.decisionOpen || staff == null || staff.IsBusy
            || staff.GetComponent<BusserHands>()?.HasTray == true || Time.time < floorRetryAfter) return false;
        if (State.floorRoute.Count == 0)
        {
            State.lobbyCleaningRequested = false;
            Changed();
            return false;
        }
        staff.StartTask(CleanLobbyFloor(staff));
        return true;
    }
    public void CancelLobbyCleaning()
    {
        if (!Authority || !State.lobbyCleaningRequested && !State.floorRouteActive) return;
        State.floorRouteActive = State.lobbyCleaningRequested = false;
        State.floorRoute.Clear(); State.ResetRouteLookup(); Changed();
    }
    private System.Collections.IEnumerator CleanLobbyFloor(AutonomousStaffBot staff)
    {
        var cleaningState = State;
        bool Current() => State == cleaningState && State.lobbyCleaningRequested
            && staff != null && staff.isActiveAndEnabled && Authority && GameDayManager.Instance?.ServiceActive == true
            && (!MultiplayerDayBridge.IsActive || !MultiplayerSessionManager.Instance.Ended && MultiplayerSessionManager.Instance.IsConnected);
        if (!Current()) yield break;
        State.floorRouteActive = true; Changed(); Publish();
        var captured = new System.Collections.Generic.HashSet<int>(State.floorRoute);
        bool finished = false;
        try
        {
            while (Current() && State.floorRoute.Count > 0)
            {
                int id = State.floorRoute[0];
                var target = State.floorMarks.Find(mark => mark.id == id);
                if (target == null) { State.floorRoute.RemoveAt(0); State.floorRouteDone++; Changed(); continue; }
                var move = staff.MoveWithin(target.position, .3f, .5f, 4f);
                // Flatten nested movement enumerators so every traveled segment
                // gets swept; yielding MoveWithin directly would skip that work.
                var stack = new System.Collections.Generic.Stack<System.Collections.IEnumerator>();
                stack.Push(move);
                Vector3 previous = staff.transform.position;
                float nextSweep = Time.time;
                try
                {
                    while (Current() && stack.Count > 0)
                    {
                        var step = stack.Peek();
                        if (!step.MoveNext()) { (step as IDisposable)?.Dispose(); stack.Pop(); continue; }
                        if (step.Current is System.Collections.IEnumerator nested) { stack.Push(nested); continue; }
                        yield return step.Current;
                        if (!Current()) break;
                        if (Time.time < nextSweep) continue;
                        nextSweep = Time.time + .1f;
                        Vector3 now = staff.transform.position;
                        if ((now - previous).sqrMagnitude > 9f) previous = now;
                        SweepCapturedFloor(captured, previous, now);
                        previous = now;
                    }
                }
                finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
                if (!Current()) yield break;
                SweepCapturedFloor(captured, staff.transform.position, staff.transform.position);
                if (State.floorRoute.Contains(id))
                {
                    SkipUnreachableMark(target);
                    // Rotate failed targets to the end so one obstruction does
                    // not prevent the rest of the restaurant being cleaned.
                    if (State.floorRoute.Remove(id)) State.floorRoute.Add(id);
                }
                float earned = HygieneCleaningRoute.WorkSeconds * State.floorRouteDone / Mathf.Max(1, State.floorRouteTotal);
                staff.PresentCommittedWork(Mathf.Max(0f, earned - State.floorWorkSeconds));
                while (Current() && State.floorWorkSeconds < earned)
                {
                    State.floorWorkSeconds = Mathf.Min(earned, State.floorWorkSeconds + Time.deltaTime);
                    Changed(); yield return null;
                }
                staff.WorkTiming.Clear();
            }
            finished = Current() && State.floorRoute.Count == 0;
        }
        finally
        {
            if (staff != null) staff.WorkTiming.Clear();
            if (State == cleaningState)
            {
                State.floorRouteActive = State.lobbyCleaningRequested = false;
                State.floorRoute.Clear(); State.ResetRouteLookup();
                if (finished)
                {
                    State.floorWorkSeconds = HygieneCleaningRoute.WorkSeconds;
                    State.floorWorkProgress = 1f;
                    if (State.floorRouteSkipped == 0)
                    {
                        State.lobbyClockHour = GameDayManager.Instance.CurrentGameHour;
                        State.lobbyGraceRemainingHours = Settings.lobbyGraceHours;
                    }
                }
                if (State.lobbyDirt < .3f) State.lobbyWarned = false;
                Changed(); Publish();
            }
        }
    }

    private void SweepCapturedFloor(System.Collections.Generic.HashSet<int> captured, Vector3 from, Vector3 to)
    {
        bool removed = false;
        for (int i = State.floorMarks.Count - 1; i >= 0; i--)
        {
            var mark = State.floorMarks[i];
            if (!captured.Contains(mark.id) || !HygieneCleaningRoute.WithinSweep(mark.position, from, to)
                || !State.floorRoute.Contains(mark.id)) continue;
            // A nearby point behind a booth/wall is not part of the mop's sweep.
            if (UnityEngine.AI.NavMesh.Raycast(to, mark.position, out _, UnityEngine.AI.NavMesh.AllAreas)) continue;
            State.floorMarks.RemoveAt(i); State.floorRoute.Remove(mark.id); State.floorRouteDone++; removed = true;
        }
        if (removed) { State.Recalculate(); Changed(); }
    }
    private static bool PlayerBusy()
    {
        var manager = ManagerPlayer.Active;
        if (manager == null || manager.Movement == null) return true;
        var hands = manager.GetComponent<WaiterHands>();
        return RestaurantTaskClaim.PlayerHasActiveTask || manager.Movement.CurrentTarget != null
            || hands != null && (hands.HasTray || hands.HasBill || hands.HasMoney || hands.HasTicket)
            || manager.GetComponent<BusserHands>()?.HasTray == true;
    }
    private void StartHelp()
    {
        if (PlayerBusy()) { WarningSlideUI.Instance?.Show("Finish your current task, then help clean the lobby."); return; }
        // Use the regular claim and pickup path, including its sink continuation.
        FoodTray best = null;
        float distance = float.MaxValue;
        helpMover = ManagerPlayer.Active.Movement;
        foreach (var tray in FindObjectsByType<FoodTray>(FindObjectsSortMode.None))
        {
            var pickup = tray.GetComponent<FoodTrayInteractable>();
            if (pickup == null || !pickup.IsCleanupPickable || tray.NetworkCarryLocked || RestaurantTaskClaim.IsClaimedByBot(tray)
                || RestaurantTaskClaim.IsClaimedByPlayer(tray)) continue;
            if (MultiplayerDayBridge.IsActive && !MultiplayerServiceActions.CanCollectDirtyTray(tray)) continue;
            float d = (tray.transform.position - helpMover.transform.position).sqrMagnitude;
            if (d < distance) { distance = d; best = tray; }
        }
        if (best != null)
        {
            helpTarget = best;
            best.GetComponent<FoodTrayInteractable>().UI_RequestPickup();
        }
        else
        {
            Booth target = null;
            foreach (var booth in FindObjectsByType<Booth>(FindObjectsSortMode.None))
            {
                if (!booth.CanRequestHumanCleanup || RestaurantTaskClaim.IsClaimedByBot(booth)) continue;
                float d = (booth.transform.position - helpMover.transform.position).sqrMagnitude;
                if (d < distance) { distance = d; target = booth; }
            }
            if (target != null) { helpTarget = target; target.RequestHygieneAssistance(helpMover); }
        }
        if (helpTarget == null) { helpMover = null; WarningSlideUI.Instance?.Show("Staff will clean the floor. Help with trays or tables when needed."); return; }
        helpStarted = Time.time;
        State.lobbyAssistance = true;
    }
    private void TickHelp()
    {
        if (!State.lobbyAssistance) return;
        bool complete = helpTarget == null || helpTarget is Booth booth && !booth.NeedsSurfaceCleaning;
        // The regular cleanup hook removes that task's dirt. Completing one tray
        // must not erase every floor mark in the restaurant.
        bool cancelled = helpMover == null || Time.time - helpStarted > 60f
            || Time.time - helpStarted > 2f && helpMover.CurrentTarget == null
            && !RestaurantTaskClaim.PlayerHasActiveTask && helpMover.GetComponent<BusserHands>()?.HasTray != true;
        if (!complete && !cancelled) return;
        if (cancelled && helpTarget is Booth abandonedBooth && !MultiplayerDayBridge.IsActive)
            abandonedBooth.CancelHygieneAssistance();
        State.lobbyAssistance = false;
        helpTarget = null; helpMover = null;
        Changed();
    }
    private void Changed() { State.revision++; dirty = true; }
    private void Publish()
    {
        dirty = false;
        nextSync = Time.unscaledTime + .5f;
        if (!MultiplayerDayBridge.IsActive || !Authority || PhotonNetwork.CurrentRoom == null) return;
        // Small hygiene-only checkpoint; contains no restaurant/save-state capture.
        State.revision++;
        State.sentAt = PhotonNetwork.Time;
        State.clockScale = Time.timeScale;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [StateKey] = HygieneSnapshot.Encode(State) });
    }
    private void ReadState()
    {
        if (Authority || PhotonNetwork.CurrentRoom == null || !(PhotonNetwork.CurrentRoom.CustomProperties[StateKey] is string payload)
            || payload == lastPayload || payload.Length > 65536) return;
        try
        {
            var incoming = HygieneSnapshot.Decode(payload);
            if (incoming == null || incoming.run != Run || incoming.day != Day || incoming.revision <= State.revision
                || !(incoming.kitchenDirt >= 0f && incoming.kitchenDirt <= 1f)
                || !(incoming.lobbyDirt >= 0f && incoming.lobbyDirt <= 1f)
                || !(incoming.cleaningRemaining >= 0f && incoming.cleaningRemaining <= 3600f)
                || !Enum.IsDefined(typeof(KitchenCleaningMode), incoming.cleaningMode)
                || !Enum.IsDefined(typeof(HygieneArea), incoming.decisionArea)) return;
            if (incoming.floorMarks == null || incoming.floorMarks.Count > HygieneState.MaxFloorMarks) return;
            foreach (var mark in incoming.floorMarks)
                if (mark == null || !(mark.dirt >= 0f && mark.dirt <= 1f)
                    || float.IsNaN(mark.position.sqrMagnitude) || float.IsInfinity(mark.position.sqrMagnitude)) return;
            State = incoming;
            lastPayload = payload;
        }
        catch (ArgumentException) { Debug.LogWarning("[Hygiene] Ignored invalid sanitation state."); }
    }
    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (changed.ContainsKey(StateKey)) ReadState();
    }
    public override void OnDisable()
    {
        if (Instance == this)
        {
            CancelLobbyCleaning();
            State.decisionOpen = false;
            State.cleaningMode = KitchenCleaningMode.None;
            State.forcedPending = false;
            ReleasePause();
            dialogue?.SetVisible(false);
            deferredActions.Clear();
            if (sceneReady && Authority) { Changed(); Publish(); }
        }
        base.OnDisable();
    }
    private void OnDestroy()
    {
        deferredActions.Clear();
        ReleasePause();
        surfaces?.Dispose();
        if (Instance == this) Instance = null;
    }
}
