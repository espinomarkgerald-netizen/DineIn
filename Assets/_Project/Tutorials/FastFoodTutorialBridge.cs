using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-local adapter between authored TutorialSystem steps and the real kitchen ledger.</summary>
[DefaultExecutionOrder(-9003)]
[DisallowMultipleComponent]
public sealed class FastFoodTutorialBridge : MonoBehaviour
{
    public const string CompletedKey = "DineIn_FastFoodTutorial_Completed";
    public const string SkippedKey = "DineIn_FastFoodTutorial_Skipped";
    public const string ChapterKey = "DineIn_FastFoodTutorial_Chapter_v1";
    public enum Chapter { Briefing, Burger, Fries, Serving, Parallel, Sandwiches, Burn, Restock, Hygiene, Practice, Complete }
    public enum ActionKind { Explain, Open, Station, Load, Ready, Collect, Assemble, Protein, Place, Serve, Burn, Discard, RestockOpen, Store, RestockExit, Clean, Practice, Purchase, Delivery, Truck, CollectDelivery, Freezer }
    [Serializable] public sealed class Lesson
    {
        public string id;
        public Chapter chapter;
        public ActionKind action;
        public Recipe recipe;
        public FastFoodStationMode station;
        public int portion;
        public int slot;
        public int ingredient;
        public string control;
    }
    [Header("Authoring — indices match TutorialSystem steps")]
    [SerializeField] private Lesson[] lessons = Array.Empty<Lesson>();
    [SerializeField] private Recipe burger, fries, chicken, fish, drink;
    [SerializeField] private FastFoodCookingController kitchen;
    [SerializeField] private FastFoodCookingView view;
    [SerializeField] private TutorialSystem tutorial;
    [SerializeField] private TutorialDayContext day;
    [Header("Saved recovery controls")]
    [SerializeField] private Canvas controlsCanvas;
    [SerializeField] private Button restart, skip, finish;
    [SerializeField] private LobbyHUDRedesign lobbyHUD;
    [SerializeField] private GameObject confirmation;
    [SerializeField] private Button confirmSkip, cancelSkip;
    [SerializeField] private TMP_Text status;
    [Header("Waiting status — reuse an authored kitchen timer")]
    [SerializeField] private FastFoodCookingTimer waitingTimerTemplate;
    [SerializeField, Min(24)] private float waitingCircleSize = 48;
    [SerializeField, Min(0)] private float waitingCircleGap = 12;
    [SerializeField, Min(0)] private float waitingActivitySpeed = 180;
    [Header("Training")]
    [SerializeField, Min(12)] private int stockPerIngredient = 80;
    [SerializeField, Min(5)] private float dependencyAllowance = 12;
    [Header("Guidance geometry (screen-space proxy, no graphics or input)")]
    [SerializeField] private RectTransform worldFocusProxy;
    public RectTransform WorldFocusProxy => worldFocusProxy;
    public float TargetAllowance => dependencyAllowance;
    public bool FreeNavigation => current != null && (current.action == ActionKind.Practice ||
        current.action == ActionKind.Station && current.id != "ff_Fries_station" && current.control == "StationArrow");
    public static FastFoodTutorialBridge Instance { get; private set; }
    public static bool Active => Instance != null && Instance.isActiveAndEnabled &&
        !Instance.leaving && Instance.gameObject.scene.name == FastFoodScene.Tutorial;
    public Camera KitchenCamera => view != null ? view.TutorialCamera : null;
    public bool HygieneLesson => current != null && current.chapter == Chapter.Hygiene;
    public bool HoldSandwichPrepView => Active && current != null && current.chapter == Chapter.Sandwiches;
    public MenuCatalog TrainingCatalog => day != null && day.IsReady ? day.Catalog : null;
    public bool Dragging => current?.action == ActionKind.Store
        ? restockHUD != null && restockHUD.HasActiveDrag : view != null && view.HasActiveDrag;
    public string TravelKey => current?.action == ActionKind.RestockOpen ? "Computer.Open" :
        current?.action == ActionKind.Truck ? "Restock.TruckOpened" :
        current?.action == ActionKind.Freezer ? "Restock.EnterAny" : null;
    public bool Initialized { get; private set; }
    public Lesson CurrentLesson => current;
    public Transform GuidanceSource => Source();
    private FastFoodCookingState State => kitchen != null ? kitchen.State : null;
    private Lesson current;
    private Chapter? chapter;
    private readonly Dictionary<string, Recipe> recipes = new();
    private readonly List<FastFoodCookingState.Portion> work = new();
    private int ticketNumber = 800001, practiceStage;
    private bool settingUp, leaving, cleaningStarted, cleanPrompted, resetPending;
    private float waitingSeconds;
    private string failure;
    private FastFoodCookingState.Ticket ticket;
    private ItemData restockItem;
    private bool replay;
    private readonly Dictionary<Button, bool> navigationStates = new();
    private Canvas[] presentationCanvases;
    private bool presentationPaused;
    private Transform lastGuidanceSource;
    private string lastPracticeObjective;
    private Transform lastGuidanceTarget;
    private float targetMissingSeconds;
    private readonly Vector3[] uiCorners = new Vector3[4];
    private RestockHotbarSlotUI restockSource;
    private Transform restockShelf;
    private int restockColumn, restockRow;
    private Button restockExit;
    private HygieneDialogue hygieneDialogue;
    private TutorialSceneBindings bindings;
    private ManagementComputerCatalogPanelUI cartPanel;
    private RestockTruckInteractable deliveryTruck;
    private RestockStockRoomEntrance freezerEntrance;
    private RestockFlowHUD restockHUD;
    private RestockOrderSaveData purchasedOrder;
    private ManagementComputerStation computerTask;
    private bool computerTravelRequested;
    private FastFoodCookingTimer waitingTimer;
    private Vector2 statusOffsetMin;
    private float waitingActivityAngle;
    private bool waitingStatusVisible;
    private ManagementComputerController Computer
    {
        get
        {
            if (bindings == null) bindings = GetComponent<TutorialSceneBindings>();
            var computer = bindings?.Computer;
            return computer != null && computer.gameObject.scene == gameObject.scene ? computer : null;
        }
    }
    private ManagementComputerCatalogPanelUI CartPanel
    {
        get
        {
            if (cartPanel == null || !cartPanel.gameObject.activeInHierarchy)
                cartPanel = Computer?.AppWindow?.GetComponentInChildren<ManagementComputerCatalogPanelUI>();
            return cartPanel != null && cartPanel.IsRestock ? cartPanel : null;
        }
    }
    private RestockTruckInteractable Truck => deliveryTruck != null ? deliveryTruck :
        deliveryTruck = FindObjectsByType<RestockTruckInteractable>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.gameObject.scene == gameObject.scene);
    private RectTransform CollectionControl
    {
        get
        {
            if (restockHUD == null) restockHUD = FindFirstObjectByType<RestockFlowHUD>();
            return restockHUD?.CollectionControl;
        }
    }
    private bool PassiveWait => current != null && (current.action == ActionKind.Ready ||
        current.action == ActionKind.Protein || current.action == ActionKind.Burn ||
        current.action == ActionKind.Delivery || current.action == ActionKind.RestockOpen && computerTravelRequested ||
        current.action == ActionKind.Freezer && RestockFlowCoordinator.Instance?.IsTransitioning == true ||
        current.action == ActionKind.Clean && cleaningStarted);

    public bool OwnsCanvas(Canvas value) => controlsCanvas != null && value != null &&
        value.transform.IsChildOf(controlsCanvas.transform.root);

    private void Awake()
    {
        if (gameObject.scene.name != FastFoodScene.Tutorial) { enabled = false; return; }
        Instance = this;
        if (status != null) statusOffsetMin = status.rectTransform.offsetMin;
        CampaignSaveStore.SelectFastFoodTraining();
    }
    private IEnumerator Start()
    {
        replay = TutorialGameModeEntry.IsRevisitLaunch;
        tutorial.PreparingStep += PrepareStep;
        tutorial.TutorialCompletedChanged += Completed;
        restart.onClick.AddListener(RestartChapter);
        skip.onClick.AddListener(ShowSkipConfirmation);
        cancelSkip.onClick.AddListener(HideSkipConfirmation);
        confirmSkip.onClick.AddListener(SkipTutorial);
        finish.onClick.AddListener(FinishTutorial);
        restart.gameObject.SetActive(false);
        skip.gameObject.SetActive(false);
        confirmation.SetActive(false);
        finish.gameObject.SetActive(false);
        presentationCanvases = controlsCanvas.transform.root.GetComponentsInChildren<Canvas>(true);
        yield return Initialize();
    }
    private IEnumerator Initialize()
    {
        failure = null;
        status.text = "";
        restart.gameObject.SetActive(false); skip.gameObject.SetActive(false);
        float until = Time.realtimeSinceStartup + dependencyAllowance;
        while ((day == null || !day.IsReady || State == null || ManagerPlayer.Active == null) && Time.realtimeSinceStartup < until)
            yield return null;
        if (day == null || !day.IsReady || State == null || ManagerPlayer.Active == null)
        { Fail("Training could not start. Retry the chapter or return to the menu."); yield break; }
        foreach (var recipe in day.Catalog.Products.Where(r => r != null)) recipes[recipe.ProductId] = recipe;
        Initialized = true;
        yield return null; // TutorialSystem completes navigation preflight first.
        int saved = replay ? 0 : PlayerPrefs.GetInt(ChapterKey, 0);
        // Keep old enum/save values stable; the removed multi-slot chapter resumes at Grill basics.
        if (saved == (int)Chapter.Parallel) saved = (int)Chapter.Burger;
        // A farewell is not a saved storage transaction. Resume its chapter from purchase.
        if (saved == (int)Chapter.Complete) saved = (int)Chapter.Restock;
        int index = Array.FindIndex(lessons, l => (int)l.chapter == saved);
        tutorial.StartAtStep(Mathf.Max(0, index));
    }
    private Recipe Runtime(Recipe recipe) => recipe != null && recipes.TryGetValue(recipe.ProductId, out var clone) ? clone : null;
    private void PrepareStep(TutorialSystem.TutorialStep step)
    {
        int index = tutorial.CurrentStepIndex;
        if (index < 0 || index >= lessons.Length) { Fail("The lesson definition is missing."); return; }
        current = lessons[index];
        ClearWaitingStatus();
        waitingSeconds = 0;
        lastGuidanceSource = null;
        lastGuidanceTarget = null;
        restockSource = null; restockShelf = null; restockExit = null;
        targetMissingSeconds = 0;
        failure = null;
        status.text = "";
        restart.gameObject.SetActive(false); skip.gameObject.SetActive(false);
        confirmation.SetActive(false);
        if (chapter != current.chapter) SetupChapter(current.chapter);
        if (failure != null) return;
        if (current.chapter == Chapter.Sandwiches && current.action == ActionKind.Protein)
            EnsureWork(Runtime(current.recipe), 1);
        if (current.chapter == Chapter.Restock && (current.action == ActionKind.RestockOpen || current.control == "Restock"))
            view.ShowTutorialRestock();
        if (current.chapter == Chapter.Hygiene && !cleanPrompted)
        {
            cleanPrompted = true;
            HygieneManager.Instance?.BeginTutorialKitchenCleaning();
        }
        // Explanations keep their informational focus. Suppression begins after handoff.
        tutorial.SetExternalGuidanceSuppressed(false);
        if (lobbyHUD == null) lobbyHUD = LobbyHUDRedesign.Instance;
        lobbyHUD?.RefreshVisibility();
        UpdateNavigation();
    }
    private void SetupChapter(Chapter value)
    {
        settingUp = true;
        bool reconstruct = chapter == null;
        chapter = value;
        if (ticket != null && ticket.submitted) State.Release(ticket.number, true);
        work.Clear(); ticket = null;
        cleaningStarted = cleanPrompted = false;
        if (!replay && !tutorial.IsDebugSession)
        { PlayerPrefs.SetInt(ChapterKey, (int)(value == Chapter.Complete ? Chapter.Restock : value)); PlayerPrefs.Save(); }
        // Only a checkpoint/retry reconstructs state. Ordinary chapter boundaries
        // keep the open station, prepared supplies and real pickup presentation.
        if (reconstruct) kitchen.ResetState();
        State.InteractionFilter = Permit;
        State.HoldPlayerStations = true;
        State.deadlineSeconds = float.MaxValue;
        State.playerBatchSize = 3;
        foreach (var recipe in day.Catalog.Products)
        {
            recipe.availableOnMenu = new[] { burger, fries, chicken, fish, drink }.Any(r => r != null && r.ProductId == recipe.ProductId);
            MenuAvailabilityManager.Instance?.SetProductAvailable(recipe, recipe.availableOnMenu);
        }
        if (reconstruct)
        {
            InventoryManager.Instance.ResetStock();
            foreach (var item in day.Catalog.Ingredients)
                InventoryManager.Instance.AddStock(item.itemType, stockPerIngredient);
        }
        if (value == Chapter.Burger || value == Chapter.Burn) EnsureWork(Runtime(burger), 1);
        if (value == Chapter.Sandwiches && !reconstruct)
        {
            // Hand the replacement burger to staff through normal ownership, so
            // it cannot steal the sandwich's first bun candidate on the prep table.
            State.Enter(FastFoodStationMode.None); State.Enter(FastFoodStationMode.Grill);
        }
        if (value == Chapter.Serving || reconstruct && value == Chapter.Fries)
        {
            foreach (var r in new[] { Runtime(burger), Runtime(fries) })
                if (reconstruct && (value == Chapter.Serving || r == Runtime(burger)) && State.ReadyStock(r) == 0)
                    State.RestoreReady(r, 1); // Only reconstructed prerequisites, never a player exercise.
            if (value == Chapter.Serving) CreateTicket(new[] { Runtime(burger), Runtime(fries), Runtime(drink) });
        }
        if (value == Chapter.Restock) PrepareRestock();
        if (value == Chapter.Practice) { practiceStage = 0; lastPracticeObjective = null; }
        if (reconstruct && value != Chapter.Briefing && value != Chapter.Complete)
        {
            kitchen.EnterKitchen();
            if (current.control != "Enter Grill")
            {
                if (value == Chapter.Serving) view.EnterFry();
                else if (value == Chapter.Practice || value == Chapter.Hygiene || value == Chapter.Restock) view.EnterAssembler();
                else view.EnterGrill();
            }
        }
        settingUp = false;
    }
    private void EnsureWork(Recipe recipe, int count)
    {
        if (recipe == null) { Fail("A training recipe is missing."); return; }
        int completed = State.Portions.Count(p => p.order < 0 && p.recipe == recipe && p.stage == FastFoodCookingStage.Complete);
        State.EnsureReserve(new[] { recipe }, completed + count);
        foreach (var p in State.Portions.Where(p => p.order < 0 && p.recipe == recipe && p.stage != FastFoodCookingStage.Complete))
            if (!work.Contains(p)) work.Add(p);
    }
    private FastFoodCookingState.Portion Expected()
    {
        var recipe = Runtime(current?.recipe);
        return work.Where(p => p.recipe == recipe).ElementAtOrDefault(current != null ? current.portion : 0);
    }
    private ItemData ExpectedItem()
    {
        var r = Runtime(current?.recipe);
        if (r == null) return null;
        return current.action == ActionKind.Load ? FastFoodCookingState.Steps(r).FirstOrDefault()?.item :
            current.action == ActionKind.Assemble ? FastFoodCookingState.AssemblySteps(r).ElementAtOrDefault(current.ingredient)?.item : null;
    }
    public bool AllowsStation(FastFoodStationMode mode) => settingUp || current == null ||
        tutorial.IsWaitingForGameplayAction && (current.action == ActionKind.Practice ||
        current.action == ActionKind.Station && (FreeNavigation || current.station == mode));
    public bool AllowsDrag(FastFoodCookingDragHandle handle)
    {
        if (current == null || failure != null || !tutorial.IsWaitingForGameplayAction || tutorial.IsPresentationBusy || LobbyPauseMenu.IsAnyOpen) return false;
        if (current.action == ActionKind.Practice) return true;
        if (current.action == ActionKind.Load || current.action == ActionKind.Assemble)
            return handle.item == ExpectedItem() && (current.action != ActionKind.Load || !handle.storedProtein);
        if (current.action == ActionKind.Discard) return handle.portion == Expected();
        return current.action == ActionKind.Place && handle.portion?.recipe == Runtime(current.recipe);
    }
    private bool Permit(string operation, FastFoodCookingState.Portion p, ItemData item, int slot)
    {
        if (settingUp) return true;
        if (!Initialized || current == null || failure != null || !tutorial.IsWaitingForGameplayAction || tutorial.IsPresentationBusy ||
            LobbyPauseMenu.IsAnyOpen || confirmation.activeSelf) return false;
        if (current.action == ActionKind.Practice) return true;
        var expected = Expected();
        switch (current.action)
        {
            case ActionKind.Load: return operation == "Load" && p == expected && item == ExpectedItem() && slot == current.slot;
            case ActionKind.Assemble: return operation == "Load" && p == expected && item == ExpectedItem() && slot == current.slot;
            case ActionKind.Collect: return operation == "Collect" && p == expected;
            case ActionKind.Discard: return operation == "Discard" && p == expected;
            case ActionKind.Place: return (operation == "Drag" || operation == "Place") && p != null && ticket != null &&
                ticket.portions.Contains(p) && p.recipe == Runtime(current.recipe);
            case ActionKind.Serve: return operation == "Serve" && ticket != null && slot == ticket.number;
            default: return false;
        }
    }
    public void TickKitchen(FastFoodCookingController controller)
    {
        bool readingWait = tutorial.IsWaitingForNext && current != null &&
            (current.action == ActionKind.Ready || current.action == ActionKind.Protein);
        if (!Initialized || controller != kitchen || current == null || leaving || failure != null ||
            tutorial.IsPresentationBusy && !tutorial.IsAwaitingExternalTarget && !readingWait || LobbyPauseMenu.IsAnyOpen || confirmation.activeSelf || Time.timeScale <= 0) return;
        // Waiting for a navigation target must not let staff start the player's
        // provisioned exercise before Enter() claims it. View/layout transitions
        // update independently of the cooking ledger.
        if (current.action == ActionKind.Station || current.action == ActionKind.Open) return;
        if (!tutorial.IsAwaitingExternalTarget && current.action == ActionKind.Explain) return;
        bool protectReady = current.action != ActionKind.Burn && current.chapter != Chapter.Practice;
        if (protectReady)
            foreach (var p in work.Where(p => p.stage == FastFoodCookingStage.Ready)) p.elapsed = 0;
        State.Tick(Time.deltaTime, HygieneManager.HoldNewCooking, HygieneManager.KitchenPaused);
        foreach (var batch in State.Batches) batch.elapsed = 0;
        foreach (var t in State.Tickets) t.elapsed = 0;
        // Only the explicit burn lesson should continue heating a ready patty during a forced wait.
        if (protectReady)
            foreach (var p in work.Where(p => p.stage == FastFoodCookingStage.Ready)) p.elapsed = 0;
    }
    private void Update()
    {
        bool paused = LobbyPauseMenu.IsAnyOpen;
        if (presentationCanvases != null && paused != presentationPaused)
        {
            presentationPaused = paused;
            foreach (var canvas in presentationCanvases) if (canvas != null) canvas.enabled = !paused;
        }
        if (!Initialized || current == null || leaving || resetPending) return;
        if (failure == null && current.control == "World:Rack" && !paused)
            view.HoldTutorialRackView();
        if (!tutorial.IsWaitingForGameplayAction || LobbyPauseMenu.IsAnyOpen || confirmation.activeSelf) return;
        if (failure != null) return;
        if (current.chapter == Chapter.Fries && State.Mode == FastFoodStationMode.Fry && work.Count == 0)
            EnsureWork(Runtime(fries), 1); // Claim the station before issuing its player exercise.
        if (current.action == ActionKind.Clean && !cleanPrompted)
        { cleanPrompted = true; HygieneManager.Instance?.BeginTutorialKitchenCleaning(); }
        bool done = Satisfied();
        if (done) { ClearWaitingStatus(); tutorial.NotifyAction(current.id); return; }
        if (current.action == ActionKind.RestockOpen && computerTravelRequested &&
            ManagerPlayer.Active?.Movement.CurrentTarget != computerTask && Computer?.IsOpen != true)
        { Fail("The trip to the computer was interrupted. Retry this chapter."); return; }
        if (current.action == ActionKind.Delivery && purchasedOrder != null && DateTime.UtcNow.Ticks >= purchasedOrder.deliveryReadyUtcTicks)
        {
            // Count only time actually spent awaiting the parked truck, not time reading or paused.
            waitingSeconds += Time.deltaTime;
            if (waitingSeconds > dependencyAllowance) { Fail("The delivery truck could not arrive. Retry this chapter."); return; }
        }
        if (current.action == ActionKind.Freezer && RestockFlowCoordinator.Instance?.IsTransitioning == true)
        {
            waitingSeconds += Time.unscaledDeltaTime;
            if (waitingSeconds > dependencyAllowance) { Fail("The freezer could not open. Retry this chapter."); return; }
        }
        tutorial.SetExternalGuidanceSuppressed(PassiveWait, PassiveWait);
        PresentWaitingStatus();
        if (current.action == ActionKind.Practice) UpdatePractice();
        if (current.action == ActionKind.Ready || current.action == ActionKind.Protein || current.action == ActionKind.Burn || current.action == ActionKind.Clean)
        {
            if (Time.timeScale > 0) waitingSeconds += Time.unscaledDeltaTime;
            float expected = current.action == ActionKind.Burn ? State.cookSeconds + State.overcookSeconds :
                current.action == ActionKind.Clean ? 120 : State.cookSeconds * 2;
            if (waitingSeconds > expected + dependencyAllowance) Fail("This lesson is waiting on a blocked task. Retry this chapter.");
        }
        if (current.action != ActionKind.Burn && current.action != ActionKind.Discard &&
            current.action != ActionKind.Practice && work.Any(p => p.player && p.stage == FastFoodCookingStage.Burnt))
            Fail("That patty burned. Retry this chapter to cook a fresh batch.");
    }
    private bool Satisfied()
    {
        var p = Expected();
        switch (current.action)
        {
            case ActionKind.Open: return view.IsOpen;
            case ActionKind.Station: return State.Mode == current.station && KitchenCamera != null && view.TutorialCameraSettled;
            case ActionKind.Load: return p != null && p.slot == current.slot && p.stage == FastFoodCookingStage.Cooking;
            case ActionKind.Ready: return p != null && p.stage == FastFoodCookingStage.Ready;
            case ActionKind.Collect: return p != null && (p.proteinReady || p.stage == FastFoodCookingStage.Complete);
            case ActionKind.Protein: return p != null && p.proteinReady && State.PrepReady && view.TutorialCameraSettled;
            case ActionKind.Assemble: return p != null && (p.stage == FastFoodCookingStage.Complete
                ? p.prepSlot < 0 && view.TutorialPrepTransferred
                : p.prepSlot == current.slot && p.ingredientStep > current.ingredient);
            case ActionKind.Place: return ticket != null && ticket.portions.Any(x => x.recipe == Runtime(current.recipe) && x.placed);
            case ActionKind.Serve: return ticket != null && ticket.submitted;
            case ActionKind.Burn: return p != null && p.stage == FastFoodCookingStage.Burnt;
            case ActionKind.Discard: return p != null && p.stage == FastFoodCookingStage.Waiting && p.slot < 0;
            case ActionKind.RestockOpen: return computerTravelRequested && Computer != null && Computer.IsOpen;
            case ActionKind.Purchase: return PurchaseSatisfied();
            case ActionKind.Delivery: return purchasedOrder != null && purchasedOrder.state == RestockOrderState.Delivered && Truck != null && Truck.IsParked;
            case ActionKind.Truck: return CollectionControl != null;
            case ActionKind.CollectDelivery: return purchasedOrder != null && purchasedOrder.state == RestockOrderState.Collected &&
                RestockOrderManager.Instance.GetHotbarContainerCount(restockItem.requiredStorage) == 1;
            case ActionKind.Freezer: return RestockFlowCoordinator.Instance?.IsRestockRoomOpen == true &&
                !RestockFlowCoordinator.Instance.IsTransitioning && RestockFlowCoordinator.Instance.ActiveStorageRoom == restockItem.requiredStorage &&
                purchasedOrder != null && purchasedOrder.state == RestockOrderState.Collected && SourceForDelivery() != null;
            case ActionKind.Store: return restockItem != null && purchasedOrder?.state == RestockOrderState.Stored &&
                purchasedOrder.lines.Any(l => l.itemID == restockItem.StableItemId && l.orderedContainers == 1 && l.storedContainers == 1) &&
                RestockOrderManager.Instance.StoredContainers.Any(box => box.itemID == restockItem.StableItemId &&
                    box.storageType == restockItem.requiredStorage && !box.wrongStorage && !string.IsNullOrEmpty(box.shelfID));
            case ActionKind.RestockExit: return RestockFlowCoordinator.Instance != null &&
                !RestockFlowCoordinator.Instance.IsRestockRoomOpen && !RestockFlowCoordinator.Instance.IsTransitioning;
            case ActionKind.Clean:
                if (HygieneManager.Instance == null) return false;
                cleaningStarted |= HygieneManager.Instance.State.Cleaning;
                return cleaningStarted && !HygieneManager.Instance.State.Cleaning && !HygieneManager.Instance.State.decisionOpen;
            case ActionKind.Practice: return practiceStage == 5 && ticket != null && ticket.submitted;
            default: return false;
        }
    }
    private void ClearWaitingStatus()
    {
        if (waitingTimer != null) waitingTimer.gameObject.SetActive(false);
        if (status != null)
        {
            if (waitingStatusVisible) status.rectTransform.offsetMin = statusOffsetMin;
            if (status.text.Length > 0) status.text = "";
        }
        waitingStatusVisible = false;
        waitingActivityAngle = 0;
    }

    private void PresentWaitingStatus()
    {
        if (!PassiveWait) { ClearWaitingStatus(); return; }
        var portion = Expected(); // Same recipe/portion identity that Satisfied() observes.
        string message = TutorialInputTerminology.Resolve(tutorial.CurrentStep.Objective);
        if (current.action == ActionKind.Burn)
            message = portion?.stage == FastFoodCookingStage.Cooking ? "The patty is cooking…" :
                "Watch what happens if we leave it on the heat.";
        else if (current.action == ActionKind.RestockOpen) message = "Heading to the computer…";
        else if (current.action == ActionKind.Freezer) message = "Opening the freezer…";
        else if (current.action == ActionKind.Clean) message = "The kitchen is being cleaned…";
        if (status == null) return;
        bool changed = status.text != message;
        if (changed) status.text = message;
        // Hygiene already presents its real progress bar; never duplicate it here.
        if (current.action == ActionKind.Clean) return;
        if (waitingTimer == null)
        {
            if (waitingTimerTemplate == null)
            { Fail("The waiting indicator is missing. Retry this chapter."); return; }
            waitingTimer = Instantiate(waitingTimerTemplate, status.transform.parent);
            waitingTimer.name = "Tutorial waiting progress";
            waitingTimer.gameObject.SetActive(false);
            waitingTimer.transform.localScale = Vector3.one;
            waitingTimer.seconds.gameObject.SetActive(false);
            waitingTimer.caption.gameObject.SetActive(false);
            foreach (var graphic in waitingTimer.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
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
        bool measurable = portion != null && (current.action == ActionKind.Ready || current.action == ActionKind.Burn ||
            current.action == ActionKind.Protein) && portion.stage == FastFoodCookingStage.Cooking;
        bool overcooking = current.action == ActionKind.Burn && portion?.stage == FastFoodCookingStage.Ready;
        if (measurable || overcooking)
        {
            waitingTimer.ring.SetAngle(0);
            waitingTimer.Present(portion, State.cookSeconds, State.overcookSeconds, 1, false, Time.timeScale <= 0 || HygieneManager.KitchenPaused);
            if (overcooking)
            {
                // This lesson watches progress TOWARD burning, not time left to collect.
                waitingTimer.ring.SetAmount(portion.elapsed / Mathf.Max(.01f, State.overcookSeconds));
                waitingTimer.ring.color = waitingTimer.warningColor;
            }
        }
        else
        {
            // No invented countdown for delivery, travel, queueing or camera readiness.
            if ((Time.timeScale > 0 || current.action == ActionKind.Freezer) && !LevelOneUIAccessibility.ReducedMotion)
                waitingActivityAngle = (waitingActivityAngle + waitingActivitySpeed * Time.unscaledDeltaTime) % 360;
            waitingTimer.transform.localScale = Vector3.one;
            waitingTimer.ring.color = waitingTimer.cookingColor;
            waitingTimer.ring.SetAmount(.25f);
            waitingTimer.ring.SetAngle(waitingActivityAngle);
        }
    }

    private void CreateTicket(Recipe[] products)
    {
        if (!State.Accept(ticketNumber++, products)) { Fail("The training order could not be prepared. Retry the chapter."); return; }
        ticket = State.FindTicket(ticketNumber - 1);
        State.Activate(ticket.number);
        ticket.player = true; // Ownership only; food still needs its real cooking and placement transactions.
    }
    private void UpdatePractice()
    {
        if (practiceStage == 0 && work.Count == 0 && State.Mode == FastFoodStationMode.Grill)
            EnsureWork(Runtime(burger), 2);
        if (practiceStage == 0 && work.Count == 2 && work.All(p => p.stage == FastFoodCookingStage.Complete) && view.TutorialPrepTransferred)
        { practiceStage = 1; work.Clear(); }
        if (practiceStage == 1 && work.Count == 0 && State.Mode == FastFoodStationMode.Fry)
            EnsureWork(Runtime(fries), 1);
        if (practiceStage == 1 && work.Count == 1 && work[0].stage == FastFoodCookingStage.Complete)
        {
            practiceStage = 2;
            CreateTicket(new[] { Runtime(burger), Runtime(burger), Runtime(fries), Runtime(drink) });
        }
        if (practiceStage == 2 && ticket != null && ticket.submitted)
        {
            State.Release(ticket.number, true);
            work.Clear(); ticket = null; practiceStage = 3;
        }
        if (practiceStage == 3 && work.Count == 0 && State.Mode == FastFoodStationMode.Grill)
        {
            // Release the previous Fryer exercise's held ownership, without moving
            // the view. Staff can now cook the two proteins; Grill stays player-owned.
            State.Enter(FastFoodStationMode.None); State.Enter(FastFoodStationMode.Grill);
            EnsureWork(Runtime(chicken), 1); EnsureWork(Runtime(fish), 1);
        }
        if (practiceStage == 3 && work.Count == 2 && work.All(p => p.stage == FastFoodCookingStage.Complete) && view.TutorialPrepTransferred)
        {
            practiceStage = 5;
            CreateTicket(new[] { Runtime(chicken), Runtime(fish), Runtime(drink) });
        }
        FastFoodStationMode destination = PracticeDestination();
        int ready = work.Count(p => p.stage == FastFoodCookingStage.Complete);
        string task = practiceStage == 0 ? "Prepare 2 burgers. "+ready+"/2 ready." :
            practiceStage == 1 ? "Cook and collect 1 portion of fries. "+ready+"/1 ready." :
            practiceStage == 3 ? "Assemble the chicken and fish sandwiches. "+ready+"/2 ready." :
            "Complete and serve the order.";
        if (State.Mode != destination)
            task = "Go to "+(destination == FastFoodStationMode.Fry ? "Fryer" : destination.ToString())+". "+task;
        task = "Let's finish this order. " + task;
        if (task != lastPracticeObjective) { lastPracticeObjective = task; tutorial.SetObjective(task); }
    }
    private FastFoodStationMode PracticeDestination() => practiceStage == 0 || practiceStage == 3
        ? FastFoodStationMode.Grill : practiceStage == 1 ? FastFoodStationMode.Fry : FastFoodStationMode.Assembler;
    private RectTransform NavigationTarget(FastFoodStationMode destination)
    {
        if (!view.IsOpen) return ResolveUI("FF.Enter");
        return view.TutorialStationArrow(destination);
    }
    private FastFoodCookingState.Portion PlacementPortion() =>
        ticket?.portions.FirstOrDefault(p => p.recipe == Runtime(current?.recipe) && !p.placed);
    public bool TryGetTargetBounds(Transform target, out Bounds bounds, out Transform space)
    {
        if (target != null && target == restockShelf && target.TryGetComponent<ShelfGrid>(out var grid))
        {
            space = target;
            bounds = new Bounds(target.InverseTransformPoint(grid.GetCellWorldPosition(restockColumn, restockRow)),
                new Vector3(grid.cellWidth, .02f, grid.cellDepth));
            return true;
        }
        return view.TryTutorialBounds(target, current?.action == ActionKind.Place ? PlacementPortion() : null, out bounds, out space);
    }
    public RectTransform ResolveUI(string key)
    {
        if (key == "FF.Source") return Source() as RectTransform;
        if (key == "FF.Enter")
        {
            if (lobbyHUD == null) lobbyHUD = LobbyHUDRedesign.Instance;
            return lobbyHUD != null && lobbyHUD.KitchenButton != null ? lobbyHUD.KitchenButton.transform as RectTransform : null;
        }
        if (key == "FF.Control")
        {
            string control = current?.control;
            if (control != null && control.StartsWith("Computer:", StringComparison.Ordinal)) return ComputerControl(control);
            if (control == "Get Orders") return CollectionControl;
            if (control == "Pause")
            {
                // Pause is a sibling HUD branch, not a child of LobbyHUDRedesign.
                // Read the same authored reference as the real pause controller.
                var button = LobbyHUDRoot.Instance?.PauseMenuView?.PauseButton;
                return button != null && button.gameObject.activeInHierarchy ? button.transform as RectTransform : null;
            }
            if (control == "Task")
            {
                // This is the exact path bound by PlayerTaskHUD.EnsureCombinedBinding.
                var controls = LobbyHUDRedesign.Instance;
                var button = controls != null ? controls.transform.Find("SafeArea/TaskButton") : null;
                return button != null && button.gameObject.activeInHierarchy ? button as RectTransform : null;
            }
            if (control == "StationArrow") return NavigationTarget(current.station);
            if (control == "Raw" || control == "ProteinCell")
                return view.TutorialSource(FastFoodCookingState.Steps(Runtime(current.recipe)).FirstOrDefault()?.item,
                    null, control == "Raw") as RectTransform;
            if (control == "Clean Now")
            {
                if (hygieneDialogue == null) hygieneDialogue = FindFirstObjectByType<HygieneDialogue>();
                return hygieneDialogue?.TutorialCleanControl;
            }
            if (current?.action == ActionKind.RestockExit)
            {
                if (restockExit == null || !restockExit.gameObject.activeInHierarchy)
                    restockExit = FindObjectsByType<Button>(FindObjectsSortMode.None)
                        .FirstOrDefault(button => button.name == "ExitButton" && button.gameObject.scene.name == "RestockScene");
                return restockExit != null ? restockExit.transform as RectTransform : null;
            }
            return view.TutorialControl(control);
        }
        if (key == "FF.Discard") return view.TutorialDiscard;
        return null;
    }
    private Transform Source()
    {
        if (current == null) return null;
        if (current.action == ActionKind.Store)
        {
            if (restockItem == null) return null;
            var source = SourceForDelivery();
            if (source != null) restockHUD = source.GetComponentInParent<RestockFlowHUD>();
            return source != null ? source.transform : null;
        }
        var p = current.action == ActionKind.Place ? PlacementPortion() : Expected();
        return view.TutorialSource(ExpectedItem(), p, current.action == ActionKind.Load);
    }
    public Transform ResolveWorld(string key)
    {
        if (current == null) return null;
        if (key == "FF.Subject")
        {
            if (current.control == "World:Truck") return Truck != null ? Truck.transform : null;
            if (current.control == "World:Freezer")
            {
                if (freezerEntrance == null) freezerEntrance = FindObjectsByType<RestockStockRoomEntrance>(FindObjectsSortMode.None)
                    .FirstOrDefault(e => e.isActiveAndEnabled && e.gameObject.scene == gameObject.scene && e.StorageType == restockItem.requiredStorage);
                return freezerEntrance != null ? freezerEntrance.transform : null;
            }
            return current.control == "World:Food" ? Source() :
                view.TutorialSubject(current.control.Substring("World:".Length), current.slot);
        }
        if (key == "FF.SourceWorld") return Source();
        if (key != "FF.Target") return null;
        if (current.action == ActionKind.Store)
        {
            if (restockItem == null) return null;
            if (restockShelf == null || !restockShelf.gameObject.activeInHierarchy ||
                !restockShelf.GetComponent<ShelfGrid>().IsCellFree(restockColumn, restockRow) || !Visible(restockShelf))
            {
                restockShelf = null;
                foreach (var grid in FindObjectsByType<ShelfGrid>(FindObjectsSortMode.None))
                    if (grid.isActiveAndEnabled && grid.StorageType == restockItem.requiredStorage && grid.gameObject.scene.name == "RestockScene" &&
                        ChooseVisibleCell(grid)) { restockShelf = grid.transform; break; }
            }
            return restockShelf;
        }
        if (current.action == ActionKind.Collect && State.Mode == FastFoodStationMode.Fry) return view.TutorialBasket(Expected()?.slot ?? current.slot);
        if (current.action == ActionKind.Collect) return Source();
        return view.TutorialTarget(current.action == ActionKind.Assemble || current.action == ActionKind.Place ? 1 :
            current.action == ActionKind.Discard ? 2 : 0, current.slot);
    }
    private bool ChooseVisibleCell(ShelfGrid grid)
    {
        var camera = TutorialWorldTargetGeometry.ResolveCamera(grid.transform, null);
        if (camera == null) return false;
        for (int c = 0; c < grid.columns; c++)
            for (int r = 0; r < grid.rows; r++)
            {
                Vector3 point = camera.WorldToScreenPoint(grid.GetCellWorldPosition(c, r));
                if (!grid.IsCellFree(c,r) || point.z <= 0 || !Screen.safeArea.Contains(point)) continue;
                restockColumn = c; restockRow = r; return true;
            }
        return false;
    }
    private bool Visible(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        if (!(target is RectTransform rect))
            return TutorialWorldTargetGeometry.TryGetScreenRect(target,
                TutorialWorldTargetGeometry.ResolveCamera(target, KitchenCamera), out var worldRect) &&
                worldRect.width > 1 && worldRect.height > 1 && worldRect.Overlaps(Screen.safeArea);
        var canvas = rect.GetComponentInParent<Canvas>();
        if (canvas != null && !canvas.isActiveAndEnabled) return false;
        foreach (var group in rect.GetComponentsInParent<CanvasGroup>())
        {
            if (group.enabled && group.alpha <= .01f) return false;
            if (group.ignoreParentGroups) break;
        }
        Rect visible = ScreenRect(rect);
        if (visible.width <= 1 || visible.height <= 1 || !visible.Overlaps(Screen.safeArea)) return false;
        // A hotbar cell outside its real viewport is not a usable spotlight.
        // Informational zero-stock/disabled cells remain valid; only clipping matters.
        foreach (var mask in rect.GetComponentsInParent<RectMask2D>())
            if (mask.isActiveAndEnabled && !IntersectVisible(ref visible, ScreenRect(mask.rectTransform))) return false;
        foreach (var mask in rect.GetComponentsInParent<Mask>())
            if (mask.isActiveAndEnabled && !IntersectVisible(ref visible, ScreenRect(mask.rectTransform))) return false;
        return true;
    }
    private static bool IntersectVisible(ref Rect rect, Rect clip)
    {
        rect = Rect.MinMaxRect(Mathf.Max(rect.xMin, clip.xMin), Mathf.Max(rect.yMin, clip.yMin),
            Mathf.Min(rect.xMax, clip.xMax), Mathf.Min(rect.yMax, clip.yMax));
        return rect.width > 1 && rect.height > 1;
    }
    private Rect ScreenRect(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        rect.GetWorldCorners(uiCorners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var corner in uiCorners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    public bool PresentationReady(TutorialSystem.TutorialStep step)
    {
        if (failure != null || step == null || step != tutorial.CurrentStep || current == null) return false;
        // Narrative lines have no geometry prerequisite, even during a view transition.
        if (current.action == ActionKind.Explain && string.IsNullOrEmpty(step.UITargetKey) &&
            string.IsNullOrEmpty(step.WorldTargetKey) && step.UIFocusTarget == null && step.HighlightTarget == null)
            return true;
        if (view.IsOpen && !view.TutorialCameraSettled) return false;
        if ((current.action == ActionKind.Store || current.action == ActionKind.RestockExit) &&
            RestockFlowCoordinator.Instance?.IsTransitioning == true) return false;
        if (current.action == ActionKind.Load && (view.TutorialPreparing || !State.PlayerWork.Contains(Expected()))) return false;
        if (current.action == ActionKind.Assemble && (!State.PrepReady || !view.TutorialPreparing)) return false;
        if (current.control == "World:Prep" && !view.TutorialPreparing) return false;
        if (current.control == "World:Cooking" && view.TutorialPreparing) return false;
        if (!string.IsNullOrEmpty(step.UITargetKey) && !Visible(ResolveUI(step.UITargetKey))) return false;
        if (!string.IsNullOrEmpty(step.WorldTargetKey) && !Visible(ResolveWorld(step.WorldTargetKey))) return false;
        if (step.HintMode == TutorialSystem.TutorialHintMode.Drag && !Visible(Source())) return false;
        return true;
    }
    public bool ActionGuidanceSuppressed => PassiveWait;
    public void RememberGuidance()
    {
        lastGuidanceSource = Source();
        lastGuidanceTarget = ResolveWorld(tutorial.CurrentStep.WorldTargetKey);
    }
    public void ReportTargetFailure(TutorialSystem.TutorialStep step)
    {
        // A cancelled lookup must not report against a later lesson or a chapter retry.
        if (failure != null || resetPending || leaving || step == null || tutorial == null ||
            step != tutorial.CurrentStep || current == null || current.id != step.Id) return;
        string activeView = RestockFlowCoordinator.Instance?.IsRestockRoomOpen == true ? "storage/" + RestockFlowCoordinator.Instance.ActiveStorageRoom :
            Computer != null && Computer.IsOpen ? "computer" : view == null ? "uninitialized" : !view.IsOpen ? "restaurant" :
            State.Mode == FastFoodStationMode.None ? "station selection" :
            State.Mode + (view.TutorialPreparing ? "/prep" : "/cooking") +
            (view.TutorialCameraSettled ? " (settled)" : " (transitioning)");
        var ui = ResolveUI(step.UITargetKey);
        var world = ResolveWorld(step.WorldTargetKey);
        Debug.LogError($"[FastFoodTutorial] Target unavailable: chapter={current.chapter} ({(int)current.chapter}), " +
            $"lesson={current.id}, action={current.action}, control='{current.control}', " +
            $"uiKey='{step.UITargetKey}', worldKey='{step.WorldTargetKey}', " +
            $"resolvedUI='{(ui != null ? ui.name : "<none>")}' (visible={Visible(ui)}), " +
            $"resolvedWorld='{(world != null ? world.name : "<none>")}' (visible={Visible(world)}), " +
            $"activeView={activeView}, playerWork={State?.PlayerWork.Count ?? 0}, " +
            $"expectedStage={Expected()?.stage.ToString() ?? "<none>"}.", this);
        Fail("The highlighted lesson target is unavailable. Retry this chapter.");
    }
    public void RefreshGuidance()
    {
        if (current == null || failure != null || leaving || resetPending || LobbyPauseMenu.IsAnyOpen ||
            (!tutorial.IsWaitingForGameplayAction && !tutorial.IsWaitingForNext) ||
            (tutorial.IsPresentationBusy && !tutorial.IsWaitingForNext)) return;
        if (tutorial.IsWaitingForNext)
        {
            var explaining = tutorial.CurrentStep;
            if (!PresentationReady(explaining))
            {
                tutorial.ReacquireExternalPresentation();
                return;
            }
            tutorial.RefreshExternalGuidance(ResolveUI(explaining.UITargetKey), ResolveWorld(explaining.WorldTargetKey));
            return;
        }
        if (current.action == ActionKind.Practice)
        {
            var arrow = State.Mode == PracticeDestination() ? null : NavigationTarget(PracticeDestination());
            tutorial.RefreshExternalGuidance(arrow, null);
            return;
        }
        if (PassiveWait || Dragging || tutorial.IsGuidedTravelActive) return;
        bool ready = PresentationReady(tutorial.CurrentStep);
        if (!ready)
        {
            tutorial.RefreshExternalGuidance(null, null);
            targetMissingSeconds += Time.unscaledDeltaTime;
            if (targetMissingSeconds > dependencyAllowance) ReportTargetFailure(tutorial.CurrentStep);
            return;
        }
        targetMissingSeconds = 0;
        var step = tutorial.CurrentStep;
        var source = Source();
        var world = ResolveWorld(step.WorldTargetKey);
        tutorial.RefreshExternalGuidance(ResolveUI(step.UITargetKey), world, source != lastGuidanceSource || world != lastGuidanceTarget);
        lastGuidanceSource = source; lastGuidanceTarget = world;
    }
    private void UpdateNavigation()
    {
        foreach (var pair in navigationStates) if (pair.Key != null) pair.Key.interactable = pair.Value;
        navigationStates.Clear();
        if (current.action == ActionKind.Practice || FreeNavigation) return;
        var required = current.action == ActionKind.Station ? NavigationTarget(current.station) : null;
        foreach (string name in new[] { "Stations", "Exit Kitchen", "Previous Station", "Next Station" })
        {
            var button = view.TutorialControl(name)?.GetComponent<Button>();
            if (button == null) continue;
            navigationStates[button] = button.interactable;
            button.interactable = required != null && button.transform == required;
        }
    }
    private void PrepareRestock()
    {
        // This runs only after TutorialDayContext has captured the career and
        // installed disposable runtime data. Kitchen practice stock (80 units
        // per ingredient) is not a shelf-capacity fixture for this chapter.
        if (!Active || day == null || !day.IsReady)
        { Fail("Training storage is not isolated. Retry this chapter."); return; }
        restockItem = FastFoodCookingState.Steps(Runtime(fries))[0].item;
        Computer?.CloseComputer(); // Only the disposable tutorial's scene-local cart.
        cartPanel = null; purchasedOrder = null; computerTravelRequested = false;
        InventoryManager.Instance.ResetStock();
        RestockOrderManager.EnsureInstance().ApplySaveData(new GameSaveData());
        var purchasing = new GameSaveData { currentDay = 1, currentPhase = 0,
            money = Mathf.Max(5000, CasualDiningPolishManager.GetCurrentBoxCostOrBase(restockItem)) };
        GameFlowManager.Instance?.ApplySaveData(purchasing);
        MoneyManager.Instance?.ApplySaveData(purchasing);
        DailyFinanceBridge.Instance?.ResetDay();
        DailyRevenueTracker.Instance?.ResetForNewDay();
    }

    public bool RouteRestockToComputer()
    {
        if (!Active || current?.action != ActionKind.RestockOpen) return false;
        if (!tutorial.IsWaitingForGameplayAction || tutorial.IsPresentationBusy) return true;
        computerTask = FindObjectsByType<ManagementComputerStation>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.gameObject.scene == gameObject.scene);
        if (computerTask == null || ManagerPlayer.Active?.Movement == null)
        { Fail("The computer cannot be reached. Retry this chapter."); return true; }
        view.Exit();
        computerTravelRequested = true;
        ManagerPlayer.Active.Movement.UI_MoveTo(computerTask);
        return true;
    }

    private RectTransform ComputerControl(string control)
    {
        var computer = Computer;
        if (computer == null || !computer.IsOpen) return null;
        if (control == "Computer:Restock") return bindings.ResolveUI("RestockButton");
        if (control == "Computer:Exit") return bindings.ResolveUI("ManagementExit");
        if (control == "Computer:CloseApp") return computer.AppWindow?.CloseButton?.transform as RectTransform;
        var panel = CartPanel;
        if (panel == null) return null;
        if (control == "Computer:FriesCard") return panel.AddControlFor(restockItem)?.GetComponentInParent<ManagementComputerCatalogCardUI>()?.transform as RectTransform;
        return (control == "Computer:Food" ? panel.FoodTab : control == "Computer:Fries" ? panel.AddControlFor(restockItem) :
            control == "Computer:Checkout" || control == "Computer:Order" ? panel.CartButton : null)?.transform as RectTransform;
    }

    private bool PurchaseSatisfied()
    {
        switch (current.control)
        {
            case "Computer:Restock": return CartPanel != null;
            case "Computer:Food": return CartPanel != null && CartPanel.ActiveCategory == MenuProductCategory.Food;
            case "Computer:Fries": return CartPanel != null && CartPanel.CartBoxes == 1 && CartPanel.QuantityFor(restockItem) == 1;
            case "Computer:Checkout": return CartPanel != null && CartPanel.IsReview;
            case "Computer:Order":
                purchasedOrder = RestockOrderManager.Instance.Orders.FirstOrDefault(o => o.lines.Count == 1 &&
                    o.lines[0].itemID == restockItem.StableItemId && o.lines[0].orderedContainers == 1 && o.totalCost > 0 &&
                    (o.state == RestockOrderState.InDelivery || o.state == RestockOrderState.Ordered || o.state == RestockOrderState.Delivered));
                return purchasedOrder != null;
            case "Computer:CloseApp": return Computer != null && Computer.IsOpen && !Computer.AppWindow.gameObject.activeInHierarchy;
            case "Computer:Exit": return Computer != null && !Computer.IsOpen;
            default: return false;
        }
    }

    private RestockHotbarSlotUI SourceForDelivery()
    {
        if (restockSource == null || !restockSource.gameObject.activeInHierarchy)
            restockSource = FindObjectsByType<RestockHotbarSlotUI>(FindObjectsSortMode.None)
                .FirstOrDefault(s => s.Item != null && s.Item.StableItemId == restockItem.StableItemId && s.GetComponentInParent<RestockFlowHUD>() != null);
        return restockSource;
    }

    public string FocusGroup
    {
        get
        {
            string owner = RestockFlowCoordinator.Instance?.IsRestockRoomOpen == true ? "Storage" :
                Computer != null && Computer.IsOpen ? "Computer" : !view.IsOpen ? "Restaurant" :
                State.Mode == FastFoodStationMode.None ? "Selection" : State.Mode + (view.TutorialPreparing ? "/Prep" : "/Cooking");
            return gameObject.scene.handle + ":" + owner;
        }
    }

    private void CancelOwnedTravel()
    {
        if (restockHUD != null)
        {
            restockHUD.CollectionControl?.GetComponent<RestockHoldButton>()?.Begin(null);
            restockHUD.HideHold();
            restockHUD.CancelPickupAnimation();
        }
        var movement = ManagerPlayer.Active?.Movement;
        if (movement != null && movement.CurrentTarget != null &&
            (movement.CurrentTarget == computerTask || movement.CurrentTarget == deliveryTruck || movement.CurrentTarget == freezerEntrance))
            movement.CancelLockedTask();
        computerTravelRequested = false;
    }
    public void RestartChapter() { if (!resetPending) StartCoroutine(RestartRoutine()); }
    private IEnumerator RestartRoutine()
    {
        resetPending = true;
        ClearWaitingStatus();
        CancelOwnedTravel();
        tutorial.ReleaseFastFoodPresentation();
        if (!Initialized)
        {
            yield return Initialize();
            resetPending = false;
            yield break;
        }
        if (RestockFlowCoordinator.Instance?.IsTransitioning == true)
        {
            float until = Time.realtimeSinceStartup + dependencyAllowance;
            while (RestockFlowCoordinator.Instance.IsTransitioning && Time.realtimeSinceStartup < until) yield return null;
            if (RestockFlowCoordinator.Instance.IsTransitioning)
            { resetPending = false; Fail("Wait for storage to finish opening before retrying."); yield break; }
        }
        if (RestockFlowCoordinator.Instance?.IsRestockRoomOpen == true)
        {
            RestockFlowCoordinator.Instance.ExitRestockRoom();
            float until = Time.realtimeSinceStartup + dependencyAllowance;
            while (RestockFlowCoordinator.Instance.IsTransitioning && Time.realtimeSinceStartup < until) yield return null;
            if (RestockFlowCoordinator.Instance.IsRestockRoomOpen) { resetPending = false; Fail("Close storage before retrying."); yield break; }
        }
        if (HygieneManager.Instance != null) HygieneManager.Instance.ResetForShift();
        var saved = chapter ?? Chapter.Briefing;
        if (saved == Chapter.Complete) saved = Chapter.Restock;
        chapter = null; failure = null;
        int index = Array.FindIndex(lessons, l => l.chapter == saved);
        tutorial.StartAtStep(Mathf.Max(0, index));
        resetPending = false;
    }
    private void Fail(string message)
    {
        ClearWaitingStatus();
        failure = message;
        if (status != null) status.text = message;
        tutorial?.SetExternalGuidanceSuppressed(true);
        restart.gameObject.SetActive(true); skip.gameObject.SetActive(true);
    }
    private void Completed()
    {
        if (!replay && !tutorial.IsDebugSession)
        {
            PlayerPrefs.DeleteKey(ChapterKey);
            PlayerPrefs.DeleteKey(SkippedKey);
            PlayerPrefs.Save();
        }
        status.text = "";
        finish.gameObject.SetActive(false);
        skip.gameObject.SetActive(false); restart.gameObject.SetActive(false);
        FinishTutorial(); // The final Big Boss line has been dismissed with its gesture consumed.
    }
    private void SkipTutorial()
    {
        if (!replay) { PlayerPrefs.SetInt(SkippedKey, 1); PlayerPrefs.DeleteKey(ChapterKey); PlayerPrefs.Save(); }
        FinishTutorial();
    }
    private void FinishTutorial()
    {
        if (leaving || resetPending) return;
        StartCoroutine(FinishRoutine());
    }
    private IEnumerator FinishRoutine()
    {
        resetPending = true;
        ClearWaitingStatus();
        CancelOwnedTravel();
        tutorial.ReleaseFastFoodPresentation();
        // Restore the normal camera/input owners before discarding the isolated payload.
        if (RestockFlowCoordinator.Instance?.IsTransitioning == true)
        {
            float until = Time.realtimeSinceStartup + dependencyAllowance;
            while (RestockFlowCoordinator.Instance.IsTransitioning && Time.realtimeSinceStartup < until) yield return null;
            if (RestockFlowCoordinator.Instance.IsTransitioning)
            { resetPending = false; Fail("Wait for storage to finish opening before leaving."); yield break; }
        }
        if (RestockFlowCoordinator.Instance?.IsRestockRoomOpen == true)
        {
            RestockFlowCoordinator.Instance.ExitRestockRoom();
            float until = Time.realtimeSinceStartup + dependencyAllowance;
            while (RestockFlowCoordinator.Instance.IsTransitioning && Time.realtimeSinceStartup < until) yield return null;
            if (RestockFlowCoordinator.Instance.IsRestockRoomOpen)
            { resetPending = false; Fail("Storage could not close. Retry this chapter."); yield break; }
        }
        leaving = true;
        Computer?.CloseComputer();
        view.Exit();
        kitchen.enabled = false; // The disposable ledger must not tick after runtime restoration.
        day.ReleaseFastFoodSession();
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene("Lobby2");
        else SceneManager.LoadSceneAsync("Lobby2");
    }
    private void ShowSkipConfirmation() => confirmation.SetActive(true);
    private void HideSkipConfirmation() => confirmation.SetActive(false);
    private void OnDisable()
    {
        ClearWaitingStatus();
        if (waitingTimer != null) Destroy(waitingTimer.gameObject);
        CancelOwnedTravel();
        StopAllCoroutines();
        Initialized = false;
        if (tutorial != null) { tutorial.PreparingStep -= PrepareStep; tutorial.TutorialCompletedChanged -= Completed; }
        if (State != null && State.InteractionFilter == Permit)
        { State.InteractionFilter = null; State.HoldPlayerStations = false; }
        if (restart != null) restart.onClick.RemoveListener(RestartChapter);
        if (skip != null) skip.onClick.RemoveListener(ShowSkipConfirmation);
        if (cancelSkip != null) cancelSkip.onClick.RemoveListener(HideSkipConfirmation);
        if (confirmSkip != null) confirmSkip.onClick.RemoveListener(SkipTutorial);
        if (finish != null) finish.onClick.RemoveListener(FinishTutorial);
        foreach (var pair in navigationStates) if (pair.Key != null) pair.Key.interactable = pair.Value;
        navigationStates.Clear();
        restockSource = null; restockShelf = null; restockExit = null;
        lastGuidanceSource = lastGuidanceTarget = null;
        targetMissingSeconds = 0;
        PlayerTaskGuidance.ClearTask(FastFoodScene.Tutorial);
        if (Instance == this) Instance = null;
    }
}
