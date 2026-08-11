using System.Collections.Generic;
using UnityEngine;

public class TakeoutQueueManager : MonoBehaviour
{
    [Header("Points")]
    [SerializeField] private Transform[] queuePoints;
    [SerializeField] private Transform orderPoint;
    [SerializeField] private Transform exitPoint;

    [Header("Settings")]
    [SerializeField] private float arrivalThreshold = 0.75f;
    [SerializeField, Min(0.5f)] private float memberSideSpacing = 1.1f;
    [SerializeField, Min(0.5f)] private float memberRowSpacing = 1f;
    [SerializeField, Min(1f)] private float overflowGroupSpacing = 2.25f;
    [SerializeField, Min(5f)] private float maxFrontTravelSeconds = 20f;
    [SerializeField, Range(0, 2)] private int maxFrontTravelRetries = 1;

    private readonly List<CustomerGroup> queue = new();
    private readonly Dictionary<CustomerGroup, int> slotLookup = new();
    private readonly List<CustomerGroup> leavingGroups = new();

    private CustomerGroup currentFront;
    private float currentFrontMoveStartedAt = -1f;
    private int currentFrontTravelRetries;

    public CustomerGroup CurrentFront => currentFront;

    public static TakeoutQueueManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        CleanupInvalidReferences();
        UpdateFrontArrival();
        UpdateQueuedArrival();
        UpdateLeavingGroups();
    }

    public void Enqueue(CustomerGroup group)
    {
        if (group == null || !group.IsTakeout)
            return;

        if (queue.Contains(group) || leavingGroups.Contains(group))
            return;

        queue.Add(group);
        RefreshQueue();
    }

    public void Remove(CustomerGroup group)
    {
        if (group == null)
            return;

        queue.Remove(group);
        slotLookup.Remove(group);
        leavingGroups.Remove(group);

        if (currentFront == group)
        {
            currentFront = null;
            currentFrontMoveStartedAt = -1f;
            currentFrontTravelRetries = 0;
        }

        RefreshQueue();
    }

    [ContextMenu("Release Front")]
    public void ReleaseFrontFromOrderPoint()
    {
        if (currentFront == null)
            return;

        CustomerGroup released = currentFront;
        currentFront = null;
        currentFrontMoveStartedAt = -1f;
        currentFrontTravelRetries = 0;

        queue.Remove(released);
        slotLookup.Remove(released);

        if (released != null)
        {
            released.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.None);

            if (exitPoint != null)
            {
                MoveGroupToTransform(released, exitPoint);

                if (!leavingGroups.Contains(released))
                    leavingGroups.Add(released);
            }
            else
            {
                Destroy(released.gameObject);
            }
        }

        RefreshQueue();
    }

    /// <summary>
    /// Releases any takeout group — whether it is the current front or still
    /// waiting in the queue — and drives it to the exit point so it despawns.
    /// Used by the unhappy/angry timeout paths in CustomerGroup.StartLeaving().
    /// </summary>
    public void ReleaseGroup(CustomerGroup group)
    {
        if (group == null)
            return;

        if (group == currentFront)
        {
            ReleaseFrontFromOrderPoint();
            return;
        }

        // Group is queued but not yet at the order point.
        queue.Remove(group);
        slotLookup.Remove(group);

        group.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.None);

        if (exitPoint != null)
        {
            MoveGroupToTransform(group, exitPoint);

            if (!leavingGroups.Contains(group))
                leavingGroups.Add(group);
        }
        else
        {
            Destroy(group.gameObject);
        }

        RefreshQueue();
    }

    private void RefreshQueue()
    {
        CleanupInvalidReferences();
        slotLookup.Clear();

        if (queue.Count == 0)
        {
            currentFront = null;
            currentFrontMoveStartedAt = -1f;
            currentFrontTravelRetries = 0;
            return;
        }

        if (currentFront == null || !queue.Contains(currentFront))
        {
            currentFront = queue[0];

            if (currentFront != null && orderPoint != null)
            {
                currentFrontMoveStartedAt = Time.time;
                currentFrontTravelRetries = 0;
                currentFront.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.WalkingToOrderPoint);
                MoveGroupToTransform(currentFront, orderPoint);
                Debug.Log($"[TakeoutQueue] {currentFront.name} is moving to the order point.", currentFront);
            }
        }

        int slot = 0;

        for (int i = 0; i < queue.Count; i++)
        {
            CustomerGroup group = queue[i];

            if (group == null || group == currentFront)
                continue;

            slotLookup[group] = slot;

            if (queuePoints != null && queuePoints.Length > 0)
            {
                if (TryGetQueueSlotPose(slot, out Vector3 position, out Vector3 forward))
                {
                    group.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.WalkingToQueueSlot);
                    group.MoveToTakeoutPoint(position, forward, memberSideSpacing, memberRowSpacing);
                }
            }

            slot++;
        }
    }

    private void UpdateFrontArrival()
    {
        if (currentFront == null)
            return;

        if (orderPoint == null)
        {
            CustomerGroup failedGroup = currentFront;
            failedGroup.FailTakeoutService("Takeout order point is missing.");
            return;
        }

        if (currentFront.CurrentTakeoutQueueState == CustomerGroup.TakeoutQueueState.AtOrderPoint)
            return;

        if (HasGroupReachedTransform(currentFront, orderPoint))
        {
            currentFront.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.AtOrderPoint);
            currentFrontMoveStartedAt = -1f;
            Debug.Log($"[TakeoutQueue] {currentFront.name} reached the order point.", currentFront);
            return;
        }

        if (currentFrontMoveStartedAt >= 0f &&
            Time.time - currentFrontMoveStartedAt >= maxFrontTravelSeconds)
        {
            if (currentFrontTravelRetries < maxFrontTravelRetries)
            {
                currentFrontTravelRetries++;
                currentFrontMoveStartedAt = Time.time;
                MoveGroupToTransform(currentFront, orderPoint);
                Debug.LogWarning(
                    $"[TakeoutQueue] Retrying {currentFront.name}'s route to the order point " +
                    $"({currentFrontTravelRetries}/{maxFrontTravelRetries}).",
                    currentFront);
                return;
            }

            CustomerGroup failedGroup = currentFront;
            currentFrontMoveStartedAt = -1f;
            currentFrontTravelRetries = 0;
            failedGroup.FailTakeoutTravel(
                $"Could not reach the takeout order point within {maxFrontTravelSeconds:0.#} seconds.");
        }
    }

    private void UpdateQueuedArrival()
    {
        if (queuePoints == null || queuePoints.Length == 0)
            return;

        foreach (var pair in slotLookup)
        {
            CustomerGroup group = pair.Key;
            int slotIndex = pair.Value;

            if (group == null)
                continue;

            if (!TryGetQueueSlotPose(slotIndex, out Vector3 position, out Vector3 forward))
                continue;

            if (group.HasReachedTakeoutPoint(
                    position,
                    forward,
                    memberSideSpacing,
                    memberRowSpacing,
                    arrivalThreshold))
                group.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.WaitingInQueue);
        }
    }

    private void UpdateLeavingGroups()
    {
        if (exitPoint == null || leavingGroups.Count == 0)
            return;

        for (int i = leavingGroups.Count - 1; i >= 0; i--)
        {
            CustomerGroup group = leavingGroups[i];

            if (group == null)
            {
                leavingGroups.RemoveAt(i);
                continue;
            }

            if (HasGroupReachedTransform(group, exitPoint))
            {
                leavingGroups.RemoveAt(i);
                Destroy(group.gameObject);
            }
        }
    }

    private void CleanupInvalidReferences()
    {
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (queue[i] == null)
                queue.RemoveAt(i);
        }

        for (int i = leavingGroups.Count - 1; i >= 0; i--)
        {
            if (leavingGroups[i] == null)
                leavingGroups.RemoveAt(i);
        }

        if (currentFront != null && !queue.Contains(currentFront))
        {
            currentFront = null;
            currentFrontMoveStartedAt = -1f;
            currentFrontTravelRetries = 0;
        }
    }

    private void MoveGroupToTransform(CustomerGroup group, Transform target)
    {
        if (group == null || target == null)
            return;

        group.MoveToTakeoutPoint(
            target.position,
            target.forward,
            memberSideSpacing,
            memberRowSpacing);
    }

    private bool HasGroupReachedTransform(CustomerGroup group, Transform target)
    {
        return group != null && target != null && group.HasReachedTakeoutPoint(
            target.position,
            target.forward,
            memberSideSpacing,
            memberRowSpacing,
            arrivalThreshold);
    }

    private bool TryGetQueueSlotPose(int slotIndex, out Vector3 position, out Vector3 forward)
    {
        position = Vector3.zero;
        forward = Vector3.forward;

        if (queuePoints == null || queuePoints.Length == 0)
            return false;

        int pointIndex = Mathf.Clamp(slotIndex, 0, queuePoints.Length - 1);
        Transform point = queuePoints[pointIndex];
        if (point == null)
            return false;

        position = point.position;
        forward = point.forward;

        int overflow = Mathf.Max(0, slotIndex - (queuePoints.Length - 1));
        if (overflow == 0)
            return true;

        Vector3 backDirection = -point.forward;
        if (queuePoints.Length > 1)
        {
            Transform previous = queuePoints[queuePoints.Length - 2];
            if (previous != null)
                backDirection = point.position - previous.position;
        }

        backDirection.y = 0f;
        if (backDirection.sqrMagnitude < 0.0001f)
            backDirection = -point.forward;

        position += backDirection.normalized * overflow * overflowGroupSpacing;
        return true;
    }
}
