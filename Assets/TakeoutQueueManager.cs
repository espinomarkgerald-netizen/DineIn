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

    private readonly List<CustomerGroup> queue = new();
    private readonly Dictionary<CustomerGroup, int> slotLookup = new();
    private readonly List<CustomerGroup> leavingGroups = new();

    private CustomerGroup currentFront;

    public CustomerGroup CurrentFront => currentFront;

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
            currentFront = null;

        RefreshQueue();
    }

    [ContextMenu("Release Front")]
    public void ReleaseFrontFromOrderPoint()
    {
        if (currentFront == null)
            return;

        CustomerGroup released = currentFront;
        currentFront = null;

        queue.Remove(released);
        slotLookup.Remove(released);

        if (released != null)
        {
            released.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.None);

            if (exitPoint != null)
            {
                released.MoveToTakeoutPoint(exitPoint.position);

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

    private void RefreshQueue()
    {
        CleanupInvalidReferences();
        slotLookup.Clear();

        if (queue.Count == 0)
        {
            currentFront = null;
            return;
        }

        if (currentFront == null || !queue.Contains(currentFront))
        {
            currentFront = queue[0];

            if (currentFront != null && orderPoint != null)
            {
                currentFront.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.WalkingToOrderPoint);
                currentFront.MoveToTakeoutPoint(orderPoint.position);
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
                int pointIndex = Mathf.Clamp(slot, 0, queuePoints.Length - 1);
                Transform point = queuePoints[pointIndex];

                if (point != null)
                {
                    group.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.WalkingToQueueSlot);
                    group.MoveToTakeoutPoint(point.position);
                }
            }

            slot++;
        }
    }

    private void UpdateFrontArrival()
    {
        if (currentFront == null || orderPoint == null)
            return;

        if (currentFront.HasReachedTakeoutPoint(orderPoint.position, arrivalThreshold))
            currentFront.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.AtOrderPoint);
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

            slotIndex = Mathf.Clamp(slotIndex, 0, queuePoints.Length - 1);
            Transform point = queuePoints[slotIndex];

            if (point == null)
                continue;

            if (group.HasReachedTakeoutPoint(point.position, arrivalThreshold))
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

            if (group.HasReachedTakeoutPoint(exitPoint.position, arrivalThreshold))
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
            currentFront = null;
    }
}