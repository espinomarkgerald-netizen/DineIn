using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TapOutlineSelector : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask selectableMask;

    private Outline currentOutline;
    private IInteractable selectedInteraction;
    private Booth selectedCleaning;
    private Component selectedComponent;
    private static readonly List<TapOutlineSelector> selectors = new();
    private bool UsesAcceptedTargets => gameObject.scene.name == "Lobby2";

    private void OnEnable() { if (!selectors.Contains(this)) selectors.Add(this); }
    private void OnDisable() { Clear(); selectors.Remove(this); }

    // Lobby2 uses the movement/cleaning resolver's accepted target. There is no
    // second raycast that can highlight a different object behind the chosen one.
    public static void PresentFor(PlayerMovement mover, IInteractable interaction)
    {
        if (mover == null || mover.gameObject.scene.name != "Lobby2") return;
        foreach (var selector in selectors)
            if (selector != null && selector.gameObject.scene == mover.gameObject.scene)
                selector.Select(interaction as Component, interaction, null);
    }

    public static void PresentCleaning(PlayerMovement mover, Booth booth)
    {
        if (mover == null || mover.gameObject.scene.name != "Lobby2") return;
        foreach (var selector in selectors)
            if (selector != null && selector.gameObject.scene == mover.gameObject.scene)
                selector.Select(booth, null, booth);
    }

    private void Select(Component target, IInteractable interaction, Booth cleaning)
    {
        Clear();
        if (target == null || interaction != null && !interaction.CanInteract()) return;
        currentOutline = target.GetComponentInParent<Outline>();
        if (currentOutline == null) currentOutline = target.GetComponentInChildren<Outline>(true);
        if (currentOutline == null) return;
        selectedComponent = target;
        selectedInteraction = interaction;
        selectedCleaning = cleaning;
        currentOutline.enabled = true;
        PublishSelection(target.transform);
    }

    private void LateUpdate()
    {
        if (!UsesAcceptedTargets || currentOutline == null) return;
        if (selectedComponent == null || !selectedComponent.gameObject.activeInHierarchy ||
            GameplayUIBlocker.IsBlocked() || HygieneManager.InputBlocked ||
            selectedInteraction != null && !selectedInteraction.CanInteract() ||
            selectedCleaning != null && (!selectedCleaning.CanRequestHumanCleanup || !selectedCleaning.CleaningPromptOpen))
            Clear();
    }

    public event Action<Transform> SelectionSucceeded;
    public Transform CurrentSelection => currentOutline != null ? currentOutline.transform : null;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    void Update()
    {
        if (UsesAcceptedTargets) return;
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
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, selectableMask))
        {
            // Turn off old outline
            if (currentOutline != null)
                currentOutline.enabled = false;
            currentOutline = null;

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
        selectedInteraction = null;
        selectedCleaning = null;
        selectedComponent = null;
    }
}
