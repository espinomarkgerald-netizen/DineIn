using UnityEngine;

public class Interactable : MonoBehaviour
{
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
}