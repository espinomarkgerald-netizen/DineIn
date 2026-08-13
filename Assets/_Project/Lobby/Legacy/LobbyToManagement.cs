using UnityEngine;
using UnityEngine.UI;

public class LobbyToManagement : MonoBehaviour
{
    [SerializeField] private Button returnButton;
    private bool listenerAdded;

    private void Start()
    {
        // GameDayManager owns the results action in the one-scene Lobby1 loop.
        // Registering this legacy callback as well would complete/reload several
        // days from a single click.
        if (GameDayManager.Instance != null || returnButton == null)
            return;

        returnButton.onClick.AddListener(OnReturnToManagement);
        listenerAdded = true;
    }

    private void OnDestroy()
    {
        if (listenerAdded && returnButton != null)
            returnButton.onClick.RemoveListener(OnReturnToManagement);
    }

    private void OnReturnToManagement()
    {
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.ReturnToManagementFromLobby();
        else
            Debug.LogError("GameFlowManager instance not found!");
    }
}
