using UnityEngine;

public enum ItemType
{
    None,
    Drumsticks,
    FrenchFryBag,
    Bun,
    Patty,
    Cheese
}

public class ItemIdentity : MonoBehaviour {
    // This creates a dropdown menu in the Unity Inspector!
    public ItemType itemType;
}