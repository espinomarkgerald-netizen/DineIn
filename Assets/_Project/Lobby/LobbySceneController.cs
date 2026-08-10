using UnityEngine;

public class LobbySceneController : MonoBehaviour
{
    public void EndShift()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }

        GameFlowManager.Instance.ReturnToManagementFromLobby();
    }
}