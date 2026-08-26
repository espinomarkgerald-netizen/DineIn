using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum ManagementComputerApp
{
    Dashboard,
    Staff,
    Menu,
    Restock,
    Equipment,
    Finances,
    Objectives
}

/// <summary>
/// Controls the Lobby1 manager computer. App content is populated from the
/// existing ScriptableObject/managers and every repeated entry is a prefab row.
/// </summary>
public sealed class ManagementComputerController : MonoBehaviour, IPointerClickHandler
{
    private const string CatalogUIConfigResource = "ManagementComputerCatalogUIConfig";

    [Header("Desktop")]
    [SerializeField] private GameObject desktopRoot;
    [SerializeField] private Button[] appButtons;
    [SerializeField] private Button startShiftButton;
    [SerializeField] private TMP_Text startShiftLabel;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text dayStatusText;
    [SerializeField] private TMP_Text moneyStatusText;
    [SerializeField] private TMP_Text approvalStatusText;
    [SerializeField] private TMP_Text clockStatusText;
    [SerializeField] private TMP_Text desktopHintText;

    [Header("App Window")]
    [SerializeField] private ManagementComputerWindow appWindow;
    [SerializeField] private ManagementComputerRowUI rowPrefab;
    [SerializeField] private ManagementComputerHRPanel hrPanelPrefab;
    [SerializeField] private ManagementComputerCatalogUIConfig catalogUIConfig;

    private ManagerPlayer activeManager;
    private ManagementComputerStation activeStation;
    private MainCameraController cameraController;
    private Vector3 savedCameraTarget;
    private bool savedCameraEnabled;
    private float nextStatusRefresh;
    private ScrollRect fallbackDragScroll;
    private Scrollbar fallbackDragScrollbar;
    private PointerEventData fallbackDragPointer;
    private bool fallbackDragStarted;
    private bool fallbackConsumedRelease;
    private int lastFallbackButtonFrame = -1;
    private bool currentAppUsesCards;
    private bool restockOrderCommitInProgress;
    private Coroutine canvasRefreshRoutine;

    private sealed class StartChecklistEntry
    {
        public Sprite icon;
        public string title;
        public string details;
        public ReadinessVisualState state;
        public string action;
        public UnityEngine.Events.UnityAction callback;
    }

    private sealed class StartChecklistSnapshot
    {
        public readonly List<StartChecklistEntry> entries = new List<StartChecklistEntry>();
        public int blockers;
        public int warnings;
    }

    public bool IsOpen => desktopRoot != null && desktopRoot.activeSelf;
    public ManagementComputerWindow AppWindow => appWindow;

    public void ConfigureReferences(
        GameObject configuredDesktopRoot,
        Button[] configuredAppButtons,
        Button configuredStartShiftButton,
        TMP_Text configuredStartShiftLabel,
        Button configuredExitButton,
        TMP_Text configuredDayStatus,
        TMP_Text configuredMoneyStatus,
        TMP_Text configuredApprovalStatus,
        TMP_Text configuredClockStatus,
        TMP_Text configuredDesktopHint,
        ManagementComputerWindow configuredWindow,
        ManagementComputerRowUI configuredRowPrefab,
        ManagementComputerHRPanel configuredHRPanelPrefab)
    {
        desktopRoot = configuredDesktopRoot;
        appButtons = configuredAppButtons;
        startShiftButton = configuredStartShiftButton;
        startShiftLabel = configuredStartShiftLabel;
        exitButton = configuredExitButton;
        dayStatusText = configuredDayStatus;
        moneyStatusText = configuredMoneyStatus;
        approvalStatusText = configuredApprovalStatus;
        clockStatusText = configuredClockStatus;
        desktopHintText = configuredDesktopHint;
        appWindow = configuredWindow;
        rowPrefab = configuredRowPrefab;
        hrPanelPrefab = configuredHRPanelPrefab;
    }

    private void Awake()
    {
        WireButtons();
        if (appWindow != null)
            appWindow.Initialize(CloseApp);

        if (desktopRoot != null)
            desktopRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (appWindow != null && appWindow.gameObject.activeSelf)
                CloseApp();
            else
                CloseComputer();
        }

        if (Time.unscaledTime >= nextStatusRefresh)
        {
            nextStatusRefresh = Time.unscaledTime + 0.5f;
            RefreshStatusBar();
        }

