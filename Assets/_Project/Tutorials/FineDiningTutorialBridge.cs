using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-local lessons over the real reservation, bar and pouring workflows.</summary>
[DefaultExecutionOrder(-9003)]
[DisallowMultipleComponent]
public sealed class FineDiningTutorialBridge : MonoBehaviour
{
    public const string SceneName = "Lobby3Tutorial";
    public const string CompletedKey = "DineIn_FineDiningTutorial_Completed";
    public const string SkippedKey = "DineIn_FineDiningTutorial_Skipped";
    public const string ChapterKey = "DineIn_FineDiningTutorial_Chapter_v1";
    public enum Chapter { Briefing, Reservation, Drinks, Pour, Complete }
    public enum ActionKind { Explain, OpenReception, AcceptReservation, CloseReception, SeatReservation,
        TakeDrinkOrder, PickUpDrink, DeliverDrink, OpenPour, Pour }
    [Serializable] public sealed class Lesson
    {
        public string id;
        public Chapter chapter;
        public ActionKind action;
        public string control;
    }
    [Header("Authored lessons - indices match TutorialSystem")]
    [SerializeField] private Lesson[] lessons = Array.Empty<Lesson>();
    [SerializeField] private TutorialSystem tutorial;
    [SerializeField] private TutorialDayContext day;
    [SerializeField] private FineDiningReservations reservations;
    [SerializeField] private ManagementComputerStation receptionistDesk;
    [SerializeField] private CashierBoothInteractable cashier;
    [SerializeField] private Transform barPickup;
    [SerializeField] private Recipe food, drink, water;
    [Header("Existing Big Boss presentation and recovery")]
    [SerializeField] private Canvas controlsCanvas;
    [SerializeField] private Button restart, skip, finish;
    [SerializeField] private GameObject confirmation;
    [SerializeField] private Button confirmSkip, cancelSkip;
    [SerializeField] private TMP_Text status;
    [SerializeField] private LobbyHUDRedesign lobbyHUD;
    [SerializeField] private RectTransform worldFocusProxy;
    [Header("Training setup")]
    [SerializeField, Min(1)] private int partySize = 2;
    [SerializeField] private string reservationPartyName = "Alex party";
    [SerializeField, Min(12)] private int stockPerIngredient = 80;
    [SerializeField, Min(5)] private float dependencyAllowance = 20f;
    [SerializeField, Min(20)] private float arrivalAllowance = 90f;

