using UnityEngine;
using UnityEngine.EventSystems;

// The existing HUD Button still owns short clicks; this only tracks its initiating hold.
public sealed class MultiplayerChatHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler, ICancelHandler
{
    [SerializeField] private MultiplayerSocialUI socialUI;
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left) socialUI.BeginHudHold(data.pointerId, data.position);
    }
    public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;
    public void OnDrag(PointerEventData data) => socialUI.MoveHudHold(data.pointerId, data.position);
    public void OnCancel(BaseEventData data) => socialUI.CancelHudHold();
    private void OnDisable() { if (socialUI != null) socialUI.CancelHudHold(); }
    public void OnPointerUp(PointerEventData data) => socialUI.EndHudHold(data.pointerId, data.position, false);
}
