using UnityEngine;

public enum EquipmentCatalogSection
{
    BoothsAndSeating,
    Upgrades
}

[CreateAssetMenu(menuName = "Game/BuyableEquipment")]
public class Equipment : ScriptableObject
{
    [Header("Item ID")]
    public string itemID;
    [Header("Name")]    
    public string displayName;
    [Header("Cost")]
    public int cost;
    [Header("Unlock")]
    public int dayToUnlock = 1;
    public Sprite sprite;

    [Header("Computer Catalog")]
    public EquipmentCatalogSection catalogSection = EquipmentCatalogSection.BoothsAndSeating;
    public int catalogSortOrder;
    [TextArea(2, 4)] public string description;
    [Header("Big Boss Unlock Guide")]
    [Tooltip("Optional usage advice after the daily unlock cards. Falls back to Description.")]
    [TextArea(2, 5)] public string coachTip;
    public string coachLocation = "Computer > Equipment";

    public static int CompareProgression(Equipment a, Equipment b)
    {
        if (a == b) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int day = Mathf.Max(1, a.dayToUnlock).CompareTo(Mathf.Max(1, b.dayToUnlock));
        if (day != 0) return day;
        int order = a.catalogSortOrder.CompareTo(b.catalogSortOrder);
        return order != 0 ? order : System.StringComparer.Ordinal.Compare(a.itemID, b.itemID);
    }
}
