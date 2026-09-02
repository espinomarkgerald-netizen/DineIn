using System.Collections.Generic;
using UnityEngine;

public readonly struct MenuPriceGuidance
{
    public readonly float CostPerServing;
    public readonly int RecommendedMinimum;
    public readonly int RecommendedMaximum;

    public MenuPriceGuidance(float costPerServing, int minimum, int maximum)
    {
        CostPerServing = Mathf.Max(0f, costPerServing);
        RecommendedMinimum = Mathf.Max(0, minimum);
        RecommendedMaximum = Mathf.Max(RecommendedMinimum + 1, maximum);
    }
}

/// <summary>
/// Read-only pricing guidance derived from the recipe's live supplier costs.
/// Customer order generation uses it only to decide whether an extreme price
/// is rejected; it never changes menu prices or wallet values.
/// </summary>
public static class MenuPriceValueService
{
    public static MenuPriceGuidance GetGuidance(Recipe product)
    {
        if (product == null)
            return new MenuPriceGuidance(0f, 0, 1);

        float servingCost = 0f;
        if (product.ingredients != null)
        {
            for (int i = 0; i < product.ingredients.Count; i++)
            {
                RecipeIngredient ingredient = product.ingredients[i];
                if (ingredient?.item == null || ingredient.amount <= 0)
                    continue;
                int boxCost = CasualDiningPolishManager.Instance != null
                    ? CasualDiningPolishManager.Instance.GetCurrentBoxCost(ingredient.item)
                    : Mathf.Max(0, ingredient.item.boxCost);
                servingCost += Mathf.Max(0, ingredient.amount) *
                               boxCost /
                               (float)Mathf.Max(1, ingredient.item.unitsPerBox);
            }
        }

        // Recipe cost provides the sustainable floor. The authored value keeps
        // deliberately premium dishes distinct without imposing one markup on
        // every item, while live supplier changes can still move the range.
        float authoredReference = Mathf.Max(1f, product.sellPrice);
        float valueAnchor = Mathf.Max(authoredReference * 0.78f, servingCost * 2.25f);
        int minimum = Mathf.CeilToInt(Mathf.Max(servingCost * 1.25f, valueAnchor * 0.72f));
        int maximum = Mathf.CeilToInt(Mathf.Max(servingCost * 3f, valueAnchor * 1.32f));
        return new MenuPriceGuidance(servingCost, minimum, maximum);
    }

    public static float GetOrderRejectionChance(
        IReadOnlyList<CustomerGroup.OrderLine> lines,
        MenuCatalog catalog = null)
    {
        if (lines == null || lines.Count == 0)
            return 0f;
        catalog ??= MenuCatalog.Default;
        if (catalog == null)
            return 0f;

        float worstRatio = 0f;
        for (int i = 0; i < lines.Count; i++)
        {
            CustomerGroup.OrderLine line = lines[i];
            if (line == null)
                continue;
            List<Recipe> products = line.ResolveProducts(catalog);
            if (products.Count == 0)
                continue;

            int reasonableMaximum = 0;
            for (int p = 0; p < products.Count; p++)
                reasonableMaximum += GetGuidance(products[p]).RecommendedMaximum;
            if (reasonableMaximum <= 0)
                continue;

            float ratio = Mathf.Max(0, line.unitPrice) / (float)reasonableMaximum;
            worstRatio = Mathf.Max(worstRatio, ratio);
        }

        return GetRejectionChanceForRatio(worstRatio);
    }

    public static float GetRejectionChanceForRatio(float priceToReasonableMaximum)
    {
        float ratio = Mathf.Max(0f, priceToReasonableMaximum);
        if (ratio <= 1f) return 0f;
        if (ratio <= 1.2f) return Mathf.Lerp(0.04f, 0.16f, (ratio - 1f) / 0.2f);
        if (ratio <= 1.6f) return Mathf.Lerp(0.16f, 0.48f, (ratio - 1.2f) / 0.4f);
        if (ratio <= 2.2f) return Mathf.Lerp(0.48f, 0.78f, (ratio - 1.6f) / 0.6f);
        if (ratio <= 3f) return Mathf.Lerp(0.78f, 0.92f, (ratio - 2.2f) / 0.8f);
        return Mathf.Lerp(0.92f, 0.98f, Mathf.Clamp01((ratio - 3f) / 2f));
    }
}
