using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KitchenManager : MonoBehaviour
{
    [Header("Spawn Points")]
    public Transform[] traySpawnPoints;
    public FoodTray foodTrayPrefab;

    [Header("Timing")]
    public float cookSeconds = 5f;

    private readonly HashSet<int> cookingOrders = new HashSet<int>();

    public void ProcessOrder(CustomerGroup group)
    {
        if (group == null) return;

        int orderNo = group.currentOrderNumber;
        if (orderNo < 0) return;

        if (group.state != CustomerGroup.GroupState.OrderTaken) return;

        if (!cookingOrders.Add(orderNo))
            return;

        StartCoroutine(CookAndSpawn(group, orderNo));
    }

    private IEnumerator CookAndSpawn(CustomerGroup group, int orderNo)
    {
        yield return new WaitForSeconds(cookSeconds);

        if (group == null)
        {
            cookingOrders.Remove(orderNo);
            yield break;
        }

        if (group.currentOrderNumber != orderNo || group.state != CustomerGroup.GroupState.OrderTaken)
        {
            cookingOrders.Remove(orderNo);
            yield break;
        }

        if (traySpawnPoints == null || traySpawnPoints.Length == 0)
        {
            cookingOrders.Remove(orderNo);
            yield break;
        }

        if (foodTrayPrefab == null)
        {
            cookingOrders.Remove(orderNo);
            yield break;
        }

        Transform freeSlot = GetFirstFreeSlot();
        if (freeSlot == null)
        {
            cookingOrders.Remove(orderNo);
            yield break;
        }

        var tray = Instantiate(foodTrayPrefab, freeSlot.position, freeSlot.rotation, freeSlot);
        tray.Init(group);

        cookingOrders.Remove(orderNo);
    }

    private Transform GetFirstFreeSlot()
    {
        for (int i = 0; i < traySpawnPoints.Length; i++)
        {
            Transform slot = traySpawnPoints[i];
            if (slot == null) continue;

            FoodTray existingTray = slot.GetComponentInChildren<FoodTray>(true);
            if (existingTray == null)
                return slot;
        }

        return null;
    }
}