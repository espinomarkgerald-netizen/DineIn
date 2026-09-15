using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(32000)]
public sealed class MultiplayerRestockView : MonoBehaviour
{
    private const int Layer = 15;
    private static MultiplayerRestockView instance;
    private Scene scene;
    private RestockStorageType room;
    private readonly Dictionary<Camera, int> masks = new();
    private readonly Dictionary<Canvas, bool> canvases = new();
    private readonly bool[] collisions = new bool[32];
    public static bool Active => instance != null && instance.scene.IsValid();
    public static int RaycastMask => 1 << Layer;
    public static bool Accepts(GameObject target, ShelfGrid shelf) => Active && target != null
        && target.scene == instance.scene && target.layer == Layer && shelf != null && shelf.StorageType == instance.room;
    public static void Begin(Scene scene, RestockStorageType room)
    {
        if (!MultiplayerRestockBridge.IsActive) return;
        End();
        var root = new GameObject("[Local restock view]");
        instance = root.AddComponent<MultiplayerRestockView>();
        instance.scene = scene; instance.room = room;
        for (int i = 0; i < 32; i++)
        { instance.collisions[i] = Physics.GetIgnoreLayerCollision(Layer, i); Physics.IgnoreLayerCollision(Layer, i, i != Layer); }
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            instance.masks[camera] = camera.cullingMask;
            camera.cullingMask = camera.gameObject.scene == scene ? (1 << Layer) | (1 << 5) : camera.cullingMask & ~(1 << Layer);
        }
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (canvas.gameObject.scene != scene && canvas.GetComponentInParent<RestockFlowHUD>() == null)
                instance.canvases[canvas] = canvas.enabled;
        foreach (var item in scene.GetRootGameObjects()) Prepare(item);
    }
    public static void Prepare(GameObject root)
    {
        if (!Active || root == null || root.scene != instance.scene) return;
        foreach (var node in root.GetComponentsInChildren<Transform>(true))
            if (node.GetComponentInParent<Canvas>() == null) node.gameObject.layer = Layer;
        foreach (var agent in root.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
        foreach (var obstacle in root.GetComponentsInChildren<NavMeshObstacle>(true)) obstacle.enabled = false;
        foreach (var light in root.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << Layer;
    }
    public static void SelectRoom(RestockStorageType room) { if (Active) instance.room = room; }
    public static bool TryShelf(Ray ray, out ShelfGrid grid, out int column, out int row)
    {
        grid = null; column = row = -1;
        if (!Active) return false;
        var hits = Physics.RaycastAll(ray, 500f, 1 << Layer, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            var candidate = hit.collider.GetComponentInParent<ShelfGrid>();
            if (candidate == null || candidate.gameObject.scene != instance.scene || candidate.StorageType != instance.room) continue;
            if (!candidate.TryGetClosestCell(hit.point, out column, out row)) continue;
            grid = candidate; return true;
        }
        return false;
    }
    private void LateUpdate()
    { foreach (var canvas in canvases.Keys) if (canvas != null) canvas.enabled = false; }
    public static void End()
    {
        if (instance == null) return;
        var view = instance; instance = null;
        view.Restore(); Destroy(view.gameObject);
    }
    private void Restore()
    {
        if (!scene.IsValid()) return;
        foreach (var mask in masks) if (mask.Key != null) mask.Key.cullingMask = mask.Value;
        foreach (var canvas in canvases) if (canvas.Key != null) canvas.Key.enabled = canvas.Value;
        for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(Layer, i, collisions[i]);
        scene = default;
    }
    private void OnDestroy() { Restore(); if (instance == this) instance = null; }
}
