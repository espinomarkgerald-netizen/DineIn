using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Editable, world-space trolley tool used by an autonomous waiter or busser.
/// The trolley stays parked when idle and follows an operator from a point in
/// front of the character only while a batch is being handled.
/// </summary>
[DisallowMultipleComponent]
public sealed class BotTrolleyCarrier : MonoBehaviour
{
    public enum TrolleyState
    {
        ParkedIdle,
        Reserved,
        Acquiring,
        Collecting,
        Transporting,
        Unloading,
        Returning,
        Recovery
    }

    [Header("Authored Trolley")]
    [SerializeField] private EquipmentUpgradeEffect effect;
    [SerializeField, Min(1)] private int capacity = 4;
    [SerializeField, Min(1)] private int minimumBatchSize = 2;
    [Tooltip("Editable presentation root. Keep model scale/rotation changes here, not on the gameplay root.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Editable marker on the trolley handle. Runtime aligns this point to the bot's Trolley Grip Point.")]
    [SerializeField] private Transform holdingPoint;
    [SerializeField] private List<Transform> traySlots = new List<Transform>();
    [SerializeField] private Vector3 trayLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 trayLocalEulerAngles = Vector3.zero;

    [Header("Position In Front Of Bot")]
    [Tooltip("Local-space position relative to the bot while the trolley is being pushed. Positive Z is in front.")]
    [SerializeField] private Vector3 pushOffset = new Vector3(0f, 0f, 1.05f);
    [SerializeField] private Vector3 pushEulerAngles = Vector3.zero;
    [Tooltip("Fine adjustment in the selected bot grip point's local space.")]
    [SerializeField] private Vector3 operatorGripLocalOffset = Vector3.zero;
    [Tooltip("Zero snaps the trolley to the bot. Raise slightly for softer cart movement.")]
    [SerializeField, Min(0f)] private float followPositionSmoothTime;
    [Tooltip("Zero snaps rotation to the bot. Raise for a softer turn response.")]
    [SerializeField, Min(0f)] private float followRotationSpeed;
    [SerializeField] private bool useBotCarryingAnimation = true;

    [Header("Movement Upgrade")]
    [Tooltip("Applied only while a bot is actively pushing this trolley. Employee speed still applies.")]
    [SerializeField, Range(1f, 1.5f)] private float movementSpeedMultiplier = 1.35f;
    [Tooltip("Small acceleration increase helps the trolley reach its boosted speed without changing stopping distance.")]
    [SerializeField, Range(1f, 1.5f)] private float accelerationMultiplier = 1.2f;

    [Header("Parking")]
    [SerializeField, Min(0.35f)] private float parkingApproachDistance = 1.1f;
    [Tooltip("Editable local-space floor position where the bot stands before taking the trolley. Keep the trolley parked beside the counter and move this offset onto open floor space.")]
    [SerializeField] private Vector3 parkingBotApproachOffset = new Vector3(0f, 0f, -0.85f);
    [Tooltip("How far runtime may search for walkable floor around the bot approach position. This makes moved parking points tolerant of nearby counters and NavMesh edges.")]
    [SerializeField, Min(0.25f)] private float parkingNavMeshSampleRadius = 2.5f;
    [SerializeField] private bool matchParkingRotation = true;
    [SerializeField] private Vector3 parkingPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 parkingEulerOffset = Vector3.zero;

    [Header("Editor Preview")]
    [SerializeField, HideInInspector] private int authoringVersion;
    [SerializeField] private bool drawTraySlotGizmos = true;
    [SerializeField] private Color traySlotGizmoColor = new Color(0.15f, 0.75f, 1f, 0.7f);
    [SerializeField] private bool drawHoldingPointGizmo = true;
    [SerializeField] private Color holdingPointGizmoColor = new Color(0.2f, 1f, 0.45f, 0.85f);

    [Header("Runtime Diagnostics")]
    [SerializeField] private TrolleyState currentState = TrolleyState.ParkedIdle;
    [SerializeField, TextArea] private string lastFailureReason;

