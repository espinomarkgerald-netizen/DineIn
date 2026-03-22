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
    public Button buyButton;
    public TMP_Text buyButtonText;

    private ItemData itemData;
    private int quantity = 0;

    public void Setup(ItemData data)
    {
        itemData = data;
        ingredientImage.sprite = itemData.sprite; // Add sprite to ItemData
        nameText.text = data.displayName;
        quantity = 0;

        UpdateStock();
        UpdateQuantity();
        UpdateBuyButton();

        plusButton.onClick.RemoveAllListeners();
        minusButton.onClick.RemoveAllListeners();
        buyButton.onClick.RemoveAllListeners();

        // Subscribe buttons
        plusButton.onClick.AddListener(() => ChangeQuantity(1));
        minusButton.onClick.AddListener(() => ChangeQuantity(-1));
        buyButton.onClick.AddListener(Buy);

        // Subscribe to inventory changes
        InventoryManager.Instance.OnStockChanged += OnStockChanged;
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
        UpdateBuyButton();
    }

    void UpdateQuantity()
    {
        quantityText.text = quantity.ToString();
    }

    void UpdateStock()
    {
        int stock = InventoryManager.Instance.GetStock(itemData.itemType);
        stockText.text = "Stock: " + stock;
    }

    void UpdateBuyButton()
    {
        int totalPrice = quantity * itemData.boxCost;
        buyButtonText.text = $"₱{totalPrice}";
    }

    void Buy()
    {
        if (quantity <= 0) return;

        int totalPrice = quantity * itemData.boxCost; // COST PER BOX
        int totalUnits = quantity * itemData.unitsPerBox; // CONVERT TO UNITS

        if (MoneyManager.Instance.Spend(totalPrice))
        {
            InventoryManager.Instance.AddStock(itemData.itemType, totalUnits); // ADD UNITS
            quantity = 0;

            UpdateQuantity();
            UpdateBuyButton();
        }
        else
        {
            Debug.Log("Not enough money");
        }
    }

    void OnStockChanged(ItemType type, int newStock)
    {
        if (type == itemData.itemType)
            UpdateStock();
    }
}