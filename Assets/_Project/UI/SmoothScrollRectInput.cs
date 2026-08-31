using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds eased mouse-wheel scrolling to an existing ScrollRect while leaving its
/// authored viewport, mask, content layout, buttons, and touch drag behavior intact.
/// Add this component to a particular ScrollRect to override the global defaults in
/// the Inspector; otherwise the runtime installer supplies the same moderate values.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScrollRect))]
public sealed class SmoothScrollRectInput : MonoBehaviour, IScrollHandler
{
    [Header("Wheel Scrolling")]
    [SerializeField, Range(0.01f, 0.3f)] private float normalizedStep = 0.085f;
    [SerializeField, Range(0.02f, 0.4f)] private float smoothTime = 0.1f;
    [SerializeField, Min(0.1f)] private float maximumSmoothSpeed = 8f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Touch / Drag Inertia")]
    [SerializeField] private bool configureDragInertia = true;
    [SerializeField, Range(0.01f, 0.5f)] private float dragDecelerationRate = 0.12f;
    [SerializeField, Range(0f, 1f)] private float elasticity = 0.1f;

    private ScrollRect scrollRect;
    private float authoredScrollSensitivity;
    private float targetHorizontal;
    private float targetVertical;
    private float horizontalVelocity;
    private float verticalVelocity;
    private bool smoothingWheel;
    private int lastQueuedFrame = -1;
    private Vector2 lastQueuedDelta;

    public float NormalizedStep => normalizedStep;
    public float SmoothTime => smoothTime;

    /// <summary>Runs the authored settings immediately for editor regression validation.</summary>
    public void ApplyNowForValidation()
    {
        ResolveScrollRect();
        ApplySettings();
        SynchronizeTargets();
    }

    private void Awake()
    {
        ResolveScrollRect();
        ApplySettings();
        SynchronizeTargets();
    }

    private void OnEnable()
    {
        ResolveScrollRect();
        ApplySettings();
        SynchronizeTargets();
    }

    private void OnDisable()
    {
        smoothingWheel = false;
        if (scrollRect != null)
            scrollRect.scrollSensitivity = authoredScrollSensitivity;
    }

    private void Update()
    {
        if (!smoothingWheel || scrollRect == null || scrollRect.content == null)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        bool settled = true;
        if (scrollRect.horizontal)
        {
            float next = Mathf.SmoothDamp(
                scrollRect.horizontalNormalizedPosition,
                targetHorizontal,
                ref horizontalVelocity,
                smoothTime,
                maximumSmoothSpeed,
                deltaTime);
            scrollRect.horizontalNormalizedPosition = next;
            settled &= Mathf.Abs(next - targetHorizontal) < 0.0005f;
        }

        if (scrollRect.vertical)
        {
            float next = Mathf.SmoothDamp(
                scrollRect.verticalNormalizedPosition,
                targetVertical,
                ref verticalVelocity,
                smoothTime,
                maximumSmoothSpeed,
                deltaTime);
            scrollRect.verticalNormalizedPosition = next;
            settled &= Mathf.Abs(next - targetVertical) < 0.0005f;
        }

        if (!settled)
            return;

        if (scrollRect.horizontal)
            scrollRect.horizontalNormalizedPosition = targetHorizontal;
        if (scrollRect.vertical)
            scrollRect.verticalNormalizedPosition = targetVertical;
        horizontalVelocity = 0f;
        verticalVelocity = 0f;
        smoothingWheel = false;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (eventData == null)
            return;
        QueueScrollDelta(eventData.scrollDelta);
    }

    /// <summary>Used by modal canvases whose Unity event depth is temporarily invalid.</summary>
    public void QueueScrollDelta(Vector2 scrollDelta)
    {
        ResolveScrollRect();
        if (scrollRect == null || scrollRect.content == null || scrollDelta.sqrMagnitude <= 0.0001f)
            return;

        // The management-computer fallback and EventSystem can both observe the
        // same wheel tick. Accept it once so it never becomes a double-sized jump.
        if (lastQueuedFrame == Time.frameCount &&
            (lastQueuedDelta - scrollDelta).sqrMagnitude < 0.0001f)
            return;
        lastQueuedFrame = Time.frameCount;
        lastQueuedDelta = scrollDelta;

        if (!smoothingWheel)
            SynchronizeTargets();

        scrollRect.StopMovement();
        if (scrollRect.vertical)
        {
            targetVertical = Mathf.Clamp01(
                targetVertical + scrollDelta.y * normalizedStep);
        }
        else if (scrollRect.horizontal)
        {
            float amount = Mathf.Abs(scrollDelta.x) > Mathf.Abs(scrollDelta.y)
                ? scrollDelta.x
                : scrollDelta.y;
            targetHorizontal = Mathf.Clamp01(
                targetHorizontal - amount * normalizedStep);
        }

        smoothingWheel = true;
    }

    private void ResolveScrollRect()
    {
        if (scrollRect != null)
            return;
        scrollRect = GetComponent<ScrollRect>();
        if (scrollRect != null)
            authoredScrollSensitivity = scrollRect.scrollSensitivity;
    }

    private void ApplySettings()
    {
        if (scrollRect == null)
            return;

        // Unity's immediate wheel movement is disabled; this component supplies
        // the eased movement. Pointer/touch dragging does not use this value.
        scrollRect.scrollSensitivity = 0f;
        if (configureDragInertia)
        {
            scrollRect.inertia = true;
            scrollRect.decelerationRate = dragDecelerationRate;
            scrollRect.elasticity = elasticity;
        }
    }

    private void SynchronizeTargets()
    {
        if (scrollRect == null)
            return;
        targetHorizontal = scrollRect.horizontalNormalizedPosition;
        targetVertical = scrollRect.verticalNormalizedPosition;
        horizontalVelocity = 0f;
        verticalVelocity = 0f;
    }
}

/// <summary>Applies the shared scrolling behavior to ScrollRects in every scene.</summary>
internal sealed class SmoothScrollRectInstaller : MonoBehaviour
{
    private const float ScanInterval = 0.75f;
    private static SmoothScrollRectInstaller instance;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;
        GameObject root = new GameObject("[Smooth ScrollRect Installer]");
        instance = root.AddComponent<SmoothScrollRectInstaller>();
        DontDestroyOnLoad(root);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyToLoadedScrollRects();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + ScanInterval;
        ApplyToLoadedScrollRects();
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        nextScan = 0f;
        ApplyToLoadedScrollRects();
    }

    private static void ApplyToLoadedScrollRects()
    {
        ScrollRect[] scrollRects = FindObjectsByType<ScrollRect>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            ScrollRect scroll = scrollRects[i];
            if (scroll != null && scroll.GetComponent<SmoothScrollRectInput>() == null)
                scroll.gameObject.AddComponent<SmoothScrollRectInput>();
        }
    }
}
