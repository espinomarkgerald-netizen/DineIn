using System;
using System.Collections;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

/// <summary>
/// Owns the player's PlayFab virtual currency balances (Gold Coins / Normal
/// Money): reading them from PlayFab and caching the latest known values.
///
/// Single Responsibility: this class knows nothing about how to log in or
/// out - it only reads PlayFabAuthManager.Instance.IsLoggedIn and listens
/// to its OnLoginSuccess / OnLoggedOut events, the same pattern
/// SettingsManager uses. It never calls Login/SignOut, and
/// PlayFabAuthManager has zero knowledge this class exists. It also knows
/// nothing about spending currency - that belongs to CurrencyExchangeManager,
/// which calls RefreshWallet() on this class after a purchase instead of
/// touching PlayFab directly.
///
/// UI scripts (like WalletUIController) don't talk to PlayFab directly at
/// all - they call RefreshWallet() / StartPolling() / StopPolling() and
/// listen to OnWalletUpdated / OnWalletRefreshFailed / OnWalletCleared to
/// refresh labels.
/// </summary>
public class PlayFabWalletManager : MonoBehaviour
{
    public static PlayFabWalletManager Instance { get; private set; }

    [Header("Currency Codes")]
    [Tooltip("PlayFab virtual currency code for the premium currency.")]
    [SerializeField] private string goldCurrencyCode = "GC";
    [Tooltip("PlayFab virtual currency code for the temporary in-game money.")]
    [SerializeField] private string moneyCurrencyCode = "NM";

    [Header("Behaviour")]
    [Tooltip("Automatically call RefreshWallet() when PlayFabAuthManager.OnLoginSuccess fires.")]
    [SerializeField] private bool refreshOnLogin = true;
    [Tooltip("Automatically clear cached wallet values when PlayFabAuthManager.OnLoggedOut fires.")]
    [SerializeField] private bool clearOnLogout = true;

    [Header("Polling")]
    [Tooltip("Master switch. If false, StartPolling() does nothing.")]
    [SerializeField] private bool enablePolling = true;
    [Tooltip("Seconds between automatic refreshes while polling is active (e.g. wallet/shop panel open).")]
    [SerializeField] private float pollingIntervalSeconds = 5f;

    [Header("Diagnostics")]
    [Tooltip("If true, prints step-by-step Debug.Log messages for login state and wallet refreshes. Warnings/errors always print regardless of this flag.")]
    [SerializeField] private bool verboseLogging = true;

    public int GoldCoins { get; private set; }
    public int NormalMoney { get; private set; }

    /// <summary>True once a successful GetUserInventory response has been applied at least once since the last login/clear.</summary>
    public bool HasLoadedWallet { get; private set; }

    /// <summary>True while a GetUserInventory request is in flight.</summary>
    public bool IsRefreshing { get; private set; }

    // ================= HOOK-READY EVENTS =================
    // Subscribe from any other script - no direct reference to this class's
    // internals required.
    public event Action<int, int> OnWalletUpdated;     // (goldCoins, normalMoney)
    public event Action<string> OnWalletRefreshFailed;  // friendly error message
    public event Action OnWalletCleared;                // fired on logout
    // ========================================================

    private Coroutine pollCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (PlayFabAuthManager.Instance != null)
        {
            PlayFabAuthManager.Instance.OnLoginSuccess += HandleLoginSuccess;
            PlayFabAuthManager.Instance.OnLoggedOut += HandleLoggedOut;

            if (verboseLogging)
            {
                string idText = string.IsNullOrEmpty(PlayFabAuthManager.Instance.PlayFabId)
                    ? "(none)"
                    : PlayFabAuthManager.Instance.PlayFabId;

                Debug.Log("PlayFabWalletManager: PlayFabAuthManager found. IsLoggedIn="
                    + PlayFabAuthManager.Instance.IsLoggedIn + ", PlayFabId=" + idText);
            }

            // Covers the case where this Start() runs after auto-login has
            // already completed on PlayFabAuthManager (e.g. script order).
            if (refreshOnLogin && PlayFabAuthManager.Instance.IsLoggedIn)
                RefreshWallet();
        }
        else
        {
            Debug.LogWarning("PlayFabWalletManager: PlayFabAuthManager.Instance is null. " +
                "If testing GameMenu directly, login state will not exist. " +
                "Start from MainMenu or make auth persistent before GameMenu.");
        }
    }

    private void OnDestroy()
    {
        if (PlayFabAuthManager.Instance != null)
        {
            PlayFabAuthManager.Instance.OnLoginSuccess -= HandleLoginSuccess;
            PlayFabAuthManager.Instance.OnLoggedOut -= HandleLoggedOut;
        }
    }

    private void HandleLoginSuccess()
    {
        if (refreshOnLogin)
            RefreshWallet();
    }

    private void HandleLoggedOut()
    {
        StopPolling();

        if (!clearOnLogout) return;

        GoldCoins = 0;
        NormalMoney = 0;
        HasLoadedWallet = false;

        OnWalletCleared?.Invoke();
    }

    // ================= REFRESH =================
    /// <summary>
    /// Reads current GC / NM balances from PlayFab. Safe to call any time -
    /// no-ops (with a failure event) if nobody is logged in. Overlapping
    /// calls are ignored while a request is already in flight.
    /// </summary>
    public void RefreshWallet()
    {
        if (verboseLogging)
            Debug.Log("PlayFabWalletManager: RefreshWallet() called.");

        if (PlayFabAuthManager.Instance == null)
        {
            Debug.LogWarning("PlayFabWalletManager: RefreshWallet skipped - PlayFabAuthManager.Instance is null.");
            OnWalletRefreshFailed?.Invoke("Auth manager missing.");
            return;
        }

        if (!PlayFabAuthManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("PlayFabWalletManager: RefreshWallet skipped - not logged in.");
            OnWalletRefreshFailed?.Invoke("Not logged in.");
            return;
        }

        if (IsRefreshing)
        {
            if (verboseLogging)
                Debug.Log("PlayFabWalletManager: RefreshWallet skipped - a refresh is already in flight.");
            return;
        }

        IsRefreshing = true;

        if (verboseLogging)
            Debug.Log("PlayFabWalletManager: Calling GetUserInventory...");

        PlayFabClientAPI.GetUserInventory(
            new GetUserInventoryRequest(),
            result =>
            {
                IsRefreshing = false;

                int gc = 0;
                int nm = 0;

                if (result.VirtualCurrency != null)
                {
                    result.VirtualCurrency.TryGetValue(goldCurrencyCode, out gc);
                    result.VirtualCurrency.TryGetValue(moneyCurrencyCode, out nm);
                }

                GoldCoins = gc;
                NormalMoney = nm;
                HasLoadedWallet = true;

                if (verboseLogging)
                    Debug.Log("PlayFabWalletManager: GetUserInventory succeeded. GC=" + gc + ", NM=" + nm);

                OnWalletUpdated?.Invoke(GoldCoins, NormalMoney);
            },
            error =>
            {
                IsRefreshing = false;
                Debug.LogWarning("PlayFabWalletManager.RefreshWallet failed: " + error.GenerateErrorReport());
                OnWalletRefreshFailed?.Invoke(error.ErrorMessage);
            }
        );
    }

    /// <summary>Convenience check for UI/shop scripts before attempting a spend.</summary>
    public bool HasEnoughGoldCoins(int amount)
    {
        return HasLoadedWallet && GoldCoins >= amount;
    }

    // ================= POLLING =================
    /// <summary>Call when wallet-facing UI opens (e.g. WalletUIController.OpenWalletOrShopPanel). No-op if enablePolling is false.</summary>
    public void StartPolling()
    {
        if (!enablePolling) return;
        if (pollCoroutine != null) return;
        pollCoroutine = StartCoroutine(PollRoutine());
    }

    /// <summary>Call when wallet-facing UI closes (e.g. WalletUIController.CloseWalletOrShopPanel).</summary>
    public void StopPolling()
    {
        if (pollCoroutine == null) return;
        StopCoroutine(pollCoroutine);
        pollCoroutine = null;
    }

    private IEnumerator PollRoutine()
    {
        var wait = new WaitForSeconds(Mathf.Max(1f, pollingIntervalSeconds));
        while (true)
        {
            yield return wait;

            if (PlayFabAuthManager.Instance != null && PlayFabAuthManager.Instance.IsLoggedIn)
                RefreshWallet();
        }
    }

    // ================= FUTURE HOOKS =================
    // From any other script (shop UI, HUD currency display, etc.):
    //
    //   void OnEnable()  => PlayFabWalletManager.Instance.OnWalletUpdated += HandleWallet;
    //   void OnDisable() => PlayFabWalletManager.Instance.OnWalletUpdated -= HandleWallet;
    //
    //   void HandleWallet(int gold, int money)
    //   {
    //       // update a HUD label, gate a purchase button, etc.
    //   }
    // ========================================================
}