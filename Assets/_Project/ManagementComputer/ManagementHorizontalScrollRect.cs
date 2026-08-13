using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Horizontal card rail that forwards vertical drags to the containing app
/// window, allowing touch users to scroll the HR page from anywhere.
/// </summary>
public sealed class ManagementHorizontalScrollRect : ScrollRect
{
    private ScrollRect parentScroll;
    private bool routeToParent;

    protected override void Awake()
    {
        base.Awake();
        Transform current = transform.parent;
        while (current != null && parentScroll == null)
        {
            ScrollRect candidate = current.GetComponent<ScrollRect>();
            if (candidate != null && candidate != this && candidate.vertical)
                parentScroll = candidate;
            current = current.parent;
        }
    }

    public override void OnInitializePotentialDrag(PointerEventData eventData)
    {
        base.OnInitializePotentialDrag(eventData);
        parentScroll?.OnInitializePotentialDrag(eventData);
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        Vector2 drag = eventData.position - eventData.pressPosition;
        routeToParent = parentScroll != null && Mathf.Abs(drag.y) > Mathf.Abs(drag.x);
        if (routeToParent)
            parentScroll.OnBeginDrag(eventData);
        else
            base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (routeToParent)
            parentScroll.OnDrag(eventData);
        else
            base.OnDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (routeToParent)
            parentScroll.OnEndDrag(eventData);
        else
            base.OnEndDrag(eventData);
        routeToParent = false;
    }
}
