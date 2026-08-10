using UnityEngine;

public class Counter : MonoBehaviour {
    public GameObject currentItem;
    public Transform itemPlacementPoint;
    public Transform standPoint;

    public virtual void Interact(PlayerHolding player) {

        // --- PLATE LOGIC 1: Player holds a Plate, Counter has Food ---
        if (player.heldObject != null && player.heldObject.TryGetComponent(out Plate playerPlate)) {
            if (currentItem != null && !currentItem.GetComponent<Plate>()) {
                if (playerPlate.TryAddIngredient(currentItem)) {
                    currentItem = null;
                    return;
                }
            }
        }

        // --- PLATE LOGIC 2: Player holds Food, Counter has a Plate ---
        if (currentItem != null && currentItem.TryGetComponent(out Plate counterPlate)) {
            if (player.heldObject != null && !player.heldObject.GetComponent<Plate>()) {
                if (counterPlate.TryAddIngredient(player.heldObject)) {
                    player.heldObject = null;
                    return;
                }
            }
        }

        // --- STANDARD LOGIC ---
        if (player.heldObject != null && currentItem == null) {
            PlaceItem(player);
        } else if (player.heldObject == null && currentItem != null) {
            PickUpItem(player);
        }
    }

    protected void PlaceItem(PlayerHolding player) {
        currentItem = player.heldObject;
        currentItem.transform.parent = itemPlacementPoint;
        currentItem.transform.localPosition = Vector3.zero;
        player.heldObject = null;
    }

    protected void PickUpItem(PlayerHolding player) {
        if (currentItem.TryGetComponent(out IngredientStack stack)) {
            GameObject single = stack.ConsumeOne();
            if (single != null)
                player.PickUp(single);
            if (stack.Remaining <= 0)
                currentItem = null;
        } else {
            player.PickUp(currentItem);
            currentItem = null;
        }
    }
}