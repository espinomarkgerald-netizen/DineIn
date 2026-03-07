using UnityEngine;
using UnityEngine.EventSystems;

public class TapOutlineSelector : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask selectableMask;

    private Outline currentOutline;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleTap(Input.mousePosition);
    }

    void HandleTap(Vector3 screenPos)
    {
        // Block UI clicks
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        if (cam == null)
            cam = Camera.main;

        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, selectableMask))
        {
            // Turn off old outline
            if (currentOutline != null)
                currentOutline.enabled = false;

            // Turn on new outline
            Outline outline = hit.collider.GetComponentInParent<Outline>();

            if (outline != null)
            {
                outline.enabled = true;
                currentOutline = outline;
            }
        }
        else
        {
            Clear();
        }
    }

    void Clear()
    {
        if (currentOutline != null)
            currentOutline.enabled = false;

        currentOutline = null;
    }
}
