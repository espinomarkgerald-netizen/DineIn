using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the player's wallet (Gold Coins / Normal Money) on the existing
/// top-screen TMP texts, and drives opening/closing a wallet or shop panel.
///
/// Single Responsibility: this script never talks to PlayFab directly. It
/// only reads PlayFabWalletManager.Instance's cached values and calls its
/// public methods (RefreshWallet / StartPolling / StopPolling), the same
/// way SettingsController would talk to SettingsManager rather than to
/// PlayFab itself.
///
/// Panel show/hide is delegated to an optional AnimatedPanel component on
/// walletOrShopPanel. If that component is present, Open()/Close() animate
/// it. If not, it falls back to a plain SetActive so nothing breaks on
/// panels that don't have AnimatedPanel attached yet.
///
/// Notifications: noteworthy events (missing manager references, not
/// logged in, refresh failures) are reported through the standalone
/// NotificationPopupController - see ReportStatus() below - instead of
/// only writing to statusText. This script never animates anything itself
/// and knows nothing about how the popup is displayed; it just calls
/// Show()/ShowPersistent() the same way any other script in the project
/// would.
/// </summary>
public class WalletUIController : MonoBehaviour
{
    [Header("Currency Labels (existing top-screen TMP texts)")]
    [SerializeField] private TMP_Text goldCoinsText;
    [SerializeField] private TMP_Text normalMoneyText;

    [Header("Optional Status / Panel")]
    [SerializeField] private TMP_Text statusText;
    [Tooltip("The wallet or shop panel this controller opens/closes. Optional. " +
             "If it has an AnimatedPanel component, that is used automatically.")]
    [SerializeField] private GameObject walletOrShopPanel;
    [Tooltip("Optional manual refresh button wired to RefreshWalletButton().")]
    [SerializeField] private Button refreshButton;
    [Tooltip("Optional close button wired to CloseWalletOrShopPanel().")]
    [SerializeField] private Button closeButton;
    [Tooltip("Optional open button wired to OpenWalletOrShopPanel().")]
    [SerializeField] private Button openButton;

    [Header("Display Formatting")]
    [SerializeField] private string goldPrefix = "G";
    [SerializeField] private string moneyPrefix = "$";
    [Tooltip("Shown in place of a value when logged out, cleared, or not yet loaded.")]
    [SerializeField] private string emptyValueText = "--";

    [Header("Auto Refresh")]
    [Tooltip("If true, this visible HUD starts wallet polling automatically while this controller is enabled.")]
    [SerializeField] private bool startPollingOnEnable = true;
    [Tooltip("If true, this controller requests a wallet refresh as soon as it is ready and the player is logged in.")]
    [SerializeField] private bool refreshOnEnable = true;

    [Header("Notifications")]
    [Tooltip("Optional. If left empty, NotificationPopupController.Instance is used automatically when one exists in the scene.")]
    [SerializeField] private NotificationPopupController popupOverride;
    [Tooltip("If true, warnings/errors/info from this controller are also surfaced through NotificationPopupController, in addition to statusText.")]
    [SerializeField] private bool showPopupNotifications = true;

    private PlayFabWalletManager subscribedWalletManager;
    private AnimatedPanel animatedPanel;

    private NotificationPopupController Popup => popupOverride != null ? popupOverride : NotificationPopupController.Instance;

