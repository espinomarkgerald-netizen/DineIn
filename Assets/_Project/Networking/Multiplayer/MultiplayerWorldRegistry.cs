using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public static class MultiplayerWorldRegistry
{
    private static readonly Dictionary<Type, IList> lists = new();
    private static readonly HashSet<Component> tracked = new();
    private static KitchenManager kitchen;
    public static KitchenManager Kitchen => kitchen != null ? kitchen : kitchen = UnityEngine.Object.FindFirstObjectByType<KitchenManager>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { lists.Clear(); tracked.Clear(); kitchen = null; }
    public static List<T> All<T>() where T : Component
    {
        if (lists.TryGetValue(typeof(T), out var existing)) return (List<T>)existing;
        var result = new List<T>(UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        lists.Add(typeof(T), result);
        foreach (var item in result.ToArray()) Track(item);
        return result;
    }
    public static void Track(Component value)
    {
        if (value == null || !tracked.Add(value)) return;
        foreach (var list in lists)
            if (list.Key.IsInstanceOfType(value) && !list.Value.Contains(value)) list.Value.Add(value);
        var life = value.GetComponent<MultiplayerWorldLifetime>();
        if (life == null) life = value.gameObject.AddComponent<MultiplayerWorldLifetime>();
        life.Track(value);
    }
    internal static void Forget(Component value)
    {
        tracked.Remove(value);
        foreach (var list in lists.Values) list.Remove(value);
    }
    public static MoneyPickup MoneyFor(CustomerGroup group)
    { foreach (var item in All<MoneyPickup>()) if (item != null && item.gameObject.activeInHierarchy && item.TargetGroup == group) return item; return null; }
    public static string HolderId(WaiterHands hands)
    {
        if (hands == null) return "";
        var manager = hands.GetComponent<ManagerPlayer>();
        var view = hands.GetComponent<PhotonView>();
        return manager != null && view != null ? "player:" + view.OwnerActorNr : "staff:" + hands.name;
    }
    public static WaiterHands ResolveHolder(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (id.StartsWith("player:") && int.TryParse(id.Substring(7), out int actor))
            return MultiplayerSessionManager.Instance.TryGetManager(actor, out var root) ? root.GetComponent<WaiterHands>() : null;
        foreach (var hands in All<WaiterHands>()) if (hands != null && HolderId(hands) == id) return hands;
        return null;
    }
}
