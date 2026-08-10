using System;
using System.Collections;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles PlayFab authentication only: manual login and persistent
/// auto-login via a locally stored CustomID. Account creation happens on
/// your website - this class never registers a new user.
///
/// Single Responsibility: this class knows nothing about Photon, session
/// locking, heartbeats, or gameplay systems. Its only job is "am I logged
/// in, and who am I". Anything that needs to react to auth state (loading
/// player data, settings, entering the lobby, etc.) subscribes to the
/// events below instead of being called directly from here - see the
/// "FUTURE HOOKS" section at the bottom for where to plug that in.
///
/// PERSISTENCE NOTE: this component is historically attached to a UI
/// canvas GameObject (e.g. "Account Canvas") that can be nested under
/// other objects in the MainMenu scene hierarchy. DontDestroyOnLoad only
/// works reliably on ROOT GameObjects - if this object has a parent, Unity
/// either throws or silently fails to persist it, which is why
/// PlayFabAuthManager.Instance was becoming null after loading GameMenu.
/// Awake() below detaches from any parent before calling
/// DontDestroyOnLoad so the auth state survives scene changes regardless
/// of where this script lives in the MainMenu hierarchy. Because
/// detaching pulls the whole canvas out into DontDestroyOnLoad (and thus
/// into every following scene), authCanvas is separately hidden outside
/// MainMenu - see hideAuthUIOutsideMainMenu below - without touching this
/// script or its login state.
/// </summary>
public class PlayFabAuthManager : MonoBehaviour
{
    public static PlayFabAuthManager Instance { get; private set; }

    [Header("UI - Shared")]
    [SerializeField] private TMP_Text messageText;

    [Header("UI - Login")]
    [SerializeField] private TMP_InputField loginUsernameInput;
    [SerializeField] private TMP_InputField loginPasswordInput;
    [SerializeField] private Button loginButton;
    [Tooltip("Optional. If left empty, the label is auto-found on the login button's children.")]
    [SerializeField] private TMP_Text loginButtonLabel;
    [SerializeField] private string loginButtonIdleText = "Login";
    [SerializeField] private string loginButtonLoggedInText = "Sign Out";

