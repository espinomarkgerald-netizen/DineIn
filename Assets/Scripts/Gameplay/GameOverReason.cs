/// <summary>
/// Identifies the reason the current run ended.
/// Passed to GameFlowManager.TriggerGameOver() and read by GameOverScreen
/// to display the correct narrative message.
/// </summary>
public enum GameOverReason
{
    /// <summary>MoneyManager.Money reached zero after end-of-day expense deduction.</summary>
    Bankruptcy,

    /// <summary>AlienApprovalManager.Approval reached zero — aliens lost all interest.</summary>
    ApprovalCollapsed,

    /// <summary>Player reached Day 30 with Approval >= 40 — Earth is saved.</summary>
    EarthSaved,

    /// <summary>Player reached Day 30 but Approval was below 40 — bittersweet loss.</summary>
    EarthConqueredDay30
}
