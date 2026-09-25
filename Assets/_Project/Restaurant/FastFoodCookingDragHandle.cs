using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public sealed class FastFoodCookingDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public FastFoodCookingView view;
    [System.NonSerialized] public ItemData item;
    [System.NonSerialized] public FastFoodCookingState.Portion portion;
    [System.NonSerialized] public GameObject previewTemplate;
    public UnityEngine.UI.Image icon;
    public TMP_Text count;
    [TextArea] public string stockFormat = "x{1}";
    [TextArea] public string servingFormat = "x{1}";
    public string readyText = "Drag to tray", waitingText = "Staff cooking...";
    public void OnBeginDrag(PointerEventData e) => view.BeginDrag(this,e.position);
    public void OnDrag(PointerEventData e) => view.MoveDrag(e.position);
    public void OnEndDrag(PointerEventData e) => view.EndDrag(e.position);
}
