using UnityEngine;
using UnityEngine.InputSystem;

public class HoverManager : MonoBehaviour
{
    Camera cam;
    Interactable currentHover;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        Vector2 inputPos = Vector2.zero;
        bool hasInput = false;

        // Mouse
        if (Mouse.current != null)
        {
            inputPos = Mouse.current.position.ReadValue();
            hasInput = true;
        }

        // Touch
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed)
        {
            inputPos = Touchscreen.current.primaryTouch.position.ReadValue();
            hasInput = true;
        }

        if (!hasInput) return;

        Ray ray = cam.ScreenPointToRay(inputPos);

        int mask = LayerMask.GetMask("Interactable");

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, mask))
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();

            if (interactable != currentHover)
            {
                if (currentHover != null)
                    currentHover.Highlight(false);

                if (interactable != null)
                    interactable.Highlight(true);

                currentHover = interactable;
            }
        }
        else
        {
            if (currentHover != null)
            {
                currentHover.Highlight(false);
                currentHover = null;
            }
        }
    }
}