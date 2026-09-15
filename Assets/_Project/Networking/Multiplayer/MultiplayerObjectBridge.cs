using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Observes the actual staff, upgrade carriers and items produced by single-player
// systems on the host. None of these snapshots starts work or earns money.
public sealed partial class MultiplayerObjectBridge : MonoBehaviour, Photon.Realtime.IOnEventCallback
{
    private const string Key = "restaurant.objects.v2";
    [Serializable] private sealed class Pose
    { public string id; public bool active; public Vector3 position; public Quaternion rotation; public float speed; public bool moving, carrying; }
    [Serializable] private sealed class TrayState
    {
        public int order, group;
        public string parent;
        public string[] products;
        public bool cleanup, locked, burnt, complaintRemoval;
        public Vector3 position, localPosition;
        public Quaternion rotation, localRotation;
    }
    [Serializable] private sealed class Snapshot
    {
        public string run;
        public int day, revision;
        public List<Pose> workers = new(), trolleys = new();
        public List<TrayState> trays = new();
    }
    private MultiplayerSessionManager session;
    private int revision, received;
    private float nextTick;
    private readonly Dictionary<int, FoodTray> observedTrays = new();
    private readonly Dictionary<GameObject, MultiplayerAnimatorState> animationStates = new();
    private MultiplayerAnimatorState Animation(GameObject root)
    {
        if (!animationStates.TryGetValue(root, out var state)) animationStates[root] = state = new MultiplayerAnimatorState(root);
        return state;
    }
    private static readonly int Speed = Animator.StringToHash("Speed"), Moving = Animator.StringToHash("IsMoving"), Carrying = Animator.StringToHash("IsCarrying");
    private void Awake() => session = GetComponent<MultiplayerSessionManager>();
    public void ResetObservedObjects() { observedTrays.Clear(); readObjects = previousObjects = null; pendingObjects = null; received = 0; poseTargets.Clear(); }
    private void LateUpdate()
    {
        InterpolatePoses();
        if (!session.IsConnected || !MultiplayerProgressionContext.Ready || session.Ended || Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + 0.1f;
        if (session.IsAuthority) Publish(); else Apply();
    }
    private Pose CapturePose(string id, GameObject root)
    {
        var animator = Animation(root);
        return new Pose { id = id, active = root.activeInHierarchy, position = root.transform.position, rotation = root.transform.rotation,
            speed = animator.Float(Speed), moving = animator.Bool(Moving), carrying = animator.Bool(Carrying) };
    }
    private static int GroupId(CustomerGroup group) => group != null ? group.GetComponentInParent<MultiplayerCustomerSpawn>()?.photonView.ViewID ?? 0 : 0;
    private static string Actor(Transform target)
    {
        var view = target.GetComponent<PhotonView>();
        return view != null && target.GetComponent<ManagerPlayer>() != null ? "player:" + view.OwnerActorNr : "staff:" + target.name;
    }
    private Transform ActorRoot(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (id.StartsWith("player:") && int.TryParse(id.Substring(7), out int actor))
            return session.TryGetManager(actor, out var manager) ? manager.transform : null;
        foreach (var bot in MultiplayerWorldRegistry.All<AutonomousStaffBot>())
            if (id == "staff:" + bot.name) return bot.transform;
        return null;
    }
    private static string Parent(FoodTray tray, IReadOnlyList<BotTrolleyCarrier> trolleys, KitchenManager kitchen)
    {
        var waiter = tray.GetComponentInParent<WaiterHands>(true);
        if (waiter != null && waiter.holdingTray == tray) return "waiter:" + Actor(waiter.transform);
        var busser = tray.GetComponentInParent<BusserHands>();
        if (busser != null && busser.holdingTray == tray) return "busser:" + Actor(busser.transform);
        foreach (var trolley in trolleys)
            for (int i = 0; i < trolley.TraySlots.Count; i++)
                if (trolley.TraySlots[i] != null && tray.transform.IsChildOf(trolley.TraySlots[i])) return "trolley:" + (int)trolley.Effect + ":" + i;
        var booth = tray.GetComponentInParent<Booth>();
        if (booth != null) return "booth:" + MultiplayerCustomerInteractionBridge.BoothIdentity(booth);
        if (kitchen != null && kitchen.traySpawnPoints != null)
            for (int i = 0; i < kitchen.traySpawnPoints.Length; i++)
                if (kitchen.traySpawnPoints[i] != null && tray.transform.parent == kitchen.traySpawnPoints[i]) return "kitchen:" + i;
        return string.Empty;
    }
    private void Publish()
    {
        using var measurement = MultiplayerDiagnostics.ObjectState.Auto();
        var state = new Snapshot { run = session.RunId, day = GameFlowManager.Instance.CurrentDay, revision = 0 };
        foreach (var worker in MultiplayerWorldRegistry.All<KitchenWorkerBot>())
            state.workers.Add(CapturePose(((int)worker.EmployeeRole).ToString(), worker.gameObject));
        var trolleys = MultiplayerWorldRegistry.All<BotTrolleyCarrier>();
        var kitchen = MultiplayerWorldRegistry.Kitchen;
        foreach (var trolley in trolleys) state.trolleys.Add(CapturePose(((int)trolley.Effect).ToString(), trolley.gameObject));
        foreach (var tray in MultiplayerWorldRegistry.All<FoodTray>())
        {
            if (tray == null || !tray.gameObject.activeInHierarchy || tray.TargetGroup == null) continue;
            state.trays.Add(new TrayState { order = tray.orderNumber, group = GroupId(tray.TargetGroup), parent = Parent(tray, trolleys, kitchen),
                products = new List<string>(tray.DeliveredProductIds).ToArray(), cleanup = tray.GetComponent<FoodTrayInteractable>()?.IsCleanupPickable == true,
                complaintRemoval = tray.GetComponent<FoodTrayInteractable>()?.IsComplaintRemoval == true,
                locked = tray.NetworkCarryLocked, burnt = tray.ContainsBurntFood,
                position = tray.transform.position, rotation = tray.transform.rotation, localPosition = tray.transform.localPosition, localRotation = tray.transform.localRotation });
        }
        SendPoses(state.workers, state.trolleys);
        // Only existence/visibility belongs in the reliable baseline. Animation
        // and movement have their own sequenced transport.
        foreach (var pose in state.workers) StripMotion(pose);
        foreach (var pose in state.trolleys) StripMotion(pose);
        foreach (var tray in state.trays)
            if (!string.IsNullOrEmpty(tray.parent)) { tray.position = Vector3.zero; tray.rotation = Quaternion.identity; }
        string json = JsonUtility.ToJson(state);
        if (json == previousObjects) return;
        previousObjects = json; state.revision = ++revision;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [Key] = JsonUtility.ToJson(state) });
    }
    private static void StripMotion(Pose pose)
    { pose.position = default; pose.rotation = Quaternion.identity; pose.speed = 0; pose.moving = pose.carrying = false; }
    private void ApplyPose(GameObject root, Pose pose, bool worker)
    {
        if (worker)
        {
            var bot = root.GetComponent<AutonomousStaffBot>(); if (bot != null) bot.enabled = false;
            var agent = root.GetComponent<NavMeshAgent>(); if (agent != null) agent.enabled = false;
            var crowd = root.GetComponent<CrowdNavigationAgent>(); if (crowd != null) crowd.enabled = false;
        }
        root.transform.SetPositionAndRotation(pose.position, pose.rotation);
        var animator = Animation(root);
        animator.Set(Speed, pose.speed); animator.Set(Moving, pose.moving); animator.Set(Carrying, pose.carrying);
    }
    private void Apply()
    {
        using var measurement = MultiplayerDiagnostics.ObjectState.Auto();
        if (PhotonNetwork.CurrentRoom.CustomProperties[Key] is not string json || json.Length > 524288) return;
        if (json != readObjects)
        {
            try { pendingObjects = JsonUtility.FromJson<Snapshot>(json); } catch (ArgumentException) { return; }
            readObjects = json;
        }
        var snapshot = pendingObjects;
        if (snapshot?.trays == null || snapshot.run != session.RunId || snapshot.day != GameFlowManager.Instance.CurrentDay
            || snapshot.revision <= received) return;
        bool unresolved = false;
        if (snapshot.workers != null) foreach (var pose in snapshot.workers)
        { var root = ResolvePoseRoot(pose, true); if (root == null) unresolved = true; else root.SetActive(pose.active); }
        if (snapshot.trolleys != null) foreach (var pose in snapshot.trolleys)
        { var root = ResolvePoseRoot(pose, false); if (root == null) unresolved = true; else root.SetActive(pose.active); }
        var trolleys = MultiplayerWorldRegistry.All<BotTrolleyCarrier>();
        var kitchen = MultiplayerWorldRegistry.Kitchen;
        if (kitchen == null || kitchen.foodTrayPrefab == null) return;
        var present = new HashSet<int>();
        foreach (var state in snapshot.trays)
        {
            if (state == null) continue;
            present.Add(state.order);
            if (state.group > 0 && MultiplayerServiceActions.Resolve(state.group) == null) { unresolved = true; continue; }
            state.parent ??= string.Empty;
            observedTrays.TryGetValue(state.order, out var tray);
            tray = tray != null ? tray : kitchen.GetPreparedResult(state.order);
            if (tray == null)
            {
                tray = Instantiate(kitchen.foodTrayPrefab, state.position, state.rotation);
                foreach (var body in tray.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
                tray.PresentNetworkData(MultiplayerServiceActions.Resolve(state.group), state.order, state.products, state.burnt);
                kitchen.RegisterObservedTray(tray);
            }
            observedTrays[state.order] = tray;
            if (tray.TargetGroup == null && state.group > 0 && MultiplayerServiceActions.Resolve(state.group) != null)
                tray.PresentNetworkData(MultiplayerServiceActions.Resolve(state.group), state.order, state.products, state.burnt);
            tray.NetworkCarryLocked = state.locked || state.parent.StartsWith("waiter:") || state.parent.StartsWith("busser:") || state.parent.StartsWith("trolley:");
            tray.SetContainsBurntFood(state.burnt);
            Transform parent = null;
            int preparedSlot = -1;
            if (state.parent.StartsWith("waiter:") || state.parent.StartsWith("busser:"))
            {
                var root = ActorRoot(state.parent.Substring(7));
                bool dirty = state.parent.StartsWith("busser:");
                if (root != null)
                {
                    if (dirty) { var hands = root.GetComponent<BusserHands>(); if (hands != null) { hands.holdingTray = tray; parent = hands.TrayHoldPoint; } }
                    else { var hands = root.GetComponent<WaiterHands>(); if (hands != null) { hands.holdingTray = tray; parent = hands.TrayHoldPoint; } }
                }
            }
            else if (state.parent.StartsWith("booth:")) parent = MultiplayerCustomerInteractionBridge.ResolveBooth(state.parent.Substring(6))?.NetworkTrayPoint;
            else if (state.parent.StartsWith("kitchen:") && int.TryParse(state.parent.Substring(8), out int slotIndex)
                && kitchen.traySpawnPoints != null && slotIndex >= 0 && slotIndex < kitchen.traySpawnPoints.Length)
            { preparedSlot = slotIndex; parent = kitchen.traySpawnPoints[slotIndex]; }
            else if (state.parent.StartsWith("trolley:"))
            {
                string[] parts = state.parent.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[1], out int effect) && int.TryParse(parts[2], out int slot))
                    foreach (var trolley in trolleys) if ((int)trolley.Effect == effect && slot >= 0 && slot < trolley.TraySlots.Count) parent = trolley.TraySlots[slot];
            }
            var oldWaiter = tray.GetComponentInParent<WaiterHands>(true);
            var oldBusser = tray.GetComponentInParent<BusserHands>();
            if (oldWaiter != null && (parent == null || !parent.IsChildOf(oldWaiter.transform)) && oldWaiter.holdingTray == tray) oldWaiter.holdingTray = null;
            if (oldBusser != null && (parent == null || !parent.IsChildOf(oldBusser.transform)) && oldBusser.holdingTray == tray) oldBusser.holdingTray = null;
            if (parent == null && !string.IsNullOrEmpty(state.parent)) { unresolved = true; continue; }
            if (parent != null) WaiterHands.AttachKeepingWorldScale(tray.transform, parent, state.localPosition, state.localRotation);
            else { tray.transform.SetParent(null, true); tray.transform.SetPositionAndRotation(state.position, state.rotation); }
            kitchen.RegisterObservedTray(tray, preparedSlot);
            if (state.parent.StartsWith("booth:") && tray.TargetGroup != null && tray.TargetGroup.state == CustomerGroup.GroupState.Eating)
                tray.TargetGroup.PresentObservedServed(tray);
            var pickup = tray.GetComponent<FoodTrayInteractable>();
            if (pickup != null && preparedSlot >= 0 && !state.cleanup && !state.locked && !pickup.IsDeliveryPickable)
                pickup.SetDeliveryPickable(null);
            if (pickup != null && pickup.IsCleanupPickable != state.cleanup) pickup.SetCleanupPickable(state.cleanup);
            if (pickup != null && state.complaintRemoval && !pickup.IsComplaintRemoval) pickup.MarkForComplaintRemoval();
        }
        foreach (int order in new List<int>(observedTrays.Keys)) if (!present.Contains(order))
        {
            var tray = observedTrays[order];
            if (tray != null) { var w = tray.GetComponentInParent<WaiterHands>(true); if (w != null && w.holdingTray == tray) w.holdingTray = null;
                var b = tray.GetComponentInParent<BusserHands>(); if (b != null && b.holdingTray == tray) b.ClearTray(); Destroy(tray.gameObject); }
            observedTrays.Remove(order);
        }
        if (!unresolved) { received = snapshot.revision; pendingObjects = null; }
    }
}
