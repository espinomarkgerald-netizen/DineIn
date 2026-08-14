using UnityEngine;
using System;

public class AlienApprovalManager : MonoBehaviour
{
    public static AlienApprovalManager Instance { get; private set; }

    [Header("Starting Approval (0-100)")]
    [SerializeField] private int startingApproval = 45;

    [Header("Approval Deltas Per Group Result")]
    [SerializeField] private int happyDelta = 1;
    [SerializeField] private int neutralDelta = -1;
    [SerializeField] private int angryDelta = -8;
    [Tooltip("Positive approval earned from customer results is capped per day. Penalties are never capped.")]
    [SerializeField, Min(0)] private int maxPositiveGroupApprovalPerDay = 5;

    private int positiveGroupApprovalEarnedToday;

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
            CustomerGroup.FinalResult.Happy => happyDelta,
            CustomerGroup.FinalResult.Neutral => neutralDelta,
            CustomerGroup.FinalResult.Angry => angryDelta,
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

        Approval = Mathf.Clamp(Approval + delta, 0, 100);
        OnApprovalChanged?.Invoke(Approval);

        if (Approval <= 0)
            GameFlowManager.Instance?.TriggerGameOver(GameOverReason.ApprovalCollapsed);

        GameSaveManager.Instance?.RequestSave();
    }

    public void ApplyGradeBonus(int delta)
    {
        Approval = Mathf.Clamp(Approval + delta, 0, 100);
        OnApprovalChanged?.Invoke(Approval);

        if (Approval <= 0)
            GameFlowManager.Instance?.TriggerGameOver(GameOverReason.ApprovalCollapsed);

        GameSaveManager.Instance?.RequestSave();
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
        OnApprovalChanged?.Invoke(Approval);
        GameSaveManager.Instance?.RequestSave();
    }

    public void BeginNewDay()
    {
        positiveGroupApprovalEarnedToday = 0;
    }

    public bool TrySetApprovalDebug(int value)
    {
        if (value < 0 || value > 100)
            return false;

        Approval = value;
        OnApprovalChanged?.Invoke(Approval);

        if (Approval <= 0)
            GameFlowManager.Instance?.TriggerGameOver(GameOverReason.ApprovalCollapsed);

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

        if (Approval <= 0)
            GameFlowManager.Instance?.TriggerGameOver(GameOverReason.ApprovalCollapsed);
    }
}
