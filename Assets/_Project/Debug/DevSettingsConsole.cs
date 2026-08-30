using System;
using System.Text.RegularExpressions;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DevSettingsConsole : MonoBehaviour
{
    private static DevSettingsConsole activeConsole;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("UI References")]
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private Button runButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text consoleText;

    [Header("Console Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color errorColor = Color.red;

    [Header("Messages")]
    [SerializeField] private string defaultConsoleMessage = "Awaiting command.";
    [SerializeField] private string emptyInputMessage = "ERROR: Empty input.";
    [SerializeField] private string invalidSyntaxMessage = "ERROR: Invalid syntax.";
    [SerializeField] private string invalidCommandMessage = "ERROR: Invalid command.";
    [SerializeField] private string commandFailedMessage = "ERROR: Command failed.";
    [SerializeField] private string developmentBuildOnlyMessage =
        "ERROR: Dev codes require the authorized Kali PlayFab account.";

    [Header("Behavior")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F10;
    [SerializeField] private bool openPanelOnStart;
    [SerializeField] private bool clearInputOnSuccess = true;
    [SerializeField] private bool focusInputWhenOpened = true;
    [SerializeField] private bool allowToggleInEditor = true;

    [Header("Authorized Player Builds")]
    [Tooltip("The real PlayFab username allowed to use dev commands in a player build. " +
             "This is verified with PlayFab GetAccountInfo and is not trusted from PlayerPrefs or visible UI text.")]
    [SerializeField] private string authorizedPlayFabUsername = "Kali";
    [SerializeField] private bool createAndroidOpenButton = true;
    [SerializeField] private string androidButtonLabel = "DEV";
    [SerializeField] private Vector2 androidButtonSize = new Vector2(96f, 56f);
    [SerializeField] private Vector2 androidButtonOffsetFromSafeTopLeft = new Vector2(14f, -84f);

    private Button androidOpenButton;
    private RectTransform androidOpenButtonRect;
    private Canvas devCanvas;
    private PlayFabAuthManager subscribedAuthManager;
    private string verifiedPlayFabId;
    private string pendingVerificationPlayFabId;
    private bool playerBuildAccessVerified;
    private bool verificationInFlight;
    private float nextAuthorizationRefreshTime;
    private Rect lastButtonSafeArea = new Rect(-1f, -1f, -1f, -1f);

    private static readonly Regex CommandRegex = new Regex(
        @"^\s*([A-Za-z]+)\s*\(\s*(-?\d+)?\s*\)\s*$",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Shared authorization boundary for debug-only services used by this console.
    /// Player builds are authorized only after this active console verifies the
    /// authenticated PlayFab account. The Editor remains available for development.
    /// </summary>
    public static bool HasAuthorizedDevAccess =>
        Application.isEditor ||
        (activeConsole != null &&
         activeConsole.isActiveAndEnabled &&
         activeConsole.CanExecuteCommands());

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        activeConsole = null;
    }

    private void Awake()
    {
        activeConsole = this;

        if (runButton != null)
        {
            runButton.onClick.RemoveListener(RunCurrentCode);
            runButton.onClick.AddListener(RunCurrentCode);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (panelRoot != null)
            panelRoot.SetActive(openPanelOnStart);

        devCanvas = panelRoot != null ? panelRoot.GetComponentInParent<Canvas>(true) : null;
        if (devCanvas != null)
        {
            // The console is a diagnostic overlay and must stay above gameplay HUDs.
            devCanvas.overrideSorting = true;
            devCanvas.sortingOrder = 32000;
        }

        if (IsAndroidPlayer() && createAndroidOpenButton)
            CreateAndroidOpenButton();

        ApplyAuthorizationState();

        SetConsoleMessage(defaultConsoleMessage, normalColor);
    }

    private void OnEnable()
    {
        EnsureAuthSubscription();
        RefreshPlayerBuildAuthorization();
    }

    private void Start()
    {
        EnsureAuthSubscription();
        RefreshPlayerBuildAuthorization();
    }

    private void OnDisable()
    {
        UnsubscribeFromAuthManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromAuthManager();

        if (activeConsole == this)
            activeConsole = null;
    }

    private void Update()
    {
        if (!Application.isEditor)
        {
            EnsureAuthSubscription();

            if (Time.unscaledTime >= nextAuthorizationRefreshTime)
            {
                nextAuthorizationRefreshTime = Time.unscaledTime + 2f;
                RefreshPlayerBuildAuthorization();
            }
        }

        if (androidOpenButtonRect != null && lastButtonSafeArea != Screen.safeArea)
            ApplyAndroidButtonSafeArea();

        if (CanUsePcToggle() && WasTogglePressedThisFrame())
        {
            if (panelRoot != null && panelRoot.activeSelf)
                ClosePanel();
            else
                OpenPanel();
        }

        if (panelRoot == null || !panelRoot.activeSelf)
            return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            RunCurrentCode();
    }

    public void OpenPanel()
    {
        if (!CanExecuteCommands())
        {
            ClosePanel();
            return;
        }

        if (panelRoot != null)
            panelRoot.SetActive(true);

        SetConsoleMessage(defaultConsoleMessage, normalColor);

        if (focusInputWhenOpened && codeInputField != null)
        {
            codeInputField.ActivateInputField();
            codeInputField.Select();
        }
    }

    public void ClosePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void RunCurrentCode()
    {
        string raw = codeInputField != null ? codeInputField.text : string.Empty;

        if (!TryExecuteCode(raw))
            return;

        if (clearInputOnSuccess && codeInputField != null)
            codeInputField.text = string.Empty;

        if (focusInputWhenOpened && codeInputField != null && panelRoot != null && panelRoot.activeSelf)
        {
            codeInputField.ActivateInputField();
            codeInputField.Select();
        }
    }

    public bool TryExecuteCode(string raw)
    {
        if (!CanExecuteCommands())
        {
            SetConsoleMessage(developmentBuildOnlyMessage, errorColor);
            return false;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            SetConsoleMessage(emptyInputMessage, errorColor);
            return false;
        }

        Match match = CommandRegex.Match(raw);
        if (!match.Success)
        {
            SetConsoleMessage(invalidSyntaxMessage, errorColor);
            return false;
        }

        string command = match.Groups[1].Value.Trim();
        bool hasValue = match.Groups[2].Success && !string.IsNullOrWhiteSpace(match.Groups[2].Value);
        int value = 0;
        if (hasValue && !int.TryParse(match.Groups[2].Value, out value))
        {
            SetConsoleMessage(invalidSyntaxMessage, errorColor);
            return false;
        }

        string normalized = command.ToLowerInvariant();
        bool expectsValue = normalized switch
        {
            "day" or "reputation" or "approval" or "money" or "addmoney" or
            "fillstocks" or "setcoin" or "addcoin" or "timescale" or
            "complaint" or "upgrade" or "unlockpopup" => true,
            _ => false
        };

        bool knownNoValueCommand = normalized switch
        {
            "endday" or "gameover" or "zerostocks" or "startday" or "resetrun" or
            "recover" or "save" or "status" or "help" or
            "wrongorder" or "burntfood" or "cardpayment" => true,
            _ => false
        };

        if (!expectsValue && !knownNoValueCommand)
            return FailUnknownCommand();

        if (expectsValue != hasValue)
        {
            SetConsoleMessage(invalidSyntaxMessage, errorColor);
            return false;
        }

        return normalized switch
        {
            "day" => TryRunDayCommand(value),
            "reputation" or "approval" => TryRunApprovalCommand(value),
            "money" => TryRunMoneyCommand(value),
            "addmoney" => TryRunAddMoneyCommand(value),
            "fillstocks" => TryRunFillStocksCommand(value),
            "setcoin" => TryRunSetCoinCommand(value),
            "addcoin" => TryRunAddCoinCommand(value),
            "timescale" => TryRunTimeScaleCommand(value),
            "complaint" => TryRunComplaintCommand(value),
            "upgrade" => TryRunUpgradeCommand(value),
            "unlockpopup" => TryRunUnlockPopupCommand(value),
            "wrongorder" => TryRunComplaintCommand((int)ManagerComplaintType.WrongOrder),
            "burntfood" => TryRunComplaintCommand((int)ManagerComplaintType.BurntFood),
            "cardpayment" => TryRunForceCardPaymentCommand(),
            "endday" => TryRunEndDayCommand(),
            "gameover" => TryRunGameOverCommand(),
            "zerostocks" => TryRunZeroStocksCommand(),
            "startday" => TryRunStartDayCommand(),
            "resetrun" => TryRunResetRunCommand(),
            "recover" => TryRunRecoverCommand(),
            "save" => TryRunSaveCommand(),
            "status" => TryRunStatusCommand(),
            "help" => TryRunHelpCommand(),
            _ => FailUnknownCommand()
        };
    }

    private bool TryRunDayCommand(int value)
    {
        if (value < 1 || value > 30)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        if (GameFlowManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        if (!GameFlowManager.Instance.TrySetCurrentDayDebug(value))
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        SetConsoleMessage($"SUCCESS: Day set to {value}.", successColor);
        return true;
    }

    private bool TryRunApprovalCommand(int value)
    {
        if (value < 0 || value > 100)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        if (AlienApprovalManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        if (!AlienApprovalManager.Instance.TrySetApprovalDebug(value))
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        SetConsoleMessage($"SUCCESS: Approval set to {value}.", successColor);
        return true;
    }

    private bool TryRunMoneyCommand(int value)
    {
        if (value < 0)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        if (MoneyManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        MoneyManager.Instance.SetMoney(value, "Dev Console");
        SetConsoleMessage($"SUCCESS: Money set to {value}.", successColor);
        return true;
    }

    private bool TryRunAddMoneyCommand(int value)
    {
        if (value < 0 || MoneyManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        MoneyManager.Instance.Earn(value, "Dev Console");
        SetConsoleMessage($"SUCCESS: Added {value} money.", successColor);
        return true;
    }

    private bool TryRunFillStocksCommand(int value)
    {
        if (value < 0 || value > 999999 || InventoryManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        InventoryManager.Instance.SetAllStock(value);
        GameSaveManager.Instance?.RequestSave();
        SetConsoleMessage($"SUCCESS: Every tracked stock set to {value}.", successColor);
        return true;
    }

    private bool TryRunZeroStocksCommand()
    {
        if (InventoryManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        InventoryManager.Instance.ResetStock();
        GameSaveManager.Instance?.RequestSave();
        SetConsoleMessage("SUCCESS: All tracked stocks emptied.", successColor);
        return true;
    }

    private bool TryRunSetCoinCommand(int value)
    {
        if (value < 0 || value > 1000000 || PlayFabWalletManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        SetConsoleMessage("PROCESSING: Updating PlayFab GC balance...", normalColor);
        PlayFabWalletManager.Instance.TrySetGoldCoinsDebug(
            value,
            balance => SetConsoleMessage($"SUCCESS: GC balance set to {balance}.", successColor),
            error => SetConsoleMessage("ERROR: " + error, errorColor));
        return true;
    }

    private bool TryRunAddCoinCommand(int value)
    {
        if (value < 0 || value > 1000000 || PlayFabWalletManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        SetConsoleMessage("PROCESSING: Adding GC through PlayFab...", normalColor);
        PlayFabWalletManager.Instance.TryAddGoldCoinsDebug(
            value,
            balance => SetConsoleMessage($"SUCCESS: GC balance is now {balance}.", successColor),
            error => SetConsoleMessage("ERROR: " + error, errorColor));
        return true;
    }

    private bool TryRunEndDayCommand()
    {
        if (GameDayManager.Instance == null || !GameDayManager.Instance.EndDayNowDebug())
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        SetConsoleMessage("SUCCESS: Day ended and results opened.", successColor);
        ClosePanel();
        return true;
    }

    private bool TryRunGameOverCommand()
    {
        if (AlienApprovalManager.Instance == null || GameDayManager.Instance == null ||
            !AlienApprovalManager.Instance.TrySetApprovalDebug(0) ||
            !GameDayManager.Instance.EndDayNowDebug())
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        SetConsoleMessage("SUCCESS: Game Over results opened.", successColor);
        ClosePanel();
        return true;
    }

    private bool TryRunStartDayCommand()
    {
        if (GameDayManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        GameDayManager.Instance.StartShift();
        if (!GameDayManager.Instance.ShiftRunning)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        SetConsoleMessage("SUCCESS: Restaurant service started.", successColor);
        ClosePanel();
        return true;
    }

    private bool TryRunResetRunCommand()
    {
        if (GameFlowManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        SetConsoleMessage("SUCCESS: Resetting campaign to Day 1.", successColor);
        GameFlowManager.Instance.ResetRun();
        return true;
    }

    private bool TryRunRecoverCommand()
    {
        if (AlienApprovalManager.Instance == null || MoneyManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        AlienApprovalManager.Instance.RestoreApprovalForContinue(
            Mathf.Max(30, AlienApprovalManager.Instance.Approval));
        if (MoneyManager.Instance.Money <= 0)
            MoneyManager.Instance.ResetToStartingMoney();

        SetConsoleMessage("SUCCESS: Approval is at least 30% and bankruptcy funds were restored.", successColor);
        return true;
    }

    private bool TryRunSaveCommand()
    {
        if (GameSaveManager.Instance == null)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        GameSaveManager.Instance.SaveGame();
        SetConsoleMessage("SUCCESS: Game saved.", successColor);
        return true;
    }

    private bool TryRunTimeScaleCommand(int value)
    {
        if (value < 0 || value > 10)
        {
            SetConsoleMessage(commandFailedMessage, errorColor);
            return false;
        }

        Time.timeScale = value;
        SetConsoleMessage($"SUCCESS: Time scale set to {value}.", successColor);
        return true;
    }

    private bool TryRunStatusCommand()
    {
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0;
        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;
        int money = MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0;
        string coins = PlayFabWalletManager.Instance != null && PlayFabWalletManager.Instance.HasLoadedWallet
            ? PlayFabWalletManager.Instance.GoldCoins.ToString()
            : "not loaded";

        int trackedStocks = 0;
        int totalStock = 0;
        if (InventoryManager.Instance != null && InventoryManager.Instance.Items != null)
        {
            foreach (ItemData item in InventoryManager.Instance.Items)
            {
                if (item == null || !InventoryManager.Instance.IsTracked(item.itemType))
                    continue;
                trackedStocks++;
                totalStock += InventoryManager.Instance.GetStock(item.itemType);
            }
        }

        SetConsoleMessage(
            $"STATUS\nDay: {day} | Reputation: {approval}% | Money: {money}\n" +
            $"GC: {coins} | Stock: {totalStock} across {trackedStocks} configured items\n" +
            $"Service active: {GameDayManager.Instance != null && GameDayManager.Instance.ServiceActive}",
            normalColor);
        return true;
    }

    private bool TryRunComplaintCommand(int value)
    {
        if (value != (int)ManagerComplaintType.WrongOrder &&
            value != (int)ManagerComplaintType.BurntFood)
        {
            SetConsoleMessage(
                "ERROR: complaint(1) = wrong order, complaint(2) = burnt food.",
                errorColor);
            return false;
        }

        ManagerComplaintSystem system = ManagerComplaintSystem.EnsureInstance();
        if (system == null || !system.DebugForceComplaint((ManagerComplaintType)value))
        {
            SetConsoleMessage(
                "ERROR: Start service and wait for a seated customer group first.",
                errorColor);
            return false;
        }

        SetConsoleMessage(
            value == (int)ManagerComplaintType.WrongOrder
                ? "SUCCESS: Wrong-order Manager complaint created."
                : "SUCCESS: Burnt-food Manager complaint created.",
            successColor);
        ClosePanel();
        return true;
    }

    private bool TryRunUpgradeCommand(int value)
    {
        string itemID = value switch
        {
            1 => EquipmentUpgradeService.BusserTrolleyID,
            2 => EquipmentUpgradeService.WaiterTrolleyID,
            3 => EquipmentUpgradeService.CardPaymentID,
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(itemID) || EquipmentManager.Instance == null ||
            !EquipmentManager.Instance.DebugUnlockAndPurchase(itemID))
        {
            SetConsoleMessage("ERROR: upgrade(1)=busser, upgrade(2)=waiter, upgrade(3)=card.", errorColor);
            return false;
        }

        SetConsoleMessage("SUCCESS: Upgrade unlocked and purchased.", successColor);
        return true;
    }

    private bool TryRunUnlockPopupCommand(int value)
    {
        string itemID = value switch
        {
            1 => EquipmentUpgradeService.BusserTrolleyID,
            2 => EquipmentUpgradeService.WaiterTrolleyID,
            3 => EquipmentUpgradeService.CardPaymentID,
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(itemID))
        {
            SetConsoleMessage("ERROR: unlockPopup(1..3).", errorColor);
            return false;
        }

        UnlockCelebrationManager.EnsureInstance().DebugReplayEquipment(itemID);
        SetConsoleMessage("SUCCESS: Unlock celebration queued.", successColor);
        ClosePanel();
        return true;
    }

    private bool TryRunForceCardPaymentCommand()
    {
        CardPaymentService.ForceNextCardPayment();
        SetConsoleMessage("SUCCESS: The next eligible payment will use a card.", successColor);
        return true;
    }

    private bool TryRunHelpCommand()
    {
        SetConsoleMessage(
            "DEV CODES\n" +
            "day(n), reputation(n), money(n), addMoney(n)\n" +
            "startDay(), endDay(), gameOver(), resetRun(), recover()\n" +
            "zeroStocks(), fillStocks(n), setCoin(n), addCoin(n)\n" +
            "complaint(1), complaint(2), wrongOrder(), burntFood()\n" +
            "upgrade(1..3), unlockPopup(1..3), cardPayment()\n" +
            "timeScale(n), save(), status(), help()",
            normalColor);
        return true;
    }

    private bool FailUnknownCommand()
    {
        SetConsoleMessage(invalidCommandMessage, errorColor);
        return false;
    }

    private void SetConsoleMessage(string message, Color color)
    {
        if (consoleText == null)
            return;

        consoleText.text = message;
        consoleText.color = color;
    }

    private bool CanUsePcToggle()
    {
        if (Application.isEditor)
            return allowToggleInEditor;

        return IsDesktopPlayer() && CanExecuteCommands();
    }

    private bool WasTogglePressedThisFrame()
    {
        bool legacyPressed = Input.GetKeyDown(toggleKey);
        bool inputSystemPressed = toggleKey == KeyCode.F10 &&
                                  Keyboard.current != null &&
                                  Keyboard.current.f10Key.wasPressedThisFrame;
        return legacyPressed || inputSystemPressed;
    }

    private bool CanExecuteCommands()
    {
        if (Application.isEditor)
            return true;

        return (IsDesktopPlayer() || IsAndroidPlayer()) && playerBuildAccessVerified;
    }

    private static bool IsDesktopPlayer()
    {
        return Application.platform == RuntimePlatform.WindowsPlayer ||
               Application.platform == RuntimePlatform.OSXPlayer ||
               Application.platform == RuntimePlatform.LinuxPlayer;
    }

    private static bool IsAndroidPlayer()
    {
        return Application.platform == RuntimePlatform.Android;
    }

    private void EnsureAuthSubscription()
    {
        PlayFabAuthManager current = PlayFabAuthManager.Instance;
        if (current == subscribedAuthManager)
            return;

        UnsubscribeFromAuthManager();
        subscribedAuthManager = current;
        if (subscribedAuthManager == null)
            return;

        subscribedAuthManager.OnLoginSuccess += HandleAuthChanged;
        subscribedAuthManager.OnLoggedOut += HandleAuthChanged;
    }

    private void UnsubscribeFromAuthManager()
    {
        if (subscribedAuthManager == null)
            return;

        subscribedAuthManager.OnLoginSuccess -= HandleAuthChanged;
        subscribedAuthManager.OnLoggedOut -= HandleAuthChanged;
        subscribedAuthManager = null;
    }

    private void HandleAuthChanged()
    {
        ClearVerifiedPlayerAccess();
        RefreshPlayerBuildAuthorization();
    }

    private void RefreshPlayerBuildAuthorization()
    {
        if (Application.isEditor)
        {
            ApplyAuthorizationState();
            return;
        }

        PlayFabAuthManager auth = PlayFabAuthManager.Instance;
        if (auth == null || !auth.IsLoggedIn || string.IsNullOrWhiteSpace(auth.PlayFabId))
        {
            ClearVerifiedPlayerAccess();
            return;
        }

        if (playerBuildAccessVerified &&
            string.Equals(verifiedPlayFabId, auth.PlayFabId, StringComparison.Ordinal))
        {
            ApplyAuthorizationState();
            return;
        }

        if (verificationInFlight &&
            string.Equals(pendingVerificationPlayFabId, auth.PlayFabId, StringComparison.Ordinal))
            return;

        string requestedPlayFabId = auth.PlayFabId;
        verificationInFlight = true;
        pendingVerificationPlayFabId = requestedPlayFabId;

        // Never authorize from PlayFabAuthManager.DisplayName: that value is cached in
        // PlayerPrefs for UI convenience and can be edited locally. This authenticated
        // API response is the source of truth for the account's unique PlayFab username.
        PlayFabClientAPI.GetAccountInfo(
            new GetAccountInfoRequest(),
            result => HandleAccountInfoVerified(requestedPlayFabId, result),
            error => HandleAccountInfoVerificationFailed(requestedPlayFabId, error));
    }

    private void HandleAccountInfoVerified(string requestedPlayFabId, GetAccountInfoResult result)
    {
        if (!string.Equals(pendingVerificationPlayFabId, requestedPlayFabId, StringComparison.Ordinal))
            return;

        verificationInFlight = false;
        pendingVerificationPlayFabId = null;

        PlayFabAuthManager auth = PlayFabAuthManager.Instance;
        bool currentSessionMatches = auth != null && auth.IsLoggedIn &&
                                     string.Equals(auth.PlayFabId, requestedPlayFabId, StringComparison.Ordinal);
        bool verified = currentSessionMatches && IsVerifiedAuthorizedAccount(
            result != null ? result.AccountInfo : null,
            requestedPlayFabId,
            authorizedPlayFabUsername);

        playerBuildAccessVerified = verified;
        verifiedPlayFabId = verified ? requestedPlayFabId : null;
        ApplyAuthorizationState();
    }

    private void HandleAccountInfoVerificationFailed(string requestedPlayFabId, PlayFabError error)
    {
        if (!string.Equals(pendingVerificationPlayFabId, requestedPlayFabId, StringComparison.Ordinal))
            return;

        verificationInFlight = false;
        pendingVerificationPlayFabId = null;
        playerBuildAccessVerified = false;
        verifiedPlayFabId = null;
        nextAuthorizationRefreshTime = Time.unscaledTime + 10f;
        ApplyAuthorizationState();

        Debug.LogWarning("DevSettingsConsole: PlayFab owner verification failed; dev access remains locked. " +
                         (error != null ? error.ErrorMessage : "Unknown PlayFab error."));
    }

    private void ClearVerifiedPlayerAccess()
    {
        verificationInFlight = false;
        pendingVerificationPlayFabId = null;
        playerBuildAccessVerified = false;
        verifiedPlayFabId = null;
        ApplyAuthorizationState();
    }

    private void ApplyAuthorizationState()
    {
        bool canExecute = CanExecuteCommands();

        if (androidOpenButton != null)
            androidOpenButton.gameObject.SetActive(IsAndroidPlayer() && canExecute);

        if (!canExecute && panelRoot != null && panelRoot.activeSelf)
            panelRoot.SetActive(false);
    }

    public static bool IsVerifiedAuthorizedAccount(
        UserAccountInfo accountInfo,
        string authenticatedPlayFabId,
        string authorizedUsername)
    {
        if (accountInfo == null || string.IsNullOrWhiteSpace(authenticatedPlayFabId) ||
            string.IsNullOrWhiteSpace(authorizedUsername))
            return false;

        return string.Equals(accountInfo.PlayFabId, authenticatedPlayFabId, StringComparison.Ordinal) &&
               string.Equals(accountInfo.Username, authorizedUsername.Trim(), StringComparison.Ordinal);
    }

    private void CreateAndroidOpenButton()
    {
        if (devCanvas == null || androidOpenButton != null)
            return;

        GameObject buttonObject = new GameObject(
            "AuthorizedMobileDevButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Shadow));
        buttonObject.layer = 5;
        buttonObject.transform.SetParent(devCanvas.transform, false);
        buttonObject.transform.SetAsLastSibling();

        androidOpenButtonRect = buttonObject.GetComponent<RectTransform>();
        androidOpenButtonRect.pivot = new Vector2(0f, 1f);
        androidOpenButtonRect.sizeDelta = new Vector2(
            Mathf.Max(88f, androidButtonSize.x),
            Mathf.Max(52f, androidButtonSize.y));

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(22, 106, 154, 245);

        Shadow shadow = buttonObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(0f, -4f);

        androidOpenButton = buttonObject.GetComponent<Button>();
        androidOpenButton.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = androidOpenButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(215, 244, 255, 255);
        colors.pressedColor = new Color32(160, 220, 245, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        androidOpenButton.colors = colors;
        androidOpenButton.onClick.AddListener(OpenPanel);

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.layer = 5;
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = string.IsNullOrWhiteSpace(androidButtonLabel) ? "DEV" : androidButtonLabel.Trim();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 22f;
        if (consoleText != null && consoleText.font != null)
            label.font = consoleText.font;

        ApplyAndroidButtonSafeArea();
    }

    private void ApplyAndroidButtonSafeArea()
    {
        if (androidOpenButtonRect == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safe = Screen.safeArea;
        androidOpenButtonRect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMax / Screen.height);
        androidOpenButtonRect.anchorMax = androidOpenButtonRect.anchorMin;
        androidOpenButtonRect.anchoredPosition = androidButtonOffsetFromSafeTopLeft;
        lastButtonSafeArea = safe;
    }
}
