using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItemUI : MonoBehaviour
{
    [Header("UI References")]
    public Image ingredientImage;
    public TMP_Text nameText;
    public TMP_Text stockText;
    public TMP_Text quantityText;
    public Button plusButton;
    public Button minusButton;
    public TMP_Text totalPriceText;
    public Action OnQuantityChanged;

    private ItemData itemData;
    private int quantity = 0;

    public int Quantity => quantity;
    public ItemData ItemData => itemData;

    public void Setup(ItemData data, bool unlocked)
    {
        itemData = data;
        quantity = 0;

        if (plusButton != null)
        {
            plusButton.onClick.RemoveAllListeners();
            plusButton.onClick.AddListener(() => ChangeQuantity(1));
        }

        if (minusButton != null)
        {
            minusButton.onClick.RemoveAllListeners();
            minusButton.onClick.AddListener(() => ChangeQuantity(-1));
        }

        UnsubscribeInventory();
        SubscribeInventory();

        bool availableNow = IsAvailableNow(unlocked);

        if (availableNow)
        {
            if (ingredientImage != null)
                ingredientImage.sprite = data != null ? data.sprite : null;

            if (nameText != null)
                nameText.text = data != null ? data.displayName : string.Empty;

            if (plusButton != null)
                plusButton.interactable = true;

            if (minusButton != null)
                minusButton.interactable = true;
        }
        else
        {
            if (ingredientImage != null)
                ingredientImage.sprite = null;

            if (nameText != null)
                nameText.text = data != null ? $"Unlock at Day {data.dayToUnlock}" : "Locked";

            if (plusButton != null)
                plusButton.interactable = false;

            if (minusButton != null)
                minusButton.interactable = false;
        }

        UpdateStock();
        UpdateQuantity();
        UpdatePriceDisplay();
    }

    private void OnDestroy()
    {
        UnsubscribeInventory();
    }

    private void OnDisable()
    {
        UnsubscribeInventory();
    }

    private void OnEnable()
    {
        SubscribeInventory();
    }

    private void SubscribeInventory()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnStockChanged += OnStockChanged;
    }

    private void UnsubscribeInventory()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnStockChanged -= OnStockChanged;
    }

    private bool IsAvailableNow(bool unlockedFromCaller)
    {
        if (itemData == null)
            return false;

        if (unlockedFromCaller)
            return true;

        if (UnlockManager.Instance != null && UnlockManager.Instance.IsIngredientUnlocked(itemData))
            return true;

        int currentDay = GetCurrentDaySafe();
        return currentDay >= itemData.dayToUnlock;
    }

    private int GetCurrentDaySafe()
    {
        if (GameFlowManager.Instance == null)
            return 1;

        object manager = GameFlowManager.Instance;
        Type type = manager.GetType();

        string[] propertyNames =
        {
            "CurrentDay",
            "currentDay",
            "Day",
            "CurrentDayIndex"
        };

        foreach (string propertyName in propertyNames)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(int))
                return Mathf.Max(1, (int)property.GetValue(manager));
        }

        string[] fieldNames =
        {
            "currentDay",
            "CurrentDay",
            "day",
            "currentDayIndex"
        };

        foreach (string fieldName in fieldNames)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int))
                return Mathf.Max(1, (int)field.GetValue(manager));
        }

        return 1;
    }

    private void ChangeQuantity(int delta)
    {
        if (itemData == null)
            return;

        quantity += delta;

        if (quantity < 0)
            quantity = 0;

        UpdateQuantity();
        UpdatePriceDisplay();
        OnQuantityChanged?.Invoke();
    }

    private void UpdateQuantity()
    {
        if (quantityText != null)
            quantityText.text = quantity.ToString();
    }

    public void ResetQuantity()
    {
        quantity = 0;
        UpdateQuantity();
        UpdatePriceDisplay();
    }

    private void UpdateStock()
    {
        if (stockText == null)
            return;

        if (itemData == null || InventoryManager.Instance == null)
        {
            stockText.text = "Stock: 0";
            return;
        }

        int stock = InventoryManager.Instance.GetStock(itemData.itemType);
        stockText.text = "Stock: " + stock;
    }

    private void UpdatePriceDisplay()
    {
        if (totalPriceText == null)
            return;

        if (itemData == null)
        {
            totalPriceText.text = "₱0";
            return;
        }

        int totalPrice = quantity * itemData.boxCost;
        totalPriceText.text = $"₱{totalPrice}";
    }

    private void OnStockChanged(ItemType type, int newStock)
    {
        if (itemData == null)
            return;

        if (type == itemData.itemType)
            UpdateStock();
    }

    public void RefreshDisplay()
    {
        bool availableNow = IsAvailableNow(false);

        if (itemData != null)
        {
            if (availableNow)
            {
                if (ingredientImage != null)
                    ingredientImage.sprite = itemData.sprite;

                if (nameText != null)
                    nameText.text = itemData.displayName;

                if (plusButton != null)
                    plusButton.interactable = true;

                if (minusButton != null)
                    minusButton.interactable = true;
            }
            else
            {
                if (ingredientImage != null)
                    ingredientImage.sprite = null;

                if (nameText != null)
                    nameText.text = $"Unlock at Day {itemData.dayToUnlock}";

                if (plusButton != null)
                    plusButton.interactable = false;

                if (minusButton != null)
                    minusButton.interactable = false;

                quantity = 0;
            }
        }

        UpdateStock();
        UpdateQuantity();
        UpdatePriceDisplay();
    }
}