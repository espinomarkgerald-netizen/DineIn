using UnityEngine;
using UnityEngine.UI;

public class LobbyToManagement : MonoBehaviour
{
    [SerializeField] private Button returnButton;

    private void Awake()
    {
        if (returnButton != null)
            returnButton.onClick.AddListener(OnReturnToManagement);
    }

    private void OnReturnToManagement()
    {
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.ReturnToManagementFromLobby();
        else
            Debug.LogError("GameFlowManager instance not found!");
    }
}
