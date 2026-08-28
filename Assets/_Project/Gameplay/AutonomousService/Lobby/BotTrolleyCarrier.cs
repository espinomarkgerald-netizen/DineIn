using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editable, world-space trolley tool used by an autonomous waiter or busser.
/// The trolley stays parked when idle and follows an operator from a point in
/// front of the character only while a batch is being handled.
/// </summary>
[DisallowMultipleComponent]
public sealed class BotTrolleyCarrier : MonoBehaviour
{
    [Header("Authored Trolley")]
    [SerializeField] private EquipmentUpgradeEffect effect;
    [SerializeField, Min(1)] private int capacity = 4;
    [SerializeField, Min(1)] private int minimumBatchSize = 2;
    [SerializeField] private List<Transform> traySlots = new List<Transform>();
    [SerializeField] private Vector3 trayLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 trayLocalEulerAngles = Vector3.zero;

    [Header("Position In Front Of Bot")]
    [Tooltip("Local-space position relative to the bot while the trolley is being pushed. Positive Z is in front.")]
    [SerializeField] private Vector3 pushOffset = new Vector3(0f, 0f, 1.05f);
    [SerializeField] private Vector3 pushEulerAngles = Vector3.zero;
    [SerializeField] private bool useBotCarryingAnimation = true;

    [Header("Parking")]
    [SerializeField, Min(0.35f)] private float parkingApproachDistance = 1.1f;
    [SerializeField] private bool matchParkingRotation = true;
    [SerializeField] private Vector3 parkingPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 parkingEulerOffset = Vector3.zero;

    [Header("Editor Preview")]
    [SerializeField, HideInInspector] private int authoringVersion;
    [SerializeField] private bool drawTraySlotGizmos = true;
    [SerializeField] private Color traySlotGizmoColor = new Color(0.15f, 0.75f, 1f, 0.7f);

    private readonly List<FoodTray> trays = new List<FoodTray>();
    private AutonomousStaffBot operatorBot;
    private Transform parkingPoint;

    public EquipmentUpgradeEffect Effect => effect;
    public int Capacity => Mathf.Max(1, capacity);
    public int MinimumBatchSize => Mathf.Clamp(minimumBatchSize, 1, Capacity);
    public float ParkingApproachDistance => Mathf.Max(0.35f, parkingApproachDistance);
    public int Count => trays.Count;
    public bool HasSpace => trays.Count < Capacity;
    public bool IsConfigured => parkingPoint != null && traySlots.Count > 0;
    public bool IsInUse => operatorBot != null;
    public Vector3 ParkingPosition => ResolveParkingPosition();
    public IReadOnlyList<FoodTray> Trays => trays;
    public int AuthoringVersion => authoringVersion;

    public void ConfigureRuntime(
        EquipmentUpgradeEffect configuredEffect,
        int configuredCapacity,
        Transform configuredParkingPoint)
    {
        effect = configuredEffect;
        capacity = Mathf.Max(1, configuredCapacity);
        parkingPoint = configuredParkingPoint;
        RemoveMissingTrays();

        if (!IsInUse)
            ParkImmediate();
    }

    /// <summary>Used by the prefab authoring tool. These values remain editable afterwards.</summary>
    public void ConfigureAuthoring(
        EquipmentUpgradeEffect configuredEffect,
        IList<Transform> configuredSlots,
        int version)
    {
        effect = configuredEffect;
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

        operatorBot = bot;
        SetOperatorAnimation(true);
        FollowOperatorImmediate();
        return true;
    }

    public void EndUse(bool returnToParking = true)
    {
        SetOperatorAnimation(false);
        operatorBot = null;
        if (returnToParking)
            ParkImmediate();
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
            EndUse();
        gameObject.SetActive(visible);
        if (visible && !IsInUse)
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

        FollowOperatorImmediate();
    }

    private void FollowOperatorImmediate()
    {
        if (operatorBot == null)
            return;

        Transform bot = operatorBot.transform;
        transform.position = bot.TransformPoint(pushOffset);
        transform.rotation = bot.rotation * Quaternion.Euler(pushEulerAngles);
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

    private void SetOperatorAnimation(bool active)
    {
        if (operatorBot != null && useBotCarryingAnimation)
            operatorBot.SetUsingTrolley(active);
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
            SetOperatorAnimation(false);
        operatorBot = null;
    }

    private void OnDestroy()
    {
        if (operatorBot != null)
            SetOperatorAnimation(false);

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
        if (!drawTraySlotGizmos || traySlots == null)
            return;

        Gizmos.color = traySlotGizmoColor;
        for (int i = 0; i < traySlots.Count; i++)
        {
            Transform slot = traySlots[i];
            if (slot == null)
                continue;
            Gizmos.matrix = slot.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.5f, 0.05f, 0.36f));
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
