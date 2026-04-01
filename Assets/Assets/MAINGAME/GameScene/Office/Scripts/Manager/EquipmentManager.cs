using System.Collections.Generic;
using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    [SerializeField] private List<Equipment> allEquipment;
    private HashSet<string> purchased = new HashSet<string>();

    private void Awake() => Instance = this;

    public void UnlockByDay(int day)
    {
        foreach (var equip in allEquipment)
        {
            if (equip.dayToUnlock <= day && !UnlockManager.Instance.IsEquipmentUnlocked(equip.itemID))
                UnlockManager.Instance.UnlockEquipment(equip.itemID);
        }
    }

    private void Start()
    {
        UnlockByDay(1); // force Day 1 unlock
    }

    public bool Purchase(string itemID)
    {
        if (purchased.Contains(itemID)) return false;

        Equipment e = allEquipment.Find(eq => eq.itemID == itemID);
        if (e == null) return false;

        if (!MoneyManager.Instance.Spend(e.cost, e.displayName)) return false;

        EquipmentLink[] allLinks = FindObjectsOfType<EquipmentLink>(true);
        foreach (var link in allLinks)
        {
            if (link.itemID == itemID)
            {
                link.gameObject.SetActive(true);
                purchased.Add(itemID);
                return true;
            }
        }

        return false;
    }

    public bool Purchased(string itemID) => purchased.Contains(itemID);
}