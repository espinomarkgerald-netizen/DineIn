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
        TakeDrinkOrder, PickUpDrink, DeliverDrink, OpenPour, Pour, SelectReservationTable, ViewTonight,
        OpenDrinkOrder, SelectDrinkQuantity, ReviewDrinkOrder }
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
    [Header("Station focus geometry")]
    [SerializeField] private Transform receptionFocus;
    [SerializeField] private Transform barFocus;
    [SerializeField] private Transform cashierFocus;
    [SerializeField] private Transform bartenderFocus;
    [Header("Existing Big Boss presentation and recovery")]
    [SerializeField] private Canvas controlsCanvas;
    [SerializeField] private Button restart, skip, finish;
    [SerializeField] private GameObject confirmation;
    [SerializeField] private Button confirmSkip, cancelSkip;
    [SerializeField] private TMP_Text status;
    [Header("Waiting status - reuse the existing kitchen timer")]
    [SerializeField] private FastFoodCookingTimer waitingTimerTemplate;
    [SerializeField, Min(24)] private float waitingCircleSize = 48;
    [SerializeField, Min(0)] private float waitingCircleGap = 12;
    [SerializeField, Min(0)] private float waitingActivitySpeed = 180;
    [SerializeField, Range(0, .5f)] private float waitingTextOutline = .2f;
    [SerializeField] private LobbyHUDRedesign lobbyHUD;
    [SerializeField] private RectTransform worldFocusProxy;
    [Header("Training setup")]
    [SerializeField, Min(1)] private int partySize = 2;
    [SerializeField] private string reservationPartyName = "Alex party";
    [SerializeField, Min(12)] private int stockPerIngredient = 80;
    [SerializeField, Min(5)] private float dependencyAllowance = 20f;
    [SerializeField, Min(20)] private float arrivalAllowance = 90f;
    [SerializeField, Range(.5f, 1f)] private float seatingCameraZoom = .8f;

    public static FineDiningTutorialBridge Instance { get; private set; }
    public static bool Active => Instance != null && Instance.isActiveAndEnabled && !Instance.leaving;
    public bool Initialized { get; private set; }
    public Lesson CurrentLesson => current;
    public RectTransform WorldFocusProxy => worldFocusProxy;
    public float TargetAllowance => current?.action == ActionKind.SeatReservation ? arrivalAllowance : dependencyAllowance;
    public bool Dragging => reservations != null && reservations.PourUI != null && reservations.PourUI.IsDragging;
    public bool FreeNavigation => current?.action == ActionKind.Pour;
    public string TravelKey => current != null && (current.action == ActionKind.OpenReception ||
        current.action == ActionKind.SeatReservation || current.action == ActionKind.OpenDrinkOrder ||
        current.action == ActionKind.PickUpDrink ||
        current.action == ActionKind.DeliverDrink || current.action == ActionKind.OpenPour) ? current.id : null;
    public Transform GuidanceSource => current?.action == ActionKind.Pour ? reservations?.PourUI?.bottle : null;
    public string FocusGroup => gameObject.scene.handle + ":" + (reservations?.PourUI?.IsOpen == true ? "Pour" :
        Computer?.IsOpen == true ? "Reception" : Checklist?.ReviewedGroup != null ? "Notepad" : "Restaurant");
    public bool ActionGuidanceSuppressed => failure != null ||
        current?.action == ActionKind.PickUpDrink && PreparedTray == null && !HoldingLessonTray ||
        current?.action == ActionKind.TakeDrinkOrder && party != null && party.HasConfirmedOrder && !kitchen.HasAcceptedOrder(lessonOrderNumber) ||
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
    private FastFoodCookingTimer waitingTimer;
    private Vector2 statusOffsetMin;
    private float waitingActivityAngle;
    private bool waitingStatusVisible;
    private int lessonOrderNumber = -1;
    private Canvas[] presentationCanvases;
    private bool presentationPaused;
    private bool focusRenderersCached;
    private Renderer[] receptionRenderers, barRenderers, cashierRenderers, bartenderRenderers;
    private Renderer receptionistMonitorRenderer;
    private Transform tableFocus;
    private Renderer[] tableFocusRenderers;
    private bool seatingCameraActive;
    private Transform seatingCameraTarget;
    private float seatingOriginalZoom;
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
            finish == null || confirmation == null || confirmSkip == null || cancelSkip == null || status == null ||
            waitingTimerTemplate == null)
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
        statusOffsetMin = status.rectTransform.offsetMin;
        status.color = Color.white;
        status.outlineColor = Color.black;
        status.outlineWidth = waitingTextOutline; // TMP creates a material instance for this label only.
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
        ClearWaitingStatus();
        restart.gameObject.SetActive(false);
        skip.gameObject.SetActive(false);
        confirmation.SetActive(false);
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
        var world = ResolveWorld("FD.Target");
        if (world != null && !(world is RectTransform)) cameraController?.CenterOnWorldPoint(world.position);
        lobbyHUD?.RefreshVisibility();
    }
    private void SetupChapter(Chapter value)
    {
        RestoreSeatingCamera();
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
        if (current.action == ActionKind.SeatReservation && party != null && !string.IsNullOrEmpty(party.TutorialSeatingFailure))
        { Fail(party.TutorialSeatingFailure); return; }
        if (!poured && current.chapter == Chapter.Pour && current.control != null &&
            current.control.StartsWith("Pour:", StringComparison.Ordinal) && reservations.PourUI?.IsOpen != true)
        { ReturnToAction(ActionKind.OpenPour); return; }
        if (current.control != null && current.control.StartsWith("Drink:", StringComparison.Ordinal) &&
            current.action != ActionKind.OpenDrinkOrder && party?.HasConfirmedOrder != true && Checklist?.ReviewedGroup != party)
        { ReturnToAction(ActionKind.OpenDrinkOrder); return; }
        if ((current.action == ActionKind.ReviewDrinkOrder || current.action == ActionKind.TakeDrinkOrder) &&
            Checklist?.ReviewedGroup == party && (Checklist.TutorialReviewOpen && !Checklist.TutorialReviewCorrect ||
            current.action == ActionKind.TakeDrinkOrder && !Checklist.TutorialReviewOpen))
        { ReturnToAction(ActionKind.SelectDrinkQuantity); return; }
        if (current.action != ActionKind.CloseReception && current.control != null &&
            current.control.StartsWith("Reservation:", StringComparison.Ordinal) && Panel == null)
        { ReturnToAction(ActionKind.OpenReception); return; }
        if (!tutorial.IsWaitingForGameplayAction)
        {
            if (tutorial.IsAwaitingExternalTarget && current.action == ActionKind.SeatReservation && party != null)
                PresentWaitingStatus();
            else ClearWaitingStatus();
            return;
        }
        if (Satisfied()) { ClearWaitingStatus(); tutorial.NotifyAction(current.id); return; }
        bool passive = ActionGuidanceSuppressed;
        tutorial.SetExternalGuidanceSuppressed(passive, passive);
        PresentWaitingStatus();
        if (passive && Time.timeScale > 0) passiveElapsed += Time.unscaledDeltaTime;
        if (passiveElapsed > arrivalAllowance) Fail("The service task could not finish. Retry this chapter.");
    }
    private void ClearWaitingStatus()
    {
        if (waitingTimer != null) waitingTimer.gameObject.SetActive(false);
        if (status != null)
        {
            if (waitingStatusVisible) status.rectTransform.offsetMin = statusOffsetMin;
            status.text = "";
        }
        waitingStatusVisible = false;
        waitingActivityAngle = 0;
    }
    private void PresentWaitingStatus()
    {
        string message = null;
        float progress = 0;
        bool measurable = false;
        if (current.action == ActionKind.PickUpDrink && PreparedTray == null && !HoldingLessonTray ||
            current.action == ActionKind.TakeDrinkOrder && party?.HasConfirmedOrder == true)
        {
            message = "Your bartender is preparing the drink.";
            measurable = kitchen.TryGetOptionalDrinkPreparationProgress(party, lessonOrderNumber, out progress);
        }
        else if (current.action == ActionKind.SeatReservation && party != null && party.HasBeenAssigned && !party.TutorialSeated)
            message = "The party is taking their seats.";
        else if (current.action == ActionKind.SeatReservation && party != null && !party.ReadyForReceptionInteraction)
            message = "Your reserved party is arriving.";
        else if (current.action == ActionKind.SelectDrinkQuantity && Checklist?.TutorialReviewOpen == true)
            message = "Go back and match the requested drink quantity.";
        else if (current.action == ActionKind.Pour && Dragging)
        {
            message = "Hold the pitcher here until the glass reaches the marked line.";
            progress = reservations.PourUI.TutorialFillProgress;
            measurable = true;
        }
        else if (tutorial.IsGuidedTravelActive) message = "Heading to the service task...";
        if (message == null) { ClearWaitingStatus(); return; }
        bool changed = status.text != message;
        status.text = message;
        if (waitingTimer == null)
        {
            waitingTimer = Instantiate(waitingTimerTemplate, status.transform.parent);
            waitingTimer.name = "Fine Dining tutorial waiting progress";
            waitingTimer.transform.localScale = Vector3.one;
            waitingTimer.seconds.gameObject.SetActive(false);
            waitingTimer.caption.gameObject.SetActive(false);
            foreach (var graphic in waitingTimer.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }
        var rect = (RectTransform)waitingTimer.transform;
        if (!waitingStatusVisible || changed)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.one * waitingCircleSize;
            float reserve = waitingCircleSize + waitingCircleGap;
            status.rectTransform.offsetMin = statusOffsetMin + new Vector2(reserve, 0);
            float textWidth = Mathf.Min(status.preferredWidth, status.rectTransform.rect.width);
            rect.anchoredPosition = new Vector2(reserve * .5f - textWidth * .5f - waitingCircleGap - waitingCircleSize * .5f, 0);
            waitingTimer.gameObject.SetActive(true);
            waitingStatusVisible = true;
        }
        waitingTimer.ring.color = waitingTimer.cookingColor;
        if (measurable)
        { waitingTimer.ring.SetAmount(progress); waitingTimer.ring.SetAngle(0); }
        else
        {
            // Travel and seating have no countdown; retain the existing activity-ring behavior.
            if (Time.timeScale > 0 && !LevelOneUIAccessibility.ReducedMotion)
                waitingActivityAngle = (waitingActivityAngle + waitingActivitySpeed * Time.unscaledDeltaTime) % 360;
            waitingTimer.ring.SetAmount(.25f);
            waitingTimer.ring.SetAngle(waitingActivityAngle);
        }
    }
    private void LateUpdate()
    {
        if (!Initialized || current?.action != ActionKind.SeatReservation || party == null ||
            cameraController == null || leaving || resetPending || failure != null || LobbyPauseMenu.IsAnyOpen) return;
        if (SelectingTable && booking != null)
        {
            var table = reservations.tables?.FirstOrDefault(t => t != null && t.tableId == booking.tableId);
            if (table?.booth == null) return;
            BeginSeatingCamera();
            if (seatingCameraTarget != table.booth.transform)
            {
                seatingCameraTarget = table.booth.transform;
                cameraController.CenterOnWorldPoint(TutorialWorldTargetGeometry.Center(seatingCameraTarget));
            }
        }
        else if (party.HasBeenAssigned && !party.TutorialSeated)
        {
            BeginSeatingCamera();
            // Follow the customers still walking, rather than the player left at the service point.
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var member in party.members)
                if (member != null && !member.IsSeated)
                { center += member.transform.position; count++; }
            if (count > 0) cameraController.CenterOnWorldPoint(center / count);
        }
        else if (!party.HasBeenAssigned) seatingCameraTarget = null;
    }
    private void BeginSeatingCamera()
    {
        if (!seatingCameraActive)
        { seatingOriginalZoom = cameraController.GetTargetOrthographicSize(); seatingCameraActive = true; }
        cameraController.SetTargetOrthographicSize(seatingOriginalZoom * seatingCameraZoom);
    }
    private void RestoreSeatingCamera()
    {
        if (seatingCameraActive && cameraController != null)
            cameraController.SetTargetOrthographicSize(seatingOriginalZoom);
        seatingCameraActive = false;
        seatingCameraTarget = null;
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
            case ActionKind.SelectReservationTable: return Panel != null && !Panel.TutorialBusy && !string.IsNullOrEmpty(Panel.TutorialSelectedTableId);
            case ActionKind.ViewTonight: return Panel != null && !Panel.TutorialBusy && Panel.TutorialShowingTonight;
            case ActionKind.CloseReception: return Computer != null && !Computer.IsOpen;
            case ActionKind.SeatReservation: return booking != null && booking.status == FineDiningBookingStatus.Seated && party != null && party.TutorialSeated;
            case ActionKind.TakeDrinkOrder: return party != null && party.HasConfirmedOrder &&
                lessonOrderNumber >= 0 && kitchen.HasAcceptedOrder(lessonOrderNumber);
            case ActionKind.OpenDrinkOrder: return Checklist != null && Checklist.ReviewedGroup == party;
            case ActionKind.SelectDrinkQuantity: return Checklist != null && !Checklist.TutorialReviewOpen && Checklist.TutorialSelectionMatchesRequest;
            case ActionKind.ReviewDrinkOrder: return Checklist != null && Checklist.TutorialReviewCorrect;
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
        if (current.control != null && current.control.StartsWith("Reservation:", StringComparison.Ordinal))
        {
            var panel = Panel;
            return panel?.ResolveTutorialTarget(current.control, booking?.id,
                !string.IsNullOrEmpty(panel.TutorialSelectedTableId) ? panel.TutorialSelectedTableId : booking?.suggestedTableId);
        }
        switch (current.control)
        {
            case "Reservation:Panel": return Panel != null ? Panel.transform as RectTransform : null;
            case "Computer:Exit": return bindings?.ResolveUI("ManagementExit");
            case "Drink:Order":
                return PlayerTaskBubbleFocus.GuidanceTarget(party);
            case "Drink:Quantity": return Checklist?.TutorialReviewOpen == true
                ? Checklist.TutorialReviewBackTarget
                : Checklist?.TutorialQuantityTarget(Runtime(drink)) ?? Checklist?.TutorialCheckOrderTarget;
            case "Drink:Check": return Checklist?.TutorialCheckOrderTarget;
            case "Drink:Confirm": return Checklist?.TutorialConfirmOrderTarget;
            case "World:TableNumber": return party?.TutorialTableNumberTarget;
            case "Pour:Stage": return reservations.PourUI?.IsOpen == true ? reservations.PourUI.stage : null;
            case "Pour:Pitcher": return reservations.PourUI?.IsOpen == true ? reservations.PourUI.TutorialBottleRect : null;
            case "Pour:Line": return reservations.PourUI?.IsOpen == true ? reservations.PourUI.TutorialTargetMarkerRect : null;
            case "Pour:Glass": return reservations.PourUI?.IsOpen == true ? reservations.PourUI.TutorialGlassRect : null;
            case "World:Party": return SelectingTable ? null : PlayerTaskBubbleFocus.GuidanceTarget(party);
            case "World:Bar": return current.action == ActionKind.PickUpDrink ? PlayerTaskBubbleFocus.GuidanceTarget(PreparedTray) : null;
            default: return null;
        }
    }
    public bool TryGetTargetBounds(Transform target, out Bounds bounds, out Transform space)
    {
        bounds = default;
        space = null;
        if (target == null || target.gameObject.scene != gameObject.scene || current == null) return false;
        if (current.control == "World:Party" || current.control == "World:TableNumber")
        {
            var table = reservations.tables?.FirstOrDefault(t => t != null && t.booth != null && t.booth.transform == target);
            if (table != null)
            {
                if (tableFocus != target)
                {
                    tableFocus = target;
                    // Use the authored table furniture, never customer bubbles or a corner pivot.
                    tableFocusRenderers = (table.presentationObjects ?? Array.Empty<GameObject>())
                        .Where(value => value != null).SelectMany(value => FocusRenderers(value.transform)).Distinct().ToArray();
                    if (tableFocusRenderers.Length == 0) tableFocusRenderers = FocusRenderers(table.diningSurface?.transform);
                }
                bool hasTable = false;
                EncapsulateFocus(tableFocusRenderers, ref bounds, ref hasTable);
                if (hasTable) return true;
            }
        }
        bool reception = current.control == "World:Reception" &&
            (target == receptionistDesk?.transform || target == receptionFocus);
        bool cashierStation = current.control == "World:Cashier" &&
            (target == cashier?.transform || target == cashierFocus);
        bool bar = current.control == "World:Bar" && current.action == ActionKind.Explain &&
            (target == barPickup || target == barFocus);
        if (!reception && !cashierStation && !bar) return false;
        if (!focusRenderersCached)
        {
            receptionRenderers = FocusRenderers(receptionFocus);
            barRenderers = FocusRenderers(barFocus);
            cashierRenderers = FocusRenderers(cashierFocus);
            bartenderRenderers = FocusRenderers(bartenderFocus);
            receptionistMonitorRenderer = receptionistDesk != null ? receptionistDesk.GetComponent<Renderer>() : null;
            focusRenderersCached = true;
        }
        bool found = false;
        if (reception)
        {
            EncapsulateFocus(receptionRenderers, ref bounds, ref found);
            EncapsulateFocus(receptionistMonitorRenderer, ref bounds, ref found);
        }
        else if (cashierStation) EncapsulateFocus(cashierRenderers, ref bounds, ref found);
        else
        {
            EncapsulateFocus(barRenderers, ref bounds, ref found);
            EncapsulateFocus(bartenderRenderers, ref bounds, ref found);
        }
        return found;
    }
    private static Renderer[] FocusRenderers(Transform subject) => subject == null ? Array.Empty<Renderer>() :
        subject.GetComponentsInChildren<Renderer>(true).Where(renderer =>
            (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) &&
            renderer.GetComponentInParent<Canvas>() == null && renderer.GetComponent<TMP_Text>() == null).ToArray();
    private static void EncapsulateFocus(Renderer[] renderers, ref Bounds bounds, ref bool found)
    {
        foreach (var renderer in renderers) EncapsulateFocus(renderer, ref bounds, ref found);
    }
    private static void EncapsulateFocus(Renderer renderer, ref Bounds bounds, ref bool found)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return;
        if (found) bounds.Encapsulate(renderer.bounds);
        else { bounds = renderer.bounds; found = true; }
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
            case "World:TableNumber":
            case "Drink:Order":
                if (SelectingTable && booking != null)
                    return reservations.tables.FirstOrDefault(t => t != null && t.tableId == booking.tableId)?.booth?.transform;
                return party != null ? party.assignedBooth != null ? party.assignedBooth.transform : party.UIAnchor : null;
            case "Pour:Glass": return reservations.PourUI?.IsOpen == true ? reservations.PourUI.TutorialGlassRect : null;
            default: return null;
        }
    }
    public bool PresentationReady(TutorialSystem.TutorialStep step)
    {
        if (failure != null || step == null || step != tutorial.CurrentStep || current == null) return false;
        if (string.IsNullOrEmpty(current.control)) return true;
        if ((current.action == ActionKind.SelectReservationTable || current.action == ActionKind.AcceptReservation ||
            current.action == ActionKind.ViewTonight) && Panel?.TutorialBusy == true) return false;
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
        RestoreSeatingCamera();
        ClearWaitingStatus();
        failure = message;
        if (status != null) status.text = message;
        // Recovery uses the existing error controls after releasing the dialogue and input mask.
        tutorial?.ReleaseFineDiningPresentation();
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
        ClearWaitingStatus();
        restart.gameObject.SetActive(false);
        skip.gameObject.SetActive(false);
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
        RestoreSeatingCamera();
        ClearWaitingStatus();
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
        RestoreSeatingCamera();
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
