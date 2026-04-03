using System.Collections.Generic;
using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    [SerializeField] private List<Equipment> allEquipment;
    private HashSet<string> purchased = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

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
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        UnlockByDay(day);
    }

    /// <summary>
    /// Records the purchase and activates any EquipmentLink in the current scene matching itemID.
    /// If no link exists yet (different scene), EquipmentLink.Start() will self-activate on load.
    /// </summary>
    public bool Purchase(string itemID)
    {
        if (purchased.Contains(itemID)) return false;

        Equipment e = allEquipment.Find(eq => eq.itemID == itemID);
        if (e == null) return false;

        if (!MoneyManager.Instance.Spend(e.cost, e.displayName)) return false;

        purchased.Add(itemID);

        EquipmentLink[] allLinks = FindObjectsOfType<EquipmentLink>(true);
        foreach (var link in allLinks)
        {
            if (link.itemID == itemID)
                link.gameObject.SetActive(true);
        }

        return true;
    }

    public bool Purchased(string itemID) => purchased.Contains(itemID);
}