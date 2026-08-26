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
    public bool enableProceduralEating = true;
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
    public bool enableVisibleBitePiece = true;

    [Header("Procedural Drinking")]
    [Tooltip("Allows occasional drink reaches when the served tray includes a drink.")]
    public bool enableDrinking = true;
    [Tooltip("One out of this many eating cycles becomes a sip. Higher values mean fewer sips.")]
    [Range(2, 8)] public int drinkEveryCycles = 4;
    [Range(0f, 1f)] public float drinkArmReachWeight = 0.72f;
    [Range(0f, 12f)] public float drinkHeadTiltDegrees = 4f;
    [Range(0f, 0.2f)] public float drinkLiftArc = 0.035f;

    [Header("Group Conversation")]
    public bool enableGroupConversation = true;
    [Tooltip("Length of one speaker/listener turn.")]
    [Min(2f)] public float conversationCycleSeconds = 6f;
    [Range(0.1f, 0.8f)] public float conversationActivePortion = 0.42f;
    [Range(0f, 25f)] public float conversationTurnDegrees = 11f;
    [Range(0f, 10f)] public float conversationNodDegrees = 3.5f;
    [Range(0f, 1f)] public float conversationArmWeight = 0.3f;
    [Range(0f, 0.5f)] public float conversationHandSide = 0.2f;
    [Range(-0.2f, 0.5f)] public float conversationHandForward = 0.18f;

    [Header("Patience Body Language")]
    public bool enablePatienceBodyLanguage = true;
    [Tooltip("Impatience begins when remaining patience falls below this value.")]
    [Range(0.1f, 0.9f)] public float patienceConcernThreshold = 0.55f;
    [Tooltip("Angry body language begins when remaining patience falls below this value.")]
    [Range(0.02f, 0.5f)] public float patienceAngryThreshold = 0.22f;
    [Range(0f, 30f)] public float impatientHeadScanDegrees = 12f;
    [Range(0f, 1f)] public float impatientTableTapWeight = 0.46f;
    [Min(0.1f)] public float impatientTableTapCyclesPerSecond = 2.2f;
    [Range(0f, 1f)] public float angryCrossedArmsWeight = 0.62f;

    [Header("Service Requests")]
    public bool enableServiceRequestGestures = true;
    [Tooltip("Only the lead member of a group makes the request gesture.")]
    [Min(1f)] public float serviceRequestCycleSeconds = 3.6f;
    [Range(0.1f, 0.9f)] public float serviceRequestActivePortion = 0.58f;
    [Range(0f, 1f)] public float serviceRequestArmWeight = 0.68f;
    [Range(0.05f, 0.7f)] public float orderRequestHandHeight = 0.32f;
    [Range(0.05f, 0.8f)] public float billRequestHandHeight = 0.38f;
    [Range(0f, 0.5f)] public float serviceRequestHandSide = 0.23f;
    [Range(-0.2f, 0.5f)] public float serviceRequestHandForward = 0.08f;
    [Range(0f, 0.2f)] public float serviceRequestWaveDistance = 0.045f;
    [Range(0.5f, 5f)] public float serviceRequestWavesAtTop = 2.25f;

    [Header("Call The Manager")]
    [Tooltip("Strength of the procedural raised-hand pose while requesting the Manager.")]
    [Range(0f, 1f)] public float managerCallArmWeight = 0.9f;
    [Tooltip("Raised hand height above the head as a fraction of hips-to-head height.")]
    [Range(0.05f, 0.8f)] public float managerCallHandHeight = 0.44f;
    [Tooltip("Horizontal distance from the head as a fraction of hips-to-head height.")]
    [Range(0f, 0.6f)] public float managerCallHandSide = 0.24f;
    [Tooltip("Forward distance from the head as a fraction of hips-to-head height.")]
    [Range(-0.2f, 0.5f)] public float managerCallHandForward = 0.08f;
    [Min(0.1f)] public float managerCallWaveCyclesPerSecond = 1.8f;
    [Range(0f, 0.3f)] public float managerCallWaveDistance = 0.08f;

    [Header("Cartoon Call Motion")]
    [Tooltip("Duration of one complete Manager call raise, wave, and lower cycle.")]
    [Min(1f)] public float managerCallGestureCycleSeconds = 2.6f;
    [Range(0.05f, 0.3f)] public float callAnticipationEnd = 0.17f;
    [Range(0.3f, 0.65f)] public float callRaiseEnd = 0.48f;
    [Range(0.65f, 0.95f)] public float callWaveEnd = 0.84f;
    [Range(0f, 0.3f)] public float callRaiseOvershoot = 0.1f;
    [Range(0f, 0.18f)] public float callTopBounceHeight = 0.045f;
    [Range(0f, 0.4f)] public float callAnticipationSideSwing = 0.16f;
    [Range(0f, 1f)] public float callOppositeArmWeight = 0.25f;
    [Range(0f, 0.5f)] public float callOppositeArmSide = 0.3f;
    [Range(0f, 10f)] public float callBodyLeanDegrees = 3.5f;
    [Range(0f, 1f)] public float callWaveSpeedVariation = 0.24f;

    [Header("Customer Reactions")]
    public bool enableCustomerReactions = true;
    [Min(0.2f)] public float reactionDurationSeconds = 1.8f;
    [Range(0f, 15f)] public float positiveNodDegrees = 5f;
    [Range(0f, 1f)] public float positiveGestureWeight = 0.24f;
    [Range(0f, 1f)] public float neutralShrugWeight = 0.44f;
    [Range(0f, 1f)] public float angryGestureWeight = 0.7f;
    [Range(0f, 30f)] public float angryHeadShakeDegrees = 14f;

    [Tooltip("Visible bite size as a fraction of hips-to-head height.")]
    [Range(0.01f, 0.15f)] public float bitePieceSize = 0.055f;

    [Header("Eating Particles")]
    public bool enableEatingParticles = true;
    [Tooltip("Shared transparent material used by automatic bite particles.")]
    public Material particleMaterial;
    [Range(0, 10)] public int particlesPerBite = 4;
    [Min(0.05f)] public float particleLifetime = 0.34f;
    [Min(0f)] public float particleSpeed = 0.2f;
    [Tooltip("Particle size as a fraction of hips-to-head height.")]
    [Range(0.002f, 0.08f)] public float particleSize = 0.022f;
    [Range(-1f, 2f)] public float particleGravity = 0.3f;
    [Tooltip("Particle spawn radius as a fraction of hips-to-head height.")]
    [Range(0f, 0.08f)] public float particleSpawnRadius = 0.012f;
    [Range(0f, 1f)] public float sampledFoodColorStrength = 0.25f;
    [Header("Food Crumb Colors")]
    public Color chickenCrumbColor = new Color(0.92f, 0.58f, 0.2f, 1f);
    public Color friesCrumbColor = new Color(1f, 0.82f, 0.28f, 1f);
    public Color burgerCrumbColor = new Color(0.58f, 0.3f, 0.14f, 1f);
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

    public Color GetFoodCrumbColor(CustomerGroup.FoodType foodType)
    {
        return foodType switch
        {
            CustomerGroup.FoodType.Fries => friesCrumbColor,
            CustomerGroup.FoodType.Burger => burgerCrumbColor,
            _ => chickenCrumbColor
        };
    }

    private static float PickRange(Vector2 range, float sample)
    {
        float minimum = Mathf.Max(0.1f, Mathf.Min(range.x, range.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(range.x, range.y));
        return Mathf.Lerp(minimum, maximum, Mathf.Clamp01(sample));
    }
}
