using System.Collections.Generic;
using UnityEngine;

public class LobbyLineManager : MonoBehaviour
{
    [Header("Line points (front to back). Size must be 4.")]
    public Transform[] linePoints = new Transform[4];

    [Header("Member formation inside each slot")]
    public float sideSpacing = 0.6f;
    public float backSpacing = 0.6f;

    // slot index -> group currently occupying that slot (waiting in line only)
    private CustomerGroup[] slots;

    private void Awake()
    {
        slots = new CustomerGroup[linePoints.Length];
    }

    // ================================
    // PUBLIC API
    // ================================

    public bool TryJoinLine(CustomerGroup group)
    {
        if (group == null) return false;

        CleanupSlots();

        int slotIndex = GetNextBackSlot();
        if (slotIndex >= slots.Length)
        {
            Debug.Log("Lobby line is full.");
            return false;
        }

        // track them in the line
        slots[slotIndex] = group;

        // subscribe to events
        group.OnGroupAssignedToBooth -= HandleGroupAssignedToBooth;
        group.OnGroupAssignedToBooth += HandleGroupAssignedToBooth;

        group.OnGroupSeated -= HandleGroupSeated;
        group.OnGroupSeated += HandleGroupSeated;

        // put them in position
        MoveGroupToSlot(group, slotIndex);
        return true;
    }

    // ================================
    // EVENT HANDLERS
    // ================================

    // Player assigned this group -> remove from the line tracking so it won't get moved to other line points
    private void HandleGroupAssignedToBooth(CustomerGroup group)
    {
        int idx = FindSlot(group);
        if (idx != -1)
            slots[idx] = null;

        // IMPORTANT: compress the line to remove gaps
        RebuildLine();
    }

    // A group finished seating -> they are no longer in line, compress remaining groups forward
    private void HandleGroupSeated(CustomerGroup group)
    {
        int idx = FindSlot(group);
        if (idx != -1)
            slots[idx] = null;

        group.OnGroupAssignedToBooth -= HandleGroupAssignedToBooth;
        group.OnGroupSeated -= HandleGroupSeated;

        RebuildLine();
    }

    // ================================
    // CORE LOGIC
    // ================================

    private void RebuildLine()
    {
        CleanupSlots();

        // collect only valid "waiting-in-line" groups in order (front->back)
        List<CustomerGroup> waiting = new List<CustomerGroup>(slots.Length);

        for (int i = 0; i < slots.Length; i++)
        {
            var g = slots[i];
            if (g == null) continue;

            // If they are already going to booth or seated, they should not be in the lobby line.
            if (g.state == CustomerGroup.GroupState.WalkingToBooth ||
                g.state == CustomerGroup.GroupState.Seated)
                continue;

            waiting.Add(g);
        }

        // clear all slots then repack from front
        for (int i = 0; i < slots.Length; i++)
            slots[i] = null;

        for (int i = 0; i < waiting.Count && i < slots.Length; i++)
        {
            slots[i] = waiting[i];
            MoveGroupToSlot(slots[i], i);
        }
    }

    private void CleanupSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var g = slots[i];
            if (g == null)
            {
                slots[i] = null;
                continue;
            }

            // destroyed object
            if (g.gameObject == null)
            {
                slots[i] = null;
                continue;
            }

            // don't let assigned/seated groups occupy a line slot
            if (g.state == CustomerGroup.GroupState.WalkingToBooth ||
                g.state == CustomerGroup.GroupState.Seated)
            {
                slots[i] = null;
            }
        }
    }

    // New groups join after the last currently occupied slot
    private int GetNextBackSlot()
    {
        for (int i = slots.Length - 1; i >= 0; i--)
        {
            if (slots[i] != null)
                return i + 1;
        }
        return 0;
    }

    private int FindSlot(CustomerGroup group)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == group) return i;
        return -1;
    }

    private void MoveGroupToSlot(CustomerGroup group, int slotIndex)
    {
        if (group == null) return;
        if (slotIndex < 0 || slotIndex >= linePoints.Length) return;

        Transform p = linePoints[slotIndex];
        if (p == null) return;

        Vector3 basePos = p.position;
        Vector3 forward = p.forward.normalized;
        Vector3 right = p.right.normalized;

        for (int m = 0; m < group.members.Count; m++)
        {
            var member = group.members[m];
            if (member == null || member.IsSeated) continue;

            Vector3 offset;

            if (group.members.Count == 1)
            {
                offset = Vector3.zero;
            }
            else if (group.members.Count == 2)
            {
                offset = (m == 0)
                    ? (-right * sideSpacing * 0.5f)
                    : (right * sideSpacing * 0.5f);
            }
            else
            {
                int row = m / 2;
                int col = m % 2;

                float x = (col == 0) ? -sideSpacing * 0.5f : sideSpacing * 0.5f;
                float z = -row * backSpacing;

                offset = right * x + forward * z;
            }

            member.WalkTo(basePos + offset);
        }

        group.transform.rotation = Quaternion.LookRotation(p.forward, Vector3.up);
    }
}
