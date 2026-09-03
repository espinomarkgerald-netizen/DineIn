using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class TapOutlineSelector : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask selectableMask;

    private Outline currentOutline;

    public event Action<Transform> SelectionSucceeded;
    public Transform CurrentSelection => currentOutline != null ? currentOutline.transform : null;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
            HandleTap(Input.mousePosition, -1);
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
                HandleTap(touch.position, touch.fingerId);
        }
#endif
    }

    private void HandleTap(Vector3 screenPos, int pointerId)
    {
        // Block UI clicks
        if (EventSystem.current != null)
        {
            bool overUi = pointerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
            if (overUi)
                return;
        }

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
                PublishSelection(outline.transform);
            }
        }
        else
        {
            Clear();
        }
    }

    private void PublishSelection(Transform selectedTransform)
    {
        if (selectedTransform != null)
            SelectionSucceeded?.Invoke(selectedTransform);
    }

    void Clear()
    {
        if (currentOutline != null)
            currentOutline.enabled = false;

        currentOutline = null;
    }
}
