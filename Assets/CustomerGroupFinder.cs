using UnityEngine;

public static class CustomerGroupFinder
{
    public static CustomerGroup FindClosestNeedsBill(Vector3 from, float maxDistance)
    {
        var groups = Object.FindObjectsOfType<CustomerGroup>();
        CustomerGroup best = null;
        float bestD = maxDistance * maxDistance;

        for (int i = 0; i < groups.Length; i++)
        {
            var g = groups[i];
            if (g == null) continue;
            if (g.state != CustomerGroup.GroupState.NeedsBill) continue;

            float d = (g.transform.position - from).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = g;
            }
        }

        return best;
    }
}