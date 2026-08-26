using UnityEngine;

/// <summary>
/// Visual-only animation layer for a CustomerAgent. Gameplay movement, seating,
/// and eating duration remain owned by CustomerAgent and CustomerGroup.
/// </summary>
internal sealed class AlienProceduralAnimation
{
    private const float MinimumBodyHeight = 0.5f;
    private const float BiteParticleStart = 0.52f;
    private const float BiteParticleEnd = 0.68f;

    private readonly CustomerAgent owner;
    private readonly Animator animator;
    private readonly AlienAnimationSettings settings;
    private readonly float baseAnimatorSpeed;
    private readonly float walkingSpeed;
    private readonly float sittingSpeed;
    private readonly float eatingSpeed;
    private readonly float idlePhase;
    private readonly float eatingPhase;
    private readonly bool prefersRightHand;

    private Transform hips;
    private Transform chest;
    private Transform head;
    private Transform jaw;
    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private Transform leftHand;
    private Transform rightUpperArm;
    private Transform rightLowerArm;
    private Transform rightHand;

    private ParticleSystem eatingParticles;
    private GameObject bitePiece;
    private Renderer bitePieceRenderer;
    private MaterialPropertyBlock bitePieceProperties;
    private FoodTray foodSource;
    private int dinerIndex;
    private int cachedFoodCycle = int.MinValue;
    private Vector3 cachedFoodPosition;
    private Color cachedFoodColor = Color.white;
    private CustomerGroup.FoodType cachedFoodType = CustomerGroup.FoodType.Chicken;
    private bool hasCachedFoodPosition;
    private float animatorSpeedMultiplier = 1f;
    private float eatingBlend;
    private float eatingClock;
    private int lastParticleCycle = int.MinValue;
    private bool isSeated;
    private bool isEating;
    private bool isMoving;
    private bool isCallingManager;
    private float managerCallClock;
    private CustomerProceduralState serviceState;
    private CustomerProceduralReaction activeReaction;
    private float reactionTimeRemaining;
    private float serviceClock;
    private float patienceRemaining = 1f;
    private int groupMemberIndex;
    private int groupMemberCount = 1;
    private Transform conversationTarget;

    private static Material sharedParticleMaterial;
    private static Texture2D sharedParticleTexture;

    public AlienProceduralAnimation(
        CustomerAgent owner,
        Animator animator,
        AlienAnimationSettings settings)
    {
        this.owner = owner;
        this.animator = animator;
        this.settings = settings;

        if (animator == null || settings == null)
            return;

        baseAnimatorSpeed = Mathf.Max(0.01f, animator.speed);

        int seed = owner != null ? owner.GetInstanceID() : animator.GetInstanceID();
        walkingSpeed = settings.PickWalkingSpeed(Hash01(seed, 11));
        sittingSpeed = settings.PickSittingSpeed(Hash01(seed, 23));
        eatingSpeed = settings.PickEatingSpeed(Hash01(seed, 37));
        idlePhase = Hash01(seed, 51) * Mathf.PI * 2f;
        eatingPhase = Hash01(seed, 67);
        prefersRightHand = Hash01(seed, 79) >= 0.5f;

        ResolveHumanoidBones();
    }

    public void SetState(bool seated, bool eating, bool moving)
    {
        isSeated = seated;
        isEating = seated && eating;
        isMoving = !seated && moving;

        if (!isEating)
        {
            lastParticleCycle = int.MinValue;
            if (bitePiece != null)
                bitePiece.SetActive(false);
        }
    }

    public void SetFoodSource(FoodTray source, int memberIndex)
    {
        foodSource = source;
        dinerIndex = memberIndex;
        cachedFoodCycle = int.MinValue;
        hasCachedFoodPosition = false;
    }

    public void SetCallingManager(bool calling)
    {
        isCallingManager = calling;
        if (!calling)
            managerCallClock = 0f;
    }

    public void SetServiceState(CustomerProceduralState state)
    {
        if (serviceState == state)
            return;

        serviceState = state;
        serviceClock = 0f;
        if (state != CustomerProceduralState.QueueWaiting &&
            state != CustomerProceduralState.RequestOrder)
        {
            patienceRemaining = 1f;
        }
    }

    public void SetPatience(float normalizedRemaining)
    {
        patienceRemaining = Mathf.Clamp01(normalizedRemaining);
    }

    public void SetGroupContext(
        int memberIndex,
        int memberCount,
        Transform partner)
    {
        groupMemberIndex = Mathf.Max(0, memberIndex);
        groupMemberCount = Mathf.Max(1, memberCount);
        conversationTarget = partner;
    }

    public void PlayReaction(CustomerProceduralReaction reaction)
    {
        if (settings == null || !settings.enableCustomerReactions)
            return;

        activeReaction = reaction;
        reactionTimeRemaining = Mathf.Max(0.2f, settings.reactionDurationSeconds);
    }

