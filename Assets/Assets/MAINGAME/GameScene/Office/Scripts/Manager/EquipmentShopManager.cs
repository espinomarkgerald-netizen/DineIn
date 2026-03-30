using UnityEngine;
using System.Collections.Generic;

public class EquipmentShopManager : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject equipmentItemPrefab;
    [SerializeField] private List<Equipment> equipmentList;

    private readonly List<EquipmentItemUI> spawnedItems = new();

    private void Start()
    {
        RebuildShop();
    }

    private void OnEnable()
    {
        RefreshShop();
    }

    public void RebuildShop()
    {
        ClearShop(); // optional: clean old items

        foreach (var equip in equipmentList)
        {
            GameObject go = Instantiate(equipmentItemPrefab, contentParent);
            EquipmentItemUI ui = go.GetComponent<EquipmentItemUI>();

            if (ui != null)
            {
                ui.Setup(equip);
                spawnedItems.Add(ui); // ADD THIS
            }
        }
    }

    public void RefreshShop()
    {
        if (spawnedItems.Count == 0)
        {
            RebuildShop();
            return;
        }

        foreach (var item in spawnedItems)
        {
            if (item != null)
                item.RefreshUI();
        }
    }

    private void ClearShop()
    {
        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);

        spawnedItems.Clear();
    }
}