    private void OnEnable()
    {
        CachePanelReference();
        TrySubscribeToWallet();
        RefreshFromCachedWallet();
        RefreshAndPollIfReady();

        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshWalletButton);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseWalletOrShopPanel);

        if (openButton != null)
            openButton.onClick.AddListener(OpenWalletOrShopPanel);
    }

    private void Start()
    {
        // Unity can enable this UI before PlayFabWalletManager has assigned
        // its singleton. Start runs after all Awake calls, so retry here and
        // pull any wallet value that may have loaded before we subscribed.
        CachePanelReference();
        TrySubscribeToWallet();
        RefreshFromCachedWallet();
        RefreshAndPollIfReady();
    }

    private void OnDisable()
    {
        UnsubscribeFromWallet();

        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(RefreshWalletButton);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseWalletOrShopPanel);

        if (openButton != null)
            openButton.onClick.RemoveListener(OpenWalletOrShopPanel);

        PlayFabWalletManager.Instance?.StopPolling();
    }

    private void CachePanelReference()
    {
        if (walletOrShopPanel == null)
        {
            animatedPanel = null;
            return;
        }

        // GetComponent is cheap but there's no reason to call it every frame -
        // cache it once the panel reference is known.
        if (animatedPanel == null || animatedPanel.gameObject != walletOrShopPanel)
            animatedPanel = walletOrShopPanel.GetComponent<AnimatedPanel>();
    }

    private void TrySubscribeToWallet()
    {
        var wallet = PlayFabWalletManager.Instance;
        if (wallet == null)
        {
            Debug.LogWarning("WalletUIController: could not subscribe, PlayFabWalletManager.Instance is null.");
            return;
        }

        if (subscribedWalletManager == wallet)
            return;

        UnsubscribeFromWallet();

        subscribedWalletManager = wallet;
        subscribedWalletManager.OnWalletUpdated += HandleWalletUpdated;
        subscribedWalletManager.OnWalletRefreshFailed += HandleWalletRefreshFailed;
        subscribedWalletManager.OnWalletCleared += HandleWalletCleared;
    }

    private void UnsubscribeFromWallet()
    {
        if (subscribedWalletManager == null)
            return;

        subscribedWalletManager.OnWalletUpdated -= HandleWalletUpdated;
        subscribedWalletManager.OnWalletRefreshFailed -= HandleWalletRefreshFailed;
        subscribedWalletManager.OnWalletCleared -= HandleWalletCleared;
        subscribedWalletManager = null;
    }

    private void RefreshFromCachedWallet()
    {
        var wallet = PlayFabWalletManager.Instance;
        if (wallet != null && wallet.HasLoadedWallet)
            HandleWalletUpdated(wallet.GoldCoins, wallet.NormalMoney);
        else
            ShowEmptyValues();
    }

    private void RefreshAndPollIfReady()
    {
        var wallet = PlayFabWalletManager.Instance;
        if (wallet == null ||
            PlayFabAuthManager.Instance == null ||
            !PlayFabAuthManager.Instance.IsLoggedIn)
        {
            return;
        }

        if (refreshOnEnable && !wallet.HasLoadedWallet)
            wallet.RefreshWallet();

        if (startPollingOnEnable)
            wallet.StartPolling();
    }

    // ================= PANEL SHOW/HIDE =================
    /// <summary>
    /// Shows the panel. Uses AnimatedPanel.Open() if the panel has that
    /// component, otherwise falls back to a plain SetActive(true).
    /// </summary>
    private void ShowPanel()
    {
        if (walletOrShopPanel == null)
        {
            Debug.LogWarning("WalletUIController: walletOrShopPanel is not assigned in the Inspector.");
            return;
        }

        CachePanelReference();

        if (animatedPanel != null)
            animatedPanel.Open();
        else
            walletOrShopPanel.SetActive(true);
    }

    /// <summary>
    /// Hides the panel. Uses AnimatedPanel.Close() if the panel has that
    /// component, otherwise falls back to a plain SetActive(false).
    /// </summary>
    private void HidePanel()
    {
        if (walletOrShopPanel == null)
            return;

        CachePanelReference();

        if (animatedPanel != null)
            animatedPanel.Close();
        else
            walletOrShopPanel.SetActive(false);
    }

    // ================= EVENT HANDLERS =================
    private void HandleWalletUpdated(int goldCoins, int normalMoney)
    {
        if (goldCoinsText != null) goldCoinsText.text = goldPrefix + goldCoins;
        if (normalMoneyText != null) normalMoneyText.text = moneyPrefix + normalMoney;
        SetStatus("");
    }

    private void HandleWalletRefreshFailed(string message)
    {
        // Not logged in, or a genuine PlayFab error - either way, don't
        // leave a stale balance on screen. PlayFabWalletManager already
        // logs the full error itself, so don't duplicate a warning here -
        // just surface it to the player via status text + popup.
        ShowEmptyValues();
        ReportStatus(message, NotificationPopupController.PopupType.Warning, logWarning: false);
    }

    private void HandleWalletCleared()
    {
        ShowEmptyValues();
    }

    private void ShowEmptyValues()
    {
        if (goldCoinsText != null) goldCoinsText.text = goldPrefix + emptyValueText;
        if (normalMoneyText != null) normalMoneyText.text = moneyPrefix + emptyValueText;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    /// <summary>
    /// Central place that reports a noteworthy status/warning/error: updates
    /// the inline statusText (if assigned), optionally logs a
    /// Debug.LogWarning, and - if showPopupNotifications is true and a
    /// NotificationPopupController exists - surfaces it as a popup too.
    /// </summary>
    private void ReportStatus(string message, NotificationPopupController.PopupType popupType, bool logWarning = true, string logDetail = null)
    {
        SetStatus(message);

        if (logWarning)
            Debug.LogWarning("WalletUIController: " + (string.IsNullOrEmpty(logDetail) ? message : logDetail));

        if (showPopupNotifications && !string.IsNullOrEmpty(message))
            Popup?.Show(message, popupType);
    }

    // ================= PUBLIC UI HOOKS =================
    /// <summary>Wire to a manual refresh button.</summary>
    public void RefreshWalletButton()
    {
        // Explicit checks instead of a single null-propagating call, so the
        // status label (and popup) always reflect why nothing happened
        // instead of silently doing nothing and staying on "Refreshing...".
        if (PlayFabWalletManager.Instance == null)
        {
            ReportStatus("Wallet manager missing.", NotificationPopupController.PopupType.Error,
                logDetail: "RefreshWalletButton - PlayFabWalletManager.Instance is null.");
            return;
        }

        if (PlayFabAuthManager.Instance == null)
        {
            ReportStatus("Auth manager missing. Start from MainMenu/login scene.", NotificationPopupController.PopupType.Error,
                logDetail: "RefreshWalletButton - PlayFabAuthManager.Instance is null.");
            return;
        }

        if (!PlayFabAuthManager.Instance.IsLoggedIn)
        {
            ReportStatus("Not logged in.", NotificationPopupController.PopupType.Warning,
                logDetail: "RefreshWalletButton - PlayFabAuthManager.Instance.IsLoggedIn is false.");
            return;
        }

        SetStatus("Refreshing...");
        TrySubscribeToWallet();
        PlayFabWalletManager.Instance.RefreshWallet();
    }

    /// <summary>Wire to the button/toggle that opens the wallet or shop panel.</summary>
    public void OpenWalletOrShopPanel()
    {
        ShowPanel();

        if (PlayFabWalletManager.Instance == null)
        {
            ReportStatus("Wallet manager missing.", NotificationPopupController.PopupType.Error,
                logDetail: "OpenWalletOrShopPanel - PlayFabWalletManager.Instance is null.");
            ShowEmptyValues();
            return;
        }

        if (PlayFabAuthManager.Instance == null)
        {
            ReportStatus("Auth manager missing. Start from MainMenu/login scene.", NotificationPopupController.PopupType.Error,
                logDetail: "OpenWalletOrShopPanel - PlayFabAuthManager.Instance is null.");
            ShowEmptyValues();
            return;
        }

        if (!PlayFabAuthManager.Instance.IsLoggedIn)
        {
            ReportStatus("Not logged in.", NotificationPopupController.PopupType.Warning,
                logDetail: "OpenWalletOrShopPanel - PlayFabAuthManager.Instance.IsLoggedIn is false.");
            ShowEmptyValues();
            return;
        }

        SetStatus("Loading...");
        TrySubscribeToWallet();
        PlayFabWalletManager.Instance.RefreshWallet();
        PlayFabWalletManager.Instance.StartPolling();
    }

    /// <summary>Wire to the button/toggle that closes the wallet or shop panel.</summary>
    public void CloseWalletOrShopPanel()
    {
        HidePanel();
        PlayFabWalletManager.Instance?.StopPolling();
    }
}