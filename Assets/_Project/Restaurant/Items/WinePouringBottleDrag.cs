using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>EventSystem routes both mouse and Android touch to the same pouring interaction.</summary>
public sealed class WinePouringBottleDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public WinePouringUI controller;
    public void OnBeginDrag(PointerEventData eventData) => controller?.BeginBottleDrag(eventData);
    public void OnDrag(PointerEventData eventData) => controller?.DragBottle(eventData);
    public void OnEndDrag(PointerEventData eventData) => controller?.EndBottleDrag(eventData);
}
