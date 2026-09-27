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
    public enum ActionKind { Explain, Open, Station, Load, Ready, Collect, Assemble, Protein, Place, Serve, Burn, Discard, RestockOpen, Store, RestockExit, Clean, Practice }
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
    public static bool Active => Instance != null && Instance.isActiveAndEnabled;
    public Camera KitchenCamera => view != null ? view.TutorialCamera : null;
    public bool HygieneLesson => current != null && current.chapter == Chapter.Hygiene;
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
    private bool PassiveWait => current != null && (current.action == ActionKind.Ready ||
        current.action == ActionKind.Protein || current.action == ActionKind.Burn ||
        current.action == ActionKind.Clean && cleaningStarted);

    public bool OwnsCanvas(Canvas value) => controlsCanvas != null && value != null &&
        value.transform.IsChildOf(controlsCanvas.transform.root);

    private void Awake()
    {
        if (gameObject.scene.name != FastFoodScene.Tutorial) { enabled = false; return; }
        Instance = this;
        CampaignSaveStore.SelectFastFoodTraining();
    }
    private IEnumerator Start()
    {
        replay = TutorialGameModeEntry.IsRevisitLaunch;
        tutorial.PreparingStep += PrepareStep;
        tutorial.TutorialCompletedChanged += Completed;
        restart.onClick.AddListener(RestartChapter);
        skip.onClick.AddListener(() => confirmation.SetActive(true));
        cancelSkip.onClick.AddListener(() => confirmation.SetActive(false));
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
        int index = Array.FindIndex(lessons, l => (int)l.chapter == saved);
        tutorial.StartAtStep(Mathf.Max(0, index));
    }
    private Recipe Runtime(Recipe recipe) => recipe != null && recipes.TryGetValue(recipe.ProductId, out var clone) ? clone : null;
    private void PrepareStep(TutorialSystem.TutorialStep step)
    {
        int index = tutorial.CurrentStepIndex;
        if (index < 0 || index >= lessons.Length) { Fail("The lesson definition is missing."); return; }
        current = lessons[index];
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
        { PlayerPrefs.SetInt(ChapterKey, (int)value); PlayerPrefs.Save(); }
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
        if (value == Chapter.Parallel) EnsureWork(Runtime(burger), 3);
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
        if (!Initialized || controller != kitchen || current == null || leaving || failure != null ||
            tutorial.IsPresentationBusy && !tutorial.IsAwaitingExternalTarget || LobbyPauseMenu.IsAnyOpen || confirmation.activeSelf || Time.timeScale <= 0) return;
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
        if (done) { tutorial.NotifyAction(current.id); return; }
        tutorial.SetExternalGuidanceSuppressed(PassiveWait, PassiveWait);
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
            case ActionKind.RestockOpen: return RestockFlowCoordinator.Instance?.IsRestockRoomOpen == true;
            case ActionKind.Store: return restockItem != null && InventoryManager.Instance.GetStock(restockItem.itemType) > 0 &&
                RestockOrderManager.Instance.HotbarContainerCount == 0;
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
            "Complete the order and press Serve.";
        if (State.Mode != destination)
            task = "Go to "+(destination == FastFoodStationMode.Fry ? "Fryer" : destination.ToString())+". "+task;
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
            if (restockSource == null || !restockSource.gameObject.activeInHierarchy)
                restockSource = FindObjectsByType<RestockHotbarSlotUI>(FindObjectsSortMode.None)
                    .FirstOrDefault(s => s.Item != null && s.Item.itemType == restockItem.itemType &&
                        s.GetComponentInParent<RestockFlowHUD>() != null);
            return restockSource != null ? restockSource.transform : null;
        }
        var p = current.action == ActionKind.Place ? PlacementPortion() : Expected();
        return view.TutorialSource(ExpectedItem(), p, current.action == ActionKind.Load);
    }
    public Transform ResolveWorld(string key)
    {
        if (current == null) return null;
        if (key == "FF.Subject")
            return current.control == "World:Food" ? Source() :
                view.TutorialSubject(current.control.Substring("World:".Length), current.slot);
        if (key == "FF.SourceWorld") return Source();
        if (key != "FF.Target") return null;
        if (current.action == ActionKind.Store)
        {
            if (restockItem == null) return null;
            if (restockShelf == null || !restockShelf.gameObject.activeInHierarchy ||
                !restockShelf.GetComponent<ShelfGrid>().IsCellFree(restockColumn, restockRow))
            {
                restockShelf = null;
                foreach (var grid in FindObjectsByType<ShelfGrid>(FindObjectsSortMode.None))
                    if (grid.StorageType == restockItem.requiredStorage && grid.gameObject.scene.name == "RestockScene" &&
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
        string activeView = view == null ? "uninitialized" : !view.IsOpen ? "restaurant" :
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
        if (current == null || failure != null || !tutorial.IsWaitingForGameplayAction || tutorial.IsPresentationBusy) return;
        if (current.action == ActionKind.Practice)
        {
            var arrow = State.Mode == PracticeDestination() ? null : NavigationTarget(PracticeDestination());
            tutorial.RefreshExternalGuidance(arrow, null);
            return;
        }
        if (PassiveWait || view.HasActiveDrag) return;
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
        restockItem = FastFoodCookingState.Steps(Runtime(fries))[0].item;
        InventoryManager.Instance.UseStock(restockItem.itemType, InventoryManager.Instance.GetStock(restockItem.itemType));
        RestockOrderManager.EnsureInstance().ApplySaveData(new GameSaveData
        {
            restockOrders = new List<RestockOrderSaveData> { new RestockOrderSaveData
            {
                orderID = "fastfood-training-supply", restaurantID = "Lobby2", state = RestockOrderState.Collected,
                createdUtcTicks = DateTime.UtcNow.Ticks,
                lines = new List<RestockOrderLineSaveData> { new RestockOrderLineSaveData
                    { itemID = restockItem.StableItemId, itemType = restockItem.itemType, orderedContainers = 1 } }
            } }
        });
    }
    public void RestartChapter() { if (!resetPending) StartCoroutine(RestartRoutine()); }
    private IEnumerator RestartRoutine()
    {
        resetPending = true;
        if (!Initialized)
        {
            yield return Initialize();
            resetPending = false;
            yield break;
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
        chapter = null; failure = null;
        int index = Array.FindIndex(lessons, l => l.chapter == saved);
        tutorial.StartAtStep(Mathf.Max(0, index));
        resetPending = false;
    }
    private void Fail(string message)
    {
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
        status.text = "Kitchen training complete.";
        finish.GetComponentInChildren<TMP_Text>().text = day.CareerSaveExisted || replay ? "RETURN TO GAME MODE" : "START DAY 1";
        finish.gameObject.SetActive(true);
        skip.gameObject.SetActive(false); restart.gameObject.SetActive(false);
        tutorial.enabled = false;
    }
    private void SkipTutorial()
    {
        if (!replay) { PlayerPrefs.SetInt(SkippedKey, 1); PlayerPrefs.DeleteKey(ChapterKey); PlayerPrefs.Save(); }
        FinishTutorial();
    }
    private void FinishTutorial()
    {
        if (leaving) return;
        leaving = true;
        string destination = day.CareerSaveExisted || replay ? "NewGameMenu" : "Lobby2";
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(destination);
        else SceneManager.LoadSceneAsync(destination);
    }
    private void OnDestroy()
    {
        if (tutorial != null) { tutorial.PreparingStep -= PrepareStep; tutorial.TutorialCompletedChanged -= Completed; }
        if (State != null) { State.InteractionFilter = null; State.HoldPlayerStations = false; }
        foreach (var pair in navigationStates) if (pair.Key != null) pair.Key.interactable = pair.Value;
        PlayerTaskGuidance.ClearTask(FastFoodScene.Tutorial);
        if (Instance == this) Instance = null;
    }
}
