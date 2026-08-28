using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Prefab-backed, responsive card payment interaction. All layout references,
/// thresholds, colours, timings and sprites remain editable in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class CardPaymentUI : MonoBehaviour
{
    public static CardPaymentUI Instance { get; private set; }

    [Header("Responsive Layout")]
    [SerializeField] private RectTransform fullScreenBackground;
    [SerializeField] private RectTransform safeAreaContent;
    [SerializeField] private RectTransform interactionPanel;
    [SerializeField, Min(0f)] private float safeAreaPadding = 20f;

    [Header("Payment References")]
    [SerializeField] private Image handheldPosImage;
    [SerializeField] private Sprite idlePosSprite;
    [SerializeField] private Sprite cardInsertedSprite;
    [SerializeField] private RectTransform cardRect;
    [SerializeField] private RectTransform cardSlot;
    [SerializeField] private TMP_Text screenText;
    [SerializeField] private Button closeButton;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Interaction")]
    [SerializeField, Min(0f)] private float magneticSnapDistance = 42f;
    [Tooltip("Keeps the card centre inside the interaction panel while still allowing the card edge to enter the POS slot.")]
    [SerializeField, Min(0f)] private float dragCenterEdgePadding = 8f;
    [Tooltip("How far the card centre may be from the authored bottom-slot target, measured as a fraction of the target size.")]
    [SerializeField, Range(0.25f, 1f)] private float seatedPositionTolerance = 0.72f;
    [SerializeField, Range(0f, 45f)] private float seatedAngleTolerance = 12f;
    [SerializeField, Min(0f)] private float failedMessageSeconds = 0.75f;
    [SerializeField] private Color idleTextColor = Color.white;
    [SerializeField] private Color successTextColor = new Color(0.35f, 1f, 0.45f, 1f);
    [SerializeField] private Color declinedTextColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Editable Prefab Version")]
    [SerializeField, HideInInspector] private int authoringVersion;

    private MoneyPickup activePayment;
    private Vector3 cardHomeLocalPosition;
    private Quaternion cardHomeRotation;
    private Vector2 dragOffset;
    private bool dragging;
    private bool completing;
    private Vector2 lastScreenSize = new Vector2(-1f, -1f);
    private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
    private Coroutine feedbackRoutine;

    public bool IsOpen => gameObject.activeSelf && canvasGroup != null && canvasGroup.alpha > 0.001f;
    public int AuthoringVersion => authoringVersion;

    public void ConfigureReferences(
        RectTransform configuredBackground,
        RectTransform configuredSafeArea,
        RectTransform configuredPanel,
        Image configuredPosImage,
        Sprite configuredIdleSprite,
        Sprite configuredInsertedSprite,
        RectTransform configuredCard,
        RectTransform configuredSlot,
        TMP_Text configuredScreenText,
        Button configuredClose,
        CanvasGroup configuredCanvasGroup)
    {
        fullScreenBackground = configuredBackground;
        safeAreaContent = configuredSafeArea;
        interactionPanel = configuredPanel;
        handheldPosImage = configuredPosImage;
        idlePosSprite = configuredIdleSprite;
        cardInsertedSprite = configuredInsertedSprite;
        cardRect = configuredCard;
        cardSlot = configuredSlot;
        screenText = configuredScreenText;
        closeButton = configuredClose;
        canvasGroup = configuredCanvasGroup;
    }

    public void ConfigureAuthoringVersion(int version)
    {
        authoringVersion = Mathf.Max(0, version);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CancelPayment);
            closeButton.onClick.AddListener(CancelPayment);
        }

        if (cardRect != null)
        {
            cardHomeLocalPosition = cardRect.localPosition;
            cardHomeRotation = cardRect.localRotation;
        }

        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, true);
        if (activePayment == null)
            HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CancelPayment);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height ||
            lastSafeArea != Screen.safeArea)
            ApplyResponsiveLayout();
    }

    public bool Open(MoneyPickup payment)
    {
        if (payment == null || !payment.IsCardPayment || completing)
            return false;

        activePayment = payment;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, true);
        ResetCardVisual();
        SetScreen($"TOTAL  ₱{payment.OrderTotal}\nINSERT CARD", idleTextColor);
        ApplyResponsiveLayout(true);
        return true;
    }

    public void CancelPayment()
    {
        if (completing)
            return;

        MoneyPickup payment = activePayment;
        activePayment = null;
        payment?.CancelCardPaymentUI();
        HideImmediate();
    }

    public void BeginCardDrag(PointerEventData eventData)
    {
        if (!IsOpen || completing || cardRect == null || interactionPanel == null)
            return;

        dragging = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            interactionPanel,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointer);
        dragOffset = (Vector2)cardRect.localPosition - pointer;
    }

    public void DragCard(PointerEventData eventData)
    {
        if (!dragging || completing || cardRect == null || interactionPanel == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                interactionPanel,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 pointer))
            return;

        Vector2 clampedPosition = ClampCardToPanel(pointer + dragOffset);
        cardRect.localPosition = new Vector3(clampedPosition.x, clampedPosition.y, 0f);
        bool nearSlot = IsCardNearSlot();
        if (handheldPosImage != null)
            handheldPosImage.sprite = nearSlot && cardInsertedSprite != null
                ? cardInsertedSprite
                : idlePosSprite;
    }

    public void EndCardDrag(PointerEventData eventData)
    {
        if (!dragging || completing)
            return;

        dragging = false;
        if (IsCardSeatedInSlot())
        {
            StartCoroutine(CompletePaymentRoutine());
            return;
        }

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(ShowDeclinedRoutine());
    }

    private IEnumerator CompletePaymentRoutine()
    {
        completing = true;
        SnapCardToSlot();
        if (handheldPosImage != null && cardInsertedSprite != null)
            handheldPosImage.sprite = cardInsertedSprite;
        if (cardRect != null)
            cardRect.gameObject.SetActive(false);
        SetScreen("PAYMENT\nCOMPLETE!", successTextColor);

        yield return new WaitForSecondsRealtime(EquipmentUpgradeService.CardPaymentCloseDelay);

        MoneyPickup payment = activePayment;
        activePayment = null;
        bool completed = payment != null && payment.CompleteCardPayment();
        completing = false;

        if (completed)
        {
            HideImmediate();
        }
        else
        {
            activePayment = payment;
            ResetCardVisual();
            SetScreen("DECLINED\nTRY AGAIN", declinedTextColor);
        }
    }

    private IEnumerator ShowDeclinedRoutine()
    {
        SetScreen("DECLINED\nTRY AGAIN", declinedTextColor);
        if (handheldPosImage != null)
            handheldPosImage.sprite = idlePosSprite;
        yield return new WaitForSecondsRealtime(failedMessageSeconds);
        ResetCardVisual();
        if (activePayment != null)
            SetScreen($"TOTAL  ₱{activePayment.OrderTotal}\nINSERT CARD", idleTextColor);
        feedbackRoutine = null;
    }

    private void ResetCardVisual()
    {
        dragging = false;
        if (cardRect != null)
        {
            cardRect.gameObject.SetActive(true);
            cardRect.localPosition = cardHomeLocalPosition;
            cardRect.localRotation = cardHomeRotation;
            cardRect.localScale = Vector3.one;
        }
        if (handheldPosImage != null)
            handheldPosImage.sprite = idlePosSprite;
    }

    private void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    private void SetScreen(string message, Color color)
    {
        if (screenText == null)
            return;
        screenText.text = message;
        screenText.color = color;
    }

    private void ApplyResponsiveLayout(bool force = false)
    {
        if (!force && lastScreenSize.x == Screen.width && lastScreenSize.y == Screen.height &&
            lastSafeArea == Screen.safeArea)
            return;

        lastScreenSize = new Vector2(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;

        if (fullScreenBackground != null)
        {
            fullScreenBackground.anchorMin = Vector2.zero;
            fullScreenBackground.anchorMax = Vector2.one;
            fullScreenBackground.offsetMin = Vector2.zero;
            fullScreenBackground.offsetMax = Vector2.zero;
        }

        if (safeAreaContent != null && Screen.width > 0 && Screen.height > 0)
        {
            Rect safe = Screen.safeArea;
            safeAreaContent.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeAreaContent.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeAreaContent.offsetMin = new Vector2(safeAreaPadding, safeAreaPadding);
            safeAreaContent.offsetMax = new Vector2(-safeAreaPadding, -safeAreaPadding);
        }

        // The foreground is authored as a stretched child of SafeAreaContent.
        // Do not rebuild or rescale it here: direct Prefab Mode edits must be
        // the same transforms used at runtime.
    }

    private Vector2 ClampCardToPanel(Vector2 position)
    {
        if (cardRect == null || interactionPanel == null)
            return position;

        // Clamp the card's centre rather than its full rotated rectangle. The
        // authored card slot sits at the bottom of the POS, where part of the
        // card must naturally extend past the interaction panel while it is
        // being inserted. Clamping by half the card size made that slot
        // unreachable, especially when the card was rotated vertically.
        Rect panel = interactionPanel.rect;
        float padding = Mathf.Max(0f, dragCenterEdgePadding);
        float minX = panel.xMin + padding;
        float maxX = panel.xMax - padding;
        float minY = panel.yMin + padding;
        float maxY = panel.yMax - padding;
        if (minX > maxX)
            minX = maxX = panel.center.x;
        if (minY > maxY)
            minY = maxY = panel.center.y;
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
        return position;
    }

    private bool IsCardNearSlot()
    {
        if (cardRect == null || cardSlot == null)
            return false;
        return Vector3.Distance(cardRect.position, cardSlot.position) <= magneticSnapDistance ||
               IsCardSeatedInSlot();
    }

    private bool IsCardSeatedInSlot()
    {
        if (cardRect == null || cardSlot == null)
            return false;

        Rect cardBounds = GetScreenRect(cardRect);
        Rect slotBounds = GetScreenRect(cardSlot);
        float allowedX = Mathf.Max(1f, slotBounds.width * 0.5f * seatedPositionTolerance);
        float allowedY = Mathf.Max(1f, slotBounds.height * 0.5f * seatedPositionTolerance);
        bool centred = Mathf.Abs(cardBounds.center.x - slotBounds.center.x) <= allowedX &&
                       Mathf.Abs(cardBounds.center.y - slotBounds.center.y) <= allowedY;
        float angle = Quaternion.Angle(cardRect.localRotation, cardHomeRotation);
        return centred && angle <= seatedAngleTolerance;
    }

    private static Rect GetScreenRect(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        float minX = first.x;
        float maxX = first.x;
        float minY = first.y;
        float maxY = first.y;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private void SnapCardToSlot()
    {
        if (cardRect == null || cardSlot == null || interactionPanel == null)
            return;

        Vector3 local = interactionPanel.InverseTransformPoint(cardSlot.position);
        cardRect.localPosition = new Vector3(local.x, local.y, 0f);
    }
}
