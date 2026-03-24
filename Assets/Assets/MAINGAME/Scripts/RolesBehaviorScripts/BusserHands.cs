using System;
using UnityEngine;

public class BusserHands : MonoBehaviour
{
    public static BusserHands Instance { get; private set; }

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

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        holdingTray = null;

        NotifyHandsChanged();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void NotifyHandsChanged()
    {
        OnHandsStateChanged?.Invoke();
    }

    public void ClearTray()
    {
        holdingTray = null;
        NotifyHandsChanged();
    }

    public bool PickupTray(FoodTray tray)
    {
        if (tray == null) return false;
        if (HasTray) return false;

        Transform parent = TrayHoldPoint;
        if (parent == null)
        {
            Debug.LogError("[BusserHands] TrayHoldPoint is NULL.");
            return false;
        }

        holdingTray = tray;

        tray.transform.SetParent(parent, false);
        tray.transform.localPosition = Vector3.zero;
        tray.transform.localRotation = Quaternion.identity;

        var col = tray.GetComponentInChildren<Collider>(true);
        if (col != null) col.enabled = false;

        NotifyHandsChanged();
        return true;
    }

    public void DisposeTray(bool destroyObject = true)
    {
        var tray = holdingTray;
        holdingTray = null;

        if (destroyObject && tray != null)
            Destroy(tray.gameObject);

        NotifyHandsChanged();
    }
}