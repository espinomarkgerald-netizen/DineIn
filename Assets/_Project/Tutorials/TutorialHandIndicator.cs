using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Reusable top-layer Mobile hand or PC mouse/cursor demonstration.</summary>
[DisallowMultipleComponent]
public sealed class TutorialHandIndicator : MonoBehaviour
{
    public enum HintMode { Hidden, Swipe, Tap, Zoom, Typing, Drag, Hold }

    [Header("Hand Image")]
    [SerializeField] private RectTransform handRect;
    [SerializeField] private Image handImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Mobile Sprites")]
    [SerializeField] private Sprite handOpenSprite;
    [SerializeField] private Sprite handClickSprite;

    [Header("PC Sprites")]
    [SerializeField] private Sprite cursorSprite;
    [SerializeField] private Sprite mouseSprite;
    [SerializeField] private Sprite mouseLeftClickSprite;
    [SerializeField] private Sprite mouseRightClickSprite;
    [SerializeField] private Sprite mouseScrollSprite;

    [Header("Canvas Reference")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera worldCamera;

    [Header("Timing")]
    [SerializeField, Min(0.5f)] private float swipeCycleSeconds = 2.1f;
    [SerializeField, Range(0.05f, 0.4f)] private float swipeTravelCanvasFraction = 0.22f;
    [SerializeField, Min(0.3f)] private float tapCycleSeconds = 1.15f;
    [SerializeField, Min(0.5f)] private float zoomCycleSeconds = 2f;

    [Header("Visual")]
    [SerializeField] private float handDisplaySize = 168f;
    [SerializeField] private float cursorDisplaySize = 76f;
    [SerializeField] private float mouseDisplaySize = 104f;

    // Retained so old scene YAML remains compatible.
    [SerializeField] private Sprite swipeSprite;
    [SerializeField] private Sprite tapSprite;

    private HintMode mode;
    private Transform currentTarget;
    private float cycleStartedAt;
    private bool initialized;
    private bool mobilePresentation;
    private RectTransform hintRoot;
    private Image pinchPartner;
    private Image mouseLegend;
    private Image cursorImage;
    private TMP_Text typingCue;
    private TMP_Text dragCue;
    private Transform dragEndTarget;
    private Vector2 dragEndOffset;

    public HintMode Mode => mode;
    public Transform CurrentTarget => currentTarget;
    public bool IsVisible => mode != HintMode.Hidden && gameObject.activeSelf;
    public Sprite CurrentSprite => handImage != null ? handImage.sprite : null;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        handDisplaySize = 180f;
        cursorDisplaySize = 56f;
        mouseDisplaySize = 160f;
        if (handOpenSprite == null) handOpenSprite = swipeSprite;
        if (handClickSprite == null) handClickSprite = tapSprite;
        if (handRect == null) handRect = transform as RectTransform;
        if (handImage == null) handImage = GetComponent<Image>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>(true);
        if (worldCamera == null) worldCamera = Camera.main;

        Canvas ownCanvas = GetComponent<Canvas>();
        if (ownCanvas == null) ownCanvas = gameObject.AddComponent<Canvas>();
        ownCanvas.overrideSorting = true;
        ownCanvas.sortingOrder = 32767;
        if (handImage != null)
        {
            handImage.preserveAspect = true;
            handImage.raycastTarget = false;
        }
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        EnsureHintRoot();
        HideHint();
    }

