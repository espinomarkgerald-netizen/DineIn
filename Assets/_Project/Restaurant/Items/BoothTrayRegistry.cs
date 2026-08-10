using UnityEngine;

public class BoothTrayRegistry : MonoBehaviour
{
    [Header("Runtime")]
    [SerializeField] private FoodTrayInteractable currentTray;

    public void RegisterTray(FoodTrayInteractable tray)
    {
        currentTray = tray;
        if (currentTray != null)
            currentTray.SetCleanupPickable(false); // not cleanable yet while eating
    }

    public void EnableCleanupPickup()
    {
        if (currentTray != null)
            currentTray.SetCleanupPickable(true);
    }

    public void DisableCleanupPickup()
    {
        if (currentTray != null)
            currentTray.SetCleanupPickable(false);
    }

    public void ClearTrayIfMatches(FoodTrayInteractable tray)
    {
        if (currentTray == tray)
            currentTray = null;
    }

    // Optional: if you want it to auto-detect a tray already on the table
    public void TryAutoFindTrayOnTable()
    {
        var t = GetComponentInChildren<FoodTrayInteractable>(true);
        if (t != null) currentTray = t;
    }
}