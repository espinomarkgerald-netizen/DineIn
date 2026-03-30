using System.Collections.Generic;
using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance;
    [SerializeField] private List<Equipment> allEquipment;
    private Dictionary<string, int> purchasedLevels = new Dictionary<string, int>();

    private void Awake() => Instance = this;

    public int GetLevel(string itemID) => purchasedLevels.TryGetValue(itemID, out int level) ? level : 0;

    public bool Purchase(string itemID, int playerMoney)
    {
        Equipment equip = allEquipment.Find(e => e.itemID == itemID);
        if (equip == null) return false;

        int currentLevel = GetLevel(itemID);
        if (currentLevel >= equip.upgradeLevels.Length || playerMoney < equip.cost) return false;

        MoneyManager.Instance.Spend(equip.cost, $"Purchased {equip.displayName} Level {currentLevel + 1}");
        purchasedLevels[itemID] = currentLevel + 1;
        UpdateVisual(equip, currentLevel + 1);

        Debug.Log($"{equip.displayName} upgraded to Level {currentLevel + 1}");
        return true;
    }

    private void UpdateVisual(Equipment equip, int level)
    {
        for (int i = 0; i < equip.upgradeLevels.Length; i++)
            if (equip.upgradeLevels[i] != null)
                equip.upgradeLevels[i].SetActive(i == level - 1);
    }

    public List<Equipment> GetPurchasable(int currentDay) =>
        allEquipment.FindAll(e => e.dayAvailable <= currentDay);
}