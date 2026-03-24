using UnityEngine;
using UnityEngine.EventSystems;

public class Interactable : MonoBehaviour
{
    [SerializeField] private SimplePlayerMovement player;
    [SerializeField] private GameObject uiPanel;

    Outline outline;

    void Awake()
    {
        outline = GetComponent<Outline>();

        if (outline != null)
            outline.enabled = false;
    }

    public void Highlight(bool state)
    {
        if (outline != null)
            outline.enabled = state;
    }

    void OnMouseDown()
    {
    #if UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            return;
    #endif

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (player == null || uiPanel == null) return;

        player.MoveToTargetAndShowUI(transform, uiPanel);
    }
}