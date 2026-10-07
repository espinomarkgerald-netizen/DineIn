using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Scene-side anchors follow the unfinished imported model without editing its meshes or transforms.
/// Authored layouts stay unchanged unless binding application is explicitly enabled or requested.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class FineDiningSceneBindings : MonoBehaviour
{
    [Serializable]
    public sealed class Anchor
    {
        public Transform target;
        public string furniture;
        [Tooltip("Offset from the furniture bounds center as a fraction of its size.")]
        public Vector3 normalizedOffset;
        [Tooltip("Additional offset in the restaurant model's local units.")]
        public Vector3 offset;
        public bool floorLevel;
        public float yaw;
        public float pitch;
    }
    [Serializable]
    public sealed class Table
    {
        public FineDiningTable binding;
        public string furniture;
        public Vector2[] seatOffsets;
        public Vector3 approachOffset;
        public Vector3 surfaceOffset;
        [Min(0f)] public float seatHeight = .56f;
        [Min(0f)] public float surfaceHeight = .8f;
    }
    public Transform importedModel;
    public Table[] tables;
    public Anchor[] anchors;
    public RestockStockRoomEntrance dryStorage;
    public RestockStockRoomEntrance freezer;
    public MeshRenderer receptionMonitor;
    public MeshRenderer entranceRampRenderer;
    public MeshRenderer deliveryRampRenderer;
    public string officeComputerName = "Computer";
    public string[] walkableFurniture = { "Cube", "Cube.006" };
    public Transform[] navigationApproaches;
    public GameObject[] obsoleteFurniture;
    [NonSerialized] private Transform[] modelNodes;

    [Tooltip("Leave disabled for the authored Lobby3 layout. Use Apply Lobby3 Furniture Bindings explicitly when the imported model changes.")]
    public bool applyBindingsOnAwake = true;
    private void Awake() { if (applyBindingsOnAwake) ApplyBindings(); }

    public void ApplyBindings()
    {
        if (importedModel == null) throw new InvalidOperationException("Fine Dining model reference is missing.");
        modelNodes = importedModel.GetComponentsInChildren<Transform>(true);
        if (walkableFurniture != null)
            foreach (string floor in walkableFurniture)
                foreach (var collider in FindFurniture(floor).GetComponentsInChildren<Collider>(true))
                    collider.gameObject.layer = LayerMask.NameToLayer("Ground");
        if (obsoleteFurniture != null) foreach (var old in obsoleteFurniture) if (old != null) old.SetActive(false);
        if (anchors != null) foreach (var anchor in anchors)
        {
            if (anchor.target == null) throw new InvalidOperationException("Fine Dining has an unassigned scene anchor.");
            Bounds bounds = FurnitureBounds(anchor.furniture);
            Vector3 point = bounds.center + Vector3.Scale(bounds.size, anchor.normalizedOffset) + anchor.offset;
            if (anchor.floorLevel) point.y = bounds.min.y + anchor.offset.y;
            anchor.target.SetPositionAndRotation(importedModel.TransformPoint(point), importedModel.rotation * Quaternion.Euler(anchor.pitch, anchor.yaw, 0));
        }
        if (tables != null) foreach (var table in tables) BindTable(table);
        if (entranceRampRenderer != null || deliveryRampRenderer != null)
        {
            var floor = FindFurniture("Cube.006").GetComponentInChildren<MeshRenderer>();
            if (floor != null && floor.sharedMaterial != null)
            {
                if (entranceRampRenderer != null) entranceRampRenderer.sharedMaterial = floor.sharedMaterial;
                if (deliveryRampRenderer != null) deliveryRampRenderer.sharedMaterial = floor.sharedMaterial;
            }
        }
        if (receptionMonitor != null)
        {
            var source = FindFurniture(officeComputerName).GetComponentInChildren<MeshRenderer>();
            if (source != null) receptionMonitor.sharedMaterials = source.sharedMaterials;
        }
    }

    public Transform FindFurniture(string furniture)
    {
        if (modelNodes == null || modelNodes.Length == 0 || modelNodes[0] != importedModel) modelNodes = importedModel != null ? importedModel.GetComponentsInChildren<Transform>(true) : Array.Empty<Transform>();
        var found = modelNodes.FirstOrDefault(node => node != null && node.name == furniture);
        if (found == null) throw new InvalidOperationException("Fine Dining furniture is missing: " + furniture);
        return found;
    }
    private Bounds FurnitureBounds(string furniture)
    {
        var renderers = FindFurniture(furniture).GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("Fine Dining furniture has no mesh: " + furniture);
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (var renderer in renderers)
        {
            // Use mesh bounds rather than world AABBs, retaining alignment at arbitrary restaurant rotations.
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var local = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = importedModel.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
    private void BindTable(Table table)
    {
        if (table.binding == null || table.binding.booth == null) throw new InvalidOperationException("Fine Dining table binding is missing.");
        var booth = table.binding.booth;
        var bounds = FurnitureBounds(table.furniture);
        Vector3 floor = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        booth.transform.position = importedModel.TransformPoint(floor);
        // Keep all existing service/cleanup components and their prop scales; only the old furniture is hidden.
        var oldFurniture = booth.GetComponent<MeshRenderer>();
        if (oldFurniture != null) oldFurniture.enabled = false;
        foreach (var collider in booth.GetComponents<Collider>()) collider.enabled = false;
        var click = booth.GetComponent<BoxCollider>();
        if (click == null) throw new InvalidOperationException(booth.name + " needs its authored click collider.");
        click.enabled = true;
        click.isTrigger = true;
        click.center = booth.transform.InverseTransformPoint(importedModel.TransformPoint(floor + table.surfaceOffset + Vector3.up * table.surfaceHeight));
        float radius = Mathf.Min(bounds.size.x, bounds.size.z) * .35f * importedModel.lossyScale.x;
        var clickBounds = new Bounds(click.center, Vector3.zero);
        Vector3 clickCenter = booth.transform.TransformPoint(click.center);
        for (int i = 0; i < 8; i++)
            clickBounds.Encapsulate(booth.transform.InverseTransformPoint(clickCenter + new Vector3(
                (i & 1) == 0 ? -radius : radius, (i & 2) == 0 ? -.6f : .6f, (i & 4) == 0 ? -radius : radius)));
        click.center = clickBounds.center;
        click.size = clickBounds.size;
        var seats = booth.seats;
        if (table.seatOffsets == null || table.seatOffsets.Length == 0 || table.seatOffsets.Length > seats.Count)
            throw new InvalidOperationException(booth.name + " has invalid seat configuration.");
        for (int i = 0; i < table.seatOffsets.Length; i++)
        {
            var offset = table.seatOffsets[i];
            Vector3 position = importedModel.TransformPoint(floor + new Vector3(bounds.size.x * offset.x, table.seatHeight, bounds.size.z * offset.y));
            seats[i].position = position;
            Vector3 inward = importedModel.TransformPoint(floor + table.surfaceOffset) - position; inward.y = 0;
            seats[i].rotation = Quaternion.LookRotation(inward);
        }
        if (seats.Count > table.seatOffsets.Length) seats.RemoveRange(table.seatOffsets.Length, seats.Count - table.seatOffsets.Length);
        booth.useAuthoredSeatRotation = true;
        booth.approachPoint.position = importedModel.TransformPoint(floor + table.approachOffset);
        if (booth.customerApproachPoint != null) booth.customerApproachPoint.position = booth.approachPoint.position;
        Vector3 surface = importedModel.TransformPoint(floor + table.surfaceOffset + Vector3.up * table.surfaceHeight);
        if (booth.tableLookTarget != null) booth.tableLookTarget.position = surface;
        if (booth.menuSpawnPoint != null) booth.menuSpawnPoint.SetPositionAndRotation(surface, importedModel.rotation * (booth.menuBookPrefab != null ? booth.menuBookPrefab.transform.localRotation : Quaternion.identity));
        if (booth.tableNumberAnchor != null) booth.tableNumberAnchor.position = surface + Vector3.up;
        foreach (var point in booth.GetComponentsInChildren<Transform>(true))
        {
            if (point.name.Contains("StandPoint") || point.name.Contains("MovePoint")) point.position = booth.approachPoint.position;
            if (point.name.Contains("FoodSpawn") || point.name.Contains("MoneySpawn")) point.SetPositionAndRotation(surface, importedModel.rotation);
            if (point.name == "SpillSpawn" || point.name == "puddle") point.position = surface + Vector3.up * .01f;
            if (point.GetComponent<Canvas>() != null) point.position = surface + Vector3.up * .5f;
        }
    }
}