    private readonly List<FoodTray> trays = new List<FoodTray>();
    private AutonomousStaffBot operatorBot;
    private Transform parkingPoint;
    private Vector3 followVelocity;
    private bool warnedAboutMissingGrip;
    private bool runtimeOwned;

    public EquipmentUpgradeEffect Effect => effect;
    public int Capacity => Mathf.Max(1, capacity);
    public int MinimumBatchSize => Mathf.Clamp(minimumBatchSize, 1, Capacity);
    public float ParkingApproachDistance => Mathf.Max(0.35f, parkingApproachDistance);
    public Vector3 ParkingBotApproachOffset => parkingBotApproachOffset;
    public float ParkingNavMeshSampleRadius => Mathf.Max(0.25f, parkingNavMeshSampleRadius);
    public int Count => trays.Count;
    public bool HasSpace => trays.Count < Capacity;
    public bool HasRenderableVisual => ResolveVisualRenderers().Length > 0;
    public bool IsConfigured => string.IsNullOrEmpty(ConfigurationProblem);
    public bool IsInUse => operatorBot != null;
    public bool IsRuntimeOwned => runtimeOwned;
    public Vector3 ParkingPosition => ResolveParkingPosition();
    public IReadOnlyList<FoodTray> Trays => trays;
    public int AuthoringVersion => authoringVersion;
    public Transform VisualRoot => visualRoot;
    public Transform HoldingPoint => holdingPoint;
    public IReadOnlyList<Transform> TraySlots => traySlots;
    public float MovementSpeedMultiplier => Mathf.Clamp(movementSpeedMultiplier, 1f, 1.5f);
    public float AccelerationMultiplier => Mathf.Clamp(accelerationMultiplier, 1f, 1.5f);
    public TrolleyState CurrentState => currentState;
    public string LastFailureReason => lastFailureReason;
    public string ConfigurationProblem
    {
        get
        {
            if (parkingPoint == null) return "parking point is missing";
            if (visualRoot == null || !HasRenderableVisual) return "renderable VisualPivot is missing";
            if (holdingPoint == null) return "HoldingPoint is missing";
            if (traySlots == null || traySlots.Count == 0) return "tray slots are missing";
            for (int i = 0; i < traySlots.Count; i++)
            {
                if (traySlots[i] == null)
                    return $"TraySlot{i + 1} is missing";
            }
            return string.Empty;
        }
    }

    public void ConfigureRuntime(
        EquipmentUpgradeEffect configuredEffect,
        int configuredCapacity,
        Transform configuredParkingPoint)
    {
        effect = configuredEffect;
        capacity = Mathf.Max(1, configuredCapacity);
        parkingPoint = configuredParkingPoint;
        // Scene instances created from the old prefab can retain a root-scale
        // override. Runtime roots are gameplay anchors; all visual scale lives
        // on VisualPivot so parking and grip alignment remain deterministic.
        transform.localScale = Vector3.one;
        RemoveMissingTrays();

        if (!IsInUse)
            ParkImmediate();
    }

    public void MarkRuntimeOwned()
    {
        runtimeOwned = true;
    }

    /// <summary>Used by the prefab authoring tool. These values remain editable afterwards.</summary>
    public void ConfigureAuthoring(
        EquipmentUpgradeEffect configuredEffect,
        IList<Transform> configuredSlots,
        Transform configuredVisualRoot,
        Transform configuredHoldingPoint,
        int version)
    {
        effect = configuredEffect;
        minimumBatchSize = Mathf.Clamp(2, 1, capacity);
        visualRoot = configuredVisualRoot;
        holdingPoint = configuredHoldingPoint;
        authoringVersion = Mathf.Max(0, version);
        traySlots.Clear();
        if (configuredSlots == null)
            return;

        for (int i = 0; i < configuredSlots.Count; i++)
        {
            Transform slot = configuredSlots[i];
            if (slot != null && !traySlots.Contains(slot))
                traySlots.Add(slot);
        }
    }

