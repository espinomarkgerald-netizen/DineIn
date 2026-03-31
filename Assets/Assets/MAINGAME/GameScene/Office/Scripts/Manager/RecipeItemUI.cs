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

    public void Setup(Recipe r)
    {
        recipe = r;
        nameText.text = r.recipeName;
        recipeImage.sprite = r.sprite;
        descriptionText.text = r.descriptionText;

        PopulateIngredients();

        bool unlocked = RecipeManager.Instance.IsUnlocked(recipe.recipeID);
        gameObject.SetActive(unlocked);
    }

    void PopulateIngredients()
    {
        foreach (Transform child in ingredientContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var ing in recipe.ingredients)
        {
            GameObject obj = Instantiate(ingredientPrefab, ingredientContainer);

            IngredientSlotUI slot = obj.GetComponent<IngredientSlotUI>();
            slot.Setup(ing.item, ing.amount);
        }
    }

    public void Refresh()
    {
        bool unlocked = RecipeManager.Instance.IsUnlocked(recipe.recipeID);
        gameObject.SetActive(unlocked);
    }
}