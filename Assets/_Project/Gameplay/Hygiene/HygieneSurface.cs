using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public enum HygieneKitchenUse { Prep, Cook, Serve, Wash, Storage }

[DisallowMultipleComponent]
public sealed class HygieneSurface : MonoBehaviour
{
    public bool exclude;
    public HygieneArea area;
    public bool floor;
    public HygieneSurfaceKind kind;
    public HygieneKitchenUse kitchenUse;
    public int surfaceId;
    [Range(.1f, 5f)] public float spotScale = 1f;
    private void OnEnable() => HygieneManager.Instance?.RegisterSurface(this);
}

// Registration is independent of dirt. Each equipment/table has its own record.
public sealed class HygieneSurfaceRegistry : IDisposable
{
    private sealed class Target
    {
        public int id;
        public Transform root;
        public HygieneArea area;
        public HygieneSurfaceKind kind;
        public HygieneKitchenUse use;
        public Bounds bounds;
        public bool visibleSurface;
    }
    private sealed class Surface
    {
        public MeshRenderer source, overlay;
        public Target target;
        public MaterialPropertyBlock properties;
        public bool[] opaque;
        public float lastDirt = -1f;
    }
    private readonly List<Surface> surfaces = new();
    private readonly List<Target> targets = new();
    private readonly HashSet<MeshRenderer> tracked = new();
    private readonly Dictionary<Transform, Target> byRoot = new();
    private KitchenManager owner;
    private Material material;
    private HygieneFootprintRenderer footprints;
    private Bounds serviceBounds;
    private bool hasServiceBounds;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private static readonly int Dirt = Shader.PropertyToID("_Dirt");
    private static readonly int Seed = Shader.PropertyToID("_Seed");
    private static readonly int Scale = Shader.PropertyToID("_SpotScale");
    private static readonly int Opacity = Shader.PropertyToID("_Opacity");
    private static readonly int Shine = Shader.PropertyToID("_CleanShine");

