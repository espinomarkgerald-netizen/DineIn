using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable readiness presentation; state and callbacks belong to the session.</summary>
public sealed class MultiplayerReadyView : MonoBehaviour
{
    public RectTransform safeArea;
    public TMP_Text title, detail, roster, readyLabel;
    public Button readyButton, cancelButton, pauseButton;
}