    public static FineDiningTutorialBridge Instance { get; private set; }
    public static bool Active => Instance != null && Instance.isActiveAndEnabled && !Instance.leaving;
    public bool Initialized { get; private set; }
    public Lesson CurrentLesson => current;
    public RectTransform WorldFocusProxy => worldFocusProxy;
    public float TargetAllowance => current?.action == ActionKind.SeatReservation ? arrivalAllowance : dependencyAllowance;
    public bool Dragging => reservations != null && reservations.PourUI != null && reservations.PourUI.IsDragging;
    public bool FreeNavigation => current?.action == ActionKind.Pour;
    public string TravelKey => current != null && (current.action == ActionKind.OpenReception ||
        current.action == ActionKind.SeatReservation || current.action == ActionKind.TakeDrinkOrder ||
        current.action == ActionKind.PickUpDrink ||
        current.action == ActionKind.DeliverDrink || current.action == ActionKind.OpenPour) ? current.id : null;
    public Transform GuidanceSource => current?.action == ActionKind.Pour ? reservations?.PourUI?.bottle : null;
    public string FocusGroup => gameObject.scene.handle + ":" + (reservations?.PourUI?.IsOpen == true ? "Pour" :
        Computer?.IsOpen == true ? "Reception" : Checklist?.ReviewedGroup != null ? "Notepad" : "Restaurant");
    public bool ActionGuidanceSuppressed => failure != null ||
        current?.action == ActionKind.PickUpDrink && PreparedTray == null && !HoldingLessonTray ||
        current?.action == ActionKind.SeatReservation && party != null && party.HasBeenAssigned && !party.TutorialSeated;
    public bool OwnsCanvas(Canvas value) => controlsCanvas != null && value != null &&
        value.transform.IsChildOf(controlsCanvas.transform.root);
    public bool BlocksDialogueInput
    {
        get
        {
            if (resetPending || leaving || confirmation != null && confirmation.activeSelf) return true;
            Vector2 point = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            return OverControl(restart, point) || OverControl(skip, point);
        }
    }
    private static bool OverControl(Button button, Vector2 point)
    {
        if (button == null || !button.isActiveAndEnabled) return false;
        var canvas = button.GetComponentInParent<Canvas>()?.rootCanvas;
        return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, point,
            canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);
    }

    private Lesson current;
    private Chapter? chapter;
    private CustomerGroup party;
    private FineDiningBooking booking;
    private FineDiningTable lessonTable;
    private GroupSpawner spawner;
    private KitchenManager kitchen;
    private TutorialSceneBindings bindings;
    private MainCameraController cameraController;
    private ManagementReservationPanel reservationPanel;
    private OrderChecklistUI checklist;
    private bool delivered, poured, leaving, resetPending, replay, originalFollowSuspended;
    private string failure;
    private float passiveElapsed;
    private int lessonOrderNumber = -1;
    private Canvas[] presentationCanvases;
    private bool presentationPaused;
    private static int retryChapter = -1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { Instance = null; retryChapter = -1; }
    private ManagementComputerController Computer => bindings != null ? bindings.Computer : null;
    private OrderChecklistUI Checklist => checklist;
    private WaiterHands Hands => WaiterHands.ActivePlayerHands;
    private FoodTray PreparedTray => kitchen != null && lessonOrderNumber >= 0 ? kitchen.GetPreparedResult(lessonOrderNumber) : null;
    private bool HoldingLessonTray => Hands != null && Hands.holdingTray != null && Hands.holdingTray.TargetGroup == party;
    private bool SelectingTable => party != null && ManagerPlayer.Active != null &&
        ManagerPlayer.Active.GetComponent<RoleBasedAssignController>()?.IsSelectingBooth(party) == true;

    private void Awake()
    {
        if (gameObject.scene.name != SceneName) { enabled = false; return; }
        Instance = this;
        CampaignSaveStore.SelectFineDiningTraining();
    }
    private IEnumerator Start()
    {
        replay = TutorialGameModeEntry.IsRevisitLaunch;
        if (tutorial == null || day == null || controlsCanvas == null || restart == null || skip == null ||
            finish == null || confirmation == null || confirmSkip == null || cancelSkip == null || status == null)
        { Debug.LogError("[FineDiningTutorial] Required presentation bindings are missing.", this); yield break; }
        tutorial.PreparingStep += PrepareStep;
        tutorial.TutorialCompletedChanged += Completed;
        restart.onClick.AddListener(RestartChapter);
        skip.onClick.AddListener(ShowSkipConfirmation);
        confirmSkip.onClick.AddListener(SkipTutorial);
        cancelSkip.onClick.AddListener(HideSkipConfirmation);
        finish.onClick.AddListener(FinishTutorial);
        confirmation.SetActive(false);
        finish.gameObject.SetActive(false);
        restart.gameObject.SetActive(false);
        skip.gameObject.SetActive(false);
        presentationCanvases = controlsCanvas.transform.root.GetComponentsInChildren<Canvas>(true);
        bindings = GetComponent<TutorialSceneBindings>();
        spawner = FindScene<GroupSpawner>();
        kitchen = cashier != null ? cashier.kitchen : FindScene<KitchenManager>();
        checklist = FindScene<OrderChecklistUI>();
        cameraController = FindScene<MainCameraController>();
        if (cameraController != null)
        {
            originalFollowSuspended = cameraController.FollowSuspended;
            cameraController.SetFollowSuspended(true);
        }
        float until = Time.realtimeSinceStartup + dependencyAllowance;
        while ((!day.IsReady || !day.IsFineDiningStaffReady || !tutorial.IsNavigationReady || ManagerPlayer.Active == null) && Time.realtimeSinceStartup < until)
            yield return null;
        if (!day.IsReady || !day.IsFineDiningStaffReady || !tutorial.IsNavigationReady || ManagerPlayer.Active == null || spawner == null ||
            reservations == null || kitchen == null || !kitchen.HasDrinkServiceOutput || Computer == null)
        { Fail("Training could not start. Retry the chapter or skip training."); yield break; }
        if (!PrepareMenu()) { Fail("Training supplies are unavailable. Retry the chapter."); yield break; }
        reservations.dayManager.SetFineDiningTutorialClockPaused(true);
        reservations.dayManager.StartShift();
        if (!reservations.dayManager.ServiceActive)
        { Fail("Training staff could not start service. Retry the chapter."); yield break; }
        tutorial.SetSpawnPermissions(false, true);
        Initialized = true;
        int saved = retryChapter >= 0 ? retryChapter : replay ? 0 : PlayerPrefs.GetInt(ChapterKey, 0);
        retryChapter = -1;
        if (saved < 0 || saved >= (int)Chapter.Complete) saved = 0;
        int index = Array.FindIndex(lessons, l => (int)l.chapter == saved);
        tutorial.StartAtStep(Mathf.Max(0, index));
    }
    private T FindScene<T>() where T : Component => FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(value => value.gameObject.scene == gameObject.scene);
    private Recipe Runtime(Recipe authored) => authored == null ? null : day.Catalog.Products.FirstOrDefault(r => r != null && r.ProductId == authored.ProductId);
    private bool PrepareMenu()
    {
        if (Runtime(food) == null || Runtime(drink) == null || Runtime(water) == null ||
            InventoryManager.Instance == null || MenuAvailabilityManager.Instance == null) return false;
        foreach (var recipe in day.Catalog.Products)
        {
            if (recipe == null) continue;
            bool available = recipe.ProductId == food.ProductId || recipe.ProductId == drink.ProductId || recipe.ProductId == water.ProductId;
            recipe.availableOnMenu = available;
            MenuAvailabilityManager.Instance.SetProductAvailable(recipe, available);
            if (!available || recipe.ingredients == null) continue;
            foreach (var ingredient in recipe.ingredients)
                if (ingredient?.item != null)
                    InventoryManager.Instance.AddStock(ingredient.item.itemType, Mathf.Max(stockPerIngredient, ingredient.amount * 12));
        }
        reservations.waterPourProduct = Runtime(water);
        return true;
    }
    private void PrepareStep(TutorialSystem.TutorialStep step)
    {
        int index = tutorial.CurrentStepIndex;
        if (index < 0 || index >= lessons.Length || lessons[index].id != step.Id)
        { Fail("This lesson's setup is incomplete. Retry the chapter."); return; }
        current = lessons[index];
        passiveElapsed = 0;
        status.text = "";
        tutorial.SetExternalGuidanceSuppressed(false);
        if (chapter != current.chapter) SetupChapter(current.chapter);
        if (failure != null) return;
        if (current.action == ActionKind.SeatReservation && party == null)
        {
            party = spawner.SpawnTutorialFineDiningGroup(partySize, booking?.id);
            if (party == null) { Fail("The booked party could not arrive. Retry the reservation chapter."); return; }
            party.SetFineDiningTutorialProtection(true);
        }
        tutorial.SetSpawnPermissions(false, true);
        restart.gameObject.SetActive(true);
        skip.gameObject.SetActive(true);
        var world = ResolveWorld("FD.Target");
        if (world != null && !(world is RectTransform)) cameraController?.CenterOnWorldPoint(world.position);
        lobbyHUD?.RefreshVisibility();
    }
    private void SetupChapter(Chapter value)
    {
        CleanupParty();
        chapter = value;
        delivered = poured = false;
        lessonOrderNumber = -1;
        if (!replay && !tutorial.IsDebugSession && value != Chapter.Complete)
        { PlayerPrefs.SetInt(ChapterKey, (int)value); PlayerPrefs.Save(); }
        if (value == Chapter.Reservation)
        {
            booking = reservations.PrepareTutorialBooking(reservationPartyName, partySize);
            if (booking == null) Fail("The training reservation could not be prepared. Retry this chapter.");
        }
        if (value != Chapter.Drinks && value != Chapter.Pour) return;
        // These are staged diners, not completions of the player's lesson.
        lessonTable = reservations.tables?.FirstOrDefault(t => t != null && t.Usable && t.Capacity >= partySize &&
            t.booth.CurrentGroup == null && !t.booth.IsDirty);
        if (lessonTable == null) { Fail("A clean training table is unavailable. Retry this chapter."); return; }
        party = spawner.SpawnTutorialFineDiningGroup(partySize);
        if (party == null) { Fail("The training diners could not be prepared. Retry this chapter."); return; }
        party.SetFineDiningTutorialProtection(true);
        party.TutorialPlaceGroupAtBooth(lessonTable.booth, false);
        party.FineDiningDrinkServiceFinished += ServiceFinished;
        if (!party.PrepareTutorialFineDiningMeal(Runtime(food)) ||
            !party.BeginTutorialFineDiningRequest(Runtime(value == Chapter.Pour ? water : drink), value == Chapter.Pour))
        { Fail("This table's drink request could not start. Retry this chapter."); return; }
        lessonOrderNumber = party.currentOrderNumber;
    }
    private void ServiceFinished(CustomerGroup group, bool served, bool isPour)
    {
        if (group != party || leaving || resetPending) return;
        if (!served) { Fail("The drink request was interrupted. Retry this chapter."); return; }
        if (isPour) poured = true; else delivered = true;
    }
    private void Update()
    {
        bool paused = LobbyPauseMenu.IsAnyOpen;
        if (presentationCanvases != null && paused != presentationPaused)
        {
            presentationPaused = paused;
            foreach (var canvas in presentationCanvases) if (canvas != null) canvas.enabled = !paused;
        }
        if (!Initialized || current == null || leaving || resetPending || paused || confirmation.activeSelf || failure != null) return;
        if (booking != null && current.chapter == Chapter.Reservation && booking.status == FineDiningBookingStatus.Declined)
        { Fail("That reservation was declined. Retry the chapter to practise accepting it."); return; }
        if (!poured && current.chapter == Chapter.Pour &&
            (current.action == ActionKind.Pour || current.control == "Pour:Stage") && reservations.PourUI?.IsOpen != true)
        { ReturnToAction(ActionKind.OpenPour); return; }
        if (current.control == "Reservation:Panel" && Panel == null)
        { ReturnToAction(ActionKind.OpenReception); return; }
        if (!tutorial.IsWaitingForGameplayAction) return;
        if (Satisfied()) { status.text = ""; tutorial.NotifyAction(current.id); return; }
        bool passive = ActionGuidanceSuppressed;
        tutorial.SetExternalGuidanceSuppressed(passive, passive);
        status.text = passive ? (current.action == ActionKind.PickUpDrink ? "Your bartender is preparing the drink." : "The party is taking their seats.") : "";
        if (passive && Time.timeScale > 0) passiveElapsed += Time.unscaledDeltaTime;
        if (passiveElapsed > arrivalAllowance) Fail("The service task could not finish. Retry this chapter.");
    }
    private void ReturnToAction(ActionKind action)
    {
        int index = Array.FindIndex(lessons, l => l.chapter == current.chapter && l.action == action);
        if (index >= 0) tutorial.StartAtStep(index);
        else Fail("This lesson cannot resume. Retry the chapter.");
    }
    private bool Satisfied()
    {
        switch (current.action)
        {
            case ActionKind.OpenReception: return Panel != null;
            case ActionKind.AcceptReservation: return booking != null && booking.status == FineDiningBookingStatus.Confirmed && !string.IsNullOrEmpty(booking.tableId);
            case ActionKind.CloseReception: return Computer != null && !Computer.IsOpen;
            case ActionKind.SeatReservation: return booking != null && booking.status == FineDiningBookingStatus.Seated && party != null && party.TutorialSeated;
            case ActionKind.TakeDrinkOrder: return party != null && party.HasConfirmedOrder &&
                lessonOrderNumber >= 0 && kitchen.HasAcceptedOrder(lessonOrderNumber);
            case ActionKind.PickUpDrink: return HoldingLessonTray;
            case ActionKind.DeliverDrink: return delivered;
            case ActionKind.OpenPour: return party != null && party.HasWineRefillRequest && reservations.PourUI?.IsOpen == true;
            case ActionKind.Pour: return poured;
            default: return false;
        }
    }
    private ManagementReservationPanel Panel
    {
        get
        {
            if (Computer == null || !Computer.IsOpen) return null;
            if (reservationPanel == null || !reservationPanel.isActiveAndEnabled)
                reservationPanel = Computer.AppWindow?.GetComponentInChildren<ManagementReservationPanel>();
            return reservationPanel != null && reservationPanel.isActiveAndEnabled ? reservationPanel : null;
        }
    }
    public RectTransform ResolveUI(string key)
    {
        if (current == null || string.IsNullOrEmpty(key)) return null;
        if (key == "FD.Source") return GuidanceSource as RectTransform;
        if (key != "FD.Target") return null;
        switch (current.control)
        {
            case "Reservation:Panel": return Panel != null ? Panel.transform as RectTransform : null;
            case "Computer:Exit": return bindings?.ResolveUI("ManagementExit");
            case "Drink:Order":
                if (Checklist != null && Checklist.ReviewedGroup == party) return Checklist.transform as RectTransform;
                return PlayerTaskBubbleFocus.GuidanceTarget(party);
            case "Pour:Stage": return reservations.PourUI?.IsOpen == true ? reservations.PourUI.stage : null;
            case "Pour:Glass": return reservations.PourUI?.IsOpen == true ? reservations.PourUI.bottle : null;
            case "World:Party": return SelectingTable ? null : PlayerTaskBubbleFocus.GuidanceTarget(party);
            case "World:Bar": return current.action == ActionKind.PickUpDrink ? PlayerTaskBubbleFocus.GuidanceTarget(PreparedTray) : null;
            default: return null;
        }
    }
    public Transform ResolveWorld(string key)
    {
        if (current == null || key != "FD.Target") return null;
        switch (current.control)
        {
            case "World:Reception": return receptionistDesk != null ? receptionistDesk.transform : null;
            case "World:Cashier": return cashier != null ? cashier.transform : null;
            case "World:Bar": return PreparedTray != null ? PreparedTray.transform : barPickup;
            case "World:Party":
            case "Drink:Order":
                if (SelectingTable && booking != null)
                    return reservations.tables.FirstOrDefault(t => t != null && t.tableId == booking.tableId)?.booth?.transform;
                return party != null ? party.assignedBooth != null ? party.assignedBooth.transform : party.UIAnchor : null;
            case "Pour:Glass": return reservations.PourUI?.IsOpen == true ? reservations.PourUI.glassOpening : null;
            default: return null;
        }
    }
    public bool PresentationReady(TutorialSystem.TutorialStep step)
    {
        if (failure != null || step == null || step != tutorial.CurrentStep || current == null) return false;
        if (string.IsNullOrEmpty(current.control)) return true;
        if (current.action == ActionKind.SeatReservation && party != null && !party.HasBeenAssigned &&
            (!party.CanBeSeated || !party.ReadyForReceptionInteraction)) return false;
        var ui = ResolveUI(step.UITargetKey);
        var world = ResolveWorld(step.WorldTargetKey);
        return (ui != null && ui.gameObject.activeInHierarchy) || (world != null && world.gameObject.activeInHierarchy);
    }
    public void RememberGuidance() { }
    public void RefreshGuidance()
    {
        if (current == null || failure != null || leaving || resetPending || LobbyPauseMenu.IsAnyOpen || confirmation.activeSelf ||
            tutorial.IsAwaitingExternalTarget || tutorial.IsGuidedTravelActive) return;
        if (!tutorial.IsWaitingForGameplayAction && !tutorial.IsWaitingForNext) return;
        if (ActionGuidanceSuppressed) return;
        var step = tutorial.CurrentStep;
        tutorial.RefreshExternalGuidance(ResolveUI(step.UITargetKey), ResolveWorld(step.WorldTargetKey));
    }
    public void ReportTargetFailure(TutorialSystem.TutorialStep step)
    {
        if (step != tutorial.CurrentStep || leaving || resetPending || failure != null) return;
        Debug.LogError("[FineDiningTutorial] Missing target for " + step.Id + " (" + current?.control + ")", this);
        Fail("The highlighted target is unavailable. Retry this chapter.");
    }
    private void Fail(string message)
    {
        failure = message;
        if (status != null) status.text = message;
        tutorial?.SetExternalGuidanceSuppressed(true);
        if (restart != null) restart.gameObject.SetActive(true);
        if (skip != null) skip.gameObject.SetActive(true);
    }
    public void RestartChapter()
    {
        if (resetPending || leaving) return;
        int target = (int)(chapter == Chapter.Complete ? Chapter.Pour : chapter ?? Chapter.Briefing);
        // Reload the isolated scene to clear in-flight walking, kitchen and reservation ownership together.
        retryChapter = target;
        if (!replay && tutorial?.IsDebugSession != true)
        { PlayerPrefs.SetInt(ChapterKey, target); PlayerPrefs.Save(); }
        StartCoroutine(ExitRoutine(SceneName));
    }
    private void Completed()
    {
        if (!replay && !tutorial.IsDebugSession)
        { PlayerPrefs.SetInt(CompletedKey, 1); PlayerPrefs.DeleteKey(SkippedKey); PlayerPrefs.DeleteKey(ChapterKey); PlayerPrefs.Save(); }
        FinishTutorial(); // Called only after dismissing the final Big Boss line.
    }
    private void SkipTutorial()
    {
        if (!replay && !tutorial.IsDebugSession)
        { PlayerPrefs.SetInt(SkippedKey, 1); PlayerPrefs.DeleteKey(ChapterKey); PlayerPrefs.Save(); }
        FinishTutorial();
    }
    private void FinishTutorial()
    {
        if (!resetPending && !leaving) StartCoroutine(ExitRoutine("Lobby3"));
    }
    private IEnumerator ExitRoutine(string destination)
    {
        resetPending = true;
        confirmation.SetActive(false);
        tutorial.ReleaseFineDiningPresentation();
        Computer?.CloseComputer();
        Checklist?.Close();
        reservations?.PourUI?.Cancel();
        ManagerPlayer.Active?.Movement.CancelLockedTask();
        CleanupParty();
        yield return null; // Allow destroyed training actors to release their table and task claims before restoring saves.
        leaving = true;
        if (cameraController != null) cameraController.SetFollowSuspended(originalFollowSuspended);
        reservations?.dayManager?.SetFineDiningTutorialClockPaused(false);
        day.ReleaseFineDiningSession();
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(destination);
        else if (!SceneLoader.TryLoadScene(destination, LoadSceneMode.Single, out _))
        { leaving = false; resetPending = false; Fail("The next scene is unavailable. Return to the game menu."); }
    }
    private void CleanupParty()
    {
        if (party == null) return;
        party.FineDiningDrinkServiceFinished -= ServiceFinished;
        ManagerPlayer.Active?.GetComponent<RoleBasedAssignController>()?.ClearBoothSelection(party);
        if (Hands?.holdingTicketFor == party) Hands.ClearTicket();
        if (HoldingLessonTray) Hands.DisposeTray();
        foreach (var tray in FindObjectsByType<FoodTray>(FindObjectsSortMode.None))
            if (tray.TargetGroup == party)
            { tray.gameObject.SetActive(false); tray.transform.SetParent(null, true); Destroy(tray.gameObject); }
        party.DisposeFineDiningTutorialGroup();
        party = null;
    }
    private void ShowSkipConfirmation() => confirmation.SetActive(true);
    private void HideSkipConfirmation() => confirmation.SetActive(false);
    private void OnDestroy()
    {
        if (tutorial != null) { tutorial.PreparingStep -= PrepareStep; tutorial.TutorialCompletedChanged -= Completed; }
        if (party != null) party.FineDiningDrinkServiceFinished -= ServiceFinished;
        if (cameraController != null) cameraController.SetFollowSuspended(originalFollowSuspended);
        if (restart != null) restart.onClick.RemoveListener(RestartChapter);
        if (skip != null) skip.onClick.RemoveListener(ShowSkipConfirmation);
        if (confirmSkip != null) confirmSkip.onClick.RemoveListener(SkipTutorial);
        if (cancelSkip != null) cancelSkip.onClick.RemoveListener(HideSkipConfirmation);
        if (finish != null) finish.onClick.RemoveListener(FinishTutorial);
        PlayerTaskGuidance.ClearTask(SceneName);
        if (Instance == this) Instance = null;
    }
}
