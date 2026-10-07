using System.Collections.Generic;
using UnityEngine;

namespace DineIn.Appearance
{
    // Per-equipped-hood visual mask. Source hairstyle assets and transforms are never edited.
    public sealed class HoodHairOcclusion : MonoBehaviour
    {
        [Tooltip("Volumes in this hood's local coordinates. Only hair inside a volume is removed. Keep the face opening clear.")]
        [SerializeField] private Bounds[] coveredHair = System.Array.Empty<Bounds>();
        private readonly List<(MeshFilter filter, Mesh source, Mesh clipped)> copies = new();
        private GameObject appliedHair;
        private struct Vertex
        {
            public Vector3 p, n;
            public Vector2 uv;
            public Vector4 tangent;
            public Color color;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex {
                p = Vector3.LerpUnclamped(a.p,b.p,t), n = Vector3.LerpUnclamped(a.n,b.n,t),
                uv = Vector2.LerpUnclamped(a.uv,b.uv,t), tangent = Vector4.LerpUnclamped(a.tangent,b.tangent,t),
                color = Color.LerpUnclamped(a.color,b.color,t) };
        }
        public void Apply(GameObject hairstyle)
        {
            if (appliedHair == hairstyle) return;
            Restore(); appliedHair = hairstyle;
            if (hairstyle == null || coveredHair.Length == 0) return;
            foreach (var filter in hairstyle.GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.sharedMesh;
                if (source == null || !source.isReadable) { Debug.LogWarning("[Hood] Hair mesh must be readable for partial masking.", filter); continue; }
                var positions = source.vertices; var normals = source.normals; var uv = source.uv;
                var tangents = source.tangents; var colors = source.colors;
                var toHood = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var vertices = new List<Vertex>(); var submeshes = new List<int[]>();
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    var output = new List<int>(); var indices = source.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var triangle = new List<Vertex>(3);
                        for (int j = 0; j < 3; j++) { int k = indices[i+j]; triangle.Add(new Vertex {
                            p = positions[k], n = normals.Length == positions.Length ? normals[k] : Vector3.up,
                            uv = uv.Length == positions.Length ? uv[k] : Vector2.zero,
                            tangent = tangents.Length == positions.Length ? tangents[k] : new Vector4(1,0,0,1),
                            color = colors.Length == positions.Length ? colors[k] : Color.white }); }
                        var pieces = new List<List<Vertex>> { triangle };
                        foreach (var box in coveredHair)
                        {
                            var remaining = new List<List<Vertex>>();
                            foreach (var piece in pieces) SubtractBox(piece, box, toHood, remaining);
                            pieces = remaining;
                        }
                        foreach (var piece in pieces)
                        {
                            int start = vertices.Count; vertices.AddRange(piece);
                            for (int j = 1; j + 1 < piece.Count; j++) { output.Add(start); output.Add(start+j); output.Add(start+j+1); }
                        }
                    }
                    submeshes.Add(output.ToArray());
                }
                var mesh = new Mesh { name = source.name + " (hood mask)", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(vertices.ConvertAll(v => v.p)); mesh.SetNormals(vertices.ConvertAll(v => v.n));
                mesh.SetUVs(0,vertices.ConvertAll(v => v.uv)); mesh.SetTangents(vertices.ConvertAll(v => v.tangent));
                if (colors.Length != 0) mesh.SetColors(vertices.ConvertAll(v => v.color));
                mesh.subMeshCount = submeshes.Count;
                for (int sub = 0; sub < submeshes.Count; sub++) mesh.SetTriangles(submeshes[sub],sub);
                mesh.RecalculateBounds(); copies.Add((filter,source,mesh)); filter.sharedMesh = mesh;
            }
        }
        private static void SubtractBox(List<Vertex> polygon, Bounds box, Matrix4x4 toHood, List<List<Vertex>> result)
        {
            // Most triangles miss narrow accessory masks. Preserve those polygons without splitting.
            var bounds = new Bounds(toHood.MultiplyPoint3x4(polygon[0].p), Vector3.zero);
            for (int i = 1; i < polygon.Count; i++) bounds.Encapsulate(toHood.MultiplyPoint3x4(polygon[i].p));
            if (!box.Intersects(bounds)) { result.Add(polygon); return; }
            // Split at each box face. Outside pieces retain their exact positions/attributes;
            // only the final polygon inside all six planes is discarded.
            for (int axis = 0; axis < 3 && polygon.Count >= 3; axis++)
                for (int side = 0; side < 2 && polygon.Count >= 3; side++)
                {
                    float sign = side == 0 ? -1 : 1, boundary = side == 0 ? box.min[axis] : box.max[axis];
                    var inside = new List<Vertex>(); var outside = new List<Vertex>();
                    var previous = polygon[polygon.Count-1];
                    float pd = (toHood.MultiplyPoint3x4(previous.p)[axis] - boundary) * sign;
                    foreach (var current in polygon)
                    {
                        float cd = (toHood.MultiplyPoint3x4(current.p)[axis] - boundary) * sign;
                        if ((pd <= 0) != (cd <= 0)) { var cut = Vertex.Lerp(previous,current,pd/(pd-cd)); inside.Add(cut); outside.Add(cut); }
                        if (cd <= 0) inside.Add(current); else outside.Add(current);
                        previous = current; pd = cd;
                    }
                    if (outside.Count >= 3) result.Add(outside);
                    polygon = inside;
                }
        }
        public void Restore()
        {
            foreach (var copy in copies)
            {
                if (copy.filter != null && copy.filter.sharedMesh == copy.clipped) copy.filter.sharedMesh = copy.source;
                if (Application.isPlaying) Destroy(copy.clipped); else DestroyImmediate(copy.clipped);
            }
            copies.Clear(); appliedHair = null;
        }
        private void OnDestroy() => Restore();
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix; Gizmos.color = new Color(1,.3f,.5f,.5f);
            foreach (var box in coveredHair) Gizmos.DrawWireCube(box.center,box.size);
        }
#endif
    }
}
