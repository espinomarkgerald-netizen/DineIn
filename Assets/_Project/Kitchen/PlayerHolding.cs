using UnityEngine;

public class PlayerHolding : MonoBehaviour {
    public Transform holdPoint;
    public GameObject heldObject;

    public void PickUp(GameObject obj) {
        heldObject = obj;
        obj.transform.parent = holdPoint;

        // Zero out the position
        obj.transform.localPosition = Vector3.zero;

        // Lock the rotation so it never spins wildly!
        obj.transform.localRotation = Quaternion.identity;

        // Disable physics while holding
        obj.GetComponent<Collider>().enabled = false;
    }

    public void Drop() {
        if (heldObject != null) {
            heldObject.transform.parent = null;
            heldObject.GetComponent<Collider>().enabled = true;
            heldObject = null;
        }
    }

    // --- NEW: TUTORIAL MAGIC ---
    public void ConsumeItem() {
        if (heldObject != null) {
            Destroy(heldObject); // This permanently deletes the Coke!
            heldObject = null;   // This tells the game your hands are empty again
        }
    }
}