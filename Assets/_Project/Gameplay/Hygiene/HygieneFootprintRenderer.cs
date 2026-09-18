using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One bounded dynamic mesh, no GameObject/material per footprint and no
// full-screen loop over every stain in a floor fragment shader.
public sealed class HygieneFootprintRenderer : System.IDisposable
{
    public static float VisibleDirtThreshold => HygieneSettings.Current.visibleFloorDirt;
    public static float VisibleOpacity(float dirt) => Mathf.InverseLerp(VisibleDirtThreshold, 1f, dirt);
    private readonly GameObject root;
    private readonly Mesh mesh;
    private readonly Material material;
    private readonly MeshRenderer renderer;
    private readonly List<Vector3> vertices = new(HygieneState.MaxFloorMarks * 4);
    private readonly List<Vector2> uvs = new(HygieneState.MaxFloorMarks * 4);
    private readonly List<Vector2> kinds = new(HygieneState.MaxFloorMarks * 4);
    private readonly List<Color> colors = new(HygieneState.MaxFloorMarks * 4);
    private readonly List<int> indices = new(HygieneState.MaxFloorMarks * 6);
    private HygieneState previous;
    private int revision = -1;
    public HygieneFootprintRenderer(Transform owner)
    {
        var shader = Resources.Load<Shader>("Hygiene/Footprints");
        if (shader == null) { Debug.LogError("[Hygiene] Missing footprint shader."); return; }
        root = new GameObject("Hygiene Footprints") { hideFlags = HideFlags.DontSave, layer = owner.gameObject.layer };
        root.transform.SetParent(owner, false);
        mesh = new Mesh { name = "Hygiene Footprints", hideFlags = HideFlags.DontSave };
        mesh.MarkDynamic(); root.AddComponent<MeshFilter>().sharedMesh = mesh;
        material = new Material(shader) { hideFlags = HideFlags.DontSave };
        renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }
    public void Refresh(HygieneState state)
    {
        if (root == null || previous == state && revision == state.revision) return;
        previous = state; revision = state.revision;
        vertices.Clear(); uvs.Clear(); kinds.Clear(); colors.Clear(); indices.Clear();
        foreach (var mark in state.floorMarks)
        {
            float opacity = VisibleOpacity(mark.dirt);
            if (opacity <= 0f) continue;
            Quaternion rotation = Quaternion.Euler(0, mark.yaw, 0);
            Vector3 right = rotation * Vector3.right * (mark.spill ? .45f : .17f);
            Vector3 forward = rotation * Vector3.forward * (mark.spill ? .45f : .30f);
            Vector3 center = mark.position + Vector3.up * .012f;
            int first = vertices.Count;
            vertices.Add(root.transform.InverseTransformPoint(center - right - forward));
            vertices.Add(root.transform.InverseTransformPoint(center - right + forward));
            vertices.Add(root.transform.InverseTransformPoint(center + right + forward));
            vertices.Add(root.transform.InverseTransformPoint(center + right - forward));
            uvs.Add(new Vector2(-1,-1)); uvs.Add(new Vector2(-1,1)); uvs.Add(new Vector2(1,1)); uvs.Add(new Vector2(1,-1));
            Color tint = mark.color == 1 ? new Color(.14f,.42f,.19f) : mark.color == 2 ? new Color(.52f,.18f,.36f)
                : mark.color == 3 ? new Color(.15f,.27f,.55f) : new Color(.12f,.09f,.065f);
            tint.a = opacity;
            for (int j = 0; j < 4; j++) { colors.Add(tint); kinds.Add(new Vector2(mark.spill ? 1f : 0f, 0f)); }
            indices.Add(first); indices.Add(first+1); indices.Add(first+2);
            indices.Add(first); indices.Add(first+2); indices.Add(first+3);
        }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetColors(colors);
        mesh.SetUVs(1, kinds);
        mesh.SetTriangles(indices, 0); mesh.RecalculateBounds(); renderer.enabled = indices.Count > 0;
    }
    public void Dispose()
    {
        if (root != null) Object.Destroy(root);
        if (mesh != null) Object.Destroy(mesh);
        if (material != null) Object.Destroy(material);
    }
}
