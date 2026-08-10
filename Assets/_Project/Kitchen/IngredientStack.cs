using UnityEngine;
using TMPro;

/// <summary>
/// Attached at runtime to an ingredient GameObject by Cupboard.
/// Tracks how many units remain in the stack. Line players consume
/// one unit at a time via Counter; the stack self-destructs at 0.
/// </summary>
public class IngredientStack : MonoBehaviour
{
    private GameObject singleUnitPrefab;
    private int remaining;
    private TextMeshPro countLabel;

    public int Remaining => remaining;

    /// <summary>Initialises the stack with its source prefab and unit count.</summary>
    public void Init(GameObject prefab, int count)
    {
        singleUnitPrefab = prefab;
        remaining = count;
        CreateCountLabel();
        UpdateLabel();
    }

    /// <summary>
    /// Removes one unit from the stack and returns it as a new GameObject
    /// ready to be handed to a player. Destroys the stack when empty.
    /// </summary>
    public GameObject ConsumeOne()
    {
        if (remaining <= 0 || singleUnitPrefab == null)
            return null;

        remaining--;
        UpdateLabel();

        GameObject single = Instantiate(singleUnitPrefab);
        single.name = singleUnitPrefab.name;

        if (remaining <= 0)
            Destroy(gameObject);

        return single;
    }

    private void CreateCountLabel()
    {
        GameObject labelGO = new GameObject("StackCountLabel");
        labelGO.transform.SetParent(transform, false);
        labelGO.transform.localPosition = Vector3.up * 0.2f;
        labelGO.transform.localScale = Vector3.one * 0.005f;

        TextMeshPro tmp = labelGO.AddComponent<TextMeshPro>();
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.yellow;
        tmp.fontStyle = FontStyles.Bold;

        countLabel = tmp;
    }

    private void UpdateLabel()
    {
        if (countLabel != null)
            countLabel.text = "\u00d7" + remaining;
    }
}
