using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public sealed class FastFoodCookingDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public FastFoodCookingView view;
    [System.NonSerialized] public ItemData item;
    [System.NonSerialized] public FastFoodCookingState.Portion portion;
    public UnityEngine.UI.Image icon;
    public TMP_Text count;
    [TextArea] public string stockFormat = "{0}\n{1} available · {2} in use\n{3} boxes";
    [TextArea] public string servingFormat = "{0} x{1}\n{2}";
    public string readyText = "Drag to tray", waitingText = "Staff cooking...";
    public void OnBeginDrag(PointerEventData e) => view.BeginDrag(this,e.position);
    public void OnDrag(PointerEventData e) => view.MoveDrag(e.position);
    public void OnEndDrag(PointerEventData e) => view.EndDrag(e.position);
}
