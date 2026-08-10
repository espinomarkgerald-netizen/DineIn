using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

using Photon.Pun;
using Photon.Realtime;

using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PlayfabManager : MonoBehaviourPunCallbacks
{
    public static PlayfabManager Instance;

    [Header("UI - Shared")]
    public TMP_Text messageText;

    [Header("LOGIN")]
    public TMP_InputField loginUsernameInput;
    public TMP_InputField loginPasswordInput;
    public Button loginButton;

    [Header("REGISTER")]
    public TMP_InputField registerUsernameInput;
    public TMP_InputField registerEmailInput;
    public TMP_InputField registerPasswordInput;
    public Button registerButton;

    [Header("CUSTOMIZATION")]
    public Button saveCustomizationButton;

    [Header("ACCOUNT UI (After Login)")]
    public GameObject loginPanel;
    public GameObject registerPanel;
    public GameObject accountPanel;

    public TMP_Text accountUsernameText;
    public TMP_Text accountPlayFabIdText;
    public Button signOutButton;

    private const string CUSTOMIZATION_KEY = "CustomizationV1";

    // Local session keys
    private const string PREF_CUSTOM_ID = "PF_CustomId";
    private const string PREF_USERNAME  = "PF_Username";

    public bool IsLoggedIn { get; private set; }
    public string PlayFabId { get; private set; }

    // ================= SINGLE SESSION (HARD) =================
    [Header("Single Session (HARD via Photon)")]
    [Tooltip("If enabled, Photon will reject duplicates when the same PlayFabId logs in on another device.")]
    [SerializeField] private bool enforceSingleSessionViaPhoton = true;

    [Tooltip("Connect to Photon immediately after PlayFab login (recommended).")]
    [SerializeField] private bool connectPhotonAfterLogin = true;

    private bool photonConnectInProgress;
    private bool duplicateBlocked;
    // ========================================================

    // ================= SESSION LOCK (SOFT, OPTIONAL) =================
    [Header("Session Lock (SOFT via PlayFab UserData)")]
    [Tooltip("Optional. Only used as a warning. Not 100% reliable alone.")]
    [SerializeField] private bool useSessionLock = false;

    [Tooltip("How long (seconds) before an 'active session' is considered expired (no heartbeat).")]
    [SerializeField] private int sessionTimeoutSeconds = 30;

    [Tooltip("How often (seconds) we update the heartbeat in PlayFab.")]
    [SerializeField] private int heartbeatIntervalSeconds = 10;

    private const string SESSION_LOCK_KEY = "ActiveSessionV1";

    [System.Serializable]
    private class SessionLockModel
    {
        public string sessionId;
        public string deviceId;
        public long lastSeenUnix;
    }

    private string mySessionId;
    private string myDeviceId;
    private Coroutine heartbeatRoutine;
    private bool lockOwned;
    // ===============================================================

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        myDeviceId = SystemInfo.deviceUniqueIdentifier;
        mySessionId = System.Guid.NewGuid().ToString("N");
    }

    void Start()
    {
        Debug.Log("PlayfabManager started");

        loginButton?.onClick.AddListener(LoginButton);
        registerButton?.onClick.AddListener(RegisterButton);
        saveCustomizationButton?.onClick.AddListener(SaveCustomizationToPlayFab);
        signOutButton?.onClick.AddListener(SignOut);

        SetLoggedInUI(false);
        TryAutoLogin();
    }

    // ================= AUTO LOGIN =================
    void TryAutoLogin()
    {
        string savedCustomId = PlayerPrefs.GetString(PREF_CUSTOM_ID, "");
        if (string.IsNullOrEmpty(savedCustomId)) return;

        if (messageText != null) messageText.text = "Auto logging in...";

        var request = new LoginWithCustomIDRequest
        {
            CustomId = savedCustomId,
            CreateAccount = false
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, error =>
        {
            Debug.LogWarning("Auto login failed: " + error.ErrorMessage);
            if (messageText != null) messageText.text = "Please log in.";

            PlayerPrefs.DeleteKey(PREF_CUSTOM_ID);
            PlayerPrefs.Save();

            SetLoggedInUI(false);
        });
    }

    // ================= REGISTER =================
    public void RegisterButton()
    {
        if (string.IsNullOrEmpty(registerUsernameInput.text) ||
            string.IsNullOrEmpty(registerEmailInput.text) ||
            string.IsNullOrEmpty(registerPasswordInput.text))
        {
            if (messageText != null) messageText.text = "Please fill in all fields";
            return;
        }

        if (registerPasswordInput.text.Length < 6)
        {
            if (messageText != null) messageText.text = "Password must be at least 6 characters";
            return;
        }

        if (messageText != null) messageText.text = "Registering...";

        var request = new RegisterPlayFabUserRequest
        {
            Username = registerUsernameInput.text,
            Email = registerEmailInput.text,
            Password = registerPasswordInput.text,
            RequireBothUsernameAndEmail = true
        };

        PlayFabClientAPI.RegisterPlayFabUser(request,
            result => { if (messageText != null) messageText.text = "Registered! Please log in."; },
            OnError);
    }

    // ================= LOGIN (Manual) =================
    public void LoginButton()
    {
        if (string.IsNullOrEmpty(loginUsernameInput.text) ||
            string.IsNullOrEmpty(loginPasswordInput.text))
        {
            if (messageText != null) messageText.text = "Enter username and password";
            return;
        }

        if (messageText != null) messageText.text = "Logging in...";

        PlayFabClientAPI.LoginWithPlayFab(
            new LoginWithPlayFabRequest
            {
                Username = loginUsernameInput.text,
                Password = loginPasswordInput.text
            },
            result =>
            {
                PlayerPrefs.SetString(PREF_USERNAME, loginUsernameInput.text);
                PlayerPrefs.Save();

                OnLoginSuccess(result);
                EnsureCustomIdLinked();
            },
            OnError
        );
    }

    // ================= LOGIN SUCCESS =================
    void OnLoginSuccess(LoginResult result)
    {
        IsLoggedIn = true;
        PlayFabId = result.PlayFabId;
        duplicateBlocked = false;

        // set Photon nickname
        string username = PlayerPrefs.GetString(PREF_USERNAME, "");
        if (string.IsNullOrEmpty(username) && loginUsernameInput != null)
            username = loginUsernameInput.text;
        PhotonNetwork.NickName = string.IsNullOrEmpty(username) ? "Player" : username;

        // HARD: enforce single session via Photon UserId = PlayFabId
        if (enforceSingleSessionViaPhoton)
        {
            EnsurePhotonUserIdIsPlayFabId();
        }

        // Optional SOFT lock (warning)
        if (useSessionLock)
        {
            if (messageText != null) messageText.text = "Checking session...";
            TryAcquireSessionLock(
                onSuccess: FinishLoginFlow,
                onBlocked: reason =>
                {
                    if (messageText != null) messageText.text = reason;
                    ForceLocalLogoutNoRelease();
                    SetLoggedInUI(false);
                }
            );
        }
        else
        {
            FinishLoginFlow();
        }
    }

    private void FinishLoginFlow()
    {
        if (messageText != null) messageText.text = "Logged in!";
        SetLoggedInUI(true);

        // Load customization (and push to Photon props after load)
        LoadCustomizationFromPlayFab();

        // Connect to Photon if desired (recommended, so duplicates are blocked immediately)
        if (enforceSingleSessionViaPhoton && connectPhotonAfterLogin)
        {
            ConnectPhotonIfNeeded();
        }
    }

    // ================= PHOTON SINGLE SESSION (HARD) =================

   private void EnsurePhotonUserIdIsPlayFabId()
    {
        if (string.IsNullOrEmpty(PlayFabId)) return;

        if (PhotonNetwork.AuthValues == null)
            PhotonNetwork.AuthValues = new AuthenticationValues();

        PhotonNetwork.AuthValues.UserId = PlayFabId;

        Debug.Log("✅ Set Photon UserId to PlayFabId (no disconnect). UserId=" + PlayFabId);
    }


    private void ConnectPhotonIfNeeded()
    {
        if (string.IsNullOrEmpty(PlayFabId)) return;
        if (duplicateBlocked) return;

        // Ensure AuthValues BEFORE any connect attempt
        EnsurePhotonUserIdIsPlayFabId();

        // Let PhotonBootstrap be the ONLY connector
        var pb = FindFirstObjectByType<PhotonBootstrap>();
        if (pb != null)
        {
            pb.SafeConnect();
            Debug.Log("✅ Requested PhotonBootstrap.SafeConnect()");
        }
        else
        {
            Debug.LogWarning("⚠️ PhotonBootstrap not found. Cannot connect.");
        }
    }


    public override void OnConnectedToMaster()
    {
        photonConnectInProgress = false;

        // If duplicates are disabled in Photon Dashboard, this connection means we’re the only active session.
        Debug.Log("✅ Photon connected. UserId=" + (PhotonNetwork.AuthValues?.UserId ?? "(null)"));

        // You can join lobby here if you want:
        // PhotonNetwork.JoinLobby();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        photonConnectInProgress = false;
        Debug.LogWarning("Photon disconnected: " + cause);
    }


    public override void OnCustomAuthenticationFailed(string debugMessage)
    {
        // Photon often sends duplicate-user errors here depending on setup
        Debug.LogWarning("Photon auth failed: " + debugMessage);

        if (!enforceSingleSessionViaPhoton) return;

        // Typical messages include "UserId already exists" / "already in use"
        string msgLower = (debugMessage ?? "").ToLowerInvariant();
        if (msgLower.Contains("userid") && (msgLower.Contains("already") || msgLower.Contains("exists") || msgLower.Contains("in use")))
        {
            duplicateBlocked = true;
            photonConnectInProgress = false;

            if (messageText != null)
                messageText.text = "This account is already active on another device.";

            // Don’t release lock if we didn’t own it. If you have soft lock enabled and we did own it,
            // releasing is okay, but not necessary.
            ForceLocalLogoutNoRelease();
            SetLoggedInUI(false);
        }
    }

    // ================= LINK CUSTOM ID =================
    void EnsureCustomIdLinked()
    {
        string customId = PlayerPrefs.GetString(PREF_CUSTOM_ID, "");
        if (string.IsNullOrEmpty(customId))
        {
            customId = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PREF_CUSTOM_ID, customId);
            PlayerPrefs.Save();
        }

        var link = new LinkCustomIDRequest
        {
            CustomId = customId,
            ForceLink = true
        };

        PlayFabClientAPI.LinkCustomID(link,
            _ => Debug.Log("✅ CustomID linked for auto-login."),
            err => Debug.LogWarning("CustomID link warning: " + err.ErrorMessage));
    }

    // ================= LOAD CUSTOMIZATION =================
    void LoadCustomizationFromPlayFab()
    {
        PlayFabClientAPI.GetUserData(new GetUserDataRequest(),
            r =>
            {
                if (r.Data != null && r.Data.TryGetValue(CUSTOMIZATION_KEY, out var record))
                    PlayerCustomizationData.LoadFromJson(record.Value);
                else
                    PlayerCustomizationData.ResetToDefault();

                // Push immediately after load (if Photon not connected yet, it will warn and skip)
                PushCustomizationToPhoton();

                RefreshPreviewIfPresent();
            },
            OnError
        );
    }

    // ================= SAVE CUSTOMIZATION =================
    public void SaveCustomizationToPlayFab()
    {
        if (!IsLoggedIn)
        {
            if (messageText != null) messageText.text = "Please log in first.";
            return;
        }

        PlayFabClientAPI.UpdateUserData(
            new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { CUSTOMIZATION_KEY, PlayerCustomizationData.ToJson() }
                }
            },
            _ =>
            {
                if (messageText != null) messageText.text = "Customization saved!";
                PushCustomizationToPhoton();
            },
            OnError
        );
    }

    // ================= PUSH TO PHOTON =================
    public void PushCustomizationToPhoton()
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.LocalPlayer == null)
        {
            Debug.LogWarning("PushCustomizationToPhoton skipped: Photon not connected yet.");
            return;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { "Username", PhotonNetwork.NickName },
            { "HeadI", PlayerCustomizationData.HeadColorIndex },
            { "BodyI", PlayerCustomizationData.BodyColorIndex },
            { "ArmsI", PlayerCustomizationData.ArmsColorIndex },
            { "LegsI", PlayerCustomizationData.LegsColorIndex },
            { "HatI",  PlayerCustomizationData.EquippedHatId }
        });
    }

    // ================= SIGN OUT =================
    public void SignOut()
    {
        Debug.Log("Signing out...");

        // Release soft lock if enabled
        ReleaseSessionLock();

        // Disconnect Photon as well
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();

        PlayFabClientAPI.ForgetAllCredentials();

        PlayerPrefs.DeleteKey(PREF_CUSTOM_ID);
        PlayerPrefs.DeleteKey(PREF_USERNAME);
        PlayerPrefs.Save();

        IsLoggedIn = false;
        PlayFabId = null;

        if (messageText != null) messageText.text = "Signed out.";
        SetLoggedInUI(false);
    }

    // Used when blocked by another device session (don’t release)
    private void ForceLocalLogoutNoRelease()
    {
        PlayFabClientAPI.ForgetAllCredentials();
        IsLoggedIn = false;
        PlayFabId = null;

        // Don’t touch PlayerPrefs here, user might want to try again
    }

    // ================= UI STATE =================
    void SetLoggedInUI(bool loggedIn)
    {
        if (loginPanel != null) loginPanel.SetActive(!loggedIn);
        if (registerPanel != null) registerPanel.SetActive(!loggedIn);
        if (accountPanel != null) accountPanel.SetActive(loggedIn);

        if (loggedIn)
        {
            string username = PlayerPrefs.GetString(PREF_USERNAME, PhotonNetwork.NickName);

            if (accountUsernameText != null) accountUsernameText.text = username;
            if (accountPlayFabIdText != null) accountPlayFabIdText.text = PlayFabId ?? "(none)";
        }
    }

    void RefreshPreviewIfPresent()
    {
        FindFirstObjectByType<CharacterColorCustomizer>()?.RefreshFromData();
        FindFirstObjectByType<CharacterHatCustomizer>()?.RefreshFromData();
    }

    void OnError(PlayFabError error)
    {
        if (messageText != null) messageText.text = error.ErrorMessage;
        Debug.LogError(error.GenerateErrorReport());
    }

    // ================= SOFT SESSION LOCK (PlayFab UserData) =================
    private void TryAcquireSessionLock(System.Action onSuccess, System.Action<string> onBlocked)
    {
        PlayFabClientAPI.GetUserData(
            new GetUserDataRequest { Keys = new List<string> { SESSION_LOCK_KEY } },
            r =>
            {
                SessionLockModel existing = null;

                if (r.Data != null && r.Data.TryGetValue(SESSION_LOCK_KEY, out var rec) && !string.IsNullOrEmpty(rec.Value))
                {
                    try { existing = JsonUtility.FromJson<SessionLockModel>(rec.Value); }
                    catch { existing = null; }
                }

                long now = GetUnixNow();

                if (existing != null &&
                    !string.IsNullOrEmpty(existing.sessionId) &&
                    (now - existing.lastSeenUnix) <= sessionTimeoutSeconds &&
                    existing.sessionId != mySessionId)
                {
                    onBlocked?.Invoke("Account is already active on another device.");
                    return;
                }

                var mine = new SessionLockModel
                {
                    sessionId = mySessionId,
                    deviceId = myDeviceId,
                    lastSeenUnix = now
                };

                PlayFabClientAPI.UpdateUserData(
                    new UpdateUserDataRequest
                    {
                        Data = new Dictionary<string, string>
                        {
                            { SESSION_LOCK_KEY, JsonUtility.ToJson(mine) }
                        }
                    },
                    _ =>
                    {
                        // verify
                        PlayFabClientAPI.GetUserData(
                            new GetUserDataRequest { Keys = new List<string> { SESSION_LOCK_KEY } },
                            verify =>
                            {
                                SessionLockModel after = null;

                                if (verify.Data != null && verify.Data.TryGetValue(SESSION_LOCK_KEY, out var rec2) && !string.IsNullOrEmpty(rec2.Value))
                                {
                                    try { after = JsonUtility.FromJson<SessionLockModel>(rec2.Value); }
                                    catch { after = null; }
                                }

                                if (after == null || after.sessionId != mySessionId)
                                {
                                    onBlocked?.Invoke("Account just became active on another device.");
                                    return;
                                }

                                lockOwned = true;

                                if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);
                                heartbeatRoutine = StartCoroutine(SessionHeartbeat());

                                onSuccess?.Invoke();
                            },
                            err2 =>
                            {
                                onBlocked?.Invoke("Could not verify session lock.");
                                Debug.LogWarning(err2.GenerateErrorReport());
                            }
                        );
                    },
                    err =>
                    {
                        onBlocked?.Invoke("Could not acquire session lock.");
                        Debug.LogWarning(err.GenerateErrorReport());
                    }
                );
            },
            err =>
            {
                onBlocked?.Invoke("Could not check session lock.");
                Debug.LogWarning(err.GenerateErrorReport());
            }
        );
    }

    private IEnumerator SessionHeartbeat()
    {
        while (IsLoggedIn && lockOwned)
        {
            yield return new WaitForSeconds(heartbeatIntervalSeconds);

            var mine = new SessionLockModel
            {
                sessionId = mySessionId,
                deviceId = myDeviceId,
                lastSeenUnix = GetUnixNow()
            };

            PlayFabClientAPI.UpdateUserData(
                new UpdateUserDataRequest
                {
                    Data = new Dictionary<string, string>
                    {
                        { SESSION_LOCK_KEY, JsonUtility.ToJson(mine) }
                    }
                },
                _ => { },
                err => Debug.LogWarning("Session heartbeat failed: " + err.ErrorMessage)
            );
        }
    }

    private void ReleaseSessionLock()
    {
        if (!useSessionLock) return;
        if (!lockOwned) return;

        lockOwned = false;

        if (heartbeatRoutine != null)
        {
            StopCoroutine(heartbeatRoutine);
            heartbeatRoutine = null;
        }

        var mine = new SessionLockModel
        {
            sessionId = mySessionId,
            deviceId = myDeviceId,
            lastSeenUnix = 0
        };

        PlayFabClientAPI.UpdateUserData(
            new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { SESSION_LOCK_KEY, JsonUtility.ToJson(mine) }
                }
            },
            _ => Debug.Log("✅ Session lock released."),
            err => Debug.LogWarning("Release lock failed: " + err.ErrorMessage)
        );
    }

    private long GetUnixNow()
    {
        return (long)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;
    }

    private void OnApplicationQuit()
    {
        ReleaseSessionLock();
    }

    private void OnDisable()
    {
        ReleaseSessionLock();
    }

    // ================= MENU UI BINDING =================
    public void BindMenuUI(
        TMP_Text messageText,
        TMP_InputField loginUsernameInput,
        TMP_InputField loginPasswordInput,
        Button loginButton,
        TMP_InputField registerUsernameInput,
        TMP_InputField registerEmailInput,
        TMP_InputField registerPasswordInput,
        Button registerButton,
        Button saveCustomizationButton,
        GameObject loginPanel,
        GameObject registerPanel,
        GameObject accountPanel,
        TMP_Text accountUsernameText,
        TMP_Text accountPlayFabIdText,
        Button signOutButton
    )
    {
        this.messageText = messageText;

        this.loginUsernameInput = loginUsernameInput;
        this.loginPasswordInput = loginPasswordInput;
        this.loginButton = loginButton;

        this.registerUsernameInput = registerUsernameInput;
        this.registerEmailInput = registerEmailInput;
        this.registerPasswordInput = registerPasswordInput;
        this.registerButton = registerButton;

        this.saveCustomizationButton = saveCustomizationButton;

        this.loginPanel = loginPanel;
        this.registerPanel = registerPanel;
        this.accountPanel = accountPanel;

        this.accountUsernameText = accountUsernameText;
        this.accountPlayFabIdText = accountPlayFabIdText;
        this.signOutButton = signOutButton;

        loginButton?.onClick.RemoveAllListeners();
        registerButton?.onClick.RemoveAllListeners();
        saveCustomizationButton?.onClick.RemoveAllListeners();
        signOutButton?.onClick.RemoveAllListeners();

        loginButton?.onClick.AddListener(LoginButton);
        registerButton?.onClick.AddListener(RegisterButton);
        saveCustomizationButton?.onClick.AddListener(SaveCustomizationToPlayFab);
        signOutButton?.onClick.AddListener(SignOut);

        SetLoggedInUI(IsLoggedIn);

        Debug.Log("✅ Playfab menu UI bound successfully.");
    }
}
