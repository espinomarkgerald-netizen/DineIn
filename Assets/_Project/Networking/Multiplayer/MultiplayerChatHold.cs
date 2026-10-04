using UnityEngine;
using UnityEngine.EventSystems;

// The existing HUD Button still owns short clicks; this only tracks its initiating hold.
public sealed class MultiplayerChatHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private MultiplayerSocialUI socialUI;
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left) socialUI.BeginHudHold(data.pointerId, data.position);
    }
    public void OnPointerUp(PointerEventData data) => socialUI.EndHudHold(data.pointerId, data.position, false);
}
