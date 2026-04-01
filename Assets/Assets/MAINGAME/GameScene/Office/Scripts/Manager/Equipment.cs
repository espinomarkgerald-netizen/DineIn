using UnityEngine;

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
}