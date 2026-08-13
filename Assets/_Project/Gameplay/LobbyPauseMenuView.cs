using UnityEngine;
using UnityEngine.UI;

/// <summary>Serialized references belonging to the editable pause prefab.</summary>
public sealed class LobbyPauseMenuView : MonoBehaviour
{
    [SerializeField] private Button pauseButton;
    [SerializeField] private GameObject overlay;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button gameMenuButton;

    public Button PauseButton => pauseButton;
    public GameObject Overlay => overlay;
    public Button ResumeButton => resumeButton;
    public Button GameMenuButton => gameMenuButton;

    public void Configure(Button pause, GameObject configuredOverlay, Button resume, Button gameMenu)
    {
        pauseButton = pause;
        overlay = configuredOverlay;
        resumeButton = resume;
        gameMenuButton = gameMenu;
    }
}
