using System.Collections.Generic;
using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance;
    [SerializeField] private List<Equipment> allEquipment;
    private HashSet<string> purchased = new HashSet<string>();

    private void Awake()
    {
        Instance = this;
    }

    public bool Purchase(string itemID)
    {
        if (purchased.Contains(itemID))
        {
            Debug.Log($"Already own {itemID}, cannot buy again");
            return false;
        }

        Equipment equip = allEquipment.Find(e => e.itemID == itemID);
        if (equip == null)
        {
            Debug.LogWarning("No Equipment found with ID: " + itemID);
            return false;
        }

        if (!MoneyManager.Instance.Spend(equip.cost, equip.displayName))
        {
            Debug.LogWarning("Not enough money for: " + equip.displayName);
            return false;
        }

        EquipmentLink[] allLinks = FindObjectsOfType<EquipmentLink>(true);
        foreach (var link in allLinks)
        {
            if (link.itemID == itemID)
            {
                link.gameObject.SetActive(true);
                purchased.Add(itemID); // mark as purchased
                Debug.Log($"Activated {equip.displayName}");
                return true;
            }
        }

        Debug.LogWarning("No matching scene object found for: " + itemID);
        return false;
    }

    public bool Purchased(string itemID)
    {
        return purchased.Contains(itemID);
    }

    public List<Equipment> GetAllEquipment() => allEquipment;
}