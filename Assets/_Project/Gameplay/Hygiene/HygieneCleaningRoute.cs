using System.Collections.Generic;
using UnityEngine;

public static class HygieneCleaningRoute
{
    public static float WorkSeconds => HygieneSettings.Current.lobbyCleanSeconds;
    public static float BrushRadius => HygieneSettings.Current.mopRadius;

    public static void Capture(HygieneState state, Vector3 origin)
    {
        state.floorRoute.Clear(); state.ResetRouteLookup();
        var remaining = new List<HygieneFloorMark>(state.floorMarks);
        while (remaining.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < remaining.Count; i++)
            {
                float a = (remaining[i].position - origin).sqrMagnitude;
                float b = (remaining[best].position - origin).sqrMagnitude;
                if (a < b || Mathf.Approximately(a, b) && remaining[i].id < remaining[best].id) best = i;
            }
            var mark = remaining[best]; remaining.RemoveAt(best);
            state.floorRoute.Add(mark.id); origin = mark.position;
        }
        state.floorRouteTotal = state.floorRoute.Count;
        state.floorRouteDone = state.floorRouteSkipped = 0;
        state.floorWorkSeconds = state.floorWorkProgress = 0f;
        state.floorRouteActive = false;
        state.lobbyCleaningRequested = state.floorRouteTotal > 0;
    }

    public static bool WithinSweep(Vector3 point, Vector3 from, Vector3 to)
    {
        point.y = from.y = to.y = 0f;
        Vector3 segment = to - from;
        float t = segment.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector3.Dot(point - from, segment) / segment.sqrMagnitude) : 0f;
        return (point - (from + segment * t)).sqrMagnitude <= BrushRadius * BrushRadius;
    }
}
