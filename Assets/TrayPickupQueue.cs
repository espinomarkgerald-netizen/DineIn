using System.Collections.Generic;
using UnityEngine;

public class TrayPickupQueue : MonoBehaviour
{
    private readonly List<FoodTrayInteractable> trays = new();

    public void Register(FoodTrayInteractable tray)
    {
        if (tray == null) return;
        if (!trays.Contains(tray)) trays.Add(tray);
        Refresh();
    }

    public void Unregister(FoodTrayInteractable tray)
    {
        if (tray == null) return;
        trays.Remove(tray);
        Refresh();
    }

    public bool IsNext(FoodTrayInteractable tray)
    {
        if (tray == null) return false;
        if (trays.Count == 0) return false;
        return trays[0] == tray;
    }

    public void OnPicked(FoodTrayInteractable tray)
    {
        Unregister(tray);
    }

    private void Refresh()
    {
        trays.RemoveAll(t => t == null);

        for (int i = 0; i < trays.Count; i++)
        {
            trays[i].SetQueuePickable(i == 0);
        }
    }
}