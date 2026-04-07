using UnityEngine;
using System;

/// <summary>
/// Persistent singleton that tracks the Alien Approval Rating (0-100).
/// Approval rises when customer groups leave happy and falls when they leave angry.
/// It drives the customer spawn modifier and triggers the Earth Conquered game over
/// if it reaches zero.
/// </summary>
public class AlienApprovalManager : MonoBehaviour
{
    public static AlienApprovalManager Instance { get; private set; }

    [Header("Starting Approval (0-100)")]
    [SerializeField] private int startingApproval = 50;

    [Header("Approval Deltas Per Group Result")]
    [SerializeField] private int happyDelta   =  3;
    [SerializeField] private int neutralDelta =  0;
    [SerializeField] private int angryDelta   = -6;

    public int Approval { get; private set; }

    /// <summary>Fires whenever the Approval value changes. Passes the new value.</summary>
    public event Action<int> OnApprovalChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Approval = Mathf.Clamp(startingApproval, 0, 100);
    }

    /// <summary>
    /// Call this from CustomerGroup.ReportFinalResult() when a group finishes their visit.
    /// Uses CustomerGroup.FinalResult enum values.
    /// </summary>
    public void RegisterGroupResult(CustomerGroup.FinalResult result)
    {
        int delta = result switch
        {
            CustomerGroup.FinalResult.Happy   => happyDelta,
            CustomerGroup.FinalResult.Neutral => neutralDelta,
            CustomerGroup.FinalResult.Angry   => angryDelta,
            _                                 => 0
        };

        // Track angry departures for the daily objective counter
        if (result == CustomerGroup.FinalResult.Angry)
            DailyObjectiveManager.Instance?.RegisterAngryDeparture();

        Approval = Mathf.Clamp(Approval + delta, 0, 100);
        OnApprovalChanged?.Invoke(Approval);

        if (Approval <= 0)
            GameFlowManager.Instance?.TriggerGameOver(GameOverReason.ApprovalCollapsed);
    }

    /// <summary>
    /// Applies a direct approval delta from the daily grade bonus/penalty.
    /// Positive values increase approval; negative values decrease it.
    /// </summary>
    public void ApplyGradeBonus(int delta)
    {
        Approval = Mathf.Clamp(Approval + delta, 0, 100);
        OnApprovalChanged?.Invoke(Approval);

        if (Approval <= 0)
            GameFlowManager.Instance?.TriggerGameOver(GameOverReason.ApprovalCollapsed);
    }

    /// <summary>
    /// Returns how many extra (or fewer) customer groups should spawn this shift
    /// based on the current approval level.
    /// </summary>
    public int GetSpawnModifier()
    {
        if (Approval >= 80) return  2;
        if (Approval >= 60) return  0;
        if (Approval >= 40) return -1;
        if (Approval >= 20) return -2;
        return -99;
    }

    /// <summary>
    /// Resets approval to the starting value. Call only on a full game restart,
    /// not between days.
    /// </summary>
    public void ResetApproval()
    {
        Approval = Mathf.Clamp(startingApproval, 0, 100);
        OnApprovalChanged?.Invoke(Approval);
    }
}
