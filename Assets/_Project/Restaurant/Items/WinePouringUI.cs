using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Authored, mouse/touch wine refill. Inventory and carried props belong to the calling service.</summary>
[DisallowMultipleComponent]
public sealed class WinePouringUI : MonoBehaviour
{
    public static WinePouringUI Instance { get; private set; }

    [Header("Layout and artwork")]
    public RectTransform safeArea;
    public RectTransform stage;
    public Vector2 authoredStageSize = new Vector2(1400f, 760f);
    public CanvasGroup panelGroup;
    public RectTransform bottle;
    public RectTransform bottleMouth;
    public RectTransform glassOpening;
    public UnityEngine.UI.Image liquid;
    public UnityEngine.UI.Image stream;
    public UnityEngine.UI.Image splash;
    public Sprite[] splashFrames;
    public RectTransform targetMarker;
    public RectTransform successMark;
    public TMP_Text instruction;
    public UnityEngine.UI.Button backButton;

    [Header("Water service artwork")]
    public Sprite waterPitcherSprite;
    public Sprite waterFillSprite;
    public Vector2 waterPitcherSize = new Vector2(430f, 430f);
    public Vector2 waterPitcherMouthAnchor = new Vector2(.17f, .84f);
    public Color waterStreamColor = new Color(.6f, .85f, 1f, .85f);

    [Header("Forgiving pouring interaction")]
    [Range(30f, 100f)] public float pourTiltDegrees = 65f;
    [Min(10f)] public float captureRadius = 185f;
    [Min(1f)] public float mouthTolerance = 60f;
    public Vector2 pourMouthOffset = new Vector2(35f, 70f);
    [Min(0.02f)] public float followSeconds = 0.15f;
    [Min(0.25f)] public float pourSeconds = 2.4f;
    [Range(0.1f, 1f)] public float targetFill = 0.78f;
    [Range(0f, 0.5f)] public float initialFill = 0.05f;
    [Min(0f)] public float successSeconds = 0.3f;
    [Min(0.01f)] public float splashFrameSeconds = 0.1f;
    [Min(1f)] public float streamWidth = 20f;
    [Min(0f)] public float safeAreaPadding = 32f;

    [Header("Optional existing UI audio")]
    public AudioSource feedbackAudio;
    public AudioClip successSound;
    [Range(0f, 1f)] public float successVolume = 0.6f;

    private Action succeeded;
    private Action cancelled;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector2 pointerPosition;
    private Vector2 pointerOffset;
    private Vector2 motionVelocity;
    private bool initialized;
    private bool open;
    private bool dragging;
    private bool completing;
    private int pointerId;
    private float fill;
    private float splashTime;
    private Vector2 screenSize;
    private Rect lastSafeArea;
    private bool waterService;
    private UnityEngine.UI.Image containerImage;
    private Sprite wineBottleSprite, wineFillSprite, wineStreamSprite, wineSplashSprite;
    private Vector2 wineBottleSize, wineMouthAnchor;
    private Color wineStreamColor;
    private TMP_Text title;

    public bool IsOpen => open && gameObject.activeInHierarchy;
    public bool IsDragging => IsOpen && dragging;
    public bool SupportsWaterService => waterPitcherSprite != null && waterFillSprite != null;
    public float CurrentFill => fill;

    private void Awake()
    {
        Initialize();
        if (!open) SetVisible(false);
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        Instance = this;
        if (panelGroup == null) panelGroup = GetComponent<CanvasGroup>();
        if (bottle != null)
        {
            restPosition = bottle.localPosition;
            restRotation = bottle.localRotation;
            containerImage = bottle.GetComponent<UnityEngine.UI.Image>();
            wineBottleSprite = containerImage != null ? containerImage.sprite : null;
            wineBottleSize = bottle.sizeDelta;
        }
        if (bottleMouth != null) wineMouthAnchor = bottleMouth.anchorMin;
        wineFillSprite = liquid != null ? liquid.sprite : null;
        wineStreamSprite = stream != null ? stream.sprite : null;
        wineStreamColor = stream != null ? stream.color : Color.white;
        wineSplashSprite = splash != null ? splash.sprite : null;
        if (safeArea != null) title = safeArea.Find("Title")?.GetComponent<TMP_Text>();
        if (backButton != null) backButton.onClick.AddListener(Cancel);
    }

    public bool Open(Action onSuccess, Action onCancel)
        => Open(false, onSuccess, onCancel);

