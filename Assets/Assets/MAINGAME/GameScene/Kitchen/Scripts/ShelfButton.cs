using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this to the Take button GameObject on a shelf Canvas.
/// It wires the Button.onClick to the parent Shelf.Button_TakeStack at runtime,
/// replacing any stale serialized listener.
/// </summary>
[RequireComponent(typeof(Button))]
public class ShelfButton : MonoBehaviour {
    void Awake() {
        Button button = GetComponent<Button>();
        Shelf shelf = GetComponentInParent<Shelf>();

        if (shelf == null) {
            Debug.LogError($"[ShelfButton] No Shelf component found in parent hierarchy of '{name}'.");
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(shelf.Button_TakeStack);
    }
}