    public void Discover(KitchenManager kitchen)
    {
        owner = kitchen;
        var shader = Resources.Load<Shader>("Hygiene/DirtOverlay");
        if (shader == null) { Debug.LogError("[Hygiene] Missing dirt overlay shader."); return; }
        material = new Material(shader) { name = "Shared Hygiene Overlay", hideFlags = HideFlags.DontSave };
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Register(renderer);
        targets.Sort((a, b) => a.id.CompareTo(b.id));
        footprints = new HygieneFootprintRenderer(kitchen.transform);
    }
    public void Register(HygieneSurface surface)
    {
        if (material == null || surface == null) return;
        foreach (var renderer in surface.GetComponentsInChildren<MeshRenderer>(true)) Register(renderer);
    }
    public void Register(Booth booth)
    {
        if (material == null || booth == null || byRoot.ContainsKey(booth.transform)) return;
        foreach (var renderer in booth.GetComponentsInChildren<MeshRenderer>(true)) Register(renderer);
    }
    public void RegisterStation(Component station)
    {
        if (material == null || station == null) return;
        foreach (var renderer in station.GetComponentsInChildren<MeshRenderer>(true)) Register(renderer);
    }
    private void Register(MeshRenderer renderer)
    {
        if (renderer == null || tracked.Contains(renderer) || renderer.name == "Hygiene Overlay"
            || renderer.gameObject.scene != owner.gameObject.scene || renderer.GetComponentInParent<Canvas>() != null
            || renderer.GetComponentInParent<CustomerGroup>() != null || renderer.GetComponentInParent<PlayerMovement>() != null
            || renderer.GetComponentInParent<AutonomousStaffBot>() != null || renderer.GetComponentInParent<PlayerHolding>() != null
            || renderer.GetComponentInParent<FoodTray>() != null || renderer.GetComponentInParent<DraggableStorageBox>() != null
            || renderer.GetComponentInParent<CleanableEvent>() != null) return;
        var authoring = renderer.GetComponentInParent<HygieneSurface>();
        if (authoring != null && (authoring.exclude || authoring.floor || authoring.kind == HygieneSurfaceKind.Floor)) return;
        var mesh = renderer.GetComponent<MeshFilter>();
        if (mesh == null || mesh.sharedMesh == null) return;
        var booth = renderer.GetComponentInParent<Booth>();
        var station = renderer.GetComponentInParent<Counter>();
        var sink = renderer.GetComponentInParent<SinkInteractable>();
        Component storage = renderer.GetComponentInParent<Cupboard>();
        if (storage == null) storage = renderer.GetComponentInParent<Shelf>();
        string name = renderer.name.ToLowerInvariant();
        bool equipment = name.Contains("stove") || name.Contains("prep") || name.Contains("oven")
            || name.Contains("grill") || name.Contains("fryer") || name.Contains("sink") || name.Contains("refrigerator")
            || name.Contains("dispenser") || name.Contains("cupboard") || name.Contains("workstation")
            || name.Contains("work station") || name.Contains("counter");
        if (authoring == null && booth == null && station == null && sink == null && storage == null && !equipment) return;
        Transform root = booth != null ? booth.transform : authoring != null ? authoring.transform
            : station != null ? station.transform : sink != null ? sink.transform : storage != null ? storage.transform : renderer.transform;
        if (!byRoot.TryGetValue(root, out var target))
        {
            var kind = booth != null ? HygieneSurfaceKind.Dining : authoring != null ? authoring.kind
                : station != null ? station.GetType() == typeof(Counter) ? HygieneSurfaceKind.Counter : HygieneSurfaceKind.Equipment
                : name.Contains("counter") || name.Contains("prep") ? HygieneSurfaceKind.Counter : HygieneSurfaceKind.Equipment;
            var use = authoring != null ? authoring.kitchenUse : station is Grill || station is Fryer ? HygieneKitchenUse.Cook
                : station is DrinkDispenser || station is CupSpawner || station is PlateSpawner || station is DeliveryCounter ? HygieneKitchenUse.Serve
                : sink != null ? HygieneKitchenUse.Wash : storage != null ? HygieneKitchenUse.Storage : InferUse(name);
            target = new Target { id = authoring != null && authoring.surfaceId != 0 ? authoring.surfaceId : StableId(root), root = root,
                area = booth != null ? HygieneArea.Lobby : authoring != null ? authoring.area : HygieneArea.Kitchen,
                kind = kind, use = use, bounds = renderer.bounds };
            byRoot.Add(root, target); targets.Add(target);
        }
        else target.bounds.Encapsulate(renderer.bounds);
        var sourceMaterials = renderer.sharedMaterials;
        int submeshes = mesh.sharedMesh.subMeshCount;
        if (submeshes == 0 || sourceMaterials.Length == 0) return;
        bool[] opaque = new bool[submeshes]; bool anyOpaque = false;
        for (int i = 0; i < submeshes; i++)
        {
            var source = sourceMaterials[Mathf.Min(i, sourceMaterials.Length - 1)];
            opaque[i] = source != null && source.renderQueue <= 2500;
            anyOpaque |= opaque[i];
        }
        if (!anyOpaque) return;
        target.visibleSurface = true;
        if (!hasServiceBounds) { serviceBounds = renderer.bounds; hasServiceBounds = true; }
        else serviceBounds.Encapsulate(renderer.bounds);
        var go = new GameObject("Hygiene Overlay") { layer = renderer.gameObject.layer, hideFlags = HideFlags.DontSave };
        go.transform.SetParent(renderer.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
        var overlay = go.AddComponent<MeshRenderer>();
        var materials = new Material[submeshes];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        overlay.sharedMaterials = materials;
        overlay.shadowCastingMode = ShadowCastingMode.Off; overlay.receiveShadows = false;
        overlay.lightProbeUsage = LightProbeUsage.Off; overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
        overlay.enabled = false;
        var properties = new MaterialPropertyBlock();
        properties.SetFloat(Seed, (uint)target.id % 8192);
        properties.SetFloat(Scale, (authoring != null ? authoring.spotScale : 1f) * (target.kind == HygieneSurfaceKind.Dining ? 1.6f : 1f));
        surfaces.Add(new Surface { source = renderer, overlay = overlay, target = target, properties = properties, opaque = opaque });
        tracked.Add(renderer);
    }
    private static HygieneKitchenUse InferUse(string name) => name.Contains("stove") || name.Contains("oven") || name.Contains("fryer") || name.Contains("grill")
        ? HygieneKitchenUse.Cook : name.Contains("sink") ? HygieneKitchenUse.Wash
        : name.Contains("refrigerator") || name.Contains("cupboard") || name.Contains("shelf") ? HygieneKitchenUse.Storage
        : name.Contains("dispenser") || name.Contains("delivery") || name.Contains("plate") || name.Contains("cup") ? HygieneKitchenUse.Serve : HygieneKitchenUse.Prep;
    public static int StableId(Transform root)
    {
        uint hash = 2166136261;
        for (var node = root; node != null; node = node.parent)
        {
            int ordinal = 0;
            if (node.parent != null)
            {
                for (int i = 0; i < node.GetSiblingIndex(); i++) if (node.parent.GetChild(i).name == node.name) ordinal++;
            }
            else foreach (var other in node.gameObject.scene.GetRootGameObjects())
            { if (other.transform == node) break; if (other.name == node.name) ordinal++; }
            foreach (char c in node.name + "#" + ordinal + "/") hash = unchecked((hash ^ c) * 16777619);
        }
        int id = (int)(hash & 0x7fffffff); return id == 0 ? 1 : id;
    }
    private Target Resolve(Component component)
    {
        if (component == null) return null;
        for (var t = component.transform; t != null; t = t.parent) if (byRoot.TryGetValue(t, out var target)) return target;
        return null;
    }
    public bool Use(HygieneState state, Component component, float amount)
    {
        var target = Resolve(component);
        return target != null && target.root != null && target.visibleSurface && target.root.gameObject.activeInHierarchy &&
            state.AddSurfaceDirt(target.id, target.area, amount * (target.kind == HygieneSurfaceKind.Counter ? .2f : 1f));
    }
    public bool UseNearest(HygieneState state, Vector3 position, float amount, HygieneKitchenUse? use = null)
    {
        Target best = null; float distance = 9f;
        foreach (var target in targets)
        {
            if (target.root == null || !target.visibleSurface || !target.root.gameObject.activeInHierarchy || target.area != HygieneArea.Kitchen || use.HasValue && target.use != use.Value) continue;
            float d = (target.bounds.ClosestPoint(position) - position).sqrMagnitude;
            if (d < distance) { distance = d; best = target; }
        }
        return best != null && state.AddSurfaceDirt(best.id, best.area, amount * (best.kind == HygieneSurfaceKind.Counter ? .2f : 1f));
    }
    public void Clean(HygieneState state, Component component)
    {
        var target = Resolve(component);
        if (target != null) state.CleanSurface(target.id, component is Booth);
    }
    public float DirtFor(HygieneState state, Component component) => Resolve(component) is Target target ? state.SurfaceDirt(target.id) : 0f;
    public bool TryGetWorkStation(EmployeeRole role, ref int cursor, Vector3 from, out Transform station, out Vector3 approach)
    {
        station = null; approach = from;
        bool cooking = role == EmployeeRole.Chef || role == EmployeeRole.LineCook;
        for (int inspected = 0; inspected < targets.Count; inspected++)
        {
            cursor = (cursor % targets.Count + targets.Count) % targets.Count;
            var target = targets[cursor]; cursor = (cursor + 1) % targets.Count;
            if (target.root == null || !target.visibleSurface || !target.root.gameObject.activeInHierarchy
                || target.area != HygieneArea.Kitchen || (cooking ? target.use != HygieneKitchenUse.Cook
                    : target.use != HygieneKitchenUse.Prep && target.use != HygieneKitchenUse.Serve)) continue;
            // Reuse the existing worker's visual visits; cooking still belongs to KitchenManager.
            Vector3 edge = target.bounds.ClosestPoint(from);
            Vector3 away = from - target.bounds.center; away.y = 0;
            if (away.sqrMagnitude < .01f) away = Vector3.forward;
            edge += away.normalized * .55f; edge.y = from.y;
            if (!NavMesh.SamplePosition(edge, out var hit, 1f, NavMesh.AllAreas)) continue;
            station = target.root; approach = hit.position; return true;
        }
        return false;
    }
    public void Refresh(HygieneState state)
    {
        for (int i = surfaces.Count - 1; i >= 0; i--)
        {
            var surface = surfaces[i];
            if (surface.source == null || surface.overlay == null)
            {
                tracked.Remove(surface.source);
                if (surface.overlay != null) UnityEngine.Object.Destroy(surface.overlay.gameObject);
                surfaces.RemoveAt(i); continue;
            }
            float dirt = state.SurfaceDirt(surface.target.id);
            float shine = state.SurfaceShine(surface.target.id) > 0f ? 1f : 0f;
            if (surface.target.kind == HygieneSurfaceKind.Dining && dirt < .25f) dirt = 0f;
            surface.overlay.enabled = (dirt > .001f || shine > 0f) && surface.source.enabled;
            surface.lastDirt = dirt;
            surface.properties.SetFloat(Dirt, dirt);
            surface.properties.SetFloat(Shine, shine);
            float opacity = surface.target.kind == HygieneSurfaceKind.Counter ? .3f : surface.target.kind == HygieneSurfaceKind.Dining ? .9f : .75f;
            for (int j = 0; j < surface.opaque.Length; j++)
            {
                surface.properties.SetFloat(Opacity, surface.opaque[j] ? opacity : 0f);
                surface.overlay.SetPropertyBlock(surface.properties, j);
            }
        }
        footprints?.Refresh(state);
    }
    public bool TryGetFloorPoint(Vector3 feet, out Vector3 point)
    {
        point = feet;
        if (!hasServiceBounds) return false;
        Bounds area = serviceBounds; area.Expand(new Vector3(3f, 6f, 3f));
        if (!area.Contains(feet)) return false;
        if (!NavMesh.SamplePosition(feet, out var hit, 1.5f, NavMesh.AllAreas)) return false;
        Vector2 planar = new Vector2(hit.position.x - feet.x, hit.position.z - feet.z);
        if (planar.sqrMagnitude > .16f || Mathf.Abs(hit.position.y - feet.y) > 1.5f) return false;
        point = hit.position;
        // Prefer the physical floor height where a collider is available. A
        // baked NavMesh can sit slightly below the rendered floor.
        int count = Physics.RaycastNonAlloc(point + Vector3.up * .4f, Vector3.down, groundHits, .8f, ~0, QueryTriggerInteraction.Ignore);
        float best = .35f;
        for (int i = 0; i < count; i++)
        {
            var ground = groundHits[i];
            if (ground.collider.gameObject.scene != owner.gameObject.scene || ground.normal.y < .85f
                || ground.collider.GetComponentInParent<Booth>() != null || ground.collider.GetComponentInParent<Counter>() != null
                || ground.collider.GetComponentInParent<CustomerAgent>() != null || ground.collider.GetComponentInParent<PlayerMovement>() != null
                || ground.collider.GetComponentInParent<AutonomousStaffBot>() != null) continue;
            float distance = Mathf.Abs(ground.point.y - hit.position.y);
            if (distance < best) { best = distance; point = ground.point; }
        }
        return true;
    }
    public void Dispose()
    {
        foreach (var surface in surfaces) if (surface.overlay != null) UnityEngine.Object.Destroy(surface.overlay.gameObject);
        surfaces.Clear(); targets.Clear(); tracked.Clear(); byRoot.Clear(); footprints?.Dispose();
        if (material != null) UnityEngine.Object.Destroy(material);
    }
}
