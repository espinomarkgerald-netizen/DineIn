using System.Collections.Generic;
using UnityEngine;

public class LobbyLineManager : MonoBehaviour
{
    [Header("Line points (front to back).")]
    public Transform[] linePoints = new Transform[4];

    [Header("Member formation inside each slot")]
    public float sideSpacing = 0.6f;
    public float backSpacing = 0.6f;

    private CustomerGroup[] slots;

    private void Awake()
    {
        if (linePoints == null || linePoints.Length == 0)
            linePoints = new Transform[4];

        slots = new CustomerGroup[linePoints.Length];
    }

    public bool TryJoinLine(CustomerGroup group)
    {
        if (group == null)
            return false;

        CleanupSlots();

        int slotIndex = GetNextBackSlot();
        if (slotIndex < 0 || slotIndex >= slots.Length)
        {
            Debug.Log("Lobby line is full.");
            return false;
        }

        UnsubscribeGroup(group);
        SubscribeGroup(group);

        slots[slotIndex] = group;
        MoveGroupToSlot(group, slotIndex);
        return true;
    }

    private void SubscribeGroup(CustomerGroup group)
    {
        if (group == null)
            return;

        group.OnGroupAssignedToBooth -= HandleGroupAssignedToBooth;
        group.OnGroupAssignedToBooth += HandleGroupAssignedToBooth;

        group.OnGroupSeated -= HandleGroupSeated;
        group.OnGroupSeated += HandleGroupSeated;

        group.OnGroupLeftLine -= HandleGroupLeftLine;
        group.OnGroupLeftLine += HandleGroupLeftLine;
    }

    private void UnsubscribeGroup(CustomerGroup group)
    {
        if (group == null)
            return;

        group.OnGroupAssignedToBooth -= HandleGroupAssignedToBooth;
        group.OnGroupSeated -= HandleGroupSeated;
        group.OnGroupLeftLine -= HandleGroupLeftLine;
    }

    private void HandleGroupAssignedToBooth(CustomerGroup group)
    {
        RemoveGroupFromSlots(group, false);
        RebuildLine();
    }

    private void HandleGroupSeated(CustomerGroup group)
    {
        RemoveGroupFromSlots(group, true);
        RebuildLine();
    }

    private void HandleGroupLeftLine(CustomerGroup group)
    {
        RemoveGroupFromSlots(group, true);
        RebuildLine();
    }

    private void RemoveGroupFromSlots(CustomerGroup group, bool unsubscribe)
    {
        if (group == null || slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == group)
                slots[i] = null;
        }

        if (unsubscribe)
            UnsubscribeGroup(group);
    }

    private void RebuildLine()
    {
        CleanupSlots();

        List<CustomerGroup> waiting = new List<CustomerGroup>(slots.Length);

        for (int i = 0; i < slots.Length; i++)
        {
            CustomerGroup g = slots[i];
            if (g == null)
                continue;

            if (ShouldRemoveFromLine(g))
                continue;

            waiting.Add(g);
        }

        for (int i = 0; i < slots.Length; i++)
            slots[i] = null;

        for (int i = 0; i < waiting.Count && i < slots.Length; i++)
        {
            slots[i] = waiting[i];
            MoveGroupToSlot(waiting[i], i);
        }
    }

    private void CleanupSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            CustomerGroup g = slots[i];

            if (g == null)
            {
                slots[i] = null;
                continue;
            }

            if (g.gameObject == null)
            {
                UnsubscribeGroup(g);
                slots[i] = null;
                continue;
            }

            if (ShouldRemoveFromLine(g))
            {
                UnsubscribeGroup(g);
                slots[i] = null;
            }
        }
    }

    private bool ShouldRemoveFromLine(CustomerGroup group)
    {
        if (group == null)
            return true;

        switch (group.state)
        {
            case CustomerGroup.GroupState.WalkingToBooth:
            case CustomerGroup.GroupState.Seated:
            case CustomerGroup.GroupState.Leaving:
            case CustomerGroup.GroupState.AngryLeft:
            case CustomerGroup.GroupState.UnhappyLeft:
                return true;
        }

        return false;
    }

    private int GetNextBackSlot()
    {
        if (slots == null || slots.Length == 0)
            return -1;

        for (int i = slots.Length - 1; i >= 0; i--)
        {
            if (slots[i] != null)
                return i + 1;
        }

        return 0;
    }

    private int FindSlot(CustomerGroup group)
    {
        if (group == null || slots == null)
            return -1;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == group)
                return i;
        }

        return -1;
    }

    private void MoveGroupToSlot(CustomerGroup group, int slotIndex)
    {
        if (group == null)
            return;

        if (slotIndex < 0 || linePoints == null || slotIndex >= linePoints.Length)
            return;

        Transform p = linePoints[slotIndex];
        if (p == null)
            return;

        Vector3 basePos = p.position;
        Vector3 forward = p.forward.normalized;
        Vector3 right = p.right.normalized;

        for (int m = 0; m < group.members.Count; m++)
        {
            CustomerAgent member = group.members[m];
            if (member == null || member.IsSeated)
                continue;

            Vector3 offset;

            if (group.members.Count == 1)
            {
                offset = Vector3.zero;
            }
            else if (group.members.Count == 2)
            {
                offset = m == 0
                    ? (-right * sideSpacing * 0.5f)
                    : (right * sideSpacing * 0.5f);
            }
            else
            {
                int row = m / 2;
                int col = m % 2;

                float x = col == 0 ? -sideSpacing * 0.5f : sideSpacing * 0.5f;
                float z = -row * backSpacing;

                offset = right * x + forward * z;
            }

            member.WalkTo(basePos + offset);
        }

        group.transform.rotation = Quaternion.LookRotation(p.forward, Vector3.up);
        group.SetLineSlotTarget(basePos);
    }

    public bool IsGroupInLine(CustomerGroup group)
    {
        if (group == null || slots == null)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == group)
                return true;
        }

        return false;
    }

    public bool IsFrontOfLine(CustomerGroup group)
    {
        CleanupSlots();

        if (group == null || slots == null || slots.Length == 0)
            return false;

        return slots[0] == group;
    }

    public CustomerGroup GetFrontOfLine()
    {
        CleanupSlots();

        if (slots == null || slots.Length == 0)
            return null;

        return slots[0];
    }

    public void ForceRebuildLine()
    {
        RebuildLine();
    }

    private void OnDestroy()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
            UnsubscribeGroup(slots[i]);
    }
}