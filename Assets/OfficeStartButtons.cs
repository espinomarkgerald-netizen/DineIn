using UnityEngine;

public class OfficeStartButtons : MonoBehaviour
{
    public void StartLobby()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }

        GameFlowManager.Instance.LoadLobbyScene();
    }

    public void StartKitchen()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }

        GameFlowManager.Instance.StartKitchenShift();
    }
}