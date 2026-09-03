using UnityEngine;

// Shared geometry for the world frame and hand. Imported model pivots are often
// at a corner or on the floor, so they are not reliable places to ask for a tap.
public static class TutorialWorldTargetGeometry
{
    public static Vector3 Center(Transform target)
    {
        return TryGetBounds(target, out Bounds bounds, out Transform space)
            ? (space != null ? space.TransformPoint(bounds.center) : bounds.center)
            : target.position;
    }

    public static bool TryGetScreenRect(Transform target, Camera camera, out Rect rect)
    {
        rect = default;
        if (!TryGetBounds(target, out Bounds bounds, out Transform space)) return false;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 screen = camera.WorldToScreenPoint(space != null ? space.TransformPoint(corner) : corner);
            if (screen.z <= 0f) return false;
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private static bool TryGetBounds(Transform target, out Bounds bounds, out Transform space)
    {
        // Prefer the object's own mesh; child food, speech bubbles and role UI
        // must not move the focus away from the booth itself.
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null) renderer = target.GetComponentInChildren<MeshRenderer>();
        if (renderer != null)
        {
            bounds = renderer.localBounds;
            space = renderer.transform;
            return true;
        }
        Collider collider = target.GetComponent<Collider>();
        bounds = collider != null ? collider.bounds : default;
        space = null;
        return collider != null;
    }
}
