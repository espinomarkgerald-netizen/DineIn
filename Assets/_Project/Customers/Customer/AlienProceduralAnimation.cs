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
    private FoodTray foodSource;
    private int dinerIndex;
    private int cachedFoodCycle = int.MinValue;
    private Vector3 cachedFoodPosition;
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

        if (isCallingManager)
            ApplyManagerCallMotion();

        if (isSeated && eatingBlend > 0.001f && !isCallingManager)
            ApplyEatingMotion();
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
        Vector3 food = ResolveFoodPosition(bodyHeight, side, cycleIndex);
        Vector3 handTarget = Vector3.Lerp(food, mouth, lift);
        handTarget += Vector3.up * (Mathf.Sin(lift * Mathf.PI) * bodyHeight * settings.handLiftArc);

        Transform upperArm = rightHandCycle ? rightUpperArm : leftUpperArm;
        Transform lowerArm = rightHandCycle ? rightLowerArm : leftLowerArm;
        Transform hand = rightHandCycle ? rightHand : leftHand;
        float armWeight = settings.armReachWeight * reachEnvelope * eatingBlend;
        ApplyCcdArm(upperArm, lowerArm, hand, handTarget, armWeight);
        UpdateBitePiece(hand, bodyHeight, phase, lift);

        UpdateParticlePosition(mouth);
        if (phase >= BiteParticleStart && phase <= BiteParticleEnd &&
            lastParticleCycle != cycleIndex)
        {
            lastParticleCycle = cycleIndex;
            EmitBiteParticles(mouth);
        }
    }

    private void ApplyManagerCallMotion()
    {
        if (head == null)
            return;

        float bodyHeight = ResolveBodyHeight();
        bool right = prefersRightHand;
        float side = right ? 1f : -1f;
        float wave = Mathf.Sin(
            managerCallClock * Mathf.Max(0.1f, settings.managerCallWaveCyclesPerSecond) *
            Mathf.PI * 2f);

        Vector3 target = head.position +
                         Vector3.up * (bodyHeight * settings.managerCallHandHeight) +
                         owner.transform.right *
                         (bodyHeight * settings.managerCallHandSide * side) +
                         owner.transform.forward *
                         (bodyHeight * settings.managerCallHandForward) +
                         owner.transform.right *
                         (bodyHeight * settings.managerCallWaveDistance * wave);

        Transform upperArm = right ? rightUpperArm : leftUpperArm;
        Transform lowerArm = right ? rightLowerArm : leftLowerArm;
        Transform hand = right ? rightHand : leftHand;
        ApplyCcdArm(
            upperArm,
            lowerArm,
            hand,
            target,
            Mathf.Clamp01(settings.managerCallArmWeight));
    }

    private float ResolveBodyHeight()
    {
        Vector3 basePosition = hips != null ? hips.position : owner.transform.position;
        return Mathf.Max(MinimumBodyHeight, Vector3.Distance(basePosition, head.position));
    }

    private Vector3 ResolveFoodPosition(float bodyHeight, float side, int cycleIndex)
    {
        if (foodSource != null)
        {
            if (!hasCachedFoodPosition || cachedFoodCycle != cycleIndex)
            {
                cachedFoodCycle = cycleIndex;
                hasCachedFoodPosition = foodSource.TryGetFoodBitePosition(
                    dinerIndex,
                    cycleIndex,
                    out cachedFoodPosition);
            }

            if (hasCachedFoodPosition)
                return cachedFoodPosition;
        }

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

    private void EmitBiteParticles(Vector3 mouthPosition)
    {
        int count = Mathf.Max(0, settings.particlesPerBite);
        if (count == 0)
            return;

        ParticleSystem particles = GetOrCreateParticles();
        if (particles == null)
            return;

        particles.transform.position = mouthPosition + owner.transform.forward * 0.03f;
        if (!particles.isPlaying)
            particles.Play(false);

        float bodyHeight = ResolveBodyHeight();
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = particles.transform.position,
                velocity = owner.transform.forward * Random.Range(0.08f, 0.22f) +
                           owner.transform.up * Random.Range(0.12f, 0.34f) +
                           owner.transform.right * Random.Range(-0.18f, 0.18f),
                startLifetime = Mathf.Max(0.15f, settings.particleLifetime),
                startSize = Mathf.Max(0.025f, settings.particleSize * bodyHeight),
                startColor = Color.Lerp(settings.crumbColorA, settings.crumbColorB, Random.value)
            };
            particles.Emit(emit, 1);
        }
    }

    private void UpdateBitePiece(Transform hand, float bodyHeight, float phase, float lift)
    {
        bool visible = hand != null && phase >= 0.24f && phase < BiteParticleStart && lift > 0.02f;
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

            Renderer biteRenderer = bitePiece.GetComponent<Renderer>();
            if (biteRenderer != null)
            {
                biteRenderer.sharedMaterial = GetParticleMaterial();
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", settings.crumbColorA);
                properties.SetColor("_Color", settings.crumbColorA);
                biteRenderer.SetPropertyBlock(properties);
                biteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                biteRenderer.receiveShadows = false;
            }
        }

        float size = Mathf.Max(0.015f, bodyHeight * settings.bitePieceSize);
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
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.maxParticles = 24;
        main.startLifetime = Mathf.Max(0.05f, settings.particleLifetime);
        main.startSpeed = Mathf.Max(0f, settings.particleSpeed);
        float bodyHeight = ResolveBodyHeight();
        main.startSize = Mathf.Max(0.001f, settings.particleSize * bodyHeight);
        main.gravityModifier = settings.particleGravity;
        main.startColor = new ParticleSystem.MinMaxGradient(
            settings.crumbColorA,
            settings.crumbColorB);

        ParticleSystem.EmissionModule emission = eatingParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = eatingParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0f, settings.particleSpawnRadius * bodyHeight);
        shape.radiusThickness = 1f;

        ParticleSystem.VelocityOverLifetimeModule velocity = eatingParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

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
