using UnityEngine;

[CreateAssetMenu(fileName = "New Ingredient", menuName = "Cooking/Ingredient")]
public class Ingredient : ScriptableObject {
    public string ingredientName;
    public GameObject prefab;
    public Ingredient processedForm;
    public float cookTime = 5f;

    [Header("Inventory Link")]
    [Tooltip("The office ItemType this ingredient maps to. Used to deduct stock on pickup.")]
    public ItemType itemType;
}