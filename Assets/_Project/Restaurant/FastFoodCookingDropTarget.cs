using UnityEngine;
public sealed class FastFoodCookingDropTarget : MonoBehaviour
{
    [HideInInspector] public FastFoodCookingView view;
    [Tooltip("0 = cooking, 1 = preparation / assembly, 2 = discard")]
    [Range(0,2)] public int kind;
    [Tooltip("Saved UI hint, shown only while dragging a matching item.")]
    public GameObject hint;
    [Min(0)] public int slotIndex;
    [Tooltip("Who may start new food here while the player is helping in the kitchen.")]
    public FastFoodCookingSlotOwner owner;
    public Transform foodAnchor;
    public TMPro.TMP_Text status;
    public FastFoodCookingTimer timer;
    public GameObject cookingFeedback;
}