    public bool BeginUse(AutonomousStaffBot bot)
    {
        if (bot == null || !IsConfigured || (operatorBot != null && operatorBot != bot))
            return false;

        if (!TryResolveOperatorGripPoint(bot, out _))
        {
            SetFailure($"{bot.name} has no dedicated TrolleyGripPoint");
            return false;
        }

        operatorBot = bot;
        warnedAboutMissingGrip = false;
        followVelocity = Vector3.zero;
        lastFailureReason = string.Empty;
        currentState = TrolleyState.Collecting;
        SetOperatorActiveBenefits(true);
        FollowOperator(true);
        return true;
    }

    public void EndUse(bool returnToParking = true)
    {
        SetOperatorActiveBenefits(false);
        operatorBot = null;
        followVelocity = Vector3.zero;
        if (returnToParking)
            ParkImmediate();
        else
            currentState = TrolleyState.ParkedIdle;
    }

    public bool CanBeOperatedBy(AutonomousStaffBot bot, out string reason)
    {
        if (bot == null)
        {
            reason = "assigned bot is missing";
            return false;
        }

        if (!IsConfigured)
        {
            reason = ConfigurationProblem;
            return false;
        }

        if (!TryResolveOperatorGripPoint(bot, out _))
        {
            reason = $"{bot.name} has no dedicated TrolleyGripPoint";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void SetReserved() => currentState = TrolleyState.Reserved;
    public void SetAcquiring() => currentState = TrolleyState.Acquiring;
    public void SetTransporting() => currentState = TrolleyState.Transporting;
    public void SetUnloading() => currentState = TrolleyState.Unloading;
    public void SetReturning() => currentState = TrolleyState.Returning;

    public void SetFailure(string reason)
    {
        lastFailureReason = string.IsNullOrWhiteSpace(reason) ? "unknown trolley failure" : reason;
        currentState = TrolleyState.Recovery;
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
            EndUse();
        gameObject.SetActive(visible);
        if (!visible)
            return;

        SetVisualRenderersEnabled(true);
        if (!IsInUse)
            ParkImmediate();
    }

    public bool TryAttach(FoodTray tray)
    {
        if (tray == null || !HasSpace || trays.Contains(tray) || traySlots.Count == 0)
            return false;

        Transform slot = GetSlot(trays.Count);
        if (slot == null)
            return false;

        trays.Add(tray);
        WaiterHands.AttachKeepingWorldScale(
            tray.transform,
            slot,
            trayLocalPosition,
            Quaternion.Euler(trayLocalEulerAngles));
        WaiterHands.SetAllColliders(tray.gameObject, false);
        currentState = TrolleyState.Collecting;
        return true;
    }

    public bool Contains(FoodTray tray) => tray != null && trays.Contains(tray);

    public bool TryDetach(FoodTray tray, Transform destination)
    {
        if (tray == null || destination == null || !trays.Remove(tray))
            return false;

        WaiterHands.AttachKeepingWorldScale(tray.transform, destination, Vector3.zero, Quaternion.identity);
        WaiterHands.SetAllColliders(tray.gameObject, true);
        CompactSlots();
        return true;
    }

    public bool TryReleaseForRetry(FoodTray tray, Vector3 worldPosition)
    {
        if (tray == null || !trays.Remove(tray))
            return false;

        FoodTrayInteractable interactable = tray.GetComponent<FoodTrayInteractable>();
        interactable?.RestoreAfterStaffPickup();
        interactable?.SetClaimedByStaff(false);
        if (operatorBot != null)
            RestaurantTaskClaim.ReleaseBot(tray, operatorBot);
        tray.transform.SetParent(null, true);
        tray.transform.position = worldPosition;
        WaiterHands.SetAllColliders(tray.gameObject, true);
        CompactSlots();
        return true;
    }

    public void Dispose(FoodTray tray)
    {
        if (tray == null)
            return;

        trays.Remove(tray);
        RestaurantTaskClaim.Complete(tray);
        Destroy(tray.gameObject);
        CompactSlots();
    }

    public void ReleaseAllForRetry(Vector3 worldPosition)
    {
        for (int i = trays.Count - 1; i >= 0; i--)
        {
            FoodTray tray = trays[i];
            if (tray != null)
                TryReleaseForRetry(tray, worldPosition);
        }
    }

    public void ParkImmediate()
    {
        if (parkingPoint == null)
            return;

        transform.position = ResolveParkingPosition();
        transform.rotation = ResolveParkingRotation();
        currentState = TrolleyState.ParkedIdle;
    }

    /// <summary>
    /// Finds walkable floor near the trolley without requiring the visual parking
    /// transform itself to sit on the NavMesh. This keeps authored placement and
    /// bot navigation independent: the trolley may be beside a counter while the
    /// bot approaches from the nearest reachable side.
    /// </summary>
    public bool TryGetParkingApproachPosition(
        Vector3 operatorPosition,
        out Vector3 approachPosition)
    {
        approachPosition = ResolveParkingPosition();
        if (parkingPoint == null)
            return false;

        Quaternion parkingRotation = ResolveParkingRotation();
        Vector3 parkingPosition = ResolveParkingPosition();
        Vector3 authoredApproach = parkingPosition + parkingRotation * parkingBotApproachOffset;
        float authoredRingDistance = Mathf.Max(
            ParkingApproachDistance,
            new Vector2(parkingBotApproachOffset.x, parkingBotApproachOffset.z).magnitude);
        float localSampleRadius = Mathf.Min(0.75f, ParkingNavMeshSampleRadius);

        NavMeshHit operatorHit;
        bool hasOperatorStart = NavMesh.SamplePosition(
            operatorPosition,
            out operatorHit,
            1.5f,
            NavMesh.AllAreas);
        NavMeshPath path = hasOperatorStart ? new NavMeshPath() : null;
        bool found = false;
        float bestScore = float.PositiveInfinity;

        // A presentation marker is allowed beside a counter and may have a Y
        // value below the baked floor. Search several rings on the operator's
        // actual NavMesh plane instead of assuming the trolley marker itself is
        // a valid navigation destination. This is what keeps a user-moved
        // parking point editable without silently disabling trolley gameplay.
        float[] ringDistances =
        {
            authoredRingDistance,
            Mathf.Max(authoredRingDistance, 1.5f),
            Mathf.Max(authoredRingDistance, ParkingNavMeshSampleRadius),
            Mathf.Max(authoredRingDistance, ParkingNavMeshSampleRadius * 1.6f)
        };

        for (int ring = -1; ring < ringDistances.Length; ring++)
        {
            int directionCount = ring < 0 ? 1 : 16;
            for (int direction = 0; direction < directionCount; direction++)
            {
                Vector3 candidate = ring < 0
                    ? authoredApproach
                    : parkingPosition + parkingRotation *
                      (Quaternion.Euler(0f, direction * (360f / directionCount), 0f) *
                       Vector3.back * ringDistances[ring]);
                if (hasOperatorStart)
                    candidate.y = operatorHit.position.y;

                if (!NavMesh.SamplePosition(
                        candidate,
                        out NavMeshHit candidateHit,
                        localSampleRadius,
                        NavMesh.AllAreas))
                {
                    continue;
                }

                if (hasOperatorStart &&
                    (!NavMesh.CalculatePath(
                        operatorHit.position,
                        candidateHit.position,
                        NavMesh.AllAreas,
                        path) ||
                     path.status != NavMeshPathStatus.PathComplete))
                {
                    continue;
                }

                float score = (candidateHit.position - authoredApproach).sqrMagnitude;
                if (hasOperatorStart)
                    score += CalculatePathLength(path);
                if (score >= bestScore)
                    continue;

                bestScore = score;
                approachPosition = candidateHit.position;
                found = true;
            }
        }

        if (found)
            return true;

        // Final wider search covers an approach marker placed slightly inside a
        // counter. Movement still validates the route; callers fall back safely
        // if congestion or disconnected NavMesh areas make it unusable.
        Vector3 fallbackApproach = authoredApproach;
        if (hasOperatorStart)
            fallbackApproach.y = operatorHit.position.y;
        if (!NavMesh.SamplePosition(
                fallbackApproach,
                out NavMeshHit fallbackHit,
                ParkingNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        if (hasOperatorStart &&
            (!NavMesh.CalculatePath(
                operatorHit.position,
                fallbackHit.position,
                NavMesh.AllAreas,
                path) ||
             path.status != NavMeshPathStatus.PathComplete))
        {
            return false;
        }

        approachPosition = fallbackHit.position;
        return true;
    }

    private void LateUpdate()
    {
        if (operatorBot == null)
            return;

        if (!operatorBot.isActiveAndEnabled)
        {
            ReleaseAllForRetry(transform.position);
            EndUse(true);
            return;
        }

        FollowOperator(false);
    }

    private void FollowOperator(bool immediate)
    {
        if (operatorBot == null)
            return;

        Transform bot = operatorBot.transform;
        Quaternion targetRotation = bot.rotation * Quaternion.Euler(pushEulerAngles);
        TryResolveOperatorGripPoint(operatorBot, out Transform gripPoint);
        Vector3 targetPosition = bot.TransformPoint(pushOffset);

        if (holdingPoint != null && gripPoint != null)
        {
            Quaternion holdingLocalRotation = Quaternion.Inverse(transform.rotation) * holdingPoint.rotation;
            Quaternion desiredHoldingRotation = gripPoint.rotation * Quaternion.Euler(pushEulerAngles);
            targetRotation = desiredHoldingRotation * Quaternion.Inverse(holdingLocalRotation);
            Vector3 holdingLocalPosition = transform.InverseTransformPoint(holdingPoint.position);
            Vector3 scaledHoldingOffset = Vector3.Scale(holdingLocalPosition, transform.lossyScale);
            Vector3 targetGripPosition = gripPoint.TransformPoint(operatorGripLocalOffset);
            targetPosition = targetGripPosition - targetRotation * scaledHoldingOffset;
        }
        else if (!warnedAboutMissingGrip)
        {
            warnedAboutMissingGrip = true;
            Debug.LogWarning(
                $"[BotTrolleyCarrier] {name} is using Push Offset because " +
                $"{(holdingPoint == null ? "HoldingPoint" : "the bot Trolley Grip Point")} is missing.",
                this);
        }

        if (immediate || followPositionSmoothTime <= 0f)
        {
            transform.position = targetPosition;
            followVelocity = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref followVelocity,
                followPositionSmoothTime,
                Mathf.Infinity,
                Time.deltaTime);
        }

        transform.rotation = immediate || followRotationSpeed <= 0f
            ? targetRotation
            : Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-followRotationSpeed * Time.deltaTime));
    }

    private static bool TryResolveOperatorGripPoint(AutonomousStaffBot bot, out Transform gripPoint)
    {
        gripPoint = null;
        if (bot == null)
            return false;

        WaiterHands waiterHands = bot.GetComponent<WaiterHands>();
        if (waiterHands != null)
            return waiterHands.TryGetTrolleyGripPoint(out gripPoint);

        BusserHands busserHands = bot.GetComponent<BusserHands>();
        return busserHands != null && busserHands.TryGetTrolleyGripPoint(out gripPoint);
    }

    private Vector3 ResolveParkingPosition()
    {
        return parkingPoint != null
            ? parkingPoint.TransformPoint(parkingPositionOffset)
            : transform.position;
    }

    private Quaternion ResolveParkingRotation()
    {
        Quaternion baseRotation = matchParkingRotation && parkingPoint != null
            ? parkingPoint.rotation
            : Quaternion.identity;
        return baseRotation * Quaternion.Euler(parkingEulerOffset);
    }

    private static float CalculatePathLength(NavMeshPath path)
    {
        if (path == null || path.corners == null || path.corners.Length < 2)
            return 0f;

        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return length;
    }

    private void SetOperatorActiveBenefits(bool active)
    {
        if (operatorBot == null)
            return;

        if (useBotCarryingAnimation)
            operatorBot.SetUsingTrolley(active);

        if (active)
        {
            operatorBot.SetTrolleyMovementModifier(
                MovementSpeedMultiplier,
                AccelerationMultiplier);
        }
        else
        {
            operatorBot.ClearTrolleyMovementModifier();
        }
    }

    private Renderer[] ResolveVisualRenderers()
    {
        Transform root = visualRoot != null ? visualRoot : transform;
        return root.GetComponentsInChildren<Renderer>(true);
    }

    private void SetVisualRenderersEnabled(bool enabled)
    {
        Renderer[] renderers = ResolveVisualRenderers();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    private void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);
        minimumBatchSize = Mathf.Clamp(minimumBatchSize, 1, capacity);
        parkingApproachDistance = Mathf.Max(0.35f, parkingApproachDistance);
        parkingNavMeshSampleRadius = Mathf.Max(0.25f, parkingNavMeshSampleRadius);
        followPositionSmoothTime = Mathf.Max(0f, followPositionSmoothTime);
        followRotationSpeed = Mathf.Max(0f, followRotationSpeed);
        movementSpeedMultiplier = Mathf.Clamp(movementSpeedMultiplier, 1f, 1.5f);
        accelerationMultiplier = Mathf.Clamp(accelerationMultiplier, 1f, 1.5f);

        if (visualRoot == null)
        {
            Transform candidate = transform.Find("VisualPivot");
            visualRoot = candidate != null ? candidate : transform.Find("TrolleyModel");
        }

        if (holdingPoint == null)
            holdingPoint = transform.Find("HoldingPoint");
    }

