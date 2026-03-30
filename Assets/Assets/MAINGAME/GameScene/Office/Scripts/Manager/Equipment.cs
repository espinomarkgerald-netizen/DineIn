using UnityEngine;

[CreateAssetMenu(menuName = "Game/BuyableEquipment")]
public class Equipment : ScriptableObject
{
    public string itemID;
    public string displayName;
    public int cost;
}