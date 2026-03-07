using System.Collections.Generic;
using UnityEngine;

public class LobbyQueueManager : MonoBehaviour
{
    [Header("Queue Anchor")]
    public Transform queueStart;

    [Header("Spacing")]
    public float groupSpacing = 2.2f;      // distance between groups
    public float memberSpacing = 0.75f;    // distance between members inside a group
    public float sideSpacing = 0.6f;       // for 2-wide formation (optional)

    private readonly List<CustomerGroup> queue = new();

    public void Enqueue(CustomerGroup group)
    {
        if (group == null) return;
        if (queue.Contains(group)) return;

        queue.Add(group);
        UpdateQueuePositions();
    }

    public void Dequeue(CustomerGroup group)
    {
        if (group == null) return;
        if (!queue.Remove(group)) return;

        UpdateQueuePositions();
    }

    public int GetIndex(CustomerGroup group) => queue.IndexOf(group);

    public void UpdateQueuePositions()
    {
        if (queueStart == null) return;

        Vector3 forward = queueStart.forward.normalized;
        Vector3 right = queueStart.right.normalized;

        for (int i = 0; i < queue.Count; i++)
        {
            var group = queue[i];
            if (group == null) continue;

            // group leader slot
            Vector3 basePos = queueStart.position + forward * (-i * groupSpacing);

            // Arrange members in a simple formation behind basePos
            // 1: just leader spot
            // 2: two in a row
            // 3-4: 2x2 block behind leader slot
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
                    // two side by side
                    offset = (m == 0) ? (-right * sideSpacing * 0.5f) : (right * sideSpacing * 0.5f);
                }
                else
                {
                    // 3-4: 2 columns, 2 rows
                    int row = m / 2;         // 0,0,1,1
                    int col = m % 2;         // 0,1,0,1
                    float x = (col == 0) ? (-sideSpacing * 0.5f) : (sideSpacing * 0.5f);
                    float z = -row * memberSpacing; // behind
                    offset = right * x + forward * z;
                }

                Vector3 target = basePos + offset;
                member.WalkTo(target);
            }

            group.transform.rotation = Quaternion.LookRotation(queueStart.forward, Vector3.up);
        }
    }
}