    private void EnsureHintRoot()
    {
        if (hintRoot != null) return;
        GameObject root = new GameObject("Tutorial Top Input Hints", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasGroup));
        root.layer = gameObject.layer;
        // A scene-root overlay survives panels disabling/rebuilding their own
        // canvases (Computer, Menu, Restock, Notepad and Cashier).
        root.transform.SetParent(null, false);
        hintRoot = (RectTransform)root.transform;
        hintRoot.anchorMin = Vector2.zero;
        hintRoot.anchorMax = Vector2.one;
        hintRoot.offsetMin = hintRoot.offsetMax = Vector2.zero;
        Canvas layer = root.GetComponent<Canvas>();
        layer.renderMode = RenderMode.ScreenSpaceOverlay;
        layer.overrideSorting = true;
        layer.sortingOrder = 32767;
        CanvasGroup input = root.GetComponent<CanvasGroup>();
        input.interactable = false;
        input.blocksRaycasts = false;
        // Move the real Hand / Hand Click image into the same topmost canvas.
        handRect.SetParent(hintRoot, false);
        handRect.SetAsLastSibling();
        pinchPartner = CreateImage("Pinch Partner", handOpenSprite, handDisplaySize);
        mouseLegend = CreateImage("Mouse Legend", mouseSprite, mouseDisplaySize);
        cursorImage = CreateImage("Cursor", cursorSprite, cursorDisplaySize);
        typingCue = CreateTypingCue();
        dragCue = CreateCue("Drag Hold Cue", new Vector2(320f, 64f));
        SetAuxiliaryVisible(false, false, false);
    }

    private Image CreateImage(string objectName, Sprite sprite, float size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(hintRoot, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        image.rectTransform.sizeDelta = Vector2.one * size;
        return image;
    }

    private TMP_Text CreateTypingCue()
    {
        GameObject go = new GameObject("Price Typing Cue", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        go.transform.SetParent(hintRoot, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontSize = 28f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.88f, 0.22f, 1f);
        text.outlineColor = new Color32(8, 18, 28, 255);
        text.outlineWidth = 0.22f;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.sizeDelta = new Vector2(250f, 84f);
        text.gameObject.SetActive(false);
        return text;
    }

    private TMP_Text CreateCue(string objectName, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        go.transform.SetParent(hintRoot, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.88f, 0.22f, 1f);
        text.outlineColor = new Color32(8, 18, 28, 255);
        text.outlineWidth = 0.22f;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.sizeDelta = size;
        text.gameObject.SetActive(false);
        return text;
    }

    private void LateUpdate()
    {
        if (mode == HintMode.Hidden || !gameObject.activeSelf) return;
        // Restock disables lobby canvases, including this detached overlay.
        // Restore only our own hint canvases while a tutorial hint is active.
        if (hintRoot != null) hintRoot.GetComponent<Canvas>().enabled = true;
        Canvas own = GetComponent<Canvas>();
        if (own != null) own.enabled = true;
        if (cursorImage != null) cursorImage.rectTransform.localScale = Vector3.one;
        if (handRect != null) handRect.localScale = Vector3.one;
        if (mode == HintMode.Tap) AnimateTap();
        else if (mode == HintMode.Swipe) AnimateSwipe();
        else if (mode == HintMode.Zoom) AnimateZoom();
        else if (mode == HintMode.Typing) { AnimateTap(); AnimateTyping(); }
        else if (mode == HintMode.Drag || mode == HintMode.Hold) AnimateDrag();
        float age = Time.unscaledTime - cycleStartedAt;
        float pop = 1f + .16f * Mathf.Sin(Mathf.Clamp01(age / .28f) * Mathf.PI);
        if (mobilePresentation && handRect != null) handRect.localScale *= pop;
        if (!mobilePresentation && cursorImage != null) cursorImage.rectTransform.localScale *= pop;
    }

    public void ApplyDebugTuning(float cursor, float mouse, float hand)
    {
        Initialize();
        cursorDisplaySize = 56f * Mathf.Clamp(cursor, .25f, 3f);
        mouseDisplaySize = 160f * Mathf.Clamp(mouse, .25f, 3f);
        handDisplaySize = 180f * Mathf.Clamp(hand, .25f, 3f);
        if (cursorImage != null) cursorImage.rectTransform.sizeDelta = Vector2.one * cursorDisplaySize;
        if (mouseLegend != null) mouseLegend.rectTransform.sizeDelta = Vector2.one * mouseDisplaySize;
        if (handRect != null) handRect.sizeDelta = Vector2.one * handDisplaySize;
        if (pinchPartner != null) pinchPartner.rectTransform.sizeDelta = Vector2.one * handDisplaySize;
    }

    public void ShowSwipeHint()
    {
        Begin(HintMode.Swipe, null);
        mobilePresentation = TutorialInputTerminology.IsMobile;
        if (mobilePresentation) ShowMobileHand(handClickSprite);
        else ShowPC(mouseRightClickSprite);
    }

    public void ShowTapHint(Transform target)
    {
        if (target == null) { HideHint(); return; }
        Begin(HintMode.Tap, target);
        mobilePresentation = TutorialInputTerminology.IsMobile;
        if (mobilePresentation) ShowMobileHand(handOpenSprite);
        else ShowPC(mouseSprite);
    }

    public void ShowZoomHint(bool mobile)
    {
        Begin(HintMode.Zoom, null);
        mobilePresentation = mobile;
        if (mobilePresentation)
        {
            ShowMobileHand(handOpenSprite);
            pinchPartner.gameObject.SetActive(true);
            pinchPartner.sprite = handOpenSprite;
            pinchPartner.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }
        else ShowPC(mouseScrollSprite);
    }

    public void ShowTypingHint(Transform target)
    {
        if (target == null) { HideHint(); return; }
        Begin(HintMode.Typing, target);
        mobilePresentation = TutorialInputTerminology.IsMobile;
        if (mobilePresentation) ShowMobileHand(handOpenSprite);
        else ShowPC(mouseLeftClickSprite);
        if (typingCue != null) typingCue.gameObject.SetActive(true);
    }

    public void ShowDragHint(Transform start, Transform end)
    {
        if (start == null || end == null) { HideHint(); return; }
        Begin(HintMode.Drag, start);
        dragEndTarget = end;
        dragEndOffset = Vector2.zero;
        mobilePresentation = TutorialInputTerminology.IsMobile;
        if (mobilePresentation) ShowMobileHand(handOpenSprite);
        else ShowPC(mouseSprite);
        if (dragCue != null) dragCue.gameObject.SetActive(true);
    }

    public void ShowHoldHint(Transform target)
    {
        if (target == null) { HideHint(); return; }
        ShowDragHint(target, target);
        mode = HintMode.Hold;
    }

    public void ShowSmallDragHint(Transform target)
    {
        if (target == null) { HideHint(); return; }
        Begin(HintMode.Drag, target);
        dragEndTarget = target;
        dragEndOffset = new Vector2(64f, 18f);
        mobilePresentation = TutorialInputTerminology.IsMobile;
        if (mobilePresentation) ShowMobileHand(handOpenSprite);
        else ShowPC(mouseSprite);
        if (dragCue != null) dragCue.gameObject.SetActive(true);
    }

    public void HideHint()
    {
        mode = HintMode.Hidden;
        currentTarget = null;
        dragEndTarget = null;
        dragEndOffset = Vector2.zero;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        SetAuxiliaryVisible(false, false, false);
        if (typingCue != null) typingCue.gameObject.SetActive(false);
        if (dragCue != null) dragCue.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void Begin(HintMode nextMode, Transform target)
    {
        Initialize();
        mode = nextMode;
        currentTarget = target;
        cycleStartedAt = Time.unscaledTime;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (hintRoot != null) hintRoot.SetAsLastSibling();
        if (cursorImage != null)
        {
            cursorImage.color = Color.white;
            cursorImage.rectTransform.localScale = Vector3.one;
        }
        if (mouseLegend != null)
        {
            mouseLegend.color = Color.white;
            mouseLegend.rectTransform.localScale = Vector3.one;
        }
        if (handRect != null)
        {
            handRect.sizeDelta = Vector2.one * handDisplaySize;
            handRect.localScale = Vector3.one;
            handRect.localRotation = Quaternion.identity;
        }
        SetAuxiliaryVisible(false, false, false);
        if (typingCue != null) typingCue.gameObject.SetActive(false);
        if (dragCue != null) dragCue.gameObject.SetActive(false);
    }

    private void ShowMobileHand(Sprite sprite)
    {
        handImage.enabled = true;
        handImage.color = Color.white;
        SetHandSprite(sprite);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    private void ShowPC(Sprite legendSprite)
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        SetAuxiliaryVisible(false, true, true);
        mouseLegend.sprite = legendSprite != null ? legendSprite : mouseSprite;
        cursorImage.sprite = cursorSprite;
        mouseLegend.rectTransform.anchoredPosition = MouseLegendPosition();
    }

    private void AnimateTap()
    {
        if (!TryGetTargetCanvasPosition(currentTarget, out Vector2 target)) return;
        float n = Mathf.Repeat(Time.unscaledTime - cycleStartedAt, tapCycleSeconds) / tapCycleSeconds;
        float press = n < .2f ? 0f : n < .4f ? Mathf.SmoothStep(0f, 1f, (n - .2f) / .2f) :
            n < .6f ? 1f : n < .8f ? Mathf.SmoothStep(1f, 0f, (n - .6f) / .2f) : 0f;
        if (mobilePresentation)
        {
            bool down = press > .5f;
            SetHandSprite(down ? handClickSprite : handOpenSprite);
            float scale = Mathf.Lerp(1f, .86f, press);
            Vector2 fingertip = down ? new Vector2(.33f, .70f) : new Vector2(.375f, .80f);
            Vector2 tip = Vector2.Scale(fingertip - handRect.pivot, handRect.rect.size) * scale;
            handRect.anchoredPosition = target - tip + Vector2.up * (14f * (1f - press));
            handRect.localScale = Vector3.one * scale;
        }
        else
        {
            cursorImage.rectTransform.anchoredPosition = target + new Vector2(12f, -12f);
            cursorImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, .82f, press);
            mouseLegend.sprite = press > .5f && mouseLeftClickSprite != null ? mouseLeftClickSprite : mouseSprite;
            mouseLegend.rectTransform.localScale = new Vector3(1f, Mathf.Lerp(1f, .9f, press), 1f);
        }
    }

    private void AnimateSwipe()
    {
        float n = Mathf.Repeat(Time.unscaledTime - cycleStartedAt, swipeCycleSeconds) / swipeCycleSeconds;
        float move = n < .16f ? 0f : n < .76f ? Mathf.SmoothStep(0f, 1f, (n - .16f) / .6f) : 1f;
        float fade = n > .88f ? 1f - Mathf.InverseLerp(.88f, 1f, n) : 1f;
        float travel = SwipeTravel();
        if (mobilePresentation)
        {
            SetHandSprite(n < .16f ? handClickSprite : handOpenSprite);
            handRect.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, move), n < .16f ? -6f : 0f);
            handRect.localScale = Vector3.one * (n < .16f ? .9f : 1f);
            if (canvasGroup != null) canvasGroup.alpha = fade;
        }
        else
        {
            mouseLegend.sprite = n < .78f && mouseRightClickSprite != null ? mouseRightClickSprite : mouseSprite;
            cursorImage.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, move), 0f);
            Color c = cursorImage.color; c.a = fade; cursorImage.color = c;
        }
    }

    private void AnimateZoom()
    {
        float n = Mathf.Repeat(Time.unscaledTime - cycleStartedAt, zoomCycleSeconds) / zoomCycleSeconds;
        if (mobilePresentation)
        {
            float approach = n < .18f ? 0f : n < .58f ? Mathf.SmoothStep(0f, 1f, (n - .18f) / .4f) : n < .72f ? 1f : 0f;
            bool pressed = n >= .10f && n < .72f;
            float spacing = Mathf.Lerp(150f, 55f, approach);
            SetHandSprite(pressed ? handClickSprite : handOpenSprite);
            pinchPartner.sprite = pressed ? handClickSprite : handOpenSprite;
            handRect.anchoredPosition = new Vector2(-spacing, 0f);
            pinchPartner.rectTransform.anchoredPosition = new Vector2(spacing, 0f);
            float scale = pressed ? .92f : 1f;
            handRect.localScale = Vector3.one * scale;
            pinchPartner.rectTransform.localScale = new Vector3(-scale, scale, 1f);
        }
        else
        {
            float wave = Mathf.Sin(n * Mathf.PI * 2f);
            mouseLegend.sprite = mouseScrollSprite != null ? mouseScrollSprite : mouseSprite;
            mouseLegend.rectTransform.localScale = Vector3.one * (1f + .05f * Mathf.Abs(wave));
            cursorImage.rectTransform.anchoredPosition = new Vector2(0f, wave * 18f);
        }
    }

    private void AnimateTyping()
    {
        if (typingCue == null || !TryGetTargetCanvasPosition(currentTarget, out Vector2 target))
            return;

        int typed = Mathf.Clamp(Mathf.FloorToInt(
            Mathf.Repeat(Time.unscaledTime - cycleStartedAt, 2f) / 0.45f), 0, 3);
        bool caret = Mathf.Repeat(Time.unscaledTime, 0.7f) < 0.42f;
        string digits = typed == 0 ? "_ _ _" : typed == 1 ? "1 _ _" :
            typed == 2 ? "1 2 _" : "1 2 0";
        typingCue.text = "TYPE\n[ " + digits + (caret ? " |" : "  ") + " ]";

        Rect bounds = hintRoot != null ? hintRoot.rect : new Rect(-800f, -450f, 1600f, 900f);
        float direction = target.y > bounds.center.y ? -1f : 1f;
        Vector2 position = target + Vector2.up * direction * 76f;
        float halfWidth = typingCue.rectTransform.rect.width * 0.5f;
        float halfHeight = typingCue.rectTransform.rect.height * 0.5f;
        position.x = Mathf.Clamp(position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
        position.y = Mathf.Clamp(position.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);
        typingCue.rectTransform.anchoredPosition = position;
    }

    private void AnimateDrag()
    {
        if (!TryGetTargetCanvasPosition(currentTarget, out Vector2 start) ||
            !TryGetTargetCanvasPosition(dragEndTarget, out Vector2 end))
            return;

        end += dragEndOffset;
        float n = Mathf.Repeat(Time.unscaledTime - cycleStartedAt, 2.55f) / 2.55f;
        bool held = n >= .16f && n < .80f;
        bool released = n >= .80f;
        float travel = n < .24f ? 0f : n < .74f
            ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.24f, .74f, n))
            : 1f;
        float fade = n > .90f ? 1f - Mathf.InverseLerp(.90f, 1f, n) : 1f;
        Vector2 position = Vector2.LerpUnclamped(start, end, travel);

        if (mobilePresentation)
        {
            SetHandSprite(held ? handClickSprite : handOpenSprite);
            float scale = held ? .88f : 1f;
            Vector2 fingertip = held ? new Vector2(.33f, .70f) : new Vector2(.375f, .80f);
            Vector2 tip = Vector2.Scale(fingertip - handRect.pivot, handRect.rect.size) * scale;
            handRect.anchoredPosition = position - tip;
            handRect.localScale = Vector3.one * scale;
            if (canvasGroup != null) canvasGroup.alpha = fade;
        }
        else
        {
            cursorImage.rectTransform.anchoredPosition = position + new Vector2(12f, -12f);
            cursorImage.rectTransform.localScale = Vector3.one * (held ? .84f : 1f);
            Color cursorColor = cursorImage.color;
            cursorColor.a = fade;
            cursorImage.color = cursorColor;
            mouseLegend.sprite = held && mouseLeftClickSprite != null
                ? mouseLeftClickSprite : mouseSprite;
            mouseLegend.rectTransform.localScale = new Vector3(1f, held ? .90f : 1f, 1f);
        }

        if (dragCue != null)
        {
            dragCue.text = released ? "RELEASE" : held
                ? (mobilePresentation ? "PRESS + HOLD" : "HOLD LEFT CLICK")
                : (mobilePresentation ? "TOUCH THE BOX" : "MOVE TO THE BOX");
            Vector2 cueAnchor = mobilePresentation ? position : mouseLegend.rectTransform.anchoredPosition;
            dragCue.rectTransform.anchoredPosition = cueAnchor + Vector2.down * 78f;
            Color cueColor = dragCue.color;
            cueColor.a = fade;
            dragCue.color = cueColor;
        }
    }

    private void SetAuxiliaryVisible(bool pinch, bool mouse, bool cursor)
    {
        if (pinchPartner != null) pinchPartner.gameObject.SetActive(pinch);
        if (mouseLegend != null) mouseLegend.gameObject.SetActive(mouse);
        if (cursorImage != null) cursorImage.gameObject.SetActive(cursor);
    }

    private void SetHandSprite(Sprite sprite)
    {
        if (handImage != null && sprite != null) handImage.sprite = sprite;
    }

    private float SwipeTravel()
    {
        RectTransform canvasRect = targetCanvas != null ? targetCanvas.transform as RectTransform : null;
        float width = canvasRect != null ? canvasRect.rect.width : 1600f;
        return Mathf.Clamp(width * swipeTravelCanvasFraction, 130f, 320f);
    }

    private Vector2 MouseLegendPosition()
    {
        Rect r = hintRoot != null ? hintRoot.rect : new Rect(-800f, -450f, 1600f, 900f);
        bool targetOnRight = currentTarget != null &&
                             TryGetTargetCanvasPosition(currentTarget, out Vector2 point) &&
                             point.x > r.center.x;
        float x = targetOnRight
            ? r.xMin + mouseDisplaySize * .85f
            : r.xMax - mouseDisplaySize * .85f;
        return new Vector2(x, r.center.y);
    }

    private bool TryGetTargetCanvasPosition(Transform target, out Vector2 canvasPosition)
    {
        canvasPosition = Vector2.zero;
        RectTransform canvasRect = hintRoot != null ? hintRoot :
            targetCanvas != null ? targetCanvas.transform as RectTransform : null;
        if (target == null || canvasRect == null) return false;
        Vector3 screen;
        if (target is RectTransform targetRect)
        {
            Canvas source = targetRect.GetComponentInParent<Canvas>();
            Camera eventCamera = source == null || source.renderMode == RenderMode.ScreenSpaceOverlay ? null : source.worldCamera;
            screen = RectTransformUtility.WorldToScreenPoint(eventCamera, targetRect.TransformPoint(targetRect.rect.center));
        }
        else
        {
            Camera camera = worldCamera != null ? worldCamera : Camera.main;
            if (target.gameObject.scene.name == "RestockScene")
                foreach (Camera candidate in Camera.allCameras)
                    if (candidate.gameObject.scene == target.gameObject.scene) { camera = candidate; break; }
            if (camera == null) return false;
            screen = camera.WorldToScreenPoint(TutorialWorldTargetGeometry.Center(target));
            if (screen.z <= 0f) return false;
        }
        Canvas canvas = hintRoot != null ? hintRoot.GetComponent<Canvas>() : targetCanvas;
        Camera canvasCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, canvasCamera, out canvasPosition);
    }
}