    public void Update(float deltaTime)
    {
        if (animator == null || settings == null)
            return;

        float targetMultiplier = isEating
            ? eatingSpeed
            : isSeated
                ? sittingSpeed
                : isMoving
                    ? walkingSpeed
                    : 1f;

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, settings.speedBlend) * deltaTime);
        animatorSpeedMultiplier = Mathf.Lerp(animatorSpeedMultiplier, targetMultiplier, blend);
        animator.speed = baseAnimatorSpeed * animatorSpeedMultiplier;

        float eatingTarget = isEating ? 1f : 0f;
        eatingBlend = Mathf.MoveTowards(eatingBlend, eatingTarget, deltaTime * 5f);

        if (isEating)
            eatingClock += deltaTime * eatingSpeed;
        if (isCallingManager)
            managerCallClock += deltaTime;
        serviceClock += deltaTime;
        if (settings.enableCustomerReactions && reactionTimeRemaining > 0f)
            reactionTimeRemaining = Mathf.Max(0f, reactionTimeRemaining - deltaTime);
    }

    public void LateUpdate()
    {
        if (animator == null || settings == null || !animator.isHuman)
            return;

        if (head == null)
            ResolveHumanoidBones();

        if (head == null)
            return;

        if (isSeated)
            ApplySeatedIdleMotion();

        if (settings.enableCustomerReactions && reactionTimeRemaining > 0f)
        {
            ApplyReactionMotion();
            return;
        }

        if (isCallingManager)
        {
            ApplyManagerCallMotion();
            return;
        }

        if (settings.enableProceduralEating &&
            isSeated && eatingBlend > 0.001f && !isCallingManager)
        {
            ApplyEatingMotion();
            return;
        }

        float impatience = ApplyPatienceMotion();
        if (impatience >= 0.55f)
            return;

        switch (serviceState)
        {
            case CustomerProceduralState.QueueWaiting:
            case CustomerProceduralState.Conversation:
            case CustomerProceduralState.WaitingForFood:
                ApplyConversationMotion();
                break;
            case CustomerProceduralState.BrowseMenu:
                ApplyMenuBrowseMotion();
                break;
            case CustomerProceduralState.RequestOrder:
                ApplyServiceRequestMotion(false);
                break;
            case CustomerProceduralState.RequestBill:
                ApplyServiceRequestMotion(true);
                break;
        }
    }

    public void Dispose()
    {
        if (animator != null)
            animator.speed = baseAnimatorSpeed;
    }

    private void ResolveHumanoidBones()
    {
        if (animator == null || !animator.isHuman)
            return;

        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null)
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null)
            chest = animator.GetBoneTransform(HumanBodyBones.Spine);

        head = animator.GetBoneTransform(HumanBodyBones.Head);
        jaw = animator.GetBoneTransform(HumanBodyBones.Jaw);

        leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    private void ApplySeatedIdleMotion()
    {
        float time = Time.time * Mathf.Max(0.1f, settings.breathingCyclesPerSecond) *
                     Mathf.PI * 2f * sittingSpeed + idlePhase;
        float breath = Mathf.Sin(time);
        float sway = Mathf.Sin(time * 0.47f + idlePhase * 0.31f);

        RotateAroundWorldAxis(
            chest,
            owner.transform.right,
            breath * settings.breathingChestDegrees);
        RotateAroundWorldAxis(
            head,
            owner.transform.forward,
            sway * settings.idleHeadSwayDegrees);
    }

    private void ApplyEatingMotion()
    {
        float cycleSeconds = Mathf.Max(0.5f, settings.eatingCycleSeconds);
        float cycleValue = eatingClock / cycleSeconds + eatingPhase;
        int cycleIndex = Mathf.FloorToInt(cycleValue);
        float phase = Mathf.Repeat(cycleValue, 1f);

        if (ShouldUseDrinkCycle(cycleIndex, out Vector3 drinkPosition))
        {
            ApplyDrinkingMotion(cycleIndex, phase, drinkPosition);
            return;
        }

        float lift = EvaluateLift(phase);
        float reachEnvelope = SmoothStep(0f, 0.14f, phase) *
                              (1f - SmoothStep(0.82f, 0.98f, phase));
        float chewEnvelope = SmoothStep(0.46f, 0.54f, phase) *
                             (1f - SmoothStep(0.72f, 0.82f, phase));

        RotateAroundWorldAxis(
            chest,
            owner.transform.right,
            settings.torsoLeanDegrees * reachEnvelope * eatingBlend);

        float chew = Mathf.Sin(phase * Mathf.PI * 12f) * chewEnvelope * eatingBlend;
        RotateAroundWorldAxis(
            head,
            owner.transform.right,
            chew * settings.chewHeadDegrees);
        RotateAroundWorldAxis(
            jaw,
            owner.transform.right,
            Mathf.Abs(chew) * settings.chewJawDegrees);

        float bodyHeight = ResolveBodyHeight();
        Vector3 mouth = ResolveMouthPosition(bodyHeight);
        bool rightHandCycle = ResolveRightHandForCycle(cycleIndex);
        float side = rightHandCycle ? 1f : -1f;
        Vector3 food = ResolveFoodPosition(
            bodyHeight,
            side,
            cycleIndex,
            out Color sampledFoodColor,
            out CustomerGroup.FoodType foodType);
        Color configuredFoodColor = settings.GetFoodCrumbColor(foodType);
        Color crumbColor = Color.Lerp(
            configuredFoodColor,
            sampledFoodColor,
            Mathf.Clamp01(settings.sampledFoodColorStrength));
        crumbColor.a = 1f;
        Vector3 handTarget = Vector3.Lerp(food, mouth, lift);
        handTarget += Vector3.up * (Mathf.Sin(lift * Mathf.PI) * bodyHeight * settings.handLiftArc);

        Transform upperArm = rightHandCycle ? rightUpperArm : leftUpperArm;
        Transform lowerArm = rightHandCycle ? rightLowerArm : leftLowerArm;
        Transform hand = rightHandCycle ? rightHand : leftHand;
        float armWeight = settings.armReachWeight * reachEnvelope * eatingBlend;
        ApplyCcdArm(upperArm, lowerArm, hand, handTarget, armWeight);
        UpdateBitePiece(hand, bodyHeight, phase, lift, crumbColor);

        UpdateParticlePosition(mouth);
        if (phase >= BiteParticleStart && phase <= BiteParticleEnd &&
            lastParticleCycle != cycleIndex)
        {
            lastParticleCycle = cycleIndex;
            EmitBiteParticles(mouth, crumbColor);
        }
    }

    private bool ShouldUseDrinkCycle(int cycleIndex, out Vector3 drinkPosition)
    {
        drinkPosition = default;
        if (!settings.enableDrinking || foodSource == null)
            return false;

        int interval = Mathf.Max(2, settings.drinkEveryCycles);
        int positiveCycle = Mathf.Abs(cycleIndex + groupMemberIndex + dinerIndex);
        return positiveCycle % interval == interval - 1 &&
               foodSource.TryGetDrinkSipPosition(out drinkPosition);
    }

    private void ApplyDrinkingMotion(
        int cycleIndex,
        float phase,
        Vector3 drinkPosition)
    {
        if (bitePiece != null)
            bitePiece.SetActive(false);

        float lift = EvaluateLift(phase);
        float reachEnvelope = SmoothStep(0f, 0.14f, phase) *
                              (1f - SmoothStep(0.82f, 0.98f, phase));
        float sipEnvelope = SmoothStep(0.46f, 0.55f, phase) *
                            (1f - SmoothStep(0.68f, 0.78f, phase));
        float bodyHeight = ResolveBodyHeight();
        Vector3 mouth = ResolveMouthPosition(bodyHeight);
        bool rightHandCycle = ResolveRightHandForCycle(cycleIndex);
        Vector3 handTarget = Vector3.Lerp(drinkPosition, mouth, lift);
        handTarget += Vector3.up *
                      (Mathf.Sin(lift * Mathf.PI) * bodyHeight * settings.drinkLiftArc);

        Transform upperArm = rightHandCycle ? rightUpperArm : leftUpperArm;
        Transform lowerArm = rightHandCycle ? rightLowerArm : leftLowerArm;
        Transform hand = rightHandCycle ? rightHand : leftHand;
        ApplyCcdArm(
            upperArm,
            lowerArm,
            hand,
            handTarget,
            settings.drinkArmReachWeight * reachEnvelope * eatingBlend);

        RotateAroundWorldAxis(
            head,
            owner.transform.right,
            -settings.drinkHeadTiltDegrees * sipEnvelope * eatingBlend);
        RotateAroundWorldAxis(
            chest,
            owner.transform.right,
            settings.torsoLeanDegrees * 0.35f * reachEnvelope * eatingBlend);
    }

    private float ApplyPatienceMotion()
    {
        if (!settings.enablePatienceBodyLanguage ||
            (serviceState != CustomerProceduralState.QueueWaiting &&
             serviceState != CustomerProceduralState.RequestOrder))
            return 0f;

        float angryThreshold = Mathf.Clamp(
            settings.patienceAngryThreshold,
            0.01f,
            0.49f);
        float concernThreshold = Mathf.Clamp(
            settings.patienceConcernThreshold,
            angryThreshold + 0.01f,
            0.95f);
        if (patienceRemaining >= concernThreshold)
            return 0f;

        float concern = Mathf.InverseLerp(
            concernThreshold,
            0f,
            patienceRemaining);
        float angry = patienceRemaining < angryThreshold
            ? Mathf.InverseLerp(angryThreshold, 0f, patienceRemaining)
            : 0f;
        float scan = Mathf.Sin(
            serviceClock * Mathf.Lerp(2.2f, 4.4f, concern) + idlePhase);
        RotateAroundWorldAxis(
            head,
            Vector3.up,
            scan * settings.impatientHeadScanDegrees * concern);

        float bodyHeight = ResolveBodyHeight();
        Vector3 torso = ResolveTorsoOrigin(bodyHeight);
        if (angry > 0.001f)
        {
            Vector3 leftTarget = torso +
                                 owner.transform.right * (bodyHeight * 0.12f) -
                                 Vector3.up * (bodyHeight * 0.08f);
            Vector3 rightTarget = torso -
                                  owner.transform.right * (bodyHeight * 0.12f) -
                                  Vector3.up * (bodyHeight * 0.08f);
            float weight = settings.angryCrossedArmsWeight * angry;
            ApplyCcdArm(leftUpperArm, leftLowerArm, leftHand, leftTarget, weight);
            ApplyCcdArm(rightUpperArm, rightLowerArm, rightHand, rightTarget, weight);
            return concern;
        }

        bool right = prefersRightHand;
        float tap = Mathf.Max(
            0f,
            Mathf.Sin(serviceClock *
                      Mathf.Max(0.1f, settings.impatientTableTapCyclesPerSecond) *
                      Mathf.PI * 2f));
        Vector3 tapTarget = torso +
                            owner.transform.forward * (bodyHeight * 0.28f) +
                            owner.transform.right *
                            (bodyHeight * (right ? 0.16f : -0.16f)) -
                            Vector3.up * (bodyHeight * (0.24f - tap * 0.035f));
        ApplyCcdArm(
            right ? rightUpperArm : leftUpperArm,
            right ? rightLowerArm : leftLowerArm,
            right ? rightHand : leftHand,
            tapTarget,
            settings.impatientTableTapWeight * concern);
        return concern;
    }

    private void ApplyConversationMotion()
    {
        if (!settings.enableGroupConversation ||
            groupMemberCount < 2 ||
            conversationTarget == null)
            return;

        float cycle = Mathf.Max(2f, settings.conversationCycleSeconds);
        int turn = Mathf.FloorToInt(serviceClock / cycle);
        float phase = Mathf.Repeat(serviceClock / cycle, 1f);
        float activePortion = Mathf.Clamp(
            settings.conversationActivePortion,
            0.1f,
            0.8f);
        if (phase > activePortion)
            return;

        float envelope = Mathf.Sin(Mathf.Clamp01(phase / activePortion) * Mathf.PI);
        int speaker = PositiveModulo(turn, groupMemberCount);
        if (speaker == groupMemberIndex)
        {
            Vector3 towardPartner = conversationTarget.position - owner.transform.position;
            towardPartner.y = 0f;
            if (towardPartner.sqrMagnitude > 0.0001f)
            {
                float turnDegrees = Mathf.Clamp(
                    Vector3.SignedAngle(
                        owner.transform.forward,
                        towardPartner.normalized,
                        Vector3.up),
                    -settings.conversationTurnDegrees,
                    settings.conversationTurnDegrees);
                RotateAroundWorldAxis(head, Vector3.up, turnDegrees * envelope);
                RotateAroundWorldAxis(chest, Vector3.up, turnDegrees * envelope * 0.35f);
            }

            float bodyHeight = ResolveBodyHeight();
            Vector3 torso = ResolveTorsoOrigin(bodyHeight);
            bool right = prefersRightHand;
            Vector3 handTarget = torso +
                                 Vector3.up * (bodyHeight * 0.02f) +
                                 owner.transform.forward *
                                 (bodyHeight * settings.conversationHandForward) +
                                 owner.transform.right *
                                 (bodyHeight * settings.conversationHandSide *
                                  (right ? 1f : -1f));
            ApplyCcdArm(
                right ? rightUpperArm : leftUpperArm,
                right ? rightLowerArm : leftLowerArm,
                right ? rightHand : leftHand,
                handTarget,
                settings.conversationArmWeight * envelope);
        }
        else
        {
            float nod = Mathf.Sin(phase / activePortion * Mathf.PI * 2f);
            RotateAroundWorldAxis(
                head,
                owner.transform.right,
                nod * settings.conversationNodDegrees * envelope);
        }
    }

    private void ApplyMenuBrowseMotion()
    {
        float bodyHeight = ResolveBodyHeight();
        float look = 0.65f + Mathf.Sin(serviceClock * 1.1f + idlePhase) * 0.2f;
        RotateAroundWorldAxis(
            head,
            owner.transform.right,
            settings.conversationNodDegrees * 1.6f * look);

        bool right = ((groupMemberIndex & 1) == 0) == prefersRightHand;
        Vector3 torso = ResolveTorsoOrigin(bodyHeight);
        Vector3 menuTarget = torso +
                             owner.transform.forward * (bodyHeight * 0.3f) -
                             Vector3.up * (bodyHeight * 0.25f) +
                             owner.transform.right *
                             (bodyHeight * (right ? 0.1f : -0.1f));
        float pointEnvelope = (Mathf.Sin(serviceClock * 1.35f + idlePhase) + 1f) * 0.5f;
        ApplyCcdArm(
            right ? rightUpperArm : leftUpperArm,
            right ? rightLowerArm : leftLowerArm,
            right ? rightHand : leftHand,
            menuTarget,
            settings.conversationArmWeight * 0.7f * pointEnvelope);
    }

    private void ApplyServiceRequestMotion(bool requestingBill)
    {
        if (!settings.enableServiceRequestGestures)
            return;

        if (groupMemberIndex != 0)
        {
            if (!requestingBill)
                ApplyMenuBrowseMotion();
            return;
        }

        float cycle = Mathf.Max(1f, settings.serviceRequestCycleSeconds);
        float phase = Mathf.Repeat(serviceClock / cycle, 1f);
        float activePortion = Mathf.Clamp(
            settings.serviceRequestActivePortion,
            0.1f,
            0.9f);
        if (phase > activePortion)
            return;

        float handHeight = requestingBill
            ? settings.billRequestHandHeight
            : settings.orderRequestHandHeight;
        ApplyCartoonCallGesture(
            Mathf.Clamp01(phase / activePortion),
            handHeight,
            settings.serviceRequestHandSide,
            settings.serviceRequestHandForward,
            settings.serviceRequestArmWeight,
            settings.serviceRequestWaveDistance,
            settings.serviceRequestWavesAtTop);
    }

    private void ApplyReactionMotion()
    {
        float duration = Mathf.Max(0.2f, settings.reactionDurationSeconds);
        float progress = 1f - Mathf.Clamp01(reactionTimeRemaining / duration);
        float envelope = Mathf.Sin(progress * Mathf.PI);
        float bodyHeight = ResolveBodyHeight();
        Vector3 torso = ResolveTorsoOrigin(bodyHeight);

        switch (activeReaction)
        {
            case CustomerProceduralReaction.Positive:
            {
                float nod = Mathf.Sin(progress * Mathf.PI * 4f);
                RotateAroundWorldAxis(
                    head,
                    owner.transform.right,
                    nod * settings.positiveNodDegrees * envelope);
                Vector3 target = torso +
                                 owner.transform.forward * (bodyHeight * 0.2f) +
                                 owner.transform.right *
                                 (bodyHeight * (prefersRightHand ? 0.15f : -0.15f));
                ApplyCcdArm(
                    prefersRightHand ? rightUpperArm : leftUpperArm,
                    prefersRightHand ? rightLowerArm : leftLowerArm,
                    prefersRightHand ? rightHand : leftHand,
                    target,
                    settings.positiveGestureWeight * envelope);
                break;
            }
            case CustomerProceduralReaction.Neutral:
            {
                Vector3 leftTarget = torso -
                                     owner.transform.right * (bodyHeight * 0.24f) +
                                     Vector3.up * (bodyHeight * 0.06f) +
                                     owner.transform.forward * (bodyHeight * 0.1f);
                Vector3 rightTarget = torso +
                                      owner.transform.right * (bodyHeight * 0.24f) +
                                      Vector3.up * (bodyHeight * 0.06f) +
                                      owner.transform.forward * (bodyHeight * 0.1f);
                float weight = settings.neutralShrugWeight * envelope;
                ApplyCcdArm(leftUpperArm, leftLowerArm, leftHand, leftTarget, weight);
                ApplyCcdArm(rightUpperArm, rightLowerArm, rightHand, rightTarget, weight);
                RotateAroundWorldAxis(
                    head,
                    owner.transform.forward,
                    settings.conversationNodDegrees * envelope);
                break;
            }
            case CustomerProceduralReaction.Angry:
            {
                float shake = Mathf.Sin(progress * Mathf.PI * 6f);
                RotateAroundWorldAxis(
                    head,
                    Vector3.up,
                    shake * settings.angryHeadShakeDegrees * envelope);
                bool right = prefersRightHand;
                Vector3 pointTarget = torso +
                                      owner.transform.forward * (bodyHeight * 0.42f) +
                                      owner.transform.right *
                                      (bodyHeight * (right ? 0.12f : -0.12f));
                ApplyCcdArm(
                    right ? rightUpperArm : leftUpperArm,
                    right ? rightLowerArm : leftLowerArm,
                    right ? rightHand : leftHand,
                    pointTarget,
                    settings.angryGestureWeight * envelope);
                break;
            }
        }
    }

    private void ApplyManagerCallMotion()
    {
        if (head == null)
            return;

        float cycle = Mathf.Max(1f, settings.managerCallGestureCycleSeconds);
        float phase = Mathf.Repeat(managerCallClock / cycle, 1f);
        float wavePortion = Mathf.Max(
            0.1f,
            settings.callWaveEnd - settings.callRaiseEnd);
        float wavesAtTop = Mathf.Max(
            0.5f,
            settings.managerCallWaveCyclesPerSecond * cycle * wavePortion);
        ApplyCartoonCallGesture(
            phase,
            settings.managerCallHandHeight,
            settings.managerCallHandSide,
            settings.managerCallHandForward,
            settings.managerCallArmWeight,
            settings.managerCallWaveDistance,
            wavesAtTop);
    }

    private void ApplyCartoonCallGesture(
        float phase,
        float handHeight,
        float handSide,
        float handForward,
        float armWeight,
        float waveDistance,
        float wavesAtTop)
    {
        float anticipationEnd = Mathf.Clamp(settings.callAnticipationEnd, 0.05f, 0.3f);
        float raiseEnd = Mathf.Clamp(
            settings.callRaiseEnd,
            anticipationEnd + 0.1f,
            0.68f);
        float waveEnd = Mathf.Clamp(
            settings.callWaveEnd,
            raiseEnd + 0.1f,
            0.95f);

        float raise;
        float poseWeight;
        float waveEnvelope = 0f;
        if (phase < anticipationEnd)
        {
            float t = Mathf.Clamp01(phase / anticipationEnd);
            poseWeight = Mathf.SmoothStep(0f, 1f, t);
            raise = -Mathf.Sin(t * Mathf.PI) * 0.12f;
        }
        else if (phase < raiseEnd)
        {
            float t = Mathf.InverseLerp(anticipationEnd, raiseEnd, phase);
            float fastEase = 1f - Mathf.Pow(1f - t, 3f);
            raise = fastEase +
                    Mathf.Sin(t * Mathf.PI) * settings.callRaiseOvershoot;
            poseWeight = 1f;
        }
        else if (phase < waveEnd)
        {
            float t = Mathf.InverseLerp(raiseEnd, waveEnd, phase);
            float settlingBounce = Mathf.Sin(t * Mathf.PI * 4f) *
                                   settings.callTopBounceHeight *
                                   (1f - t * 0.55f);
            raise = 1f + settlingBounce;
            poseWeight = 1f;
            waveEnvelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.14f)) *
                           (1f - Mathf.SmoothStep(0.82f, 1f, t));
        }
        else
        {
            float t = Mathf.InverseLerp(waveEnd, 1f, phase);
            float easedDown = Mathf.SmoothStep(0f, 1f, t);
            raise = 1f - easedDown;
            poseWeight = 1f - easedDown;
        }

        float waveProgress = Mathf.Clamp01(
            Mathf.InverseLerp(raiseEnd, waveEnd, phase));
        float waveAngle = waveProgress * Mathf.Max(0.5f, wavesAtTop) * Mathf.PI * 2f;
        waveAngle += Mathf.Sin(waveAngle * 0.5f + idlePhase) *
                     settings.callWaveSpeedVariation;
        float wave = Mathf.Sin(waveAngle) * waveEnvelope;

        float bodyHeight = ResolveBodyHeight();
        Vector3 torso = ResolveTorsoOrigin(bodyHeight);
        bool right = prefersRightHand;
        float side = right ? 1f : -1f;
        Vector3 sideDirection = owner.transform.right * side;
        Vector3 lowTarget = torso +
                            sideDirection *
                            (bodyHeight *
                             (handSide + settings.callAnticipationSideSwing)) -
                            Vector3.up * (bodyHeight * 0.12f) +
                            owner.transform.forward * (bodyHeight * handForward * 0.35f);
        Vector3 topTarget = head.position +
                            Vector3.up * (bodyHeight * handHeight) +
                            sideDirection * (bodyHeight * handSide) +
                            owner.transform.forward * (bodyHeight * handForward);
        Vector3 mainTarget = Vector3.LerpUnclamped(lowTarget, topTarget, raise) +
                             sideDirection * (bodyHeight * waveDistance * wave);

        ApplyCcdArm(
            right ? rightUpperArm : leftUpperArm,
            right ? rightLowerArm : leftLowerArm,
            right ? rightHand : leftHand,
            mainTarget,
            Mathf.Clamp01(armWeight) * poseWeight);

        Vector3 oppositeTarget = torso -
                                 sideDirection *
                                 (bodyHeight * settings.callOppositeArmSide) +
                                 owner.transform.forward * (bodyHeight * 0.08f) +
                                 Vector3.up *
                                 (bodyHeight * (0.02f + Mathf.Max(0f, raise) * 0.1f));
        float oppositePulse = 0.78f +
                              Mathf.Sin(phase * Mathf.PI * 2f + idlePhase) * 0.22f;
        ApplyCcdArm(
            right ? leftUpperArm : rightUpperArm,
            right ? leftLowerArm : rightLowerArm,
            right ? leftHand : rightHand,
            oppositeTarget,
            settings.callOppositeArmWeight * poseWeight * oppositePulse);

        float bodyBounce = Mathf.Sin(phase * Mathf.PI * 2f) *
                           settings.callBodyLeanDegrees * poseWeight;
        RotateAroundWorldAxis(
            chest,
            owner.transform.forward,
            bodyBounce * side);
    }

    private float ResolveBodyHeight()
    {
        Vector3 basePosition = hips != null ? hips.position : owner.transform.position;
        return Mathf.Max(MinimumBodyHeight, Vector3.Distance(basePosition, head.position));
    }

    private Vector3 ResolveTorsoOrigin(float bodyHeight)
    {
        return chest != null
            ? chest.position
            : head.position - Vector3.up * (bodyHeight * 0.3f);
    }

    private static int PositiveModulo(int value, int modulus)
    {
        modulus = Mathf.Max(1, modulus);
        return ((value % modulus) + modulus) % modulus;
    }

    private Vector3 ResolveFoodPosition(
        float bodyHeight,
        float side,
        int cycleIndex,
        out Color sampledColor,
        out CustomerGroup.FoodType foodType)
    {
        if (foodSource != null)
        {
            if (!hasCachedFoodPosition || cachedFoodCycle != cycleIndex)
            {
                cachedFoodCycle = cycleIndex;
                hasCachedFoodPosition = foodSource.TryGetFoodBiteData(
                    dinerIndex,
                    cycleIndex,
                    out cachedFoodPosition,
                    out cachedFoodColor,
                    out cachedFoodType);
            }

            if (hasCachedFoodPosition)
            {
                sampledColor = cachedFoodColor;
                foodType = cachedFoodType;
                return cachedFoodPosition;
            }
        }

        sampledColor = settings.crumbColorA;
        foodType = CustomerGroup.FoodType.Chicken;
        Vector3 foodOrigin = hips != null ? hips.position : owner.transform.position;
        return foodOrigin +
               owner.transform.forward * (bodyHeight * settings.foodForward) +
               Vector3.up * (bodyHeight * settings.foodHeight) +
               owner.transform.right * (bodyHeight * settings.foodSideOffset * side);
    }

    private Vector3 ResolveMouthPosition(float bodyHeight)
    {
        return head.position +
               owner.transform.forward * (bodyHeight * settings.mouthForward) -
               Vector3.up * (bodyHeight * settings.mouthDown);
    }

    private bool ResolveRightHandForCycle(int cycleIndex)
    {
        if (!settings.alternateHands)
            return prefersRightHand;

        return (cycleIndex & 1) == 0 ? prefersRightHand : !prefersRightHand;
    }

    private static void ApplyCcdArm(
        Transform upperArm,
        Transform lowerArm,
        Transform hand,
        Vector3 target,
        float weight)
    {
        if (upperArm == null || lowerArm == null || hand == null || weight <= 0.001f)
            return;

        float iterationWeight = 1f - Mathf.Pow(1f - Mathf.Clamp01(weight), 0.5f);
        for (int i = 0; i < 2; i++)
        {
            RotateBoneToward(lowerArm, hand, target, iterationWeight);
            RotateBoneToward(upperArm, hand, target, iterationWeight);
        }
    }

    private static void RotateBoneToward(
        Transform bone,
        Transform endEffector,
        Vector3 target,
        float weight)
    {
        Vector3 toEffector = endEffector.position - bone.position;
        Vector3 toTarget = target - bone.position;
        if (toEffector.sqrMagnitude < 0.000001f || toTarget.sqrMagnitude < 0.000001f)
            return;

        Quaternion targetRotation =
            Quaternion.FromToRotation(toEffector, toTarget) * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, targetRotation, Mathf.Clamp01(weight));
    }

    private static void RotateAroundWorldAxis(Transform bone, Vector3 axis, float degrees)
    {
        if (bone == null || Mathf.Abs(degrees) < 0.0001f || axis.sqrMagnitude < 0.0001f)
            return;

        bone.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * bone.rotation;
    }

    private static float EvaluateLift(float phase)
    {
        if (phase < 0.22f)
            return 0f;
        if (phase < 0.48f)
            return SmoothStep(0.22f, 0.48f, phase);
        if (phase < 0.68f)
            return 1f;
        if (phase < 0.9f)
            return 1f - SmoothStep(0.68f, 0.9f, phase);
        return 0f;
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, value));
    }

    private void UpdateParticlePosition(Vector3 mouthPosition)
    {
        if (eatingParticles != null)
            eatingParticles.transform.position = mouthPosition;
    }

    private void EmitBiteParticles(Vector3 mouthPosition, Color foodColor)
    {
        if (!settings.enableEatingParticles)
            return;

        int count = Mathf.Max(0, settings.particlesPerBite);
        if (count == 0)
            return;

        ParticleSystem particles = GetOrCreateParticles();
        if (particles == null)
            return;

        float bodyHeight = ResolveBodyHeight();
        Vector3 origin = mouthPosition +
                         owner.transform.forward * (bodyHeight * 0.018f);
        particles.transform.position = origin;
        if (!particles.isPlaying)
            particles.Play(false);

        float radius = Mathf.Max(0f, settings.particleSpawnRadius * bodyHeight);
        float speed = Mathf.Max(0f, settings.particleSpeed);
        for (int i = 0; i < count; i++)
        {
            Vector3 scatter = Random.insideUnitSphere * radius;
            scatter -= owner.transform.forward *
                       Mathf.Min(0f, Vector3.Dot(scatter, owner.transform.forward));
            float randomSpeed = speed * Random.Range(0.75f, 1.2f);
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = origin + scatter,
                velocity = owner.transform.forward * (randomSpeed * Random.Range(0.35f, 0.75f)) +
                           owner.transform.up * (randomSpeed * Random.Range(0.45f, 1f)) +
                           owner.transform.right * (randomSpeed * Random.Range(-0.55f, 0.55f)),
                startLifetime = Mathf.Max(0.08f, settings.particleLifetime) *
                                Random.Range(0.85f, 1.15f),
                startSize = Mathf.Max(0.002f, settings.particleSize * bodyHeight) *
                            Random.Range(0.72f, 1.12f),
                startColor = Color.Lerp(foodColor, settings.crumbColorB, Random.Range(0f, 0.34f))
            };
            particles.Emit(emit, 1);
        }
    }

    private void UpdateBitePiece(
        Transform hand,
        float bodyHeight,
        float phase,
        float lift,
        Color foodColor)
    {
        bool visible = settings.enableVisibleBitePiece &&
                       hand != null &&
                       phase >= 0.24f &&
                       phase < BiteParticleStart &&
                       lift > 0.02f;
        if (!visible)
        {
            if (bitePiece != null)
                bitePiece.SetActive(false);
            return;
        }

        if (bitePiece == null)
        {
            bitePiece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bitePiece.name = "Visible Food Bite";
            bitePiece.transform.SetParent(owner.transform, true);
            Collider collider = bitePiece.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            bitePieceRenderer = bitePiece.GetComponent<Renderer>();
            bitePieceProperties = new MaterialPropertyBlock();
            if (bitePieceRenderer != null)
            {
                bitePieceRenderer.sharedMaterial = GetParticleMaterial();
                bitePieceRenderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                bitePieceRenderer.receiveShadows = false;
            }
        }

        float size = Mathf.Max(0.015f, bodyHeight * settings.bitePieceSize);
        if (bitePieceRenderer != null && bitePieceProperties != null)
        {
            bitePieceRenderer.GetPropertyBlock(bitePieceProperties);
            bitePieceProperties.SetColor("_BaseColor", foodColor);
            bitePieceProperties.SetColor("_Color", foodColor);
            bitePieceRenderer.SetPropertyBlock(bitePieceProperties);
        }
        bitePiece.SetActive(true);
        bitePiece.transform.position = hand.position + owner.transform.forward * (size * 0.35f);
        bitePiece.transform.localScale = Vector3.one * size;
    }

    private ParticleSystem GetOrCreateParticles()
    {
        if (eatingParticles != null)
            return eatingParticles;

        GameObject particleObject = new GameObject("Eating Particles");
        particleObject.transform.SetParent(owner.transform, false);
        eatingParticles = particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = eatingParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.maxParticles = Mathf.Max(12, settings.particlesPerBite * 3);
        main.startLifetime = Mathf.Max(0.05f, settings.particleLifetime);
        main.startSpeed = 0f;
        float bodyHeight = ResolveBodyHeight();
        main.startSize = Mathf.Max(0.001f, settings.particleSize * bodyHeight);
        main.gravityModifier = settings.particleGravity;
        main.startColor = new ParticleSystem.MinMaxGradient(
            settings.crumbColorA,
            settings.crumbColorB);

        ParticleSystem.EmissionModule emission = eatingParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = eatingParticles.shape;
        shape.enabled = false;

        ParticleSystem.VelocityOverLifetimeModule velocity = eatingParticles.velocityOverLifetime;
        velocity.enabled = false;

        ParticleSystem.SizeOverLifetimeModule size = eatingParticles.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.15f),
            new Keyframe(0.18f, 1f),
            new Keyframe(1f, 0f));
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer =
            particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetParticleMaterial();
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sortingOrder = 2;
        renderer.sortingFudge = 1f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        eatingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        return eatingParticles;
    }

    private Material GetParticleMaterial()
    {
        if (sharedParticleMaterial != null)
            return sharedParticleMaterial;

        if (settings.particleMaterial != null)
        {
            sharedParticleMaterial = new Material(settings.particleMaterial);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            sharedParticleMaterial = new Material(shader);
        }

        sharedParticleMaterial.name = "Alien Eating Particle Material (Runtime)";
        sharedParticleMaterial.hideFlags = HideFlags.HideAndDontSave;
        sharedParticleMaterial.renderQueue = 3000;

        Texture2D texture = GetParticleTexture();
        if (sharedParticleMaterial.HasProperty("_BaseMap"))
            sharedParticleMaterial.SetTexture("_BaseMap", texture);
        if (sharedParticleMaterial.HasProperty("_MainTex"))
            sharedParticleMaterial.SetTexture("_MainTex", texture);
        if (sharedParticleMaterial.HasProperty("_Surface"))
            sharedParticleMaterial.SetFloat("_Surface", 1f);
        if (sharedParticleMaterial.HasProperty("_Blend"))
            sharedParticleMaterial.SetFloat("_Blend", 0f);
        if (sharedParticleMaterial.HasProperty("_SrcBlend"))
        {
            sharedParticleMaterial.SetFloat(
                "_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }
        if (sharedParticleMaterial.HasProperty("_DstBlend"))
        {
            sharedParticleMaterial.SetFloat(
                "_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        if (sharedParticleMaterial.HasProperty("_ZWrite"))
            sharedParticleMaterial.SetFloat("_ZWrite", 0f);
        sharedParticleMaterial.SetOverrideTag("RenderType", "Transparent");
        sharedParticleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        return sharedParticleMaterial;
    }

    private static Texture2D GetParticleTexture()
    {
        if (sharedParticleTexture != null)
            return sharedParticleTexture;

        const int size = 16;
        sharedParticleTexture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = "Alien Eating Particle Dot (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = 1f - Mathf.SmoothStep(0.62f, 1f, distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        sharedParticleTexture.SetPixels(pixels);
        sharedParticleTexture.Apply(false, true);
        return sharedParticleTexture;
    }

    private static float Hash01(int seed, int salt)
    {
        unchecked
        {
            uint value = (uint)(seed ^ (salt * 374761393));
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
