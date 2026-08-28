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
    [TextArea(2, 4)] public string description;
}
