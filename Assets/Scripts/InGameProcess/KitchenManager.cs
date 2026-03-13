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

    [Header("Queueing")]
    [SerializeField] private float waitForFreeSlotCheckInterval = 0.25f;

    private readonly HashSet<int> cookingOrders = new HashSet<int>();
    private TrayPickupQueue pickupQueue;

    private void Awake()
    {
        pickupQueue = GetComponent<TrayPickupQueue>();
        if (pickupQueue == null)
            pickupQueue = gameObject.AddComponent<TrayPickupQueue>();
    }

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

        try
        {
            if (!IsOrderStillValid(group, orderNo))
                yield break;

            if (traySpawnPoints == null || traySpawnPoints.Length == 0) yield break;
            if (foodTrayPrefab == null) yield break;

            Transform freeSlot = null;

            while (freeSlot == null)
            {
                if (!IsOrderStillValid(group, orderNo))
                    yield break;

                freeSlot = GetFirstFreeSlot();

                if (freeSlot == null)
                    yield return new WaitForSeconds(waitForFreeSlotCheckInterval);
            }

            var tray = Instantiate(foodTrayPrefab, freeSlot.position, freeSlot.rotation, freeSlot);
            tray.Init(group);

            var it = tray.GetComponent<FoodTrayInteractable>();
            if (it != null)
                it.SetDeliveryPickable(pickupQueue);
        }
        finally
        {
            cookingOrders.Remove(orderNo);
        }
    }

    private bool IsOrderStillValid(CustomerGroup group, int orderNo)
    {
        if (group == null) return false;
        if (group.currentOrderNumber != orderNo) return false;
        if (group.state != CustomerGroup.GroupState.OrderTaken) return false;
        return true;
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