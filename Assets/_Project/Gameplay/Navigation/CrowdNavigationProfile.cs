using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shared, editable crowd-navigation tuning for customers and autonomous staff.
/// The runtime agents read this asset without replacing their authored movement speed.
/// </summary>
[CreateAssetMenu(
    fileName = "CrowdNavigationProfile",
    menuName = "Dine In/Navigation/Crowd Navigation Profile")]
public class CrowdNavigationProfile : ScriptableObject
{
    [Header("Local Avoidance")]
    [SerializeField] private ObstacleAvoidanceType obstacleAvoidance =
        ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    [SerializeField, Min(0.1f)] private float customerRadius = 0.42f;
    [SerializeField, Min(0.1f)] private float staffRadius = 0.48f;
    [SerializeField, Min(0f)] private float minimumStoppingDistance = 0.15f;
    [SerializeField, Min(0.1f)] private float minimumAcceleration = 12f;
    [SerializeField, Min(1f)] private float minimumAngularSpeed = 360f;

    [Header("Priority Lanes (lower number moves first)")]
    [SerializeField, Range(0, 99)] private int staffPriorityMin = 20;
    [SerializeField, Range(0, 99)] private int staffPriorityMax = 44;
    [SerializeField, Range(0, 99)] private int customerPriorityMin = 55;
    [SerializeField, Range(0, 99)] private int customerPriorityMax = 89;

    [Header("Stall Recovery")]
    [SerializeField, Min(0.1f)] private float stalledSpeed = 0.08f;
    [SerializeField, Min(0.25f)] private float stalledSecondsBeforeRepath = 1.15f;
    [SerializeField, Min(0.1f)] private float repathCooldown = 0.55f;
    [SerializeField, Range(1, 30)] private int temporaryYieldPriority = 12;
    [SerializeField, Min(0.1f)] private float temporaryYieldSeconds = 0.7f;

    [Header("Physical Separation")]
    [Tooltip("Lets NavMesh avoidance handle characters instead of their colliders physically shoving one another.")]
    [SerializeField] private bool ignoreCharacterToCharacterCollisions = true;

    public ObstacleAvoidanceType ObstacleAvoidance => obstacleAvoidance;
    public float CustomerRadius => Mathf.Max(0.1f, customerRadius);
    public float StaffRadius => Mathf.Max(0.1f, staffRadius);
    public float MinimumStoppingDistance => Mathf.Max(0f, minimumStoppingDistance);
    public float MinimumAcceleration => Mathf.Max(0.1f, minimumAcceleration);
    public float MinimumAngularSpeed => Mathf.Max(1f, minimumAngularSpeed);
    public float StalledSpeed => Mathf.Max(0.01f, stalledSpeed);
    public float StalledSecondsBeforeRepath => Mathf.Max(0.25f, stalledSecondsBeforeRepath);
    public float RepathCooldown => Mathf.Max(0.1f, repathCooldown);
    public int TemporaryYieldPriority => Mathf.Clamp(temporaryYieldPriority, 1, 30);
    public float TemporaryYieldSeconds => Mathf.Max(0.1f, temporaryYieldSeconds);
    public bool IgnoreCharacterToCharacterCollisions => ignoreCharacterToCharacterCollisions;

    public int GetStablePriority(bool customer, int instanceId, int authoredPriority = -1)
    {
        if (authoredPriority >= 0)
            return Mathf.Clamp(authoredPriority, 0, 99);

        int min = customer
            ? Mathf.Min(customerPriorityMin, customerPriorityMax)
            : Mathf.Min(staffPriorityMin, staffPriorityMax);
        int max = customer
            ? Mathf.Max(customerPriorityMin, customerPriorityMax)
            : Mathf.Max(staffPriorityMin, staffPriorityMax);
        int spread = Mathf.Max(1, max - min + 1);
        return min + Mathf.Abs(instanceId % spread);
    }

    public static CrowdNavigationProfile LoadGlobal()
    {
        return Resources.Load<CrowdNavigationProfile>("Settings/CrowdNavigationProfile");
    }
}
