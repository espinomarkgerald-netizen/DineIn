using UnityEngine;

// This is the master list! If you ever add Pizza or Sprite later, just type it in here!
public enum ItemType {
    None,
    Burger,
    Chicken,
    Fries,
    Coke,
    Pineapple,
    IcedTea
}

public class ItemIdentity : MonoBehaviour {
    // This creates a dropdown menu in the Unity Inspector!
    public ItemType itemType;
}