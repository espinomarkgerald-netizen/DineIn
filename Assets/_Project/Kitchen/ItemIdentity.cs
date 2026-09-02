using UnityEngine;

// This is the master list! If you ever add Pizza or Sprite later, just type it in here!
public enum ItemTypeKitchen {
    None,
    Burger,
    Chicken,
    Fries,
    Coke,
    Pineapple,
    IcedTea,

    // Restaurant-specific products are appended for serialized compatibility.
    RoastedChicken,
    TomatoSoup,
    PorkChop,
    GarlicButterShrimp,
    FriedSalmon,
    CaesarSalad
}

public class ItemIdentity : MonoBehaviour {
    // This creates a dropdown menu in the Unity Inspector!
    public ItemTypeKitchen itemType;
}
