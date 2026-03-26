using UnityEngine;

public class DeliveryCounter : Counter {

    public override void Interact(PlayerHolding player) {

        if (player.heldObject != null && player.heldObject.TryGetComponent(out Plate playerPlate)) {

            PlatingRecipe foodOnPlate = playerPlate.GetRecipe();

            if (foodOnPlate != null) {

                bool success = OrderManager.Instance.TryDeliver(foodOnPlate);

                if (success) {
                    Destroy(player.heldObject);
                    player.heldObject = null;
                }
            }
        }
    }
}