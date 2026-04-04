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

    private readonly HashSet<int> cookingOrders = new HashSet<int>();
    private TrayPickupQueue pickupQueue;

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

    public void ProcessOrder(CustomerGroup group)
    {
        if (group == null)
            return;

        int orderNo = group.currentOrderNumber;
        if (orderNo < 0)
            return;

        if (group.state != CustomerGroup.GroupState.OrderTaken)
            return;

        if (!cookingOrders.Add(orderNo))
            return;

        StartCoroutine(CookAndSpawn(group, orderNo));
    }

    private IEnumerator CookAndSpawn(CustomerGroup group, int orderNo)
    {
        try
        {
            yield return new WaitForSeconds(2f);

            bool isTakeout = group.IsTakeout;

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

            while (freeSlot == null)
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

            if (isTakeout)
            {
                GameObject bag = Instantiate(takeoutBagPrefab, freeSlot.position, freeSlot.rotation, freeSlot);

                TakeoutBagMarker marker = bag.GetComponent<TakeoutBagMarker>();
                if (marker != null)
                    marker.Init(group);

                TakeoutBagInteractable bagInteractable = bag.GetComponent<TakeoutBagInteractable>();
                if (bagInteractable != null)
                    bagInteractable.Init(group);
                else
                    Debug.LogWarning("[KitchenManager] TakeoutBagInteractable missing on PaperBag prefab — deliveredContents will be empty.");

                TakeoutFlowManager.Instance?.NotifyBagReady(group);

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

                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();
            }
        }
        finally
        {
            cookingOrders.Remove(orderNo);
        }
    }

    private bool IsOrderStillValid(CustomerGroup group, int orderNo)
    {
        if (group == null)
            return false;

        if (group.currentOrderNumber != orderNo)
            return false;

        if (group.state != CustomerGroup.GroupState.OrderTaken)
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

            if (slot.childCount == 0)
                return slot;
        }

        return null;
    }
}