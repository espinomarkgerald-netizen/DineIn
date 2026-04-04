using UnityEngine;

public class KitchenSceneController : MonoBehaviour
{
    /// <summary>
    /// Called by the End Shift button in the Kitchen scene.
    /// Shows the daily financial report; scene transition happens when the player confirms.
    /// </summary>
    public void EndShift()
    {
        if (DailyReportUI.Instance != null)
        {
            DailyReportUI.Instance.Show();
            return;
        }

        // Fallback if DailyReportUI is not in the scene
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }

        GameFlowManager.Instance.ReturnToManagementFromKitchen();
    }
}
