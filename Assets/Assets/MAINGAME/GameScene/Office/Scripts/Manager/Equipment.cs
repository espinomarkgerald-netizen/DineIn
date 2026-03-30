using UnityEngine;

[CreateAssetMenu(menuName = "Game/BuyableEquipment")]
public class Equipment : ScriptableObject
{
    public string itemID;                  // Unique ID for save/load
    public string displayName;             // Name in UI
    public int cost;                       // Purchase price
    public int dayAvailable = 1;           // First day item can be purchased
    public GameObject[] upgradeLevels;     // Level1, Level2, Level3
}