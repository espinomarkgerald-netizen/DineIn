using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Attach to the preview RawImage; vertical drags remain available to its scroll view.</summary>
public sealed class AlmanacPreviewDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private AlmanacPreviewStage previewStage;
    private bool rotating;

    public AlmanacPreviewStage Stage { get => previewStage; set => previewStage = value; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        rotating = Mathf.Abs(eventData.delta.x) >= Mathf.Abs(eventData.delta.y);
        if (!rotating && transform.parent != null)
            ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.beginDragHandler);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rotating)
        {
            if (previewStage != null) previewStage.Rotate(eventData.delta.x);
        }
        else if (transform.parent != null)
            ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.dragHandler);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!rotating && transform.parent != null)
            ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.endDragHandler);
        rotating = false;
    }

    private void OnDisable() => rotating = false;
}
