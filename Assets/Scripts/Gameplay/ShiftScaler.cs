using UnityEngine;

/// <summary>
/// Persistent singleton that scales shift difficulty based on the current day
/// and the Alien Approval rating.
///
/// Patience decreases linearly from basePatienceSeconds on Day 1 to
/// minPatienceSeconds at maxScalingDay and beyond.
///
/// Group count starts at baseGroupCount on Day 1 and grows by groupCountPerDay
/// each day, capped at maxGroupCount, then adjusted by AlienApprovalManager.GetSpawnModifier().
///
/// Setup:
///   - Add to the same persistent manager GameObject as GameFlowManager.
///   - Call ApplyScaling(day) from GameFlowManager.StartLobbyShift().
///   - GroupSpawner.SetPatienceSeconds() is applied to each group at spawn time
///     via the spawner reading ShiftScaler.CurrentPatienceSeconds.
/// </summary>
public class ShiftScaler : MonoBehaviour
{
    public static ShiftScaler Instance { get; private set; }

    [Header("Patience Curve")]
    [SerializeField] private float basePatienceSeconds = 60f;
    [SerializeField] private float minPatienceSeconds  = 25f;
    [SerializeField] private int   maxScalingDay       = 20;
    public float MinPatienceSeconds => minPatienceSeconds;
    public float BasePatienceSeconds => basePatienceSeconds;

    [Header("Group Count Curve")]
    [SerializeField] private int baseGroupCount   = 8;
    [SerializeField] private int groupCountPerDay = 1;
    [SerializeField] private int maxGroupCount    = 20;

    /// <summary>Patience in seconds that GroupSpawner should apply to each spawned group this shift.</summary>
    public float CurrentPatienceSeconds { get; private set; }

    /// <summary>Number of groups to spawn this shift after approval modifier is applied.</summary>
    public int CurrentGroupCount { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Set safe defaults so values are valid before the first ApplyScaling call
        CurrentPatienceSeconds = basePatienceSeconds;
        CurrentGroupCount      = baseGroupCount;
    }

    /// <summary>
    /// Computes and caches scaled patience and group count for the given day,
    /// then pushes the group count to GroupSpawner.
    /// Call this from GameFlowManager.StartLobbyShift().
    /// </summary>
    public void ApplyScaling(int day)
    {
        float t = Mathf.Clamp01((float)(day - 1) / Mathf.Max(1, maxScalingDay - 1));
        CurrentPatienceSeconds = Mathf.Lerp(basePatienceSeconds, minPatienceSeconds, t);

        int rawGroupCount = baseGroupCount + groupCountPerDay * (day - 1);
        int capped        = Mathf.Clamp(rawGroupCount, 1, maxGroupCount);
        int modifier      = AlienApprovalManager.Instance != null
            ? AlienApprovalManager.Instance.GetSpawnModifier()
            : 0;

        // Hard clamp at 1 — never schedule zero or negative groups
        CurrentGroupCount = Mathf.Max(1, capped + modifier);

        GroupSpawner spawner = FindFirstObjectByType<GroupSpawner>();
        if (spawner != null)
            spawner.SetGroupsForShift(CurrentGroupCount);
        else
            Debug.LogWarning("[ShiftScaler] No GroupSpawner found in the scene. " +
                             "ApplyScaling() must be called after the lobby scene loads.");

        Debug.Log($"[ShiftScaler] Day {day} — Patience: {CurrentPatienceSeconds:F1}s  " +
                  $"Groups: {CurrentGroupCount} (raw {capped} + modifier {modifier})");
    }
}
