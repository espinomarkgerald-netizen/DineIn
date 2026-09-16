using UnityEngine;
using UnityEngine.UI;

// A small vector ring uses the existing UI renderer without another texture,
// font, raycaster or interactive element.
public sealed class MultiplayerWorkCircleGraphic : MaskableGraphic
{
    private float amount = 0.24f;
    private float angle;
    public void SetAngle(float value)
    { if (Mathf.Abs(Mathf.DeltaAngle(angle, value)) < 0.1f) return; angle = value; SetVerticesDirty(); }
    public void SetAmount(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Abs(value - amount) < 0.001f) return;
        amount = value;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        AddArc(vertices, rect.center, radius, 1f, new Color(0.07f, 0.14f, 0.20f, 0.75f), 0f);
        AddArc(vertices, rect.center, radius, amount, color, angle);
    }
    private static void AddArc(VertexHelper vertices, Vector2 centre, float radius, float fraction, Color tint, float angle)
    {
        if (fraction <= 0f || radius <= 0f) return;
        int segments = Mathf.Max(1, Mathf.CeilToInt(40 * fraction));
        float inner = radius * (2f / 3f); // Preserve ring thickness proportion at smaller sizes.
        for (int i = 0; i < segments; i++)
        {
            float a = (90f + angle - 360f * fraction * i / segments) * Mathf.Deg2Rad;
            float b = (90f + angle - 360f * fraction * (i + 1) / segments) * Mathf.Deg2Rad;
            Vector2 start = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 end = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            int first = vertices.currentVertCount;
            vertices.AddVert(centre + start * inner, tint, Vector2.zero);
            vertices.AddVert(centre + start * radius, tint, Vector2.zero);
            vertices.AddVert(centre + end * radius, tint, Vector2.zero);
            vertices.AddVert(centre + end * inner, tint, Vector2.zero);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first, first + 2, first + 3);
        }
    }
}
