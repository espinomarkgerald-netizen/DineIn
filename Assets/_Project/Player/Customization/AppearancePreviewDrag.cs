using UnityEngine;
using UnityEngine.EventSystems;

namespace DineIn.Appearance
{
    // Dedicated preview hit area: option scrolling cannot rotate the character.
    public sealed class AppearancePreviewDrag : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        [SerializeField] private AppearanceCustomizationPanel panel;
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) panel.BeginPreviewDrag();
        }
        public void OnDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) panel.RotatePreview(data.delta.x);
        }
    }
}
