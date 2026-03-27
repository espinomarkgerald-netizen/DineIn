using UnityEngine;

public class KitchenSceneController : MonoBehaviour
{
    public void EndShift()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }

        GameFlowManager.Instance.ReturnToManagementFromKitchen();
    }
}