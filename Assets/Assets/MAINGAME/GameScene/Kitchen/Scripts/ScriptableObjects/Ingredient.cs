using UnityEngine;

[CreateAssetMenu(fileName = "New Ingredient", menuName = "Cooking/Ingredient")]
public class Ingredient : ScriptableObject {
    public string ingredientName;
    public GameObject prefab; // Visual model
    public Ingredient processedForm; // What it becomes after cooking
    public float cookTime = 5f;
}