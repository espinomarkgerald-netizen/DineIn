using UnityEngine;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public Transform contentParent; // The ScrollView content
    public GameObject shopItemPrefab;
    public List<ItemData> itemList;

    void Start()
    {
        PopulateShop();
    }

    void PopulateShop()
    {
        foreach (var item in itemList)
        {
            GameObject go = Instantiate(shopItemPrefab, contentParent);
            ShopItemUI ui = go.GetComponent<ShopItemUI>();
            ui.Setup(item);
        }
    }
}