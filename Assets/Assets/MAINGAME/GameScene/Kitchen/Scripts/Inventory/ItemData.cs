using UnityEngine;

[CreateAssetMenu(fileName = "NewItemData", menuName = "Inventory/ItemData")]
public class ItemData : ScriptableObject
{
    public ItemType itemType;

    [Header("Name")]
    public string displayName;

    [Header("Inventory")]
    public int unitsPerBox;

    [Header("Economy")]
    public int boxCost;

    [Header("Visuals")]
    public Sprite sprite;
    

    public float CostPerUnit
    {
        get
        {
            if (unitsPerBox == 0) return 0;
            return (float)boxCost / unitsPerBox;
        }
    }
}