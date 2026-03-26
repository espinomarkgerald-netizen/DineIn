using UnityEngine;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject shopItemPrefab;
    [SerializeField] private List<ItemData> itemList;

    private readonly List<ShopItemUI> spawnedItems = new List<ShopItemUI>();

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
        ClearShop();

        if (contentParent == null || shopItemPrefab == null || itemList == null)
            return;

        for (int i = 0; i < itemList.Count; i++)
        {
            ItemData item = itemList[i];
            if (item == null) continue;

            GameObject go = Instantiate(shopItemPrefab, contentParent);
            ShopItemUI ui = go.GetComponent<ShopItemUI>();

            if (ui != null)
            {
                ui.Setup(item);
                spawnedItems.Add(ui);
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

        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] != null)
                spawnedItems[i].RefreshDisplay();
        }
    }

    private void ClearShop()
    {
        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);

        spawnedItems.Clear();
    }

    
}