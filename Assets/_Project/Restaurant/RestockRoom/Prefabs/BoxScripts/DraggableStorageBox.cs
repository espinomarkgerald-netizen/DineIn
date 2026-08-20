using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class DraggableStorageBox : MonoBehaviour
{
    [Header("Dragging")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("How far the pointer must move before a tap/click becomes a drag.")]
    [SerializeField] private float dragThreshold = 10f;

    [Header("Shelf Detection")]
    [SerializeField] private LayerMask shelfGridLayer;

    [Header("Placement")]
    [SerializeField] private Vector3 placementOffset = Vector3.zero;

    [Header("Placement Ghost")]
    [SerializeField] private Material ghostMaterial;

    [SerializeField]
    private Color validGhostColor =
        new Color(0.2f, 1f, 0.2f, 0.35f);

    [SerializeField]
    private Color invalidGhostColor =
        new Color(1f, 0.15f, 0.15f, 0.35f);

    [Header("Box Interaction UI")]
    [SerializeField] private GameObject interactionUIRoot;

    private bool pointerHeld;
    private bool isDragging;

    private Vector2 pointerDownPosition;

    private Plane dragPlane;
    private Vector3 dragOffset;

    private Vector3 startingPosition;
    private Quaternion startingRotation;

    private ShelfGrid currentGrid;
    private int currentColumn = -1;
    private int currentRow = -1;

    private ShelfGrid previousGrid;
    private int previousColumn = -1;
    private int previousRow = -1;

    // Placement ghost
    private GameObject ghostObject;
    private Material ghostRuntimeMaterial;

    // Current preview
    private ShelfGrid previewGrid;
    private int previewColumn = -1;
    private int previewRow = -1;
    private bool previewCanPlace;

    // Mobile
    private int activeFingerId = -1;

    private void Awake()
    {
        FindCamera();

        // Authored boxes created before the restock flow had no explicit shelf
        // mask. Treat zero as "all shelves" so placed deliveries stay movable.
        if (shelfGridLayer.value == 0)
            shelfGridLayer = ~0;

        if (interactionUIRoot != null)
            interactionUIRoot.SetActive(false);
    }

    public bool TryPlaceInitially(ShelfGrid grid, int column, int row)
    {
        if (grid == null || !grid.PlaceObject(gameObject, column, row))
            return false;

        currentGrid = grid;
        currentColumn = column;
        currentRow = row;
        previousGrid = null;
        previousColumn = -1;
        previousRow = -1;
        startingPosition = grid.GetCellWorldPosition(column, row) + placementOffset;
        startingRotation = transform.rotation;
        transform.position = startingPosition;
        return true;
    }

    private void FindCamera()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Update()
    {
#if UNITY_ANDROID || UNITY_IOS
        HandleTouchInput();
#endif
    }

    // =========================================================
    // PC
    // =========================================================

#if !UNITY_ANDROID && !UNITY_IOS

    private void OnMouseDown()
    {
        FindCamera();

        if (playerCamera == null)
            return;

        // Only block actual interactive UI elements such as Buttons.
        // Decorative Images/Panels will NOT block the box anymore.
        if (IsPointerOverInteractiveUI(Input.mousePosition))
            return;

        BeginPointer(Input.mousePosition);
    }

    private void OnMouseDrag()
    {
        if (!pointerHeld)
            return;

        ContinuePointer(Input.mousePosition);
    }

    private void OnMouseUp()
    {
        if (!pointerHeld)
            return;

        EndPointer(Input.mousePosition);
    }

#endif

    // =========================================================
    // MOBILE
    // =========================================================

    private void HandleTouchInput()
    {
        FindCamera();

        if (playerCamera == null)
            return;

        // Find a new finger touching this box.
        if (activeFingerId == -1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (touch.phase != TouchPhase.Began)
                    continue;

                // Actual buttons should receive the touch instead.
                if (IsPointerOverInteractiveUI(
                    touch.position,
                    touch.fingerId))
                {
                    continue;
                }

                Ray ray =
                    playerCamera.ScreenPointToRay(
                        touch.position);

                if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    Mathf.Infinity))
                {
                    continue;
                }

                DraggableStorageBox touchedBox =
                    hit.collider.GetComponentInParent<
                        DraggableStorageBox>();

                if (touchedBox != this)
                    continue;

                activeFingerId =
                    touch.fingerId;

                BeginPointer(
                    touch.position);

                break;
            }

            return;
        }

        // Continue tracking the same finger.
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch =
                Input.GetTouch(i);

            if (touch.fingerId != activeFingerId)
                continue;

            switch (touch.phase)
            {
                case TouchPhase.Moved:
                case TouchPhase.Stationary:

                    ContinuePointer(
                        touch.position);

                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:

                    EndPointer(
                        touch.position);

                    activeFingerId = -1;

                    break;
            }

            break;
        }
    }

    // =========================================================
    // UI CHECK
    // =========================================================

    private bool IsPointerOverInteractiveUI(
        Vector2 screenPosition,
        int pointerId = -1)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData =
            new PointerEventData(
                EventSystem.current);

        eventData.position =
            screenPosition;

        eventData.pointerId =
            pointerId;

        List<RaycastResult> results =
            new List<RaycastResult>();

        EventSystem.current.RaycastAll(
            eventData,
            results);

        foreach (RaycastResult result in results)
        {
            // Only UI controls such as Button,
            // Toggle, Slider, etc. block interaction.
            Selectable selectable =
                result.gameObject.GetComponentInParent<
                    Selectable>();

            if (selectable != null &&
                selectable.interactable)
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // SHARED CLICK / TOUCH
    // =========================================================

    private void BeginPointer(
        Vector2 screenPosition)
    {
        pointerHeld = true;
        isDragging = false;

        pointerDownPosition =
            screenPosition;

        startingPosition =
            transform.position;

        startingRotation =
            transform.rotation;
    }

    private void ContinuePointer(
        Vector2 screenPosition)
    {
        if (!pointerHeld)
            return;

        if (!isDragging)
        {
            float distance =
                Vector2.Distance(
                    pointerDownPosition,
                    screenPosition);

            if (distance < dragThreshold)
                return;

            BeginActualDrag(
                screenPosition);
        }

        Drag(screenPosition);
    }

    private void EndPointer(
        Vector2 screenPosition)
    {
        if (!pointerHeld)
            return;

        pointerHeld = false;

        if (isDragging)
        {
            EndDrag(
                screenPosition);

            // Moving does not open the box menu.
            HideInteractionUI();
        }
        else
        {
            // Simple click / tap.
            ShowInteractionUI();
        }
    }

    // =========================================================
    // DRAGGING
    // =========================================================

    private void BeginActualDrag(
        Vector2 screenPosition)
    {
        isDragging = true;

        HideInteractionUI();

        previousGrid =
            currentGrid;

        previousColumn =
            currentColumn;

        previousRow =
            currentRow;

        // Free the previous shelf slot while moving.
        if (currentGrid != null)
        {
            currentGrid.RemoveObject(
                gameObject,
                currentColumn,
                currentRow);

            currentGrid = null;
            currentColumn = -1;
            currentRow = -1;
        }

        // Camera-facing plane.
        dragPlane = new Plane(
            -playerCamera.transform.forward,
            transform.position);

        Ray ray =
            playerCamera.ScreenPointToRay(
                screenPosition);

        if (dragPlane.Raycast(
            ray,
            out float distance))
        {
            Vector3 worldPosition =
                ray.GetPoint(distance);

            dragOffset =
                transform.position -
                worldPosition;
        }

        CreateGhost();

        UpdateGhostPreview(
            screenPosition);
    }

    private void Drag(
        Vector2 screenPosition)
    {
        Ray ray =
            playerCamera.ScreenPointToRay(
                screenPosition);

        if (dragPlane.Raycast(
            ray,
            out float distance))
        {
            Vector3 worldPosition =
                ray.GetPoint(distance);

            transform.position =
                worldPosition +
                dragOffset;
        }

        UpdateGhostPreview(
            screenPosition);
    }

    private void EndDrag(
        Vector2 screenPosition)
    {
        UpdateGhostPreview(
            screenPosition);

        if (previewGrid != null &&
            previewCanPlace)
        {
            PlaceOnGrid(
                previewGrid,
                previewColumn,
                previewRow);
        }
        else
        {
            ReturnToPreviousPosition();
        }

        HideGhost();

        isDragging = false;
    }

    // =========================================================
    // PLACEMENT GHOST
    // =========================================================

    private void UpdateGhostPreview(
        Vector2 screenPosition)
    {
        previewGrid = null;
        previewColumn = -1;
        previewRow = -1;
        previewCanPlace = false;

        if (ghostObject == null)
            return;

        Ray ray =
            playerCamera.ScreenPointToRay(
                screenPosition);

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            Mathf.Infinity,
            shelfGridLayer))
        {
            ghostObject.SetActive(false);
            return;
        }

        ShelfGrid grid =
            hit.collider.GetComponentInParent<
                ShelfGrid>();

        if (grid == null)
        {
            ghostObject.SetActive(false);
            return;
        }

        if (!grid.TryGetClosestCell(
            hit.point,
            out int column,
            out int row))
        {
            ghostObject.SetActive(false);
            return;
        }

        previewGrid =
            grid;

        previewColumn =
            column;

        previewRow =
            row;

        previewCanPlace =
            grid.IsCellFree(
                column,
                row);

        Vector3 targetPosition =
            grid.GetCellWorldPosition(
                column,
                row);

        ghostObject.transform.position =
            targetPosition +
            placementOffset;

        ghostObject.transform.rotation =
            startingRotation;

        ghostObject.SetActive(true);

        SetGhostColor(
            previewCanPlace
                ? validGhostColor
                : invalidGhostColor);
    }

    private void CreateGhost()
    {
        if (ghostObject != null)
            return;

        if (ghostMaterial == null)
        {
            Debug.LogWarning(
                $"{name}: No Ghost Material assigned.");

            return;
        }

        ghostObject = Instantiate(
            gameObject,
            transform.position,
            transform.rotation);

        ghostObject.name =
            gameObject.name +
            "_PlacementGhost";

        // IMPORTANT:
        // Disable scripts rather than Destroying them.
        // This prevents errors from components such as
        // ButtonAnimator requiring a Button component.
        MonoBehaviour[] behaviours =
            ghostObject.GetComponentsInChildren<
                MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour
                 in behaviours)
        {
            behaviour.enabled = false;
        }

        // Ghost must never block raycasts.
        Collider[] colliders =
            ghostObject.GetComponentsInChildren<
                Collider>(true);

        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Rigidbody[] rigidbodies =
            ghostObject.GetComponentsInChildren<
                Rigidbody>(true);

        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // Hide all UI from ghost copy.
        Canvas[] canvases =
            ghostObject.GetComponentsInChildren<
                Canvas>(true);

        foreach (Canvas canvas in canvases)
        {
            canvas.gameObject.SetActive(false);
        }

        ghostRuntimeMaterial =
            new Material(
                ghostMaterial);

        Renderer[] renderers =
            ghostObject.GetComponentsInChildren<
                Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] materials =
                new Material[
                    renderer.sharedMaterials.Length];

            for (int i = 0;
                 i < materials.Length;
                 i++)
            {
                materials[i] =
                    ghostRuntimeMaterial;
            }

            renderer.materials =
                materials;
        }

        ghostObject.SetActive(false);
    }

    private void SetGhostColor(
        Color color)
    {
        if (ghostRuntimeMaterial == null)
            return;

        if (ghostRuntimeMaterial.HasProperty(
            "_BaseColor"))
        {
            ghostRuntimeMaterial.SetColor(
                "_BaseColor",
                color);
        }

        if (ghostRuntimeMaterial.HasProperty(
            "_Color"))
        {
            ghostRuntimeMaterial.SetColor(
                "_Color",
                color);
        }
    }

    private void HideGhost()
    {
        if (ghostObject != null)
            ghostObject.SetActive(false);

        previewGrid = null;
        previewColumn = -1;
        previewRow = -1;
        previewCanPlace = false;
    }

    // =========================================================
    // GRID
    // =========================================================

    private void PlaceOnGrid(
        ShelfGrid grid,
        int column,
        int row)
    {
        if (!grid.PlaceObject(
            gameObject,
            column,
            row))
        {
            ReturnToPreviousPosition();
            return;
        }

        currentGrid =
            grid;

        currentColumn =
            column;

        currentRow =
            row;

        Vector3 targetPosition =
            grid.GetCellWorldPosition(
                column,
                row);

        transform.position =
            targetPosition +
            placementOffset;

        transform.rotation =
            startingRotation;
    }

    private void ReturnToPreviousPosition()
    {
        transform.position =
            startingPosition;

        transform.rotation =
            startingRotation;

        if (previousGrid != null)
        {
            if (previousGrid.PlaceObject(
                gameObject,
                previousColumn,
                previousRow))
            {
                currentGrid =
                    previousGrid;

                currentColumn =
                    previousColumn;

                currentRow =
                    previousRow;
            }
        }
    }

    // =========================================================
    // BOX UI
    // =========================================================

    public void ShowInteractionUI()
    {
        if (interactionUIRoot != null)
            interactionUIRoot.SetActive(true);
    }

    public void HideInteractionUI()
    {
        if (interactionUIRoot != null)
            interactionUIRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (ghostObject != null)
            Destroy(ghostObject);

        if (ghostRuntimeMaterial != null)
            Destroy(ghostRuntimeMaterial);
    }
}
