using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonBootstrap : MonoBehaviourPunCallbacks
{
    public static PhotonBootstrap Instance { get; private set; }
    public static event Action<string> ConnectionFailed;

    [Header("Settings")]
    [SerializeField] private bool autoConnectOnStart = true;
    [SerializeField] private bool autoJoinLobby = true;
    [Tooltip("Enable only after configuring PlayFab custom authentication in both dashboards. Disabled preserves the existing Photon connection path.")]
    [SerializeField] private bool usePlayFabCustomAuthentication = false;

    public bool AccountConnectionPending { get; private set; }
    public string LastConnectionError { get; private set; }
    private string authenticatingAccount;
    private int attempt;
    private float connectionDeadline;
    private bool reconnectAfterDisconnect;
    private static string ConnectionVersion => Application.version + "." + MultiplayerSessionManager.Protocol;
    private CustomAuthenticationType ExpectedAuth => usePlayFabCustomAuthentication
        ? CustomAuthenticationType.Custom : CustomAuthenticationType.None;

    public static bool ConnectionMatchesAccount(string accountId)
    {
        var expected = Instance != null ? Instance.ExpectedAuth : CustomAuthenticationType.None;
        // AuthValues can be changed by login code before a reconnect. Check the
        // actual connected player as well, so another account cannot reuse it.
        return PhotonNetwork.IsConnected && !string.IsNullOrEmpty(accountId)
            && PhotonNetwork.LocalPlayer?.UserId == accountId
            && PhotonNetwork.AuthValues?.UserId == accountId
            && PhotonNetwork.AuthValues.AuthType == expected
            && PhotonNetwork.GameVersion == ConnectionVersion;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    private void Start()
    {
        if (autoConnectOnStart && PlayFabAuthManager.Instance?.IsLoggedIn == true) SafeConnect();
    }

    private void Update()
    {
        if (!AccountConnectionPending || Time.realtimeSinceStartup < connectionDeadline) return;
        FailConnection("Photon connection timed out. Please retry.");
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
    }

    public void SafeConnect()
    {
        var account = PlayFabAuthManager.Instance;
        if (account == null || !account.IsLoggedIn || string.IsNullOrEmpty(account.PlayFabId))
        { FailConnection("Sign in before connecting to multiplayer."); return; }
        if (AccountConnectionPending && authenticatingAccount == account.PlayFabId) return;
        LastConnectionError = null;
        if (ConnectionMatchesAccount(account.PlayFabId)) return;

        authenticatingAccount = account.PlayFabId;
        AccountConnectionPending = true;
        connectionDeadline = Time.realtimeSinceStartup + 20f;
        int request = ++attempt;
        if (PhotonNetwork.IsConnected || PhotonNetwork.NetworkClientState == ClientState.Disconnecting)
        {
            reconnectAfterDisconnect = true;
            PhotonNetwork.Disconnect();
            return;
        }
        reconnectAfterDisconnect = false;
        string requestedAccount = authenticatingAccount;
        if (!usePlayFabCustomAuthentication)
        {
            // Preserve the established connection path. Signing into PlayFab and
            // uploading results do not require enabling Photon custom auth.
            Connect(requestedAccount, request, new AuthenticationValues(requestedAccount));
            return;
        }
        var settings = PhotonNetwork.PhotonServerSettings;
        if (settings == null) { FailConnection("Photon connection settings are missing."); return; }
        PlayFab.PlayFabClientAPI.GetPhotonAuthenticationToken(new PlayFab.ClientModels.GetPhotonAuthenticationTokenRequest
        {
            PhotonApplicationId = settings.AppSettings.AppIdRealtime
        }, result =>
        {
            if (!IsCurrentAttempt(request, requestedAccount)) return;
            if (string.IsNullOrEmpty(result.PhotonCustomAuthenticationToken))
            { FailConnection("PlayFab returned no Photon authentication token."); return; }
            var authentication = new AuthenticationValues(requestedAccount) { AuthType = CustomAuthenticationType.Custom };
            authentication.AddAuthParameter("username", requestedAccount);
            authentication.AddAuthParameter("token", result.PhotonCustomAuthenticationToken);
            Connect(requestedAccount, request, authentication);
        }, error =>
        {
            if (IsCurrentAttempt(request, requestedAccount))
                FailConnection("PlayFab–Photon authentication failed (" + error.Error + "). Check the custom-auth integration settings.");
        });
    }

    private bool IsCurrentAttempt(int request, string accountId) => this != null && request == attempt
        && AccountConnectionPending && PlayFabAuthManager.Instance?.IsLoggedIn == true
        && PlayFabAuthManager.Instance.PlayFabId == accountId;

    private void Connect(string accountId, int request, AuthenticationValues authentication)
    {
        if (!IsCurrentAttempt(request, accountId)) return;
        var settings = PhotonNetwork.PhotonServerSettings;
        if (settings == null) { FailConnection("Photon connection settings are missing."); return; }
        PhotonNetwork.AuthValues = authentication;
        // ConnectUsingSettings assigns GameVersion from AppSettings. Set the
        // version on a runtime copy rather than having that call overwrite it.
        var connection = settings.AppSettings.CopyTo(new AppSettings());
        connection.AppVersion = ConnectionVersion;
        if (!PhotonNetwork.ConnectUsingSettings(connection))
            FailConnection("Photon could not start connecting. Please retry.");
    }

    private void FailConnection(string message)
    {
        attempt++; // Ignore late token responses from failed or abandoned attempts.
        AccountConnectionPending = false;
        reconnectAfterDisconnect = false;
        LastConnectionError = message;
        Debug.LogWarning("[PhotonBootstrap] " + message);
        ConnectionFailed?.Invoke(message);
    }

    public override void OnConnectedToMaster()
    {
        AccountConnectionPending = false;
        reconnectAfterDisconnect = false;
        LastConnectionError = null;
        if (autoJoinLobby && !PhotonNetwork.InRoom && !PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby(TypedLobby.Default);
    }

    public override void OnCustomAuthenticationFailed(string debugMessage)
    {
        FailConnection("Photon rejected authentication. Check the configured authentication mode and account session.");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        bool retry = reconnectAfterDisconnect && cause == DisconnectCause.DisconnectByClientLogic;
        AccountConnectionPending = false;
        reconnectAfterDisconnect = false;
        attempt++;
        if (retry) { SafeConnect(); return; }
        if (cause != DisconnectCause.DisconnectByClientLogic && string.IsNullOrEmpty(LastConnectionError))
            FailConnection("Photon disconnected (" + cause + "). Please retry.");
        // Live-run reconnection is owned by MultiplayerSessionManager.
    }

    public void LeaveRoomAndReturnToLobby()
    {
        if (!PhotonNetwork.IsConnectedAndReady) return;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        else if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby(TypedLobby.Default);
    }

    public override void OnLeftRoom()
    {
        if (autoJoinLobby && PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby(TypedLobby.Default);
    }

    private void OnDestroy()
    {
        attempt++;
        if (Instance == this) Instance = null;
    }
}
