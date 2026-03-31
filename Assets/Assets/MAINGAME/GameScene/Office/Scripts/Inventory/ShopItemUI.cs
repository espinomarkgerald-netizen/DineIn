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

    public void Setup(ItemData data)
    {
        itemData = data;
        ingredientImage.sprite = itemData.sprite; // Add sprite to ItemData
        nameText.text = data.displayName;
        quantity = 0;

        UpdateStock();
        UpdateQuantity();
        UpdatePriceDisplay();

        plusButton.onClick.RemoveAllListeners();
        minusButton.onClick.RemoveAllListeners();

        // Subscribe buttons
        plusButton.onClick.AddListener(() => ChangeQuantity(1));
        minusButton.onClick.AddListener(() => ChangeQuantity(-1));

        // Subscribe to inventory changes
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnStockChanged -= OnStockChanged;
            InventoryManager.Instance.OnStockChanged += OnStockChanged;
        }
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