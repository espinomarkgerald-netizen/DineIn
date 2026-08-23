using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DevSettingsConsole : MonoBehaviour
{
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
        "ERROR: Dev codes are available only in the Unity Editor or desktop PC builds.";

    [Header("Behavior")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F10;
    [SerializeField] private bool openPanelOnStart;
    [SerializeField] private bool clearInputOnSuccess = true;
    [SerializeField] private bool focusInputWhenOpened = true;
    [SerializeField] private bool allowToggleInEditor = true;

    private static readonly Regex CommandRegex = new Regex(
        @"^\s*([A-Za-z]+)\s*\(\s*(-?\d+)?\s*\)\s*$",
        RegexOptions.Compiled
    );

    private void Awake()
    {
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

        SetConsoleMessage(defaultConsoleMessage, normalColor);
    }

    private void Update()
    {
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
            "fillstocks" or "setcoin" or "addcoin" or "timescale" => true,
            _ => false
        };

        bool knownNoValueCommand = normalized switch
        {
            "endday" or "gameover" or "zerostocks" or "startday" or "resetrun" or
            "recover" or "save" or "status" or "help" => true,
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

    private bool TryRunHelpCommand()
    {
        SetConsoleMessage(
            "DEV CODES\n" +
            "day(n), reputation(n), money(n), addMoney(n)\n" +
            "startDay(), endDay(), gameOver(), resetRun(), recover()\n" +
            "zeroStocks(), fillStocks(n), setCoin(n), addCoin(n)\n" +
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

        return IsDesktopPlayer();
    }

    private bool WasTogglePressedThisFrame()
    {
        bool legacyPressed = Input.GetKeyDown(toggleKey);
        bool inputSystemPressed = toggleKey == KeyCode.F10 &&
                                  Keyboard.current != null &&
                                  Keyboard.current.f10Key.wasPressedThisFrame;
        return legacyPressed || inputSystemPressed;
    }

    private static bool CanExecuteCommands()
    {
        return Application.isEditor || IsDesktopPlayer();
    }

    private static bool IsDesktopPlayer()
    {
        return Application.platform == RuntimePlatform.WindowsPlayer ||
               Application.platform == RuntimePlatform.OSXPlayer ||
               Application.platform == RuntimePlatform.LinuxPlayer;
    }
}