    [Header("UI - Panels (optional)")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject accountPanel;
    [SerializeField] private TMP_Text accountUsernameText;
    [SerializeField] private TMP_Text accountPlayFabIdText;
    [SerializeField] private Button signOutButton;

    [Header("Message Animation")]
    [SerializeField] private float messageAnimDuration = 0.25f;
    [SerializeField] private AnimationCurve messageAnimCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Auth UI Visibility Across Scenes")]
    [Tooltip("If true, authCanvas is disabled automatically whenever a scene other than mainMenuSceneName loads, without disabling this script or signing out.")]
    [SerializeField] private bool hideAuthUIOutsideMainMenu = true;
    [Tooltip("Exact scene name where the auth/login UI should be visible.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Canvas to show/hide. If left empty, GetComponent<Canvas>() is used.")]
    [SerializeField] private Canvas authCanvas;

    [Header("Diagnostics")]
    [Tooltip("If true, prints step-by-step Debug.Log messages for auth lifecycle events. Warnings/errors always print regardless of this flag.")]
    [SerializeField] private bool verboseLogging = true;

    private const string PREF_CUSTOM_ID = "PF_CustomId";
    private const string PREF_USERNAME = "PF_Username";

    public bool IsLoggedIn { get; private set; }
    public string PlayFabId { get; private set; }
    public string DisplayName { get; private set; }

    private Coroutine messageAnimCoroutine;
    private Vector3 messageBaseScale = Vector3.one;

    // ================= HOOK-READY EVENTS =================
    // Subscribe from any other script - no direct reference to this class's
    // internals required. These fire regardless of whether the built-in UI
    // fields above are even assigned, so this class works headless too.
    public event Action OnLoginSuccess;
    public event Action<string> OnLoginFailed;
    public event Action OnLoggedOut;
    // ========================================================

    private void Awake()
    {
        PlayFabSettings.staticSettings.TitleId = "1021C5";

        if (verboseLogging)
            Debug.Log("PlayFabAuthManager: Awake() running on GameObject '" + gameObject.name + "'.");

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PlayFabAuthManager: duplicate instance detected on '" + gameObject.name + "'. Destroying this duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad only reliably persists ROOT GameObjects. If this
        // component ended up nested under another object in the scene
        // hierarchy (e.g. under a parent canvas/container), detach first so
        // login state actually survives scene changes.
        if (transform.parent != null)
        {
            Debug.LogWarning("PlayFabAuthManager was not on a root GameObject. Detaching before DontDestroyOnLoad so login survives scene changes.");
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);

        if (verboseLogging)
            Debug.Log("PlayFabAuthManager: this instance is now the singleton Instance and will persist across scenes.");

        if (authCanvas == null)
            authCanvas = GetComponent<Canvas>();

        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (loginButtonLabel == null && loginButton != null)
            loginButtonLabel = loginButton.GetComponentInChildren<TMP_Text>();

        if (messageText != null)
            messageBaseScale = messageText.transform.localScale;
    }

    private void Start()
    {
        // The login button now does double duty: it logs in while signed
        // out, and signs out while logged in. Its label swaps to match.
        loginButton?.onClick.AddListener(HandleLoginButtonClicked);
        signOutButton?.onClick.AddListener(SignOut);

        SetLoggedInUI(false);
        TryAutoLogin();

        // sceneLoaded doesn't always fire for the scene that's already
        // active when this object first runs, so evaluate the current
        // scene once manually right after subscribing.
        ApplyAuthCanvasVisibility(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (verboseLogging)
            Debug.Log("PlayFabAuthManager: OnDestroy() on GameObject '" + gameObject.name + "'.");

        if (PlayFabAuthManager.Instance == this)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleLoginButtonClicked()
    {
        if (IsLoggedIn)
            SignOut();
        else
            LoginButton();
    }

    // ================= SCENE-BASED AUTH UI VISIBILITY =================
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAuthCanvasVisibility(scene.name);
    }

    private void ApplyAuthCanvasVisibility(string sceneName)
    {
        if (!hideAuthUIOutsideMainMenu || authCanvas == null)
            return;

        bool isMainMenu = sceneName == mainMenuSceneName;
        authCanvas.enabled = isMainMenu;

        if (verboseLogging)
            Debug.Log("PlayFabAuthManager: scene '" + sceneName + "' loaded. authCanvas.enabled=" + isMainMenu +
                " (PlayFabAuthManager itself stays active and logged-in state is untouched).");
    }

    // ================= AUTO LOGIN =================
    private void TryAutoLogin()
    {
        string savedCustomId = PlayerPrefs.GetString(PREF_CUSTOM_ID, "");
        if (string.IsNullOrEmpty(savedCustomId)) return;

        SetMessage("Logging in...");

        var request = new LoginWithCustomIDRequest
        {
            CustomId = savedCustomId,
            CreateAccount = false
        };

        PlayFabClientAPI.LoginWithCustomID(request, HandleLoginSuccess, error =>
        {
            Debug.LogWarning("Auto login failed: " + error.ErrorMessage);
            PlayerPrefs.DeleteKey(PREF_CUSTOM_ID);
            PlayerPrefs.Save();

            SetMessage("Please log in.");
            SetLoggedInUI(false);
        });
    }

    // ================= MANUAL LOGIN =================
    public void LoginButton()
    {
        string username = loginUsernameInput != null ? loginUsernameInput.text : "";
        string password = loginPasswordInput != null ? loginPasswordInput.text : "";
        Login(username, password);
    }

    public void Login(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            const string error = "Enter username and password";
            SetMessage(error);
            OnLoginFailed?.Invoke(error);
            return;
        }

        SetMessage("Logging in...");

        PlayFabClientAPI.LoginWithPlayFab(
            new LoginWithPlayFabRequest { Username = username, Password = password },
            result =>
            {
                PlayerPrefs.SetString(PREF_USERNAME, username);
                PlayerPrefs.Save();

                HandleLoginSuccess(result);
                EnsureCustomIdLinked();
            },
            error => HandleAuthError(error, OnLoginFailed)
        );
    }

    private void HandleLoginSuccess(LoginResult result)
    {
        IsLoggedIn = true;
        PlayFabId = result.PlayFabId;
        DisplayName = PlayerPrefs.GetString(PREF_USERNAME, "Player");

        if (verboseLogging)
            Debug.Log("PlayFabAuthManager: login succeeded. PlayFabId=" + PlayFabId);

        SetMessage("Logged in!");
        SetLoggedInUI(true);

        // Notify any subscribers (data loaders, lobby controllers, etc.)
        OnLoginSuccess?.Invoke();
    }

    // Links a locally generated CustomID to the account so future sessions
    // can auto-login without re-entering a password.
    private void EnsureCustomIdLinked()
    {
        string customId = PlayerPrefs.GetString(PREF_CUSTOM_ID, "");
        if (string.IsNullOrEmpty(customId))
        {
            customId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PREF_CUSTOM_ID, customId);
            PlayerPrefs.Save();
        }

        var link = new LinkCustomIDRequest { CustomId = customId, ForceLink = true };

        PlayFabClientAPI.LinkCustomID(link,
            _ => Debug.Log("CustomID linked for auto-login."),
            error => Debug.LogWarning("CustomID link warning: " + error.ErrorMessage));
    }

