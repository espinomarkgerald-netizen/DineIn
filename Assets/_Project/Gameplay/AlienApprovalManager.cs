using UnityEngine;
using System;

public class AlienApprovalManager : MonoBehaviour
{
    public static AlienApprovalManager Instance { get; private set; }

    [Header("Starting Approval (0-100)")]
    [SerializeField] private int startingApproval = 30;

    [Header("Approval Rewards")]
    [Tooltip("Approval gained when a customer group leaves happy.")]
    [SerializeField] private int happyDelta = 1;
    [Tooltip("Positive approval earned from customer results is capped per day.")]
    [SerializeField, Min(0)] private int maxPositiveGroupApprovalPerDay = 5;

    [Header("Approval Penalties")]
    [Tooltip("Approval lost when one customer group leaves angry. Neutral and happy groups never reduce approval.")]
    [SerializeField, Min(0)] private int angryCustomerPenalty = 4;
    [Tooltip("One additional end-of-shift penalty when the restaurant earns exactly one star.")]
    [SerializeField, Min(0)] private int oneStarShiftPenalty = 3;
    [Tooltip("Maximum approval that angry customers and a one-star result can remove in one day. All allowed losses share this cap.")]
    [SerializeField, Min(0)] private int maxNegativeGroupApprovalPerDay = 15;

    private int positiveGroupApprovalEarnedToday;
    private int approvalLostToday;
    private int oneStarPenaltyAppliedDay = -1;

    public int Approval { get; private set; }

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

    public void RegisterGroupResult(CustomerGroup.FinalResult result)
    {
        int delta = result switch
        {
            CustomerGroup.FinalResult.Happy => Mathf.Max(0, happyDelta),
            CustomerGroup.FinalResult.Neutral => 0,
            CustomerGroup.FinalResult.Angry => GetCappedLoss(angryCustomerPenalty),
            _ => 0
        };

        if (delta > 0)
        {
            int remainingGain = Mathf.Max(0,
                maxPositiveGroupApprovalPerDay - positiveGroupApprovalEarnedToday);
            delta = Mathf.Min(delta, remainingGain);
            positiveGroupApprovalEarnedToday += delta;
        }
        if (result == CustomerGroup.FinalResult.Angry)
            DailyObjectiveManager.Instance?.RegisterAngryDeparture();

        ApplyApprovalDelta(delta);
    }

    /// <summary>
    /// Applies the only end-of-shift approval penalty. The day token prevents
    /// the results panel from charging the same one-star shift more than once.
    /// </summary>
    public void RegisterDailyStarRating(int earnedStars, int day)
    {
        if (earnedStars != 1)
            return;

        day = Mathf.Max(1, day);
        if (oneStarPenaltyAppliedDay == day)
            return;

        oneStarPenaltyAppliedDay = day;
        ApplyApprovalDelta(GetCappedLoss(oneStarShiftPenalty));
    }

    public void ApplyGradeBonus(int delta)
    {
        // Daily objectives can reward strong management, but missing one does
        // not reduce approval. Losses are reserved for angry customers and a
        // one-star shift result so the player always understands the cause.
        ApplyApprovalDelta(Mathf.Max(0, delta));
    }

    public int GetSpawnModifier()
    {
        if (Approval >= 80) return 2;
        if (Approval >= 60) return 0;
        if (Approval >= 40) return -1;
        return -2;
    }

    public void ResetApproval()
    {
        Approval = Mathf.Clamp(startingApproval, 0, 100);
        positiveGroupApprovalEarnedToday = 0;
        approvalLostToday = 0;
        oneStarPenaltyAppliedDay = -1;
        OnApprovalChanged?.Invoke(Approval);
        GameSaveManager.Instance?.RequestSave();
    }

    public void BeginNewDay()
    {
        positiveGroupApprovalEarnedToday = 0;
        approvalLostToday = 0;
        oneStarPenaltyAppliedDay = -1;
    }

    public void RestoreApprovalForContinue(int approval)
    {
        Approval = Mathf.Clamp(approval, 1, 100);
        positiveGroupApprovalEarnedToday = 0;
        approvalLostToday = 0;
        oneStarPenaltyAppliedDay = -1;
        OnApprovalChanged?.Invoke(Approval);
        GameSaveManager.Instance?.RequestSave();
    }

    public bool TrySetApprovalDebug(int value)
    {
        if (value < 0 || value > 100)
            return false;

        Approval = value;
        OnApprovalChanged?.Invoke(Approval);

        TriggerImmediateGameOverForLegacyFlow();

        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.approval = Approval;
    }

    public void ApplySaveData(GameSaveData data)
    {
        if (data == null)
            return;

        Approval = Mathf.Clamp(data.approval, 0, 100);
        OnApprovalChanged?.Invoke(Approval);

        TriggerImmediateGameOverForLegacyFlow();
    }

    private void TriggerImmediateGameOverForLegacyFlow()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (Approval <= 0 && flow != null && !flow.UsesSingleRestaurantFlow)
            flow.TriggerGameOver(GameOverReason.ApprovalCollapsed);
    }

    private int GetCappedLoss(int requestedPenalty)
    {
        int remainingLoss = Mathf.Max(0,
            maxNegativeGroupApprovalPerDay - approvalLostToday);
        int appliedLoss = Mathf.Min(Mathf.Max(0, requestedPenalty), remainingLoss);
        approvalLostToday += appliedLoss;
        return -appliedLoss;
    }

    private void ApplyApprovalDelta(int delta)
    {
        if (delta == 0)
            return;

        Approval = Mathf.Clamp(Approval + delta, 0, 100);
        OnApprovalChanged?.Invoke(Approval);

        TriggerImmediateGameOverForLegacyFlow();

        GameSaveManager.Instance?.RequestSave();
    }

    private void OnValidate()
    {
        startingApproval = Mathf.Clamp(startingApproval, 0, 100);
        happyDelta = Mathf.Max(0, happyDelta);
        maxPositiveGroupApprovalPerDay = Mathf.Max(0, maxPositiveGroupApprovalPerDay);
        angryCustomerPenalty = Mathf.Max(0, angryCustomerPenalty);
        oneStarShiftPenalty = Mathf.Max(0, oneStarShiftPenalty);
        maxNegativeGroupApprovalPerDay = Mathf.Max(0, maxNegativeGroupApprovalPerDay);
    }
}