    private Transform GetSlot(int index)
    {
        return index >= 0 && index < traySlots.Count ? traySlots[index] : null;
    }

    private void RemoveMissingTrays()
    {
        for (int i = trays.Count - 1; i >= 0; i--)
        {
            if (trays[i] == null)
                trays.RemoveAt(i);
        }
    }

    private void CompactSlots()
    {
        RemoveMissingTrays();
        for (int i = 0; i < trays.Count; i++)
        {
            FoodTray tray = trays[i];
            Transform slot = GetSlot(i);
            if (tray == null || slot == null)
                continue;

            WaiterHands.AttachKeepingWorldScale(
                tray.transform,
                slot,
                trayLocalPosition,
                Quaternion.Euler(trayLocalEulerAngles));
        }
    }

    private void OnDisable()
    {
        if (operatorBot != null)
            SetOperatorActiveBenefits(false);
        operatorBot = null;
        followVelocity = Vector3.zero;
        if (currentState != TrolleyState.Recovery)
            currentState = TrolleyState.ParkedIdle;
    }

    private void OnDestroy()
    {
        if (operatorBot != null)
            SetOperatorActiveBenefits(false);

        for (int i = 0; i < trays.Count; i++)
        {
            FoodTray tray = trays[i];
            if (tray != null)
                RestaurantTaskClaim.Complete(tray);
        }
        trays.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawTraySlotGizmos && traySlots != null)
        {
            Gizmos.color = traySlotGizmoColor;
            for (int i = 0; i < traySlots.Count; i++)
            {
                Transform slot = traySlots[i];
                if (slot == null)
                    continue;
                Gizmos.matrix = slot.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.5f, 0.05f, 0.36f));
            }
        }

        if (drawHoldingPointGizmo && holdingPoint != null)
        {
            Gizmos.matrix = holdingPoint.localToWorldMatrix;
            Gizmos.color = holdingPointGizmoColor;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.35f);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
