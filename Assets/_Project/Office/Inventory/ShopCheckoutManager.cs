using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ShopCheckoutManager : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public ShopManager shopManager;
    public GameObject receiptPanel;
    public TMP_Text receiptText;
    public TMP_Text totalCostText;

    public void Checkout()
    {
        if (!shopManager) return;

        List<ReceiptItem> receiptItems = new List<ReceiptItem>();
        int totalCost = 0;

        // STEP 1: Collect data
        foreach (var itemUI in shopManager.GetSpawnedItems())
        {
            if (itemUI.Quantity <= 0) continue;

            int cost = itemUI.Quantity *
                       CasualDiningPolishManager.GetCurrentBoxCostOrBase(itemUI.ItemData);
            totalCost += cost;

            receiptItems.Add(new ReceiptItem
            {
                name = itemUI.ItemData.displayName,
                quantity = itemUI.Quantity,
                cost = cost
            });
        }

        if (totalCost == 0)
        {
            Debug.Log("Nothing selected for purchase.");
            return;
        }

        // STEP 2: Spend money
        if (!MoneyManager.Instance.Spend(totalCost))
        {
            Debug.Log("Not enough money.");
            return;
        }

        DailyRevenueTracker.Instance?.RecordIngredientCost(totalCost);

        // STEP 3: Add to inventory
        foreach (var itemUI in shopManager.GetSpawnedItems())
        {
            if (itemUI.Quantity <= 0) continue;
            int totalUnits = itemUI.Quantity * itemUI.ItemData.unitsPerBox;
            InventoryManager.Instance.AddStock(itemUI.ItemData.itemType, totalUnits);
            itemUI.ResetQuantity();
        }

        // STEP 4: Show receipt
        PopulateReceipt(receiptItems, totalCost);

        // STEP 5: Refresh UI
        shopManager.RebuildShop();
        UpdateTotalCost();
    }

    public void UpdateTotalCost()
    {
        if (!shopManager || !totalCostText) return;

        int total = 0;
        foreach (var itemUI in shopManager.GetSpawnedItems())
        {
            total += itemUI.Quantity * itemUI.ItemData.boxCost;
        }

        totalCostText.text = $"₱{total}";
    }

    public void CloseReceipt()
    {
        if (receiptPanel)
            receiptPanel.SetActive(false);
    }

    private void PopulateReceipt(List<ReceiptItem> items, int totalCost)
    {
        if (!receiptPanel || !receiptText) return;

        receiptPanel.SetActive(true);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var item in items)
        {
            sb.AppendLine($"{item.name} x{item.quantity} = ₱{item.cost}");
        }
        sb.AppendLine("\n------------------");
        sb.AppendLine($"TOTAL: ₱{totalCost}");
        receiptText.text = sb.ToString();
    }
}
