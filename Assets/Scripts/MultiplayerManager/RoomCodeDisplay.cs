using Photon.Pun;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays the current Photon room name as a formatted code (e.g. "DINE-8890")
/// in a TextMeshPro label. Attach this to the Room Code Text GameObject in the
/// Multiplayer scene HUD canvas.
///
/// The room name already stored in PhotonNetwork.CurrentRoom.Name is used directly.
/// If the name already contains the "DINE-" prefix it is shown as-is; otherwise
/// the prefix is prepended automatically.
/// </summary>
public class RoomCodeDisplay : MonoBehaviourPunCallbacks
{
    [Tooltip("TextMeshPro label that will show the room code. Auto-resolved from this GameObject if left empty.")]
    [SerializeField] private TMP_Text codeLabel;

    [Tooltip("Prefix shown before the room code number, e.g. 'DINE-'.")]
    [SerializeField] private string codePrefix = "DINE-";

    [Tooltip("Text shown while not yet connected to a room.")]
    [SerializeField] private string placeholderText = "Connecting...";

    private const string LogTag = "[RoomCodeDisplay]";

    private void Awake()
    {
        if (codeLabel == null)
            codeLabel = GetComponent<TMP_Text>();

        if (codeLabel == null)
            Debug.LogError($"{LogTag} No TMP_Text found. Assign it in the Inspector.");
    }

    private void Start()
    {
        RefreshRoomCode();
    }

    // -------------------------------------------------------------------------
    // Photon callbacks
    // -------------------------------------------------------------------------

    /// <summary>Called when this client successfully joins or creates a room.</summary>
    public override void OnJoinedRoom()
    {
        RefreshRoomCode();
    }

    /// <summary>Called when this client leaves a room.</summary>
    public override void OnLeftRoom()
    {
        SetLabel(placeholderText);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Reads the current room name from Photon and updates the label.</summary>
    public void RefreshRoomCode()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            SetLabel(placeholderText);
            return;
        }

        string roomName = PhotonNetwork.CurrentRoom.Name;

        // Prepend the prefix only if it is not already there.
        string display = roomName.StartsWith(codePrefix, System.StringComparison.OrdinalIgnoreCase)
            ? roomName.ToUpperInvariant()
            : codePrefix + roomName.ToUpperInvariant();

        SetLabel(display);
        Debug.Log($"{LogTag} Showing room code: {display}");
    }

    private void SetLabel(string text)
    {
        if (codeLabel != null)
            codeLabel.text = text;
    }
}
