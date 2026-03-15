using UnityEngine;

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
        if (player == null || uiPanel == null) return;

        player.MoveToTargetAndShowUI(transform, uiPanel);
    }
}