using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public sealed class FastFoodCookingDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [HideInInspector] public FastFoodCookingView view;
    [System.NonSerialized] public ItemData item;
    [System.NonSerialized] public FastFoodCookingState.Portion portion;
    [System.NonSerialized] public GameObject previewTemplate;
    [System.NonSerialized] public bool storedProtein;
    public UnityEngine.UI.Image icon;
    public TMP_Text count;
    [Tooltip("Saved visual centre used to author the pickup collider and drag pivot.")]
    public Transform grabAnchor;
    [Min(0)] public float hitPadding=.05f;
    [TextArea] public string stockFormat = "x{1}";
    [TextArea] public string servingFormat = "x{1}";
    public string readyText = "Drag to tray", waitingText = "Staff cooking...";
    private Outline[] interactionOutlines;
    public void OnPointerEnter(PointerEventData e)
    {
        if (transform is RectTransform || !enabled) return;
        if (interactionOutlines == null) interactionOutlines = GetComponentsInChildren<Outline>();
        foreach (var outline in interactionOutlines) { outline.OutlineColor = Color.white; outline.enabled = true; }
    }
    public void OnPointerExit(PointerEventData e) => ClearHighlight();
    void OnDisable() => ClearHighlight();
    void ClearHighlight()
    { if (interactionOutlines != null) foreach (var outline in interactionOutlines) if (outline != null) outline.enabled = false; }
    public void OnBeginDrag(PointerEventData e) => view.BeginDrag(this,e.position);
    public void OnDrag(PointerEventData e) => view.MoveDrag(e.position);
    public void OnEndDrag(PointerEventData e) => view.EndDrag(e.position);
    public void OnPointerClick(PointerEventData e)
    {
        if (!(transform is RectTransform) && e.button == PointerEventData.InputButton.Left && !e.dragging)
            view?.CollectFood(portion);
    }
}
