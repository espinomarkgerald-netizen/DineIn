using UnityEngine;

[CreateAssetMenu(
    fileName = "AlienAnimationSettings",
    menuName = "DineIn/Customers/Alien Animation Settings")]
public sealed class AlienAnimationSettings : ScriptableObject
{
    public const string ResourceName = "AlienAnimationSettings";

    [Header("Individual Animation Speed")]
    [Tooltip("Per-customer multiplier used while walking.")]
    public Vector2 walkingSpeedRange = new Vector2(0.93f, 1.07f);

    [Tooltip("Per-customer multiplier used while sitting but not eating.")]
    public Vector2 sittingSpeedRange = new Vector2(0.96f, 1.04f);

    [Tooltip("Per-customer multiplier used by the seated eating cycle.")]
    public Vector2 eatingSpeedRange = new Vector2(0.88f, 1.12f);

    [Tooltip("How quickly the Animator changes between personal speed multipliers.")]
    [Min(0.1f)] public float speedBlend = 5f;

    [Header("Seated Idle Variation")]
    [Min(0.1f)] public float breathingCyclesPerSecond = 0.28f;
    [Range(0f, 4f)] public float breathingChestDegrees = 1.25f;
    [Range(0f, 4f)] public float idleHeadSwayDegrees = 1.1f;

    [Header("Procedural Eating")]
    [Tooltip("Duration of one reach, bite, chew, and return cycle before the personal speed multiplier.")]
    [Min(0.5f)] public float eatingCycleSeconds = 1.65f;

    [Range(0f, 1f)] public float armReachWeight = 0.82f;
    [Range(0f, 18f)] public float torsoLeanDegrees = 7f;
    [Range(0f, 10f)] public float chewHeadDegrees = 3.2f;
    [Range(0f, 10f)] public float chewJawDegrees = 4.5f;

    [Tooltip("Food position measured forward from the seated customer as a fraction of root-to-head height.")]
    [Range(0.05f, 0.8f)] public float foodForward = 0.32f;

    [Tooltip("Food position measured upward from the hips as a fraction of hips-to-head height.")]
    [Range(-0.2f, 0.6f)] public float foodHeight = 0.12f;

    [Range(0f, 0.3f)] public float foodSideOffset = 0.06f;
    [Range(0f, 0.2f)] public float handLiftArc = 0.055f;

    [Tooltip("Mouth offset forward from the Head bone as a fraction of root-to-head height.")]
    [Range(0f, 0.25f)] public float mouthForward = 0.075f;

    [Tooltip("Mouth offset below the Head bone as a fraction of root-to-head height.")]
    [Range(0f, 0.2f)] public float mouthDown = 0.045f;

    [Tooltip("When enabled, customers may swap hands between bites.")]
    public bool alternateHands = true;

    [Header("Eating Particles")]
    [Tooltip("Shared transparent material used by automatic bite particles.")]
    public Material particleMaterial;
    [Range(0, 12)] public int particlesPerBite = 5;
    [Min(0.05f)] public float particleLifetime = 0.48f;
    [Min(0f)] public float particleSpeed = 0.42f;
    [Tooltip("Particle size as a fraction of hips-to-head height.")]
    [Min(0.001f)] public float particleSize = 0.075f;
    [Range(-1f, 2f)] public float particleGravity = 0.18f;
    [Tooltip("Particle spawn radius as a fraction of hips-to-head height.")]
    [Min(0f)] public float particleSpawnRadius = 0.035f;
    public Color crumbColorA = new Color(1f, 0.76f, 0.2f, 1f);
    public Color crumbColorB = new Color(1f, 0.96f, 0.66f, 1f);

    private static AlienAnimationSettings cachedGlobal;

    public static AlienAnimationSettings LoadGlobal()
    {
        if (cachedGlobal == null)
            cachedGlobal = Resources.Load<AlienAnimationSettings>(ResourceName);

        if (cachedGlobal != null)
            return cachedGlobal;

        cachedGlobal = CreateInstance<AlienAnimationSettings>();
        cachedGlobal.name = $"{ResourceName} (Runtime Defaults)";
        cachedGlobal.hideFlags = HideFlags.HideAndDontSave;
        Debug.LogWarning(
            $"[AlienAnimation] Resources/{ResourceName}.asset is missing. Runtime defaults will be used.");
        return cachedGlobal;
    }

    public float PickWalkingSpeed(float sample) => PickRange(walkingSpeedRange, sample);
    public float PickSittingSpeed(float sample) => PickRange(sittingSpeedRange, sample);
    public float PickEatingSpeed(float sample) => PickRange(eatingSpeedRange, sample);

    private static float PickRange(Vector2 range, float sample)
    {
        float minimum = Mathf.Max(0.1f, Mathf.Min(range.x, range.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(range.x, range.y));
        return Mathf.Lerp(minimum, maximum, Mathf.Clamp01(sample));
    }
}
