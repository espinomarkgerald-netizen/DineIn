using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Defines the type of metric an objective measures.
/// Each type maps to a specific data source evaluated at end of day.
/// </summary>
public enum ObjectiveType
{
    /// <summary>DailyFinanceBridge.EarnedToday must be >= target.</summary>
    MinRevenue,

    /// <summary>DailyRevenueTracker.OrdersFailed must be <= target.</summary>
    MaxFailedOrders,

    /// <summary>Angry departures counted this shift must be <= target.</summary>
    MaxAngryDepartures,

    /// <summary>DailyRevenueTracker.OrdersCompleted must be >= target.</summary>
    MinGroupsServed
}

/// <summary>
/// Represents a single objective the player must complete during a shift.
/// Configured directly in the DailyObjectiveManager Inspector — no ScriptableObject needed.
/// </summary>
[Serializable]
public class ObjectiveDefinition
{
    [Tooltip("What metric this objective measures.")]
    public ObjectiveType type;

    [Tooltip("Displayed in the pre-shift panel. Use {0} for the computed target value.\n" +
             "Example: 'Earn at least {0} today'")]
    public string descriptionTemplate;

    [Tooltip("The base target value on Day 1.")]
    public int baseTargetValue;

    [Tooltip("Fractional increase per day. 0.05 = 5% harder each day.\n" +
             "For MaxAngryDepartures / MaxFailedOrders, keep this at 0 or negative.")]
    public float scalingPerDay;

    /// <summary>Computes the target value for the given day number.</summary>
    public int GetTargetForDay(int day)
        => Mathf.RoundToInt(baseTargetValue * (1f + scalingPerDay * (day - 1)));

    /// <summary>Returns the display string with the target value substituted.</summary>
    public string GetDescription(int day)
        => string.Format(descriptionTemplate, GetTargetForDay(day));
}

/// <summary>
/// The letter grade awarded at end of day based on how many objectives were cleared.
/// </summary>
public enum ObjectiveGrade { S, A, B, C, F }

/// <summary>
/// Persistent singleton that manages the three daily objectives (Alien Demands).
/// Rolls objectives at the start of each day, counts angry departures during the shift,
/// evaluates results at day end, and applies the grade bonus/penalty to AlienApprovalManager.
///
/// Setup: Add this component to the same persistent manager GameObject as
/// AlienApprovalManager, MoneyManager, and DailyFinanceBridge.
/// Populate the three objective pools in the Inspector.
/// </summary>
public class DailyObjectiveManager : MonoBehaviour
{
    public static DailyObjectiveManager Instance { get; private set; }

    [Header("Objective Pools — at least one entry required in each")]
    [SerializeField] private List<ObjectiveDefinition> mandatoryPool  = new();
    [SerializeField] private List<ObjectiveDefinition> secondaryPool  = new();
    [SerializeField] private List<ObjectiveDefinition> bonusPool      = new();

    [Header("Grade → Approval Delta")]
    [SerializeField] private int gradeS_Bonus   =  5;
    [SerializeField] private int gradeA_Bonus   =  3;
    [SerializeField] private int gradeB_Bonus   =  1;
    [SerializeField] private int gradeC_Bonus   =  0;
    [SerializeField] private int gradeF_Penalty = -8;

    /// <summary>The three active objectives for the current day. Set by RollObjectivesForDay().</summary>
    public ObjectiveDefinition ActiveMandatory  { get; private set; }
    public ObjectiveDefinition ActiveSecondary  { get; private set; }
    public ObjectiveDefinition ActiveBonus      { get; private set; }

    /// <summary>The grade computed at end of day. Available after EvaluateAndApply() is called.</summary>
    public ObjectiveGrade LastGrade { get; private set; } = ObjectiveGrade.C;

    /// <summary>Fires after EvaluateAndApply() completes. Passes the grade and each pass/fail result.</summary>
    public event Action<ObjectiveGrade, bool, bool, bool> OnObjectivesEvaluated;

