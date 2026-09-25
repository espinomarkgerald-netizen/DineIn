using UnityEngine;
public sealed class FastFoodCookingDropTarget : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
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
    [Tooltip("A broad grill click area; collects only when this grill has one ready patty.")]
    public bool collectionSurface;
    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
    {
        if (kind == 0 && e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left && !e.dragging)
            view?.CollectFromSurface(slotIndex, collectionSurface);
    }
}
