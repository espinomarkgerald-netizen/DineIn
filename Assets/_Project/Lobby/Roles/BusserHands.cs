using System;
using UnityEngine;

public class BusserHands : MonoBehaviour
{
    public static BusserHands Instance { get; private set; }

    public static BusserHands ActivePlayerHands
    {
        get
        {
            if (ManagerPlayer.Active != null)
            {
                BusserHands managerHands = ManagerPlayer.Active.GetComponent<BusserHands>();
                if (managerHands != null)
                    return managerHands;
            }

            return Instance;
        }
    }

    public static event Action OnHandsStateChanged;

    [Header("Holding")]
    public FoodTray holdingTray;

    [Header("Hold Points")]
    [SerializeField] private Transform trayHoldPoint;

    public bool HasTray => holdingTray != null;
    public Transform TrayHoldPoint => trayHoldPoint != null ? trayHoldPoint : transform;

    private void Awake()
    {
        Debug.Log($"[BusserHands] Awake on {name} id={GetInstanceID()}");

        bool belongsToManager = GetComponent<ManagerPlayer>() != null;
        if (!belongsToManager && Instance != null && Instance != this)
        {
            Debug.LogWarning($"[BusserHands] Duplicate staff instance ignored on {name}");
            enabled = false;
            return;
        }

        if (!belongsToManager)
            Instance = this;
        holdingTray = null;

        NotifyHandsChanged();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static BusserHands For(PlayerMovement mover)
    {
        if (mover != null)
        {
            BusserHands ownedHands = mover.GetComponent<BusserHands>();
            if (ownedHands != null)
                return ownedHands;
        }

        return ActivePlayerHands;
    }

    private void NotifyHandsChanged()
    {
        OnHandsStateChanged?.Invoke();
    }

    public void ClearTray()
    {
        Debug.Log("[BusserHands] ClearTray");
        FoodTray completedTray = holdingTray;
        holdingTray = null;
        RestaurantTaskClaim.Complete(completedTray);
        NotifyHandsChanged();
    }

    public bool PickupTray(FoodTray tray)
    {
        if (tray == null)
        {
            Debug.LogWarning("[BusserHands] PickupTray failed: tray is null");
            return false;
        }

        if (HasTray)
        {
            Debug.LogWarning("[BusserHands] PickupTray failed: already holding a tray");
            return false;
        }

        Transform parent = TrayHoldPoint;
        if (parent == null)
        {
            Debug.LogError("[BusserHands] PickupTray failed: TrayHoldPoint is null");
            return false;
        }

        holdingTray = tray;

        WaiterHands.AttachKeepingWorldScale(
            tray.transform,
            parent,
            Vector3.zero,
            Quaternion.identity);
        WaiterHands.SetAllColliders(tray.gameObject, false);

        Debug.Log($"[BusserHands] PickupTray success: {tray.name}");

        NotifyHandsChanged();
        return true;
    }

    public void DisposeTray(bool destroyObject = true)
    {
        var tray = holdingTray;
        holdingTray = null;

        RestaurantTaskClaim.Complete(tray);

        if (destroyObject && tray != null)
            Destroy(tray.gameObject);

        Debug.Log("[BusserHands] DisposeTray");

        NotifyHandsChanged();
    }
}
