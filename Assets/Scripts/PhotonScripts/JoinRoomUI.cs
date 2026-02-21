using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class JoinRoomUI : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "CoreGameplay";

    private void Awake()
    {
        if (joinButton) joinButton.onClick.AddListener(JoinByCode);
    }

    private void OnEnable()
    {
        SetStatus("Ready.");
    }

    private void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log("[JoinRoomUI] " + msg);
    }

    public void JoinByCode()
    {
        string code = codeInput ? codeInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("Enter code to join.");
            return;
        }

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("Not connected yet. Connecting...");
            var boot = FindFirstObjectByType<PhotonBootstrap>();
            if (boot) boot.SafeConnect();
            return;
        }

        SetStatus("Joining: " + code);
        PhotonNetwork.JoinRoom(code);
    }

    public override void OnJoinedRoom()
    {
        SetStatus("Joined room: " + PhotonNetwork.CurrentRoom.Name);

        // Master loads the scene, others auto follow because AutomaticallySyncScene = true
        if (PhotonNetwork.IsMasterClient)
        {
            SetStatus("Loading gameplay...");
            PhotonNetwork.LoadLevel(gameplaySceneName);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        SetStatus($"Join failed: {message} ({returnCode})");
    }
}