    public bool Open(bool pourWater, Action onSuccess, Action onCancel)
    {
        Initialize();
        if (open || stage == null || bottle == null || bottleMouth == null ||
            glassOpening == null || liquid == null || panelGroup == null) return false;
        if (pourWater && !SupportsWaterService) return false;
        waterService = pourWater;
        if (containerImage != null) containerImage.sprite = waterService ? waterPitcherSprite : wineBottleSprite;
        bottle.sizeDelta = waterService ? waterPitcherSize : wineBottleSize;
        bottleMouth.anchorMin = bottleMouth.anchorMax = waterService ? waterPitcherMouthAnchor : wineMouthAnchor;
        liquid.sprite = waterService ? waterFillSprite : wineFillSprite;
        if (stream != null)
        {
            stream.sprite = waterService ? null : wineStreamSprite;
            stream.color = waterService ? waterStreamColor : wineStreamColor;
        }
        if (splash != null) splash.sprite = waterService ? waterFillSprite : wineSplashSprite;
        if (title != null) title.text = waterService ? "WATER SERVICE" : "WINE REFILL";
        succeeded = onSuccess;
        cancelled = onCancel;
        open = true;
        completing = dragging = false;
        motionVelocity = Vector2.zero;
        fill = Mathf.Min(initialFill, targetFill * 0.5f);
        liquid.fillAmount = fill;
        if (targetMarker != null)
        {
            Rect bounds = liquid.rectTransform.rect;
            targetMarker.localPosition = stage.InverseTransformPoint(liquid.rectTransform.TransformPoint(new Vector3(bounds.center.x, Mathf.Lerp(bounds.yMin, bounds.yMax, targetFill), 0f)));
        }
        bottle.localPosition = restPosition;
        bottle.localRotation = restRotation;
        if (successMark != null) successMark.gameObject.SetActive(false);
        if (targetMarker != null) targetMarker.gameObject.SetActive(true);
        if (instruction != null) instruction.text = waterService ? "Drag the pitcher over the glass" : "Drag the bottle over the glass";
        SetPourVisible(false);
        SetVisible(true);
        transform.SetAsLastSibling();
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, true);
        ApplyLayout();
        return true;
    }

    public void Cancel()
    {
        if (!open || completing) return;
        Action callback = cancelled;
        CloseWithoutResult();
        callback?.Invoke();
    }

    /// <summary>Scene/service teardown: callbacks are deliberately not invoked.</summary>
    public void CloseWithoutResult()
    {
        StopAllCoroutines();
        succeeded = cancelled = null;
        open = dragging = completing = false;
        SetPourVisible(false);
        SetVisible(false);
    }

    private void OnDisable()
    {
        // External panel closure must restore the caller's temporary carry state as well.
        if (!open) return;
        Action callback = cancelled;
        open = dragging = completing = false;
        succeeded = cancelled = null;
        StopAllCoroutines();
        callback?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (backButton != null) backButton.onClick.RemoveListener(Cancel);
    }

    public void BeginBottleDrag(PointerEventData eventData)
    {
        if (!IsOpen || completing || dragging) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(stage,
                eventData.position, eventData.pressEventCamera, out Vector2 point)) return;
        dragging = true;
        pointerId = eventData.pointerId;
        pointerOffset = (Vector2)bottle.localPosition - point;
        pointerPosition = (Vector2)bottle.localPosition;
    }

    public void DragBottle(PointerEventData eventData)
    {
        if (!dragging || pointerId != eventData.pointerId || completing) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(stage,
                eventData.position, eventData.pressEventCamera, out Vector2 point))
            pointerPosition = point + pointerOffset;
    }

    public void EndBottleDrag(PointerEventData eventData)
    {
        if (!dragging || pointerId != eventData.pointerId) return;
        dragging = false;
        SetPourVisible(false);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (screenSize.x != Screen.width || screenSize.y != Screen.height || lastSafeArea != Screen.safeArea)
            ApplyLayout();
        if (completing) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        Quaternion tilt = Quaternion.Euler(0f, 0f, pourTiltDegrees);
        Vector2 opening = stage.InverseTransformPoint(glassOpening.position);
        Vector2 desiredMouth = opening + pourMouthOffset;
        Vector2 mouthFromPivot = bottle.InverseTransformPoint(bottleMouth.position);
        Vector2 pourPosition = desiredMouth - (Vector2)(tilt * mouthFromPivot);
        bool close = dragging && Vector2.Distance(pointerPosition, pourPosition) <= captureRadius;
        Vector2 destination = !dragging ? (Vector2)restPosition : close ? pourPosition : ClampPointer(pointerPosition);
        float smoothing = LevelOneUIAccessibility.ReducedMotion ? 0.025f : followSeconds;
        bottle.localPosition = Vector2.SmoothDamp(bottle.localPosition, destination, ref motionVelocity,
            Mathf.Max(0.02f, smoothing), Mathf.Infinity, dt);
        bottle.localRotation = Quaternion.Slerp(bottle.localRotation, close ? tilt : restRotation,
            1f - Mathf.Exp(-dt / Mathf.Max(0.02f, smoothing)));
        Vector2 mouth = stage.InverseTransformPoint(bottleMouth.position);
        bool pouring = close && Vector2.Distance(mouth, desiredMouth) < mouthTolerance &&
            Quaternion.Angle(bottle.localRotation, tilt) < 14f;
        SetPourVisible(pouring);
        if (instruction != null)
            instruction.text = pouring ? "Hold to fill" : waterService ? "Drag the pitcher over the glass" : "Drag the bottle over the glass";
        if (!pouring) return;

        fill = Mathf.MoveTowards(fill, targetFill, dt * Mathf.Max(0.01f, targetFill - Mathf.Min(initialFill, targetFill * 0.5f)) / Mathf.Max(0.25f, pourSeconds));
        liquid.fillAmount = fill;
        AnimateStream(mouth, opening, dt);
        if (fill >= targetFill) StartCoroutine(CompleteRoutine());
    }

    private Vector2 ClampPointer(Vector2 point)
    {
        Rect bounds = stage.rect;
        point.x = Mathf.Clamp(point.x, bounds.xMin + 70f, bounds.xMax - 70f);
        point.y = Mathf.Clamp(point.y, bounds.yMin + 60f, bounds.yMax - 70f);
        return point;
    }

    private void AnimateStream(Vector2 mouth, Vector2 opening, float dt)
    {
        Rect bounds = liquid.rectTransform.rect;
        Vector2 end = stage.InverseTransformPoint(liquid.rectTransform.TransformPoint(new Vector3(bounds.center.x, Mathf.Lerp(bounds.yMin, bounds.yMax, fill), 0f)));
        if (stream != null)
        {
            RectTransform rect = stream.rectTransform;
            rect.localPosition = (mouth + end) * 0.5f;
            Vector2 direction = mouth - end;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            rect.sizeDelta = new Vector2(streamWidth * (1f + Mathf.Sin(Time.unscaledTime * 18f) * 0.06f), direction.magnitude);
        }
        if (splash != null)
        {
            splash.rectTransform.localPosition = end;
            splashTime += dt;
            if (!waterService && splashFrames != null && splashFrames.Length > 0)
                splash.sprite = splashFrames[(int)(splashTime / Mathf.Max(0.01f, splashFrameSeconds)) % splashFrames.Length];
        }
    }

    private IEnumerator CompleteRoutine()
    {
        completing = true;
        dragging = false;
        SetPourVisible(false);
        if (targetMarker != null) targetMarker.gameObject.SetActive(false);
        if (instruction != null) instruction.text = "Perfect pour!";
        if (successMark != null) successMark.gameObject.SetActive(true);
        if (feedbackAudio != null && successSound != null)
            feedbackAudio.PlayOneShot(successSound, successVolume);
        Vector3 startPosition = bottle.localPosition;
        Quaternion startRotation = bottle.localRotation;
        float duration = LevelOneUIAccessibility.ReducedMotion ? 0.15f : Mathf.Max(0.15f, successSeconds);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            bottle.localPosition = Vector3.Lerp(startPosition, restPosition, t);
            bottle.localRotation = Quaternion.Slerp(startRotation, restRotation, t);
            if (successMark != null)
                successMark.localScale = Vector3.one * (LevelOneUIAccessibility.ReducedMotion ? 1f : 1f + Mathf.Sin(t * Mathf.PI) * 0.16f);
            yield return null;
        }
        Action callback = succeeded;
        CloseWithoutResult();
        callback?.Invoke();
    }

    private void SetPourVisible(bool visible)
    {
        if (stream != null) stream.gameObject.SetActive(visible);
        if (splash != null) splash.gameObject.SetActive(visible);
    }

    private void SetVisible(bool visible)
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = visible ? 1f : 0f;
            panelGroup.interactable = visible;
            panelGroup.blocksRaycasts = visible;
        }
        gameObject.SetActive(visible);
    }

    private void ApplyLayout()
    {
        screenSize = new Vector2(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;
        if (safeArea != null && Screen.width > 0 && Screen.height > 0)
        {
            safeArea.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            safeArea.offsetMin = Vector2.one * safeAreaPadding;
            safeArea.offsetMax = -Vector2.one * safeAreaPadding;
            Canvas.ForceUpdateCanvases();
            float scale = Mathf.Min(safeArea.rect.width / Mathf.Max(1f, authoredStageSize.x),
                (safeArea.rect.height - 140f) / Mathf.Max(1f, authoredStageSize.y));
            stage.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }
    }
}
