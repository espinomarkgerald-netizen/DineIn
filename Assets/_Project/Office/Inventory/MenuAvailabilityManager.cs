using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores runtime menu choices without editing Recipe assets. The authored
/// availableOnMenu flag remains the default; this manager records only disabled IDs.
/// </summary>
public sealed class MenuAvailabilityManager : MonoBehaviour
{
    public static MenuAvailabilityManager Instance { get; private set; }

    private readonly HashSet<string> disabledProductIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> productPriceOverrides =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public event Action MenuChanged;

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

    public static bool IsProductAvailable(Recipe product)
    {
        if (product == null || !product.availableOnMenu)
            return false;

        return Instance == null || !Instance.disabledProductIds.Contains(product.ProductId);
    }

    public static int GetProductPrice(Recipe product)
    {
        if (product == null)
            return 0;

        if (Instance != null &&
            !string.IsNullOrWhiteSpace(product.ProductId) &&
            Instance.productPriceOverrides.TryGetValue(product.ProductId, out int price))
            return Mathf.Max(0, price);

        return Mathf.Max(0, product.sellPrice);
    }

    public bool SetProductPrice(Recipe product, int price)
    {
        if (product == null || string.IsNullOrWhiteSpace(product.ProductId))
            return false;

        price = Mathf.Max(0, price);
        bool changed;
        if (price == Mathf.Max(0, product.sellPrice))
            changed = productPriceOverrides.Remove(product.ProductId);
        else if (!productPriceOverrides.TryGetValue(product.ProductId, out int current) || current != price)
        {
            productPriceOverrides[product.ProductId] = price;
            changed = true;
        }
        else
        {
            changed = false;
        }

        if (changed)
        {
            MenuChanged?.Invoke();
            GameSaveManager.Instance?.RequestSave();
        }

        return changed;
    }

    public static bool IsBundleAvailable(MenuBundle bundle)
    {
        if (bundle == null || !bundle.availableOnMenu || bundle.products == null || bundle.products.Count == 0)
            return false;

        foreach (Recipe product in bundle.products)
        {
            if (!IsProductAvailable(product))
                return false;
        }

        return true;
    }

    public bool SetProductAvailable(Recipe product, bool available)
    {
        if (product == null || string.IsNullOrWhiteSpace(product.ProductId) || !product.availableOnMenu)
            return false;

        bool changed = available
            ? disabledProductIds.Remove(product.ProductId)
            : disabledProductIds.Add(product.ProductId);

        if (changed)
        {
            MenuChanged?.Invoke();
            GameSaveManager.Instance?.RequestSave();
        }

        return changed;
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.disabledMenuProductIDs.Clear();
        data.disabledMenuProductIDs.AddRange(disabledProductIds);
        data.menuPriceOverrides.Clear();
        foreach (KeyValuePair<string, int> entry in productPriceOverrides)
        {
            data.menuPriceOverrides.Add(new MenuPriceOverrideSaveEntry
            {
                productID = entry.Key,
                price = entry.Value
            });
        }
    }

    public void ApplySaveData(GameSaveData data)
    {
        disabledProductIds.Clear();
        productPriceOverrides.Clear();
        if (data?.disabledMenuProductIDs != null)
        {
            foreach (string productId in data.disabledMenuProductIDs)
            {
                if (!string.IsNullOrWhiteSpace(productId))
                    disabledProductIds.Add(productId);
            }
        }


        if (data?.menuPriceOverrides != null)
        {
            foreach (MenuPriceOverrideSaveEntry entry in data.menuPriceOverrides)
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.productID))
                    productPriceOverrides[entry.productID] = Mathf.Max(0, entry.price);
            }
        }

        MenuChanged?.Invoke();
    }
}