    private int angryDeparturesToday;
    private int currentDay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Picks one objective from each pool at random for the given day.
    /// Call this at the start of each day, before showing the pre-shift panel.
    /// </summary>
    public void RollObjectivesForDay(int day)
    {
        currentDay          = day;
        angryDeparturesToday = 0;

        ActiveMandatory = PickRandom(mandatoryPool);
        ActiveSecondary = PickRandom(secondaryPool);
        ActiveBonus     = PickRandom(bonusPool);
    }

    /// <summary>
    /// Increments the angry departure counter for the current day.
    /// Called by AlienApprovalManager.RegisterGroupResult() on Angry or Unhappy results.
    /// </summary>
    public void RegisterAngryDeparture() => angryDeparturesToday++;

    /// <summary>
    /// Evaluates all three objectives against the day's tracked data,
    /// computes a grade, applies the approval bonus/penalty, and fires OnObjectivesEvaluated.
    /// Call this from GameFlowManager.EvaluateEndOfDay(), before StartNewDay().
    /// </summary>
    /// <returns>The ObjectiveGrade awarded for this day.</returns>
    public ObjectiveGrade EvaluateAndApply()
    {
        bool mandatoryPassed = Evaluate(ActiveMandatory);
        bool secondaryPassed = Evaluate(ActiveSecondary);
        bool bonusPassed     = Evaluate(ActiveBonus);

        ObjectiveGrade grade;

        if (!mandatoryPassed)
            grade = ObjectiveGrade.F;
        else if (bonusPassed && secondaryPassed)
            grade = ObjectiveGrade.S;
        else if (secondaryPassed)
            grade = ObjectiveGrade.A;
        else
            grade = ObjectiveGrade.B;

        // C is only reached if mandatory passed but nothing else —
        // the branch above covers B for that case, so C is not directly reachable
        // unless we add a fourth objective. Keep the mapping clean.
        // F is explicitly set when mandatory fails.

        LastGrade = grade;

        int approvalDelta = grade switch
        {
            ObjectiveGrade.S => gradeS_Bonus,
            ObjectiveGrade.A => gradeA_Bonus,
            ObjectiveGrade.B => gradeB_Bonus,
            ObjectiveGrade.C => gradeC_Bonus,
            ObjectiveGrade.F => gradeF_Penalty,
            _                => 0
        };

        AlienApprovalManager.Instance?.ApplyGradeBonus(approvalDelta);

        OnObjectivesEvaluated?.Invoke(grade, mandatoryPassed, secondaryPassed, bonusPassed);

        return grade;
    }

    /// <summary>Resets the angry departure counter for a new day. Called by RollObjectivesForDay.</summary>
    public void ResetForNewDay() => angryDeparturesToday = 0;

    private bool Evaluate(ObjectiveDefinition obj)
    {
        if (obj == null) return false;

        int target = obj.GetTargetForDay(currentDay);

        return obj.type switch
        {
            ObjectiveType.MinRevenue =>
                DailyFinanceBridge.Instance != null &&
                DailyFinanceBridge.Instance.EarnedToday >= target,

            ObjectiveType.MaxFailedOrders =>
                DailyRevenueTracker.Instance != null &&
                DailyRevenueTracker.Instance.OrdersFailed <= target,

            ObjectiveType.MaxAngryDepartures =>
                angryDeparturesToday <= target,

            ObjectiveType.MinGroupsServed =>
                DailyRevenueTracker.Instance != null &&
                DailyRevenueTracker.Instance.OrdersCompleted >= target,

            _ => false
        };
    }

    private ObjectiveDefinition PickRandom(List<ObjectiveDefinition> pool)
    {
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[DailyObjectiveManager] An objective pool is empty. " +
                             "Add at least one entry to each pool in the Inspector.");
            return null;
        }

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }
}
