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
    public TMP_Text totalPriceText; // NEW
    public System.Action OnQuantityChanged;

    private ItemData itemData;
    private int quantity = 0;
    public int Quantity => quantity;
    public ItemData ItemData => itemData;

    public void Setup(ItemData data, bool unlocked)
    {
        itemData = data;

        plusButton.onClick.RemoveAllListeners();
        minusButton.onClick.RemoveAllListeners();

        if (unlocked)
        {
            ingredientImage.sprite = data.sprite;
            nameText.text = data.displayName;

            plusButton.interactable = true;
            minusButton.interactable = true;

            plusButton.onClick.AddListener(() => ChangeQuantity(1));
            minusButton.onClick.AddListener(() => ChangeQuantity(-1));
        }
        else
        {
            ingredientImage.sprite = null;
            nameText.text = $"Unlock at Day {data.dayToUnlock}";

            plusButton.interactable = false;
            minusButton.interactable = false;
        }

        quantity = 0;

        UpdateStock();
        UpdateQuantity();
        UpdatePriceDisplay();
    }

    void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnStockChanged -= OnStockChanged;
    }

    void ChangeQuantity(int delta)
    {
        Debug.Log("Clicked: " + delta);
        quantity += delta;
        if (quantity < 0) quantity = 0;
        UpdateQuantity();
        UpdatePriceDisplay();

        OnQuantityChanged?.Invoke();        
    }

    void UpdateQuantity()
    {
        quantityText.text = quantity.ToString();
    }

    public void ResetQuantity()
    {
        quantity = 0;
        UpdateQuantity();
        UpdatePriceDisplay();
    }

    void UpdateStock()
    {
        int stock = InventoryManager.Instance.GetStock(itemData.itemType);
        stockText.text = "Stock: " + stock;
    }

    void UpdatePriceDisplay()
    {
        int totalPrice = quantity * itemData.boxCost;
        totalPriceText.text = $"₱{totalPrice}";
    }

    void OnStockChanged(ItemType type, int newStock)
    {
        if (type == itemData.itemType)
            UpdateStock();
    }

    public void RefreshDisplay()
    {
        UpdateStock();
        UpdateQuantity();
        UpdatePriceDisplay();
    }
}