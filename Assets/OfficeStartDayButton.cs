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

        // Make sure ShiftScaler has applied scaling first
        int maxGroupsThisShift = ShiftScaler.Instance != null
            ? ShiftScaler.Instance.CurrentGroupCount
            : 5; // fallback if ShiftScaler is not initialized yet

        DailyObjectiveManager.Instance?.RollObjectivesForDay(day, maxGroupsThisShift);

        if (alienDemandsPanel != null)
            alienDemandsPanel.ShowPanel();
        else
            Debug.LogWarning("[OfficeStartDayButton] AlienDemandsPanel not assigned.");
    }
}