        fallbackConsumedRelease = false;
        RouteScrollingWhenCanvasDepthIsInvalid();
        RoutePointerReleaseWhenCanvasDepthIsInvalid();
    }

    private void RouteScrollingWhenCanvasDepthIsInvalid()
    {
        Vector2 position = Input.mousePosition;
        Vector2 wheel = Input.mouseScrollDelta;

        if (Input.touchCount == 0 && wheel.sqrMagnitude > 0.001f)
        {
            ScrollRect scroll = FindTopmostScrollableAt(position);
            if (scroll != null)
            {
                // Move normalized positions directly because Unity's
                // ScrollRect event path is the part blocked by this modal.
                if (scroll.horizontal && !scroll.vertical)
                {
                    float amount = Mathf.Abs(wheel.x) > Mathf.Abs(wheel.y) ? wheel.x : wheel.y;
                    scroll.horizontalNormalizedPosition = Mathf.Clamp01(
                        scroll.horizontalNormalizedPosition - amount * 0.12f);
                }
                else if (scroll.vertical)
                {
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(
                        scroll.verticalNormalizedPosition + wheel.y * 0.12f);
                }
            }
        }

        bool pressed;
        bool held;
        bool released;
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            position = touch.position;
            pressed = touch.phase == TouchPhase.Began;
            held = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
            released = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
        }
        else
        {
            pressed = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            released = Input.GetMouseButtonUp(0);
        }

        if (pressed)
            BeginFallbackDrag(position);

        if (held && fallbackDragPointer != null)
            ContinueFallbackDrag(position);

        if (released && fallbackDragPointer != null)
            EndFallbackDrag(position);
    }

    private void BeginFallbackDrag(Vector2 position)
    {
        fallbackDragScrollbar = FindTopmostScrollbarAt(position);
        fallbackDragScroll = fallbackDragScrollbar == null
            ? FindTopmostScrollableAt(position)
            : null;

        if (fallbackDragScrollbar == null && fallbackDragScroll == null)
            return;

        fallbackDragPointer = CreatePointerEvent(position);
        fallbackDragPointer.pressPosition = position;
        fallbackDragPointer.pointerPress = fallbackDragScrollbar != null
            ? fallbackDragScrollbar.gameObject
            : fallbackDragScroll.gameObject;
        fallbackDragStarted = false;

        if (fallbackDragScrollbar != null)
            ExecuteEvents.Execute(fallbackDragScrollbar.gameObject, fallbackDragPointer,
                ExecuteEvents.pointerDownHandler);
        else
            fallbackDragScroll.OnInitializePotentialDrag(fallbackDragPointer);
    }

    private void ContinueFallbackDrag(Vector2 position)
    {
        Vector2 previous = fallbackDragPointer.position;
        fallbackDragPointer.position = position;
        fallbackDragPointer.delta = position - previous;

        if (!fallbackDragStarted &&
            (position - fallbackDragPointer.pressPosition).sqrMagnitude >= 16f)
        {
            fallbackDragStarted = true;
            fallbackDragPointer.dragging = true;
            if (fallbackDragScroll != null)
                fallbackDragScroll.OnBeginDrag(fallbackDragPointer);
        }

        if (!fallbackDragStarted)
            return;

        if (fallbackDragScrollbar != null)
            ExecuteEvents.Execute(fallbackDragScrollbar.gameObject, fallbackDragPointer,
                ExecuteEvents.dragHandler);
        else
            fallbackDragScroll.OnDrag(fallbackDragPointer);
    }

    private void EndFallbackDrag(Vector2 position)
    {
        fallbackDragPointer.delta = position - fallbackDragPointer.position;
        fallbackDragPointer.position = position;

        if (fallbackDragScrollbar != null)
        {
            ExecuteEvents.Execute(fallbackDragScrollbar.gameObject, fallbackDragPointer,
                ExecuteEvents.pointerUpHandler);
            fallbackConsumedRelease = true;
        }
        else if (fallbackDragStarted && fallbackDragScroll != null)
        {
            fallbackDragScroll.OnEndDrag(fallbackDragPointer);
            fallbackConsumedRelease = true;
        }

        fallbackDragScroll = null;
        fallbackDragScrollbar = null;
        fallbackDragPointer = null;
        fallbackDragStarted = false;
    }

    private void RoutePointerReleaseWhenCanvasDepthIsInvalid()
    {
        Vector2 position;
        bool released;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            position = touch.position;
            released = touch.phase == TouchPhase.Ended;
        }
        else
        {
            position = Input.mousePosition;
            released = Input.GetMouseButtonUp(0);
        }

        if (!released || fallbackConsumedRelease)
            return;

        TMP_InputField input = FindTopmostInputFieldAt(position);
        if (input != null &&
            (input.targetGraphic == null || input.targetGraphic.depth < 0))
        {
            ActivateInputField(input);
            return;
        }

        Button target = FindTopmostButtonAt(position);
        // A valid Graphic depth means Unity's normal EventSystem will dispatch
        // the click. Only route the known Unity 6 inactive-modal failure case.
        if (target == null ||
            (target.targetGraphic != null && target.targetGraphic.depth >= 0))
            return;

        InvokeFallbackButton(target, position);
    }

    /// <summary>
    /// Fallback dispatcher for Unity 6 scenes where graphics under a modal that
    /// was inactive during canvas rebuild can retain depth -1. The full-screen
    /// desktop background still receives the pointer, then routes the click to
    /// the topmost interactable Button under that screen position.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsOpen || eventData == null ||
            eventData.button != PointerEventData.InputButton.Left || desktopRoot == null)
            return;

        TMP_InputField input = FindTopmostInputFieldAt(eventData.position);
        if (input != null)
        {
            ActivateInputField(input);
            eventData.Use();
            return;
        }

        Button target = FindTopmostButtonAt(eventData.position);
        if (target == null)
            return;

        InvokeFallbackButton(target, eventData.position);
        eventData.Use();
    }

    private void InvokeFallbackButton(Button target, Vector2 screenPosition)
    {
        if (target == null || lastFallbackButtonFrame == Time.frameCount)
            return;

        // In the Unity 6 depth -1 case, both Update's raw-input fallback and
        // the desktop background's pointer handler can see the same release.
        // Process that physical click only once, even if the first callback
        // refreshes the applicant list underneath the pointer.
        lastFallbackButtonFrame = Time.frameCount;

        if (EventSystem.current == null)
        {
            target.onClick.Invoke();
            return;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = screenPosition,
            button = PointerEventData.InputButton.Left,
            pointerPress = target.gameObject,
            pointerEnter = target.gameObject
        };
        ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static PointerEventData CreatePointerEvent(Vector2 screenPosition)
    {
        return new PointerEventData(EventSystem.current)
        {
            position = screenPosition,
            button = PointerEventData.InputButton.Left
        };
    }

    private ScrollRect FindTopmostScrollableAt(Vector2 screenPosition)
    {
        ScrollRect[] scrolls = desktopRoot.GetComponentsInChildren<ScrollRect>(false);
        ScrollRect best = null;

        for (int i = 0; i < scrolls.Length; i++)
        {
            ScrollRect candidate = scrolls[i];
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.content == null)
                continue;

            RectTransform rect = candidate.viewport != null
                ? candidate.viewport
                : candidate.transform as RectTransform;
            if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(
                    rect, screenPosition, null))
                continue;

            RectTransform viewport = candidate.viewport != null
                ? candidate.viewport
                : candidate.transform as RectTransform;
            bool canMoveHorizontally = candidate.horizontal &&
                candidate.content.rect.width > viewport.rect.width + 1f;
            bool canMoveVertically = candidate.vertical &&
                candidate.content.rect.height > viewport.rect.height + 1f;
            if (!canMoveHorizontally && !canMoveVertically)
                continue;

            best = candidate;
        }

        return best;
    }

    private Scrollbar FindTopmostScrollbarAt(Vector2 screenPosition)
    {
        Scrollbar[] scrollbars = desktopRoot.GetComponentsInChildren<Scrollbar>(false);
        Scrollbar best = null;

        for (int i = 0; i < scrollbars.Length; i++)
        {
            Scrollbar candidate = scrollbars[i];
            if (candidate == null || !candidate.IsActive() || !candidate.IsInteractable())
                continue;

            RectTransform rect = candidate.transform as RectTransform;
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(
                    rect, screenPosition, null))
                best = candidate;
        }

        return best;
    }

    private Button FindTopmostButtonAt(Vector2 screenPosition)
    {
        Button[] buttons = desktopRoot.GetComponentsInChildren<Button>(false);
        Button best = null;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null || !candidate.IsActive() || !candidate.IsInteractable())
                continue;

            RectTransform rect = candidate.transform as RectTransform;
            if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(
                    rect, screenPosition, null))
                continue;

            // GetComponentsInChildren follows rendered hierarchy order. Keeping
            // the last match makes modal-window controls win over desktop apps.
            best = candidate;
        }

        return best;
    }

    private TMP_InputField FindTopmostInputFieldAt(Vector2 screenPosition)
    {
        TMP_InputField[] inputs = desktopRoot.GetComponentsInChildren<TMP_InputField>(false);
        TMP_InputField best = null;
        for (int i = 0; i < inputs.Length; i++)
        {
            TMP_InputField candidate = inputs[i];
            if (candidate == null || !candidate.IsActive() || !candidate.IsInteractable())
                continue;

            RectTransform rect = candidate.transform as RectTransform;
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(
                    rect, screenPosition, null))
                best = candidate;
        }
        return best;
    }

    private static void ActivateInputField(TMP_InputField input)
    {
        if (input == null)
            return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(input.gameObject);
        input.Select();
        input.ActivateInputField();
    }

    public void OpenComputer(ManagerPlayer manager, ManagementComputerStation station)
    {
        if (desktopRoot == null || IsOpen)
            return;

        RestockFlowCoordinator.Instance?.EnsureLobbyUIInputReady();
        activeManager = manager;
        activeStation = station;

        if (activeManager != null)
            activeManager.SetExternalInputSuppressed(true);

        cameraController = FindFirstObjectByType<MainCameraController>();
        if (cameraController != null)
        {
            savedCameraTarget = cameraController.GetRigTargetPosition();
            savedCameraEnabled = cameraController.enabled;
            if (station != null)
                cameraController.SetRigTargetPosition(station.transform.position, true);
            cameraController.enabled = false;
        }

        desktopRoot.SetActive(true);
        ResetComputerInputState();
        CloseApp();
        RefreshStatusBar();

        Canvas.ForceUpdateCanvases();
        if (canvasRefreshRoutine != null)
            StopCoroutine(canvasRefreshRoutine);
        canvasRefreshRoutine = StartCoroutine(RefreshComputerCanvasNextFrame());

        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
            MoneyManager.Instance.OnMoneyChanged += OnMoneyChanged;
        }
    }

    public void CloseComputer()
    {
        if (!IsOpen)
            return;

        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= OnMoneyChanged;

        CloseApp();
        desktopRoot.SetActive(false);
        ResetFallbackInputState();

        if (canvasRefreshRoutine != null)
        {
            StopCoroutine(canvasRefreshRoutine);
            canvasRefreshRoutine = null;
        }

        if (cameraController != null)
        {
            cameraController.enabled = savedCameraEnabled;
            cameraController.SetRigTargetPosition(savedCameraTarget, true);
        }

        if (activeManager != null)
            activeManager.SetExternalInputSuppressed(false);

        activeManager = null;
        activeStation = null;
        cameraController = null;
    }

    private void ResetComputerInputState()
    {
        ResetFallbackInputState();

        Canvas hostCanvas = GetComponent<Canvas>();
        if (hostCanvas != null)
            hostCanvas.enabled = true;
        GraphicRaycaster hostRaycaster = GetComponent<GraphicRaycaster>();
        if (hostRaycaster != null)
            hostRaycaster.enabled = true;

        Canvas desktopCanvas = desktopRoot.GetComponent<Canvas>();
        if (desktopCanvas != null)
            desktopCanvas.enabled = true;
        GraphicRaycaster desktopRaycaster = desktopRoot.GetComponent<GraphicRaycaster>();
        if (desktopRaycaster != null)
            desktopRaycaster.enabled = true;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void ResetFallbackInputState()
    {
        fallbackDragScroll = null;
        fallbackDragScrollbar = null;
        fallbackDragPointer = null;
        fallbackDragStarted = false;
        fallbackConsumedRelease = false;
        lastFallbackButtonFrame = -1;
    }

    private IEnumerator RefreshComputerCanvasNextFrame()
    {
        yield return null;
        canvasRefreshRoutine = null;
        if (!IsOpen)
            yield break;

        Canvas.ForceUpdateCanvases();
        if (appWindow != null && appWindow.gameObject.activeSelf)
            appWindow.RefreshContentLayout();
    }

    public void OpenApp(int appIndex)
    {
        if (!Enum.IsDefined(typeof(ManagementComputerApp), appIndex) || appWindow == null)
            return;

        appWindow.ClearRows();

        ManagementComputerApp app = (ManagementComputerApp)appIndex;
        appWindow.Open(GetAppTitle(app));
        currentAppUsesCards = UsesCardLayout(app);
        appWindow.SetContentLayout(currentAppUsesCards);
        appWindow.SetEmbeddedPanelLayout(UsesEmbeddedCatalogLayout(app));

        switch (app)
        {
            case ManagementComputerApp.Dashboard: PopulateDashboard(); break;
            case ManagementComputerApp.Staff: PopulateStaff(); break;
            case ManagementComputerApp.Menu: PopulateMenu(); break;
            case ManagementComputerApp.Restock: PopulateRestock(); break;
            case ManagementComputerApp.Equipment: PopulateEquipment(); break;
            case ManagementComputerApp.Finances: PopulateFinances(); break;
            case ManagementComputerApp.Objectives: PopulateObjectives(); break;
        }

        appWindow.RefreshContentLayout();
    }

    public void CloseApp()
    {
        if (appWindow != null)
        {
            appWindow.ClearRows();
            appWindow.Close();
        }
    }

    private void WireButtons()
    {
        if (appButtons != null)
        {
            for (int i = 0; i < appButtons.Length; i++)
            {
                if (appButtons[i] == null)
                    continue;

                int captured = i;
                appButtons[i].onClick.RemoveAllListeners();
                appButtons[i].onClick.AddListener(() => OpenApp(captured));
            }
        }

        if (startShiftButton != null)
        {
            startShiftButton.onClick.RemoveAllListeners();
            startShiftButton.onClick.AddListener(TryStartShift);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(CloseComputer);
        }
    }

    private void PopulateDashboard()
    {
        int day = CurrentDay;
        int menuCount = 0;
        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog != null)
        {
            foreach (Recipe product in catalog.Products)
            {
                if (MenuAvailabilityManager.IsProductAvailable(product) && product.IsUnlocked)
                    menuCount++;
            }
        }

        AddRow(null, "Restaurant status", IsShiftActive ? "Service is currently running" : "Pre-opening management phase",
            IsShiftActive ? "OPEN" : "CLOSED", string.Empty, null, false);
        AddRow(null, "Today's menu", "Products available to the notepad, customers, kitchen and bar",
            menuCount.ToString(), "OPEN MENU", () => OpenApp((int)ManagementComputerApp.Menu));
        AddRow(null, "Scheduled staff", "One employee can be scheduled for each role",
            (EmployeeManager.Instance != null ? EmployeeManager.Instance.AssignedEmployeeCount : 0).ToString(),
            "OPEN STAFF", () => OpenApp((int)ManagementComputerApp.Staff));
        AddRow(null, "Inventory", "Ingredient stock shared with restaurant orders",
            InventoryManager.Instance != null ? InventoryManager.Instance.Items.Count + " items" : "Unavailable",
            "RESTOCK", () => OpenApp((int)ManagementComputerApp.Restock));

        CasualDiningPolishManager polish = CasualDiningPolishManager.EnsureInstance();
        AddRow(null, "Restaurant rating", "Operational quality, separate from Alien Approval",
            polish.RestaurantStars.ToString("0.0") + " / 5",
            string.Empty, null, false);
        AddRow(null, "Latest alien review", polish.GetLatestReviewText(),
            string.Empty, string.Empty, null, false);

        appWindow.SetMessage($"Day {day} setup. Changes lock when the shift starts.");
    }

    private void PopulateStaff()
    {
        EmployeeManager manager = EmployeeManager.Instance;
        if (manager == null)
        {
            appWindow.SetMessage("Employee system is unavailable.", true);
            return;
        }

        manager.EnsureEmployeesGenerated();
        bool editable = !IsShiftActive && !manager.SlotsLocked;

        if (hrPanelPrefab == null)
        {
            appWindow.SetMessage("The HR board prefab is missing.", true);
            return;
        }

        ManagementComputerHRPanel panel = Instantiate(hrPanelPrefab, appWindow.Content);
        panel.name = "HRBoard";
        panel.Bind(manager, editable);

        appWindow.SetMessage(editable
            ? "Hire applicants, keep up to three workers per role, and choose one active worker for the shift."
            : "HR decisions are locked while the shift is running.");
    }

    private void PopulateMenu()
    {
        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog == null)
        {
            appWindow.SetMessage("MenuCatalog could not be loaded from Resources.", true);
            return;
        }

        ManagementComputerCatalogUIConfig config = GetCatalogUIConfig();
        if (config == null || config.CatalogPanelPrefab == null)
        {
            appWindow.SetMessage(
                "The editable Menu/Restock catalog prefab is missing. Run Tools > Dine In > Create Missing Management Catalog Prefabs.",
                true);
            return;
        }

        ManagementComputerCatalogPanelUI panel = Instantiate(
            config.CatalogPanelPrefab,
            appWindow.Content);
        panel.gameObject.SetActive(true);
        panel.BindMenu(
            catalog.Products,
            !IsShiftActive && MenuAvailabilityManager.Instance != null,
            SetMenuAvailability,
            SetMenuPrice);
        appWindow.SetMessage(string.Empty);
    }

    private void PopulateRestock()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null || inventory.Items == null)
        {
            appWindow.SetMessage("Inventory system is unavailable.", true);
            return;
        }

        ManagementComputerCatalogUIConfig config = GetCatalogUIConfig();
        if (config == null || config.CatalogPanelPrefab == null || config.StorageConfig == null)
        {
            appWindow.SetMessage(
                "The Restock catalog or restaurant storage configuration is missing.",
                true);
            return;
        }

        ManagementComputerCatalogPanelUI panel = Instantiate(
            config.CatalogPanelPrefab,
            appWindow.Content);
        panel.gameObject.SetActive(true);
        panel.BindRestock(
            inventory.Items,
            config.StorageConfig,
            RestockOrderManager.EnsureInstance(),
            GetExpectedCustomers(),
            ConfirmRestockOrder);
        appWindow.SetMessage(string.Empty);
    }

    private void PopulateEquipment()
    {
        EquipmentManager manager = EquipmentManager.Instance;
        if (manager == null || manager.AllEquipment == null)
        {
            appWindow.SetMessage("Equipment system is unavailable.", true);
            return;
        }

        bool editable = !IsShiftActive;
        foreach (Equipment equipment in manager.AllEquipment)
        {
            if (equipment == null)
                continue;

            Equipment captured = equipment;
            bool purchased = manager.Purchased(equipment.itemID);
            bool unlocked = equipment.dayToUnlock <= CurrentDay ||
                (UnlockManager.Instance != null && UnlockManager.Instance.IsEquipmentUnlocked(equipment.itemID));
            AddRow(equipment.sprite, equipment.displayName,
                "Unlock day " + equipment.dayToUnlock,
                purchased ? "OWNED" : "₱" + equipment.cost,
                purchased ? "PURCHASED" : "BUY",
                () =>
                {
                    manager.Purchase(captured.itemID);
                    PopulateAgain(ManagementComputerApp.Equipment);
                }, editable && unlocked && !purchased && MoneyManager.Instance != null && MoneyManager.Instance.HasEnough(equipment.cost));
        }

        appWindow.SetMessage(editable
            ? "Purchased equipment persists and activates matching equipment links."
            : "Equipment purchases are locked while service is active.");
    }

    private void PopulateFinances()
    {
        DailyFinanceBridge bridge = DailyFinanceBridge.Instance;
        DailyRevenueTracker revenue = DailyRevenueTracker.Instance;
        EmployeeManager employees = EmployeeManager.Instance;
        int payroll = employees != null && employees.salaryConfig != null ? employees.CalculateTotalPayroll() : 0;

        AddRow(null, "Cash balance", "Available for restocking and equipment", MoneyText, string.Empty, null, false);
        AddRow(null, "Revenue today", "Completed customer payments", "₱" + (bridge != null ? bridge.EarnedToday : 0), string.Empty, null, false);
        AddRow(null, "Ingredient purchases", "Boxes purchased during management", "₱" + (revenue != null ? revenue.IngredientCost : 0), string.Empty, null, false);
        AddRow(null, "Scheduled payroll", "Deducted during end-of-day settlement", "₱" + payroll, string.Empty, null, false);

        IReadOnlyList<string> transactions = MoneyManager.Instance != null
            ? MoneyManager.Instance.TransactionLog
            : null;
        if (transactions != null)
        {
            for (int i = transactions.Count - 1, shown = 0; i >= 0 && shown < 8; i--, shown++)
                AddRow(null, "Transaction", transactions[i], string.Empty, string.Empty, null, false);
        }

        appWindow.SetMessage("Live financial overview. The latest eight wallet transactions are shown below.");
    }

    private void PopulateObjectives()
    {
        DailyObjectiveManager objectives = DailyObjectiveManager.Instance;
        if (objectives == null)
        {
            appWindow.SetMessage("Objective system is unavailable.", true);
            return;
        }

        objectives.EnsureDefaultObjectives();
        if (objectives.ActiveMandatory == null)
        {
            int maxGroups = GameDayManager.Instance != null ? GameDayManager.Instance.MaxCustomersThisShift : 5;
            objectives.RollObjectivesForDay(CurrentDay, maxGroups);
        }

        AddObjectiveRow("MANDATORY", objectives.ActiveMandatory);
        AddObjectiveRow("SECONDARY", objectives.ActiveSecondary);
        AddObjectiveRow("BONUS", objectives.ActiveBonus);

        if (objectives.HasPreviousDayResult)
        {
            AddRow(null, "Previous result", "Day " + objectives.LastResultDay,
                objectives.LastGrade.ToString(), string.Empty, null, false);
        }

        appWindow.SetMessage("Alien demands are evaluated automatically at the end of the shift.");
    }

    private void AddObjectiveRow(string label, ObjectiveDefinition objective)
    {
        string description = objective != null ? objective.GetDescription(CurrentDay) : "Not configured";
        AddRow(null, label, description, string.Empty, string.Empty, null, false);
    }

    private bool ConfirmRestockOrder(IReadOnlyList<RestockCartLine> cart)
    {
        if (restockOrderCommitInProgress || cart == null || cart.Count == 0 ||
            MoneyManager.Instance == null)
            return false;

        restockOrderCommitInProgress = true;
        try
        {
            List<RestockCartLine> sanitized = new List<RestockCartLine>();
            int totalCost = 0;
            for (int i = 0; i < cart.Count; i++)
            {
                RestockCartLine source = cart[i];
                if (source?.item == null || source.quantity <= 0)
                    continue;

                RestockCartLine line = new RestockCartLine
                {
                    item = source.item,
                    quantity = Mathf.Max(0, source.quantity)
                };
                sanitized.Add(line);
                totalCost += line.LineCost;
            }

            if (sanitized.Count == 0 || totalCost <= 0 ||
                !MoneyManager.Instance.HasEnough(totalCost))
                return false;

            RestockOrderManager orders = RestockOrderManager.EnsureInstance();
            ManagementComputerCatalogUIConfig config = GetCatalogUIConfig();
            if (orders == null || config?.StorageConfig == null)
                return false;

            bool spent = DailyFinanceBridge.Instance != null
                ? DailyFinanceBridge.Instance.SpendMoney(totalCost, "Restock delivery order")
                : MoneyManager.Instance.Spend(totalCost, "Restock delivery order");
            if (!spent)
                return false;

            string orderID = orders.CreateOrder(
                config.StorageConfig.RestaurantID,
                sanitized,
                totalCost);
            if (string.IsNullOrWhiteSpace(orderID))
            {
                // This path is defensive: validation above guarantees a valid
                // order, but never leave the wallet charged without a ledger row.
                MoneyManager.Instance.Earn(totalCost, "Restock order rollback");
                return false;
            }

            DailyRevenueTracker.Instance?.RecordIngredientCost(totalCost);
            GameSaveManager.Instance?.RequestSave();
            RefreshStatusBar();
            return true;
        }
        finally
        {
            restockOrderCommitInProgress = false;
        }
    }

    private bool SetMenuAvailability(Recipe product, bool available)
    {
        return MenuAvailabilityManager.Instance != null &&
               MenuAvailabilityManager.Instance.SetProductAvailable(product, available);
    }

    private bool SetMenuPrice(Recipe product, int price)
    {
        return MenuAvailabilityManager.Instance != null &&
               MenuAvailabilityManager.Instance.SetProductPrice(product, price);
    }

    public void TryStartShift()
    {
        if (GameDayManager.Instance == null)
        {
            ShowDesktopHint("Shift controller not found.", true);
            return;
        }

        if (IsShiftActive)
        {
            ShowDesktopHint("The shift is already running.");
            return;
        }

        OpenStartChecklist();
    }

    private void StartShiftConfirmed()
    {
        StartChecklistSnapshot readiness = BuildStartChecklist();
        if (readiness.blockers > 0)
        {
            OpenStartChecklist();
            return;
        }

        // Race-safe final newspaper gate. In normal use this is already green
        // in the checklist; if the day changed underneath the UI it opens the
        // new issue instead of starting with stale readiness.
        if (!CasualDiningPolishManager.EnsureInstance().TryAllowStartShift())
        {
            OpenStartChecklist();
            return;
        }

        RestockFlowCoordinator.Instance?.AcknowledgeStartReadinessWarnings();
        EmployeeManager.Instance?.LockAllSlots();

        int payroll = EmployeeManager.Instance != null && EmployeeManager.Instance.salaryConfig != null
            ? EmployeeManager.Instance.CalculateTotalPayroll()
            : 0;
        int ingredientSpend = DailyRevenueTracker.Instance != null
            ? DailyRevenueTracker.Instance.IngredientCost
            : 0;
        DailyFinanceBridge.Instance?.SetDailyCosts(payroll, 0, 0, ingredientSpend);

        DailyObjectiveManager objectives = DailyObjectiveManager.Instance;
        if (objectives != null && objectives.ActiveMandatory == null)
        {
            objectives.EnsureDefaultObjectives();
            objectives.RollObjectivesForDay(CurrentDay,
                GameDayManager.Instance != null ? GameDayManager.Instance.MaxCustomersThisShift : 5);
        }

        GameSaveManager.Instance?.RequestSave();
        CloseComputer();
        GameDayManager.Instance.ShowShiftIntro();
    }

    private void OpenStartChecklist()
    {
        if (appWindow == null)
            return;

        appWindow.ClearRows();
        appWindow.Open("PRE-OPEN CHECKLIST");
        currentAppUsesCards = false;
        appWindow.SetContentLayout(false);
        appWindow.SetEmbeddedPanelLayout(false);

        StartChecklistSnapshot snapshot = BuildStartChecklist();
        for (int i = 0; i < snapshot.entries.Count; i++)
        {
            StartChecklistEntry entry = snapshot.entries[i];
            AddReadinessRow(
                entry.icon,
                entry.title,
                entry.details,
                entry.state,
                entry.action,
                entry.callback);
        }

        if (snapshot.blockers > 0)
        {
            appWindow.SetMessage("× FIX THE RED ITEMS", true);
            appWindow.SetFooter("FIX " + snapshot.blockers + " REQUIRED", null, false);
        }
        else if (snapshot.warnings > 0)
        {
            appWindow.SetMessage("! READY WITH " + snapshot.warnings + " WARNING" +
                                 (snapshot.warnings == 1 ? string.Empty : "S"));
            appWindow.SetFooter(
                "OPEN WITH " + snapshot.warnings + " !",
                StartShiftConfirmed);
        }
        else
        {
            appWindow.SetMessage("✓ READY TO OPEN");
            appWindow.SetFooter("START SHIFT  ✓", StartShiftConfirmed);
        }

        appWindow.RefreshContentLayout();
    }

    private StartChecklistSnapshot BuildStartChecklist()
    {
        StartChecklistSnapshot snapshot = new StartChecklistSnapshot();
        CasualDiningPolishManager polish = CasualDiningPolishManager.EnsureInstance();
        NewspaperIssueSaveEntry issue = polish.GetIssueForDay(CurrentDay);
        bool newsReady = issue == null || issue.viewed;
        AddChecklistEntry(
            snapshot,
            GetAppIcon(ManagementComputerApp.Dashboard),
            "TODAY'S NEWS",
            newsReady ? "TODAY'S ISSUE READ" : "CLOSE COMPUTER AND READ TODAY'S NEWS",
            newsReady ? ReadinessVisualState.Ready : ReadinessVisualState.Blocked,
            string.Empty,
            null);

        int menuCount = 0;
        Dictionary<ItemData, int> requiredIngredients = new Dictionary<ItemData, int>();

        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog != null)
        {
            foreach (Recipe product in catalog.Products)
            {
                if (!MenuAvailabilityManager.IsProductAvailable(product) || !product.IsUnlocked)
                    continue;

                menuCount++;
                if (product.ingredients == null)
                    continue;

                foreach (RecipeIngredient ingredient in product.ingredients)
                {
                    if (ingredient?.item == null)
                        continue;
                    if (!requiredIngredients.TryGetValue(ingredient.item, out int amount))
                        amount = 0;
                    requiredIngredients[ingredient.item] = Mathf.Max(amount, ingredient.amount);
                }
            }
        }

        AddChecklistEntry(
            snapshot,
            GetAppIcon(ManagementComputerApp.Menu),
            "MENU",
            menuCount > 0 ? menuCount + " ITEMS AVAILABLE" : "NO USABLE ITEMS",
            menuCount > 0 ? ReadinessVisualState.Ready : ReadinessVisualState.Blocked,
            "OPEN",
            () => OpenApp((int)ManagementComputerApp.Menu));

        int assignedStaff = EmployeeManager.Instance != null
            ? EmployeeManager.Instance.AssignedEmployeeCount
            : 0;
        AddChecklistEntry(
            snapshot,
            GetAppIcon(ManagementComputerApp.Staff),
            "STAFF",
            assignedStaff > 0
                ? assignedStaff + " ROLE" + (assignedStaff == 1 ? string.Empty : "S") + " COVERED"
                : "BOTS WILL COVER SERVICE",
            assignedStaff > 0 ? ReadinessVisualState.Ready : ReadinessVisualState.Warning,
            "OPEN",
            () => OpenApp((int)ManagementComputerApp.Staff));

        int ingredientCount = 0;
        int uncovered = 0;
        int coveredByIncoming = 0;
        RestockOrderManager orders = RestockOrderManager.EnsureInstance();
        foreach (KeyValuePair<ItemData, int> pair in requiredIngredients)
        {
            ItemData item = pair.Key;
            if (item == null || InventoryManager.Instance == null ||
                !InventoryManager.Instance.IsTracked(item.itemType))
                continue;

            ingredientCount++;
            RestockStockProjection projection = RestockStockProjection.Calculate(
                item,
                GetExpectedCustomers(),
                orders);
            int minimumProjected = projection.FreshUnits +
                                   projection.PendingContainers * projection.UnitsPerBox;
            bool needsMore = projection.RecommendedContainers > 0 ||
                             minimumProjected < Mathf.Max(1, pair.Value);
            if (needsMore)
                uncovered++;
            else if (projection.HasIncoming)
                coveredByIncoming++;
        }

        ReadinessVisualState stockState = uncovered > 0
            ? ReadinessVisualState.Warning
            : coveredByIncoming > 0
                ? ReadinessVisualState.Incoming
                : ReadinessVisualState.Ready;
        string stockDetails = uncovered > 0
            ? uncovered + " LOW   •   " + coveredByIncoming + " COVERED BY DELIVERY"
            : coveredByIncoming > 0
                ? "✓ ALL COVERED   •   → " + coveredByIncoming + " INCOMING"
                : ingredientCount + " INGREDIENTS COVERED";
        AddChecklistEntry(
            snapshot,
            GetAppIcon(ManagementComputerApp.Restock),
            "STOCK",
            stockDetails,
            stockState,
            "CHECK",
            () => OpenApp((int)ManagementComputerApp.Restock));

        int inTransit = orders.GetContainerCountInStates(
            RestockOrderState.Ordered,
            RestockOrderState.InDelivery);
        int atTruck = orders.DeliveredContainerCount;
        int inHotbar = orders.HotbarContainerCount;
        ReadinessVisualState deliveryState = atTruck > 0 || inHotbar > 0
            ? ReadinessVisualState.Warning
            : inTransit > 0
                ? ReadinessVisualState.Incoming
                : ReadinessVisualState.Ready;
        string deliveryDetails = atTruck > 0 || inHotbar > 0
            ? "TRUCK " + atTruck + "   •   HOTBAR " + inHotbar
            : inTransit > 0
                ? "→ " + inTransit + " BOX" + (inTransit == 1 ? string.Empty : "ES") + " INCOMING"
                : "NO BOXES WAITING";
        AddChecklistEntry(
            snapshot,
            GetAppIcon(ManagementComputerApp.Restock),
            "DELIVERIES",
            deliveryDetails,
            deliveryState,
            "VIEW",
            () => OpenApp((int)ManagementComputerApp.Restock));

        return snapshot;
    }

    private static void AddChecklistEntry(
        StartChecklistSnapshot snapshot,
        Sprite icon,
        string title,
        string details,
        ReadinessVisualState state,
        string action,
        UnityEngine.Events.UnityAction callback)
    {
        snapshot.entries.Add(new StartChecklistEntry
        {
            icon = icon,
            title = title,
            details = details,
            state = state,
            action = action,
            callback = callback
        });

        if (state == ReadinessVisualState.Blocked)
            snapshot.blockers++;
        else if (state == ReadinessVisualState.Warning)
            snapshot.warnings++;
    }

    private Sprite GetAppIcon(ManagementComputerApp app)
    {
        int index = (int)app;
        if (appButtons == null || index < 0 || index >= appButtons.Length || appButtons[index] == null)
            return null;

        Button button = appButtons[index];
        Image[] images = button.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i] != button.targetGraphic && images[i].sprite != null)
                return images[i].sprite;
        }
        return button.image != null ? button.image.sprite : null;
    }

    private void AddReadinessRow(
        Sprite icon,
        string title,
        string details,
        ReadinessVisualState state,
        string action,
        UnityEngine.Events.UnityAction callback)
    {
        if (rowPrefab == null || appWindow == null || appWindow.Content == null)
            return;

        ManagementComputerRowUI row = Instantiate(rowPrefab, appWindow.Content);
        row.gameObject.SetActive(true);
        row.ApplyPresentation(false);
        row.BindReadiness(icon, title, details, state, action, callback);
    }

    private void AddRow(Sprite icon, string title, string details, string value, string action,
        UnityEngine.Events.UnityAction callback, bool enabled = true)
    {
        if (rowPrefab == null || appWindow == null || appWindow.Content == null)
            return;

        ManagementComputerRowUI row = Instantiate(rowPrefab, appWindow.Content);
        row.gameObject.SetActive(true);
        row.ApplyPresentation(currentAppUsesCards);
        row.Bind(icon, title, details, value, action, callback, enabled);
    }

    private static bool UsesCardLayout(ManagementComputerApp app)
    {
        return app == ManagementComputerApp.Equipment;
    }

    private static bool UsesEmbeddedCatalogLayout(ManagementComputerApp app)
    {
        return app == ManagementComputerApp.Menu ||
               app == ManagementComputerApp.Restock;
    }

    private void PopulateAgain(ManagementComputerApp app)
    {
        float scrollPosition = appWindow != null
            ? appWindow.VerticalNormalizedPosition
            : 1f;
        OpenApp((int)app);
        appWindow?.RestoreVerticalNormalizedPositionNextFrame(scrollPosition);
        RefreshStatusBar();
    }

    private ManagementComputerCatalogUIConfig GetCatalogUIConfig()
    {
        if (catalogUIConfig == null)
            catalogUIConfig = Resources.Load<ManagementComputerCatalogUIConfig>(
                CatalogUIConfigResource);
        return catalogUIConfig;
    }

    private int GetExpectedCustomers()
    {
        if (GameDayManager.Instance != null && GameDayManager.Instance.MaxCustomersThisShift > 0)
            return GameDayManager.Instance.MaxCustomersThisShift;

        ManagementComputerCatalogUIConfig config = GetCatalogUIConfig();
        return config?.StorageConfig != null
            ? config.StorageConfig.ExpectedCustomers
            : 10;
    }

    private void RefreshStatusBar()
    {
        if (dayStatusText != null) dayStatusText.text = "DAY " + CurrentDay;
        if (moneyStatusText != null) moneyStatusText.text = MoneyText;
        if (approvalStatusText != null)
            approvalStatusText.text = "APPROVAL " + (AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0) + "%";
        if (clockStatusText != null)
        {
            clockStatusText.text = GameDayManager.Instance != null
                ? GameDayManager.Instance.FormattedGameTime
                : DateTime.Now.ToString("h:mm tt");
        }

        bool active = IsShiftActive;
        if (startShiftLabel != null) startShiftLabel.text = active ? "SHIFT RUNNING" : "START SHIFT";
        if (startShiftButton != null) startShiftButton.interactable = !active;
    }

    private void OnMoneyChanged(int _) => RefreshStatusBar();

    private void ShowDesktopHint(string message, bool warning = false)
    {
        if (desktopHintText == null)
            return;

        desktopHintText.text = message;
        desktopHintText.color = warning ? new Color(1f, 0.55f, 0.5f) : Color.white;
    }

    private string GetAppTitle(ManagementComputerApp app)
    {
        switch (app)
        {
            case ManagementComputerApp.Dashboard: return "Manager Dashboard";
            case ManagementComputerApp.Staff: return "Staff Scheduler";
            case ManagementComputerApp.Menu: return "Menu Editor";
            case ManagementComputerApp.Restock: return "Ingredient Restock";
            case ManagementComputerApp.Equipment: return "Equipment Store";
            case ManagementComputerApp.Finances: return "Finance Report";
            case ManagementComputerApp.Objectives: return "Alien Demands";
            default: return "Management";
        }
    }

    private int CurrentDay => GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
    private bool IsShiftActive => GameDayManager.Instance != null && GameDayManager.Instance.ServiceActive;
    private string MoneyText => "₱" + (MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0);
}
