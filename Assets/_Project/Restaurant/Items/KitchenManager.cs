using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KitchenManager : MonoBehaviour
{
    [Header("Dine-In Spawn Points")]
    public Transform[] traySpawnPoints;
    public FoodTray foodTrayPrefab;

    [Header("Takeout Spawn Points")]
    [SerializeField] private Transform[] takeoutSpawnPoints;
    [SerializeField] private GameObject takeoutBagPrefab;

    [Header("Timing")]
    public float cookSeconds = 5f;

    [Header("Queueing")]
    [SerializeField] private float waitForFreeSlotCheckInterval = 0.25f;
    [SerializeField, Min(1f)] private float maxSlotWaitSeconds = 8f;

    private readonly HashSet<int> cookingOrders = new HashSet<int>();
    private readonly HashSet<int> completedOrders = new HashSet<int>();

    private TrayPickupQueue pickupQueue;

    public event Action<CustomerGroup, int> OrderStarted;
    public event Action<CustomerGroup, int, bool> OrderFinished;

    private void Awake()
    {
        pickupQueue = GetComponent<TrayPickupQueue>();
        if (pickupQueue == null)
            pickupQueue = gameObject.AddComponent<TrayPickupQueue>();
    }

    private void Start()
    {
        ApplyKitchenAssignmentCookTime();
    }

    private void ApplyKitchenAssignmentCookTime()
    {
        if (KitchenAssignmentSaveBridge.Instance == null)
        {
            Debug.LogWarning("[KitchenManager] KitchenAssignmentSaveBridge not found. Using default cookSeconds.");
            return;
        }

        cookSeconds = KitchenAssignmentSaveBridge.Instance.GetMealSpawnTime();

        Debug.Log(
            $"[KitchenManager] Applied cookSeconds = {cookSeconds} | " +
            $"Chef: {KitchenAssignmentSaveBridge.Instance.AssignedChefName} ({KitchenAssignmentSaveBridge.Instance.AssignedChefStars}★) | " +
            $"Barista: {KitchenAssignmentSaveBridge.Instance.AssignedBaristaName} ({KitchenAssignmentSaveBridge.Instance.AssignedBaristaStars}★)"
        );
    }

    public bool ProcessOrder(CustomerGroup group)
    {
        if (group == null)
        {
            Debug.LogError("[KitchenManager] ProcessOrder called with null group.");
            return false;
        }

        int orderNo = group.currentOrderNumber;
        if (orderNo < 0)
        {
            Debug.LogError($"[KitchenManager] ProcessOrder — invalid orderNumber ({orderNo}) on {group.name}. Order not started.");
            return false;
        }

        if (group.state != CustomerGroup.GroupState.OrderTaken)
        {
            Debug.LogError($"[KitchenManager] ProcessOrder — {group.name} is in state '{group.state}', expected 'OrderTaken'. Order not started.");
            return false;
        }

        if (completedOrders.Contains(orderNo))
        {
            Debug.LogWarning($"[KitchenManager] Order #{orderNo} already finished spawning. Duplicate call ignored.");
            return false;
        }

        if (!cookingOrders.Add(orderNo))
        {
            Debug.LogWarning($"[KitchenManager] Order #{orderNo} is already being cooked. Duplicate call ignored.");
            return false;
        }

        bool isTakeout = group.IsTakeout;

        Debug.Log($"[KitchenManager] Starting cook for order #{orderNo} — group={group.name} isTakeout={isTakeout}.");
        StartCoroutine(CookAndSpawn(group, orderNo, isTakeout));
        OrderStarted?.Invoke(group, orderNo);
        return true;
    }

    private IEnumerator CookAndSpawn(CustomerGroup group, int orderNo, bool isTakeout)
    {
        bool spawnedSuccessfully = false;

        try
        {
            yield return new WaitForSeconds(2f);

            if (ProcessingBillIndicatorUI.Instance != null)
                ProcessingBillIndicatorUI.Instance.Show("Order #" + orderNo + " is being prepared");

            yield return new WaitForSeconds(cookSeconds);

            if (!IsOrderStillValid(group, orderNo))
            {
                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();
                yield break;
            }

            Transform[] targetSlots = isTakeout ? takeoutSpawnPoints : traySpawnPoints;

            if (targetSlots == null || targetSlots.Length == 0)
            {
                Debug.LogError($"[KitchenManager] No spawn points assigned for {(isTakeout ? "takeout" : "dine-in")} — assign them in the Inspector on KitchenManager.");
                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();
                yield break;
            }

            if (!isTakeout && foodTrayPrefab == null)
            {
                Debug.LogError("[KitchenManager] FoodTray prefab not assigned in Inspector.");
                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();
                yield break;
            }

            if (isTakeout && takeoutBagPrefab == null)
            {
                Debug.LogError("[KitchenManager] Takeout bag prefab not assigned in Inspector — assign PaperBag prefab to KitchenManager.takeoutBagPrefab.");
                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();
                yield break;
            }

            Transform freeSlot = null;
            float slotWaitStarted = Time.time;

            while (freeSlot == null && Time.time - slotWaitStarted < maxSlotWaitSeconds)
            {
                if (!IsOrderStillValid(group, orderNo))
                {
                    if (ProcessingBillIndicatorUI.Instance != null)
                        ProcessingBillIndicatorUI.Instance.Hide();
                    yield break;
                }

                freeSlot = GetFirstFreeSlot(targetSlots);

                if (freeSlot == null)
                    yield return new WaitForSeconds(waitForFreeSlotCheckInterval);
            }

            if (freeSlot == null)
            {
                string serviceType = isTakeout ? "takeout" : "dine-in";
                Debug.LogError($"[KitchenManager] Timed out waiting for a free {serviceType} slot for order #{orderNo}.", this);
                yield break;
            }

            if (isTakeout)
            {
                GameObject bag = Instantiate(takeoutBagPrefab, freeSlot.position, freeSlot.rotation, freeSlot);

                TakeoutBagInteractable requiredInteractable = bag.GetComponent<TakeoutBagInteractable>();
                if (requiredInteractable == null)
                {
                    Debug.LogError("[KitchenManager] Spawned takeout bag is missing TakeoutBagInteractable. Order cannot be delivered.", bag);
                    Destroy(bag);
                    yield break;
                }

                TakeoutBagMarker marker = bag.GetComponent<TakeoutBagMarker>();
                if (marker != null)
                    marker.Init(group);

                requiredInteractable.Init(group);

                TakeoutFlowManager flow = TakeoutFlowManager.Instance;
                if (flow == null || !flow.NotifyBagReady(group))
                {
                    Debug.LogError($"[KitchenManager] Takeout flow rejected the ready bag for order #{orderNo}.", this);
                    Destroy(bag);
                    yield break;
                }

                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.ShowForSeconds("Takeout order #" + orderNo + " is ready for pickup!", 3f);

                Debug.Log($"[KitchenManager] Takeout bag spawned at '{freeSlot.name}' for order #{orderNo}.");
            }
            else
            {
                FoodTray tray = Instantiate(foodTrayPrefab, freeSlot.position, freeSlot.rotation, freeSlot);
                tray.Init(group);

                FoodTrayInteractable it = tray.GetComponent<FoodTrayInteractable>();
                if (it != null)
                    it.SetDeliveryPickable(pickupQueue);
                else
                    Debug.LogWarning("[KitchenManager] FoodTrayInteractable missing on FoodTray prefab.");

                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();

                Debug.Log($"[KitchenManager] Dine-in tray spawned at '{freeSlot.name}' for order #{orderNo}.");
            }

            spawnedSuccessfully = true;
            completedOrders.Add(orderNo);
        }
        finally
        {
            cookingOrders.Remove(orderNo);
            OrderFinished?.Invoke(group, orderNo, spawnedSuccessfully);

            if (!spawnedSuccessfully && ProcessingBillIndicatorUI.Instance != null && cookingOrders.Count == 0)
                ProcessingBillIndicatorUI.Instance.Hide();
        }
    }

    private bool IsOrderStillValid(CustomerGroup group, int orderNo)
    {
        if (group == null)
            return false;

        if (group.currentOrderNumber != orderNo)
            return false;

        if (completedOrders.Contains(orderNo))
            return false;

        return true;
    }

    private Transform GetFirstFreeSlot(Transform[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Transform slot = slots[i];
            if (slot == null)
                continue;

            if (!SlotHasSpawnedOrder(slot))
                return slot;
        }

        return null;
    }

    private bool SlotHasSpawnedOrder(Transform slot)
    {
        for (int i = 0; i < slot.childCount; i++)
        {
            Transform child = slot.GetChild(i);
            if (child == null)
                continue;

            if (child.GetComponent<FoodTray>() != null)
                return true;

            TakeoutBagInteractable bag = child.GetComponent<TakeoutBagInteractable>();
            if (bag != null)
            {
                if (bag.TargetGroup == null)
                {
                    Destroy(child.gameObject);
                    continue;
                }

                return true;
            }

            if (child.GetComponent<TakeoutBagMarker>() != null)
                return true;
        }

        return false;
    }
}
