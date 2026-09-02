using UnityEngine;

public enum RestockStorageType
{
    Dry,
    Frozen
}

public enum RestockContainerType
{
    CardboardBox,
    Crate,
    FreezerBox
}

[CreateAssetMenu(fileName = "NewItemData", menuName = "Inventory/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable ID used by restock orders and saves. Leave empty only for legacy items; the ItemType name is used as a fallback.")]
    public string itemID;
    public ItemType itemType;
    [Tooltip("Restaurant catalog this ingredient belongs to.")]
    public RestaurantType restaurantType = RestaurantType.FastFood;

    [Header("Name")]
    public string displayName;

    [Header("Inventory")]
    [Min(1)]
    public int unitsPerBox;

    [Tooltip("Storage room required for normal freshness and capacity accounting.")]
    public RestockStorageType requiredStorage = RestockStorageType.Dry;

    [Tooltip("Reusable visual container used when this item is placed in the stock room.")]
    public RestockContainerType containerType = RestockContainerType.CardboardBox;

    [Tooltip("Cardboard-box or crate prefab. Its label is populated from this item data.")]
    public GameObject worldContainerPrefab;

    [Tooltip("First version uses one physical shelf cell per container.")]
    public Vector2Int gridFootprint = Vector2Int.one;

    [Header("Economy")]
    [Min(0)]
    public int boxCost;

    [Header("Forecast")]
    [Tooltip("Average units expected to be consumed per customer. Used only for restock advice.")]
    [Min(0f)] public float averageUsagePerCustomer = 0.5f;

    [Header("Freshness")]
    [Min(0.1f)] public float shelfLifeDays = 7f;
    [Min(1f)] public float wrongStorageSpoilageMultiplier = 3f;

    [Header("Visuals")]
    public Sprite sprite;
    public int dayToUnlock = 1;

    public string StableItemId => string.IsNullOrWhiteSpace(itemID)
        ? itemType.ToString()
        : itemID.Trim();

    public float CostPerUnit
    {
        get
        {
            if (unitsPerBox == 0) return 0;
            return (float)boxCost / unitsPerBox;
        }
    }

    private void OnValidate()
    {
        itemID = itemID != null ? itemID.Trim() : string.Empty;
        displayName = displayName != null ? displayName.Trim() : string.Empty;
        unitsPerBox = Mathf.Max(1, unitsPerBox);
        boxCost = Mathf.Max(0, boxCost);
        gridFootprint = Vector2Int.one;
        averageUsagePerCustomer = Mathf.Max(0f, averageUsagePerCustomer);
        shelfLifeDays = Mathf.Max(0.1f, shelfLifeDays);
        wrongStorageSpoilageMultiplier = Mathf.Max(1f, wrongStorageSpoilageMultiplier);
    }
}
