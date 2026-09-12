using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Scene-owned inspection UI. Stock mutations remain on DraggableStorageBox.</summary>
public sealed class ThrowKeepPanel : MonoBehaviour
{
    public static ThrowKeepPanel Instance { get; private set; }
    [SerializeField] private RectTransform panel;
    [Header("Left-side layout (canvas units)")]
    [SerializeField] private Vector2 panelSize = new Vector2(180f, 166f);
    [SerializeField] private Vector2 panelOffset = new Vector2(16f, 0f);
    [SerializeField, Min(0.1f)] private float mobileScale = 1f;
    [SerializeField] private Vector2 imageSize = new Vector2(32f, 32f);
    [SerializeField] private Vector2 buttonSize = new Vector2(76f, 38f);
    [SerializeField, Min(0f)] private float padding = 10f;
    [SerializeField, Min(0f)] private float spacing = 6f;
    [SerializeField, Min(1f)] private float nameFontSize = 18f;
    [SerializeField, Min(1f)] private float detailFontSize = 14f;
    [SerializeField, Min(1f)] private float buttonFontSize = 18f;
    [Header("Confirmation")]
    [SerializeField] private Vector2 confirmationSize = new Vector2(220f, 162f);
    [SerializeField] private Vector2 confirmationOffset = new Vector2(16f, 0f);
    [SerializeField, Min(1f)] private float confirmationFontSize = 16f;
    [SerializeField] private Sprite confirmationSprite;
    [SerializeField] private TMP_FontAsset confirmationFont;
    [SerializeField] private Color confirmationColor = new Color(0.89f, 0.95f, 0.99f);
    [SerializeField] private Color confirmationTextColor = new Color(0.025f, 0.08f, 0.14f);
    [SerializeField] private Color confirmationTitleColor = new Color(0.02f, 0.32f, 0.62f);
    [SerializeField] private string confirmationTitle = "Discard item?";
    [SerializeField, Min(1f)] private float confirmationTitleSize = 19f;
    [SerializeField, Min(1f)] private float confirmationTitleHeight = 24f;
    [SerializeField, TextArea] private string confirmationMessage =
        "This item is still in good condition. Throw it away?";
    [Header("Style")]
    [SerializeField] private Sprite keepSprite;
    [SerializeField] private Sprite throwSprite;
    [SerializeField] private Color goodColor = new Color(0.35f, 1f, 0.2f);
    [SerializeField] private Color expiredColor = new Color(1f, 0.2f, 0.28f);
    [SerializeField] private Color warningColor = new Color(1f, 0.68f, 0.15f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color pressedTint = new Color(0.78f, 0.78f, 0.78f);
    [Header("Feedback")]
    [SerializeField, Min(0f)] private float openDuration = 0.16f;
    [SerializeField, Min(0f)] private float refreshDuration = 0.10f;
    [SerializeField, Min(0f)] private float confirmationDuration = 0.12f;
    [SerializeField, Min(0f)] private float closeDuration = 0.10f;
    [SerializeField, Min(0f)] private float buttonFadeDuration = 0.08f;
    [SerializeField, Range(0.5f, 1f)] private float popStartScale = 0.94f;
    [SerializeField, Range(0.8f, 1f)] private float buttonPressScale = 0.94f;
    [SerializeField, Range(1f, 1.1f)] private float buttonReleaseScale = 1.025f;
    [SerializeField, Min(0.01f)] private float buttonPressDuration = 0.06f;
    [SerializeField, Min(0.01f)] private float buttonReleaseDuration = 0.14f;
    private readonly Dictionary<Button, Coroutine> buttonAnimations = new Dictionary<Button, Coroutine>();

    public DraggableStorageBox SelectedBox { get; private set; }
    public Button KeepButton { get; private set; }
    public Button ThrowButton { get; private set; }
    public bool BlocksPointer(Vector2 position)
    {
        RectTransform visible = confirming ? confirmation : panel;
        return SelectedBox != null && (confirming || visible.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(visible, position));
    }
    private RestockStorageContainer selectedItem;
    private TMP_Text itemName, quantity, quality, date;
    private Image itemImage;
    private CanvasGroup panelGroup, confirmationGroup;
    private RectTransform confirmation;
    private Button confirmButton, cancelButton;
    private TMP_Text confirmationText, confirmationHeading;
    private Coroutine transition;
    private bool confirming;
    private bool closing;

    private void Awake()
    {
        if (gameObject.scene.name != "RestockScene" || panel == null)
        {
            enabled = false;
            return;
        }
        Instance = this;
        itemImage = panel.Find("ItemImage").GetComponent<Image>();
        itemName = panel.Find("ItemName").GetComponent<TMP_Text>();
        quantity = panel.Find("Quantity").GetComponent<TMP_Text>();
        quality = panel.Find("Quality").GetComponent<TMP_Text>();
        date = panel.Find("Date").GetComponent<TMP_Text>();
        panelGroup = panel.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        KeepButton = CreateButton(panel, "KeepButton", "KEEP", keepSprite, Keep);
        ThrowButton = CreateButton(panel, "ThrowAwayButton", "THROW", throwSprite, RequestThrow);
        confirmation = new GameObject("ThrowConfirmation", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup)).GetComponent<RectTransform>();
        confirmation.SetParent(panel.parent, false);
        Image background = confirmation.GetComponent<Image>();
        background.sprite = confirmationSprite != null ? confirmationSprite : panel.GetComponent<Image>().sprite;
        background.type = Image.Type.Sliced;
        background.color = confirmationColor;
        confirmationGroup = confirmation.GetComponent<CanvasGroup>();
        confirmationText = CreateText(confirmation, "Message", confirmationMessage);
        confirmationHeading = CreateText(confirmation, "Heading", confirmationTitle);
        if (confirmationFont != null)
            confirmationText.font = confirmationHeading.font = confirmationFont;
        confirmButton = CreateButton(confirmation, "ConfirmThrow", "THROW", throwSprite, Discard);
        cancelButton = CreateButton(confirmation, "CancelThrow", "CANCEL", keepSprite, CancelThrow);
        Layout();
        panel.gameObject.SetActive(false);
        confirmation.gameObject.SetActive(false);
    }

    public void Select(DraggableStorageBox box)
    {
        if (box == null || !box.CanInteractInRestock || box.gameObject.scene != gameObject.scene) return;
        bool refresh = SelectedBox != null;
        if (SelectedBox != null && SelectedBox != box) SelectedBox.HideInteractionUI();
        SelectedBox = box;
        selectedItem = box.GetComponent<RestockStorageContainer>();
        confirming = false;
        confirmation.gameObject.SetActive(false);
        KeepButton.interactable = true;
        ThrowButton.interactable = !MultiplayerRestockBridge.IsActive;
        RefreshDetails();
        Animate(panel, panelGroup, refresh ? refreshDuration : openDuration);
    }

    public void Deselect(DraggableStorageBox box)
    {
        if (SelectedBox != box) return;
        SelectedBox = null;
        selectedItem = null;
        if (transition != null) StopCoroutine(transition);
        CloseVisible(null);
    }

    private void Update()
    {
        if (closing) return;
        if (SelectedBox != null && !SelectedBox.CanInteractInRestock)
        {
            SelectedBox.HideInteractionUI();
            return;
        }
        if (SelectedBox == null || !SelectedBox.isActiveAndEnabled)
        {
            if (panel.gameObject.activeSelf || confirmation.gameObject.activeSelf) Deselect(SelectedBox);
            return;
        }
        Layout();
        RefreshDetails();
        if (MultiplayerRestockBridge.IsActive) ThrowButton.interactable = false;
    }

    private bool IsExpired => selectedItem != null && selectedItem.ExpiresDay > 0 &&
        (GameFlowManager.Instance != null ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay) : 1)
        >= selectedItem.ExpiresDay;

    private void RefreshDetails()
    {
        ItemData item = selectedItem != null ? selectedItem.Item : null;
        itemImage.sprite = item != null ? item.sprite : null;
        itemImage.enabled = itemImage.sprite != null;
        itemImage.preserveAspect = true;
        itemName.text = item != null ? item.displayName : "Unknown item";
        quantity.text = "Quantity: x" + (selectedItem != null ? selectedItem.CurrentRemainingQuantity : 0);
        quality.text = "Condition: " + (IsExpired ? "EXPIRED" : selectedItem != null && selectedItem.WrongStorage
            ? "WRONG STORAGE" : "GOOD");
        quality.color = IsExpired ? expiredColor : selectedItem != null && selectedItem.WrongStorage
            ? warningColor : goodColor;
        date.text = selectedItem != null && selectedItem.ExpiresDay > 0
            ? "Expires: Day " + selectedItem.ExpiresDay : "Expires: Unknown";
    }

    private void Keep() { if (SelectedBox != null) SelectedBox.KeepStock(); }
    private void RequestThrow()
    {
        if (SelectedBox == null || MultiplayerRestockBridge.IsActive) return;
        if (IsExpired) { Discard(); return; }
        confirming = true;
        panel.gameObject.SetActive(false);
        Animate(confirmation, confirmationGroup, confirmationDuration);
    }
    private void CancelThrow()
    {
        if (SelectedBox == null) return;
        CloseVisible(() =>
        {
            confirming = false;
            Animate(panel, panelGroup, confirmationDuration);
        });
    }
    private void Discard()
    {
        if (SelectedBox != null && !MultiplayerRestockBridge.IsActive) SelectedBox.ThrowAway();
    }

    private void OnDisable()
    {
        if (SelectedBox != null) SelectedBox.HideInteractionUI();
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Layout()
    {
        float scale = Application.isMobilePlatform ? mobileScale : 1f;
        PlacePanel(panel, panelSize, panelOffset, scale);
        PlacePanel(confirmation, confirmationSize, confirmationOffset, scale);
        SetRect(itemImage.rectTransform, new Vector2(padding, -padding), imageSize);
        float textX = padding + imageSize.x + spacing;
        float textWidth = Mathf.Max(1f, panelSize.x - textX - padding);
        SetText(itemName, new Vector2(textX, -padding), new Vector2(textWidth, imageSize.y), nameFontSize);
        float y = padding + imageSize.y + spacing;
        Vector2 rowSize = new Vector2(panelSize.x - padding * 2f, detailFontSize * 1.5f);
        SetText(quantity, new Vector2(padding, -y), rowSize, detailFontSize);
        SetText(quality, new Vector2(padding, -y - rowSize.y), rowSize, detailFontSize);
        SetText(date, new Vector2(padding, -y - rowSize.y * 2f), rowSize, detailFontSize);
        LayoutButtons(KeepButton, ThrowButton, panelSize);
        LayoutButtons(cancelButton, confirmButton, confirmationSize);
        SetText(confirmationHeading, new Vector2(padding, -padding),
            new Vector2(confirmationSize.x - padding * 2f, confirmationTitleHeight), confirmationTitleSize);
        confirmationHeading.alignment = TextAlignmentOptions.Center;
        confirmationHeading.fontStyle = FontStyles.Bold;
        confirmationHeading.color = confirmationTitleColor;
        SetText(confirmationText, new Vector2(padding, -padding - confirmationTitleHeight - spacing),
            new Vector2(confirmationSize.x - padding * 2f,
                confirmationSize.y - padding * 3f - buttonSize.y - confirmationTitleHeight - spacing), confirmationFontSize);
        confirmationText.alignment = TextAlignmentOptions.Center;
        confirmationText.color = confirmationTextColor;
    }

    private void PlacePanel(RectTransform rect, Vector2 size, Vector2 offset, float scale)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        Canvas canvas = GetComponentInParent<Canvas>();
        float canvasScale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        offset.x += Screen.safeArea.xMin / canvasScale;
        rect.anchoredPosition = offset;
        if (transition == null) rect.localScale = Vector3.one * scale;
    }

    private void LayoutButtons(Button left, Button right, Vector2 size)
    {
        float width = Mathf.Min(buttonSize.x, (size.x - padding * 2f - spacing) / 2f);
        Vector2 actualSize = new Vector2(Mathf.Max(1f, width), buttonSize.y);
        SetButtonRect(left, new Vector2(padding, -size.y + padding + buttonSize.y), actualSize);
        SetButtonRect(right, new Vector2(size.x - padding - actualSize.x,
            -size.y + padding + buttonSize.y), actualSize);
    }

    private static void SetButtonRect(Button button, Vector2 topLeft, Vector2 size)
    {
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = topLeft + new Vector2(size.x, -size.y) * 0.5f;
        rect.sizeDelta = size;
        // Do not overwrite the press animation during the per-frame layout pass.
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }
    private void SetText(TMP_Text text, Vector2 position, Vector2 size, float fontSize)
    {
        SetRect(text.rectTransform, position, size);
        text.fontSize = text.fontSizeMax = fontSize;
        text.fontSizeMin = fontSize * 0.8f;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        if (text != quality) text.color = textColor;
    }
    private TMP_Text CreateText(Transform parent, string objectName, string value)
    {
        TMP_Text text = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TMP_Text>();
        text.transform.SetParent(parent, false);
        text.font = itemName.font;
        text.text = value;
        text.color = textColor;
        text.raycastTarget = false;
        return text;
    }
    private Button CreateButton(Transform parent, string objectName, string label, Sprite sprite,
        UnityEngine.Events.UnityAction action)
    {
        Button button = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button))
            .GetComponent<Button>();
        button.transform.SetParent(parent, false);
        Image image = button.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.pressedColor = pressedTint;
        colors.fadeDuration = buttonFadeDuration;
        button.colors = colors;
        button.onClick.AddListener(action);
        EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
        AddButtonFeedback(trigger, EventTriggerType.PointerDown, button, true);
        AddButtonFeedback(trigger, EventTriggerType.PointerUp, button, false);
        AddButtonFeedback(trigger, EventTriggerType.PointerExit, button, false);
        TMP_Text text = CreateText(button.transform, "Label", label);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(spacing, 0f);
        text.rectTransform.offsetMax = new Vector2(-spacing, 0f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = buttonFontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = buttonFontSize * 0.8f;
        text.fontSizeMax = buttonFontSize;
        return button;
    }
    private void Animate(RectTransform rect, CanvasGroup group, float duration)
    {
        closing = false;
        group.interactable = group.blocksRaycasts = true;
        foreach (var animation in buttonAnimations)
        {
            if (animation.Value != null) StopCoroutine(animation.Value);
            if (animation.Key != null) animation.Key.transform.localScale = Vector3.one;
        }
        buttonAnimations.Clear();
        if (transition != null) StopCoroutine(transition);
        transition = null;
        rect.gameObject.SetActive(true);
        if (duration <= 0f)
        {
            group.alpha = 1f;
            rect.localScale = Vector3.one * (Application.isMobilePlatform ? mobileScale : 1f);
            return;
        }
        transition = StartCoroutine(Pop(rect, group, duration));
    }

    private void CloseVisible(System.Action completed)
    {
        if (transition != null) StopCoroutine(transition);
        transition = null;
        if (!isActiveAndEnabled || closeDuration <= 0f)
        {
            panel.gameObject.SetActive(false);
            confirmation.gameObject.SetActive(false);
            closing = false;
            completed?.Invoke();
            return;
        }
        closing = true;
        transition = StartCoroutine(ClosePanel(completed));
    }

    private IEnumerator ClosePanel(System.Action completed)
    {
        RectTransform rect = confirmation.gameObject.activeSelf ? confirmation : panel;
        CanvasGroup group = rect == confirmation ? confirmationGroup : panelGroup;
        group.interactable = group.blocksRaycasts = false;
        float alpha = group.alpha;
        Vector3 scale = rect.localScale;
        for (float elapsed = 0f; elapsed < closeDuration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / closeDuration);
            group.alpha = Mathf.Lerp(alpha, 0f, t);
            rect.localScale = scale * Mathf.Lerp(1f, popStartScale, t);
            yield return null;
        }
        rect.gameObject.SetActive(false);
        closing = false;
        transition = null;
        completed?.Invoke();
    }

    private void AddButtonFeedback(EventTrigger trigger, EventTriggerType type, Button button, bool pressed)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ =>
        {
            if (!button.IsInteractable()) return;
            if (buttonAnimations.TryGetValue(button, out Coroutine running) && running != null)
                StopCoroutine(running);
            buttonAnimations[button] = StartCoroutine(AnimateButton(button, pressed));
        });
        trigger.triggers.Add(entry);
    }

    private IEnumerator AnimateButton(Button button, bool pressed)
    {
        float start = button.transform.localScale.x;
        float duration = pressed ? buttonPressDuration : buttonReleaseDuration;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / duration;
            float scale = pressed ? Mathf.Lerp(start, buttonPressScale, Mathf.SmoothStep(0f, 1f, t))
                : t < 0.5f ? Mathf.Lerp(start, buttonReleaseScale, Mathf.SmoothStep(0f, 1f, t * 2f))
                : Mathf.Lerp(buttonReleaseScale, 1f, Mathf.SmoothStep(0f, 1f, t * 2f - 1f));
            button.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        button.transform.localScale = Vector3.one * (pressed ? buttonPressScale : 1f);
        buttonAnimations[button] = null;
    }
    private IEnumerator Pop(RectTransform rect, CanvasGroup group, float duration)
    {
        float scale = Application.isMobilePlatform ? mobileScale : 1f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            group.alpha = t;
            rect.localScale = Vector3.one * scale * Mathf.Lerp(popStartScale, 1f, t);
            yield return null;
        }
        group.alpha = 1f;
        rect.localScale = Vector3.one * scale;
        transition = null;
    }
}
