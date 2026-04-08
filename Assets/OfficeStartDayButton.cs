using UnityEngine;

public class OfficeStartDayButton : MonoBehaviour
{
    [SerializeField] private AlienDemandsPanel alienDemandsPanel;

    /// <summary>
    /// Called by LobbyButton.onClick. Objectives are rolled here so the panel
    /// can immediately display them. Expense deduction and bankruptcy evaluation
    /// happen at end of day inside GameFlowManager.EvaluateEndOfDay().
    /// </summary>
    public void OnClickStartDay()
    {
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;

        DailyObjectiveManager.Instance?.RollObjectivesForDay(day);

        if (alienDemandsPanel != null)
            alienDemandsPanel.ShowPanel();
        else
            Debug.LogWarning("[OfficeStartDayButton] AlienDemandsPanel not assigned.");
    }
}