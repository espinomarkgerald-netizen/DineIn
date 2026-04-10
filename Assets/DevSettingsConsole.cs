using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
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

    [Header("Behavior")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F10;
    [SerializeField] private bool openPanelOnStart;
    [SerializeField] private bool clearInputOnSuccess = true;
    [SerializeField] private bool focusInputWhenOpened = true;
    [SerializeField] private bool allowToggleInEditor = true;

    private static readonly Regex CommandRegex = new Regex(
        @"^\s*([A-Za-z]+)\s*\(\s*(-?\d+)\s*\)\s*$",
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
        if (CanUsePcToggle() && Input.GetKeyDown(toggleKey))
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

        if (string.IsNullOrWhiteSpace(raw))
        {
            SetConsoleMessage(emptyInputMessage, errorColor);
            return;
        }

        Match match = CommandRegex.Match(raw);
        if (!match.Success)
        {
            SetConsoleMessage(invalidSyntaxMessage, errorColor);
            return;
        }

        string command = match.Groups[1].Value.Trim();
        if (!int.TryParse(match.Groups[2].Value, out int value))
        {
            SetConsoleMessage(invalidSyntaxMessage, errorColor);
            return;
        }

        bool success = command.ToLowerInvariant() switch
        {
            "day" => TryRunDayCommand(value),
            "approval" => TryRunApprovalCommand(value),
            "money" => TryRunMoneyCommand(value),
            _ => FailUnknownCommand()
        };

        if (!success)
            return;

        if (clearInputOnSuccess && codeInputField != null)
            codeInputField.text = string.Empty;

        if (focusInputWhenOpened && codeInputField != null && panelRoot != null && panelRoot.activeSelf)
        {
            codeInputField.ActivateInputField();
            codeInputField.Select();
        }
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

        return Application.platform == RuntimePlatform.WindowsPlayer ||
               Application.platform == RuntimePlatform.OSXPlayer ||
               Application.platform == RuntimePlatform.LinuxPlayer;
    }
}