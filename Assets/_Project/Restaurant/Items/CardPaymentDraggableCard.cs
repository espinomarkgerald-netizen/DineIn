using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Mouse/touch drag surface for the authored card image.</summary>
public sealed class CardPaymentDraggableCard : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private CardPaymentUI controller;
    [SerializeField] private RectTransform cardRect;

    public void Configure(CardPaymentUI configuredController, RectTransform configuredCard)
    {
        controller = configuredController;
        cardRect = configuredCard;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        controller?.BeginCardDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        controller?.DragCard(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        controller?.EndCardDrag(eventData);
    }
}
