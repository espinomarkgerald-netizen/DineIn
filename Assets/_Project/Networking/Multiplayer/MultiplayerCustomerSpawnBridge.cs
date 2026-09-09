using Photon.Pun;
using UnityEngine;

[DefaultExecutionOrder(-140)]
public class MultiplayerCustomerSpawnBridge : MonoBehaviour
{
    [SerializeField] private GroupSpawner spawner;
    [SerializeField] private MultiplayerSessionManager session;
    [SerializeField] private string networkPrefab = "MultiplayerCustomerSpawn";
    // Consumed synchronously by PUN's local instantiation callback, never another spawn.
    public CustomerGroup PendingGroup { get; private set; }

    private void OnEnable()
    {
        spawner.SpawnPermission = CanSpawn;
        spawner.GroupCreated += PublishGroup;
    }

    private bool CanSpawn() => session != null && session.IsAuthority
        && GameDayManager.Instance != null && GameDayManager.Instance.ShiftRunning;

    private void PublishGroup(CustomerGroup group, bool takeout)
    {
        if (!CanSpawn()) return;
        PendingGroup = group;
        try
        {
            PhotonNetwork.InstantiateRoomObject(networkPrefab, group.transform.position,
                group.transform.rotation, 0,
                new object[] { (int)group.CurrentCustomerType, group.members.Count, takeout });
        }
        finally { PendingGroup = null; }
    }

    private void OnDisable()
    {
        if (spawner == null) return;
        if (spawner.SpawnPermission == (System.Func<bool>)CanSpawn) spawner.SpawnPermission = null;
        spawner.GroupCreated -= PublishGroup;
    }
}
