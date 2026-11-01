using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uses the game's authored shop purchase card, including its fonts, artwork and buttons.
public sealed class RestaurantPurchaseView : MonoBehaviour
{
    private AnimatedPanel animationPanel;
    private TMP_Text heading, restaurant, details, status, buyLabel, backLabel;
    private Button buy, back, close;
    private RectTransform card;
    private Vector3 cardScale;
    private RectTransform fitRoot;
    public Action Buy, Closed;
    public bool IsOpen => animationPanel != null && animationPanel.IsOpen;

    public void Configure(AnimatedPanel panel, TMP_Text title, TMP_Text nameText, TMP_Text detailText,
        TMP_Text statusText, TMP_Text confirmText, TMP_Text cancelText, Button confirm, Button cancel, Button closeButton,
        Image icon, GameObject check)
    {
        animationPanel = panel; heading = title; restaurant = nameText; details = detailText; status = statusText;
        buyLabel = confirmText; backLabel = cancelText; buy = confirm; back = cancel; close = closeButton;
        card = heading.transform.parent as RectTransform;
        cardScale = card.localScale;
        fitRoot = new GameObject("Restaurant card safe area", typeof(RectTransform)).GetComponent<RectTransform>();
        fitRoot.SetParent(transform, false); fitRoot.anchorMin = Vector2.zero; fitRoot.anchorMax = Vector2.one;
        fitRoot.offsetMin = fitRoot.offsetMax = Vector2.zero;
        card.SetParent(fitRoot, false);
        if (icon != null) icon.gameObject.SetActive(false);
        if (check != null) check.SetActive(false);
        // Preserve authored sprites/fonts and fit requirement text into separate, bounded bands.
        card.sizeDelta = new Vector2(Mathf.Max(360f, card.sizeDelta.x), 410f);
        Place(heading.rectTransform, -28f, 320f, 34f);
        Place(restaurant.rectTransform, -77f, 320f, 36f);
        Place(details.rectTransform, -165f, 316f, 120f);
        Place(status.rectTransform, -284f, 316f, 86f);
        heading.text = "RESTAURANT ACCESS";
        foreach (var text in new[] { heading, restaurant, details, status })
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Min(14f, text.fontSize);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
        }
        PositionButton(buy.transform as RectTransform, 83f);
        PositionButton(back.transform as RectTransform, -83f);
        foreach (var label in new[] { buyLabel, backLabel })
        {
            label.enableAutoSizing = true; label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Min(12f, label.fontSize); label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        buy.onClick = new Button.ButtonClickedEvent(); buy.onClick.AddListener(() => Buy?.Invoke());
        back.onClick = new Button.ButtonClickedEvent(); back.onClick.AddListener(Hide);
        if (close != null) { close.onClick = new Button.ButtonClickedEvent(); close.onClick.AddListener(Hide); }
        animationPanel.OnClosed = new UnityEngine.Events.UnityEvent();
        animationPanel.OnClosed.AddListener(() => gameObject.SetActive(false));
        UISubtlePressFeedback.EnsureButtons(transform);
    }

    private static void Place(RectTransform rect, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y); rect.sizeDelta = new Vector2(width, height);
    }
    private static void PositionButton(RectTransform rect, float x)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(x, -363f);
    }
    public void Show() { transform.SetAsLastSibling(); gameObject.SetActive(true); animationPanel.Open(); backLabel.text = "BACK"; }
    public void Hide() => animationPanel.Close();
    public void Render(string name, string requirements, string message, string action, bool canBuy, bool newlyOwned)
    {
        restaurant.text = name; details.text = requirements; status.text = message;
        buyLabel.text = action; buy.interactable = canBuy;
        if (newlyOwned) buy.GetComponent<ButtonAnimator>()?.PlayConfirmation(.035f);
    }
    private void LateUpdate()
    {
        if (card == null || fitRoot == null) return;
        var canvas = GetComponentInParent<Canvas>();
        float factor = canvas != null ? canvas.scaleFactor : 1f;
        float fit = Mathf.Min(1f, Screen.safeArea.width / Mathf.Max(1f, factor * card.sizeDelta.x * Mathf.Abs(cardScale.x) + 32f),
            Screen.safeArea.height / Mathf.Max(1f, factor * card.sizeDelta.y * Mathf.Abs(cardScale.y) + 32f));
        fitRoot.localScale = Vector3.one * fit;
        fitRoot.anchoredPosition = (Screen.safeArea.center - new Vector2(Screen.width, Screen.height) * .5f) / Mathf.Max(.001f, factor);
    }
    private void OnDisable() { Closed?.Invoke(); }
}
