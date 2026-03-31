using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class ShopCheckoutManager : MonoBehaviour
{
    [Header("References")]
    public ShopManager shopManager;          // Reference to ShopManager
    public GameObject receiptPanel;          // Panel to display receipt
    public TMP_Text receiptText;             // Text component for receipt
    public TMP_Text totalCostText;

    public void Checkout()
    {
        List<ReceiptItem> receiptItems = new List<ReceiptItem>();
        int totalCost = 0;

        // STEP 1: Gather data
        foreach (var itemUI in shopManager.GetSpawnedItems())
        {
            if (itemUI.Quantity <= 0) continue;

            int cost = itemUI.Quantity * itemUI.ItemData.boxCost;

            receiptItems.Add(new ReceiptItem
            {
                name = itemUI.ItemData.displayName,
                quantity = itemUI.Quantity,
                cost = cost
            });

            totalCost += cost;
        }

        // If nothing selected
        if (totalCost == 0)
        {
            Debug.Log("Nothing to buy");
            return;
        }

        // STEP 2: Spend money FIRST
        if (!MoneyManager.Instance.Spend(totalCost))
        {
            Debug.Log("Not enough money");
            return;
        }

        // STEP 3: Apply purchase
        foreach (var itemUI in shopManager.GetSpawnedItems())
        {
            if (itemUI.Quantity <= 0) continue;

            int totalUnits = itemUI.Quantity * itemUI.ItemData.unitsPerBox;
            InventoryManager.Instance.AddStock(itemUI.ItemData.itemType, totalUnits);

            itemUI.ResetQuantity();
        }

        // STEP 4: Show receipt
        PopulateReceipt(receiptItems, totalCost);

        // STEP 5: Refresh UI AFTER everything
        shopManager.RefreshShop();
        UpdateTotalCost();
    }

    public void UpdateTotalCost()
    {
        int total = 0;

        foreach (var itemUI in shopManager.GetSpawnedItems())
        {
            if (itemUI.Quantity <= 0) continue;

            total += itemUI.Quantity * itemUI.ItemData.boxCost;
        }

        totalCostText.text = $"₱{total}";
    }

    public void CloseReceipt()
    {
        receiptPanel.SetActive(false);
    }

    public void PopulateReceipt(List<ReceiptItem> items, int totalCost)
    {
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