    // ================= SIGN OUT =================
    public void SignOut()
    {
        PlayFabClientAPI.ForgetAllCredentials();

        PlayerPrefs.DeleteKey(PREF_CUSTOM_ID);
        PlayerPrefs.DeleteKey(PREF_USERNAME);
        PlayerPrefs.Save();

        IsLoggedIn = false;
        PlayFabId = null;
        DisplayName = null;

        if (verboseLogging)
            Debug.Log("PlayFabAuthManager: SignOut() called. Local credentials cleared.");

        SetMessage("Signed out.");
        SetLoggedInUI(false);

        OnLoggedOut?.Invoke();
    }

    // ================= ERROR HANDLING =================
    private void HandleAuthError(PlayFabError error, Action<string> failureEvent)
    {
        string friendlyMessage = ToFriendlyMessage(error);
        Debug.LogError(error.GenerateErrorReport());

        SetMessage(friendlyMessage);
        failureEvent?.Invoke(friendlyMessage);
    }

    // Maps common PlayFab error codes to messages a player can actually act on.
    // Falls back to PlayFab's own message for anything not covered here.
    private string ToFriendlyMessage(PlayFabError error)
    {
        switch (error.Error)
        {
            case PlayFabErrorCode.InvalidUsernameOrPassword:
            case PlayFabErrorCode.AccountNotFound:
                return "Incorrect username or password.";
            case PlayFabErrorCode.ConnectionError:
                return "Connection error. Please check your internet and try again.";
            default:
                return error.ErrorMessage;
        }
    }

    // ================= UI STATE =================
    private void SetLoggedInUI(bool loggedIn)
    {
        if (loginPanel != null) loginPanel.SetActive(!loggedIn);
        if (accountPanel != null) accountPanel.SetActive(loggedIn);

        if (loginButtonLabel != null)
            loginButtonLabel.text = loggedIn ? loginButtonLoggedInText : loginButtonIdleText;

        if (loggedIn)
        {
            if (accountUsernameText != null) accountUsernameText.text = DisplayName;
            if (accountPlayFabIdText != null) accountPlayFabIdText.text = PlayFabId ?? "(none)";
        }
    }

    // ================= MESSAGE + ANIMATION =================
    private void SetMessage(string text)
    {
        if (messageText == null) return;

        messageText.text = text;

        if (messageAnimCoroutine != null) StopCoroutine(messageAnimCoroutine);
        messageAnimCoroutine = StartCoroutine(AnimateMessage());
    }

    // Quick fade + pop-in: the message starts slightly scaled down and
    // transparent, then eases up to full size and opacity. Uses
    // unscaledDeltaTime so it still plays correctly if Time.timeScale
    // is ever changed (e.g. a pause menu).
    private IEnumerator AnimateMessage()
    {
        Transform t = messageText.transform;
        Color baseColor = messageText.color;

        // Animate relative to the text's actual original scale, not an
        // absolute Vector3.one - on a World Space canvas that original
        // scale can be tiny (e.g. ~0.003), and overwriting it with ~1
        // is what made the text balloon to a huge size.
        Vector3 startScale = messageBaseScale * 0.85f;
        Vector3 endScale = messageBaseScale;

        float elapsed = 0f;
        while (elapsed < messageAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = messageAnimCurve.Evaluate(Mathf.Clamp01(elapsed / messageAnimDuration));

            t.localScale = Vector3.LerpUnclamped(startScale, endScale, progress);
            messageText.color = new Color(baseColor.r, baseColor.g, baseColor.b, progress);

            yield return null;
        }

        t.localScale = endScale;
        messageText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
        messageAnimCoroutine = null;
    }

    // ================= FUTURE HOOKS =================
    // From any other script:
    //
    //   void OnEnable()  => PlayFabAuthManager.Instance.OnLoginSuccess += HandleLogin;
    //   void OnDisable() => PlayFabAuthManager.Instance.OnLoginSuccess -= HandleLogin;
    //
    //   void HandleLogin()
    //   {
    //       // e.g. fetch player data, settings, cosmetics, etc. here -
    //       // this class deliberately doesn't know that any of that exists.
    //   }
    // ========================================================
}