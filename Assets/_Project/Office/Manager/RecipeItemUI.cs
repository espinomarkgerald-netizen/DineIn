using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecipeItemUI : MonoBehaviour
{
    public Image recipeImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;

    [Header("Ingredients UI")]
    public Transform ingredientContainer;
    public GameObject ingredientPrefab;

    private Recipe recipe;

    public void Setup(Recipe r, bool unlocked)
    {
        recipe = r;

        if (unlocked)
        {
            nameText.text = r.recipeName;
            descriptionText.text = r.descriptionText;
            recipeImage.sprite = r.sprite;
            PopulateIngredients();
        }
        else
        {
            nameText.text = "???";
            descriptionText.text = $"Unlock at Day {r.dayToUnlock}";
            recipeImage.sprite = null;
        }
    }

    private void PopulateIngredients()
    {
        foreach (Transform child in ingredientContainer)
            Destroy(child.gameObject);

        foreach (var ing in recipe.ingredients)
        {
            GameObject obj = Instantiate(ingredientPrefab, ingredientContainer);
            if (obj.TryGetComponent(out IngredientSlotUI slot))
                slot.Setup(ing.item, ing.amount);
        }
    }
}