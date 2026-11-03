using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reuses the shop's authored frame, fonts, close artwork and red/green buttons on this clone only.
public sealed class RestaurantPurchaseView : MonoBehaviour
{
    private AnimatedPanel animationPanel;
    private TMP_Text heading, restaurant, details, status, priceText, balanceText, buyLabel, backLabel;
    private Button buy, back, close;
    private RectTransform card, fitRoot;
    private Vector3 cardScale;
    public Action Buy, Closed;
    public bool IsOpen => animationPanel != null && animationPanel.IsOpen;

    public void Configure(AnimatedPanel panel, TMP_Text title, TMP_Text nameText, TMP_Text detailText,
        TMP_Text statusText, TMP_Text confirmText, TMP_Text cancelText, Button confirm, Button cancel, Button closeButton,
        Image icon, GameObject check)
    {
        animationPanel = panel; heading = title; restaurant = nameText; details = detailText; status = statusText;
        buyLabel = confirmText; backLabel = cancelText; buy = confirm; back = cancel; close = closeButton;
        card = heading.transform.parent as RectTransform; cardScale = card.localScale;
        fitRoot = new GameObject("Restaurant card safe area", typeof(RectTransform)).GetComponent<RectTransform>();
        fitRoot.SetParent(transform, false); fitRoot.anchorMin = Vector2.zero; fitRoot.anchorMax = Vector2.one;
        fitRoot.offsetMin = fitRoot.offsetMax = Vector2.zero;
        card.SetParent(fitRoot, false); card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f;
        card.anchoredPosition = Vector2.zero; card.sizeDelta = new Vector2(480, 460);
        if (icon != null) icon.gameObject.SetActive(false);
        if (check != null) check.SetActive(false);
        var badge = card.Find("Purchase Preview Backing");
        if (badge != null) badge.gameObject.SetActive(false);
        priceText = CopyText(details, "Restaurant Price"); balanceText = CopyText(details, "Combined Normal Money");
        priceText.richText = balanceText.richText = true;
        Place(heading.rectTransform, 0, -28, 352, 30);
        Place(restaurant.rectTransform, 0, -68, 420, 32);
        Place(priceText.rectTransform, -110, -127, 200, 60);
        Place(balanceText.rectTransform, 110, -127, 220, 60);
        Place(details.rectTransform, 0, -218, 428, 94);
        Place(status.rectTransform, 0, -314, 428, 90);
        heading.text = "RESTAURANT ACCESS";
        foreach (var text in new[] { heading, restaurant, details, status, priceText, balanceText }) {
            text.enableAutoSizing = true; text.fontSize = text.fontSizeMax = text == heading || text == restaurant ? 26 : text == priceText || text == balanceText ? 24 : 18;
            text.fontSizeMin = 14; text.textWrappingMode = TextWrappingModes.Normal;
            text.margin = Vector4.zero; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.autoSizeTextContainer = false;
            text.fontSharedMaterial = text.font.material;
        }
        Place((RectTransform)back.transform, -113, -414, 200, 52);
        Place((RectTransform)buy.transform, 113, -414, 200, 52);
        foreach (var label in new[] { buyLabel, backLabel }) {
            var rect = label.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10,4); rect.offsetMax = new Vector2(-10,-4); rect.localScale = Vector3.one;
            label.enableAutoSizing = true; label.fontSize = label.fontSizeMax = 24; label.fontSizeMin = 16;
            label.margin = Vector4.zero; label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        if (close != null) {
            var rect = (RectTransform)close.transform; rect.anchorMin = rect.anchorMax = new Vector2(1,1);
            rect.pivot = Vector2.one * .5f; rect.anchoredPosition = new Vector2(-30,-28); rect.sizeDelta = new Vector2(40,40);
        }
        buy.onClick = new Button.ButtonClickedEvent(); buy.onClick.AddListener(() => Buy?.Invoke());
        back.onClick = new Button.ButtonClickedEvent(); back.onClick.AddListener(Hide);
        if (close != null) { close.onClick = new Button.ButtonClickedEvent(); close.onClick.AddListener(Hide); }
        animationPanel.OnClosed = new UnityEngine.Events.UnityEvent();
        animationPanel.OnClosed.AddListener(() => gameObject.SetActive(false));
        UISubtlePressFeedback.EnsureButtons(transform);
    }
    private TMP_Text CopyText(TMP_Text template, string name)
    {
        var text = Instantiate(template, card, false); text.name = name; return text;
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = Vector2.one * .5f;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = new Vector2(x,y); rect.sizeDelta = new Vector2(width,height);
    }
    public void Show() { transform.SetAsLastSibling(); gameObject.SetActive(true); animationPanel.Open(); backLabel.text = "BACK"; }
    public void Hide() => animationPanel.Close();
    public void Render(string name, int price, long balance, string requirements, string message, string action, bool canBuy, bool newlyOwned)
    {
        restaurant.text = name; details.text = requirements; status.text = message;
        priceText.text = "<size=65%>PRICE</size>\n₱" + price.ToString("N0");
        balanceText.text = "<size=65%>NORMAL MONEY</size>\n" + (balance < 0 ? "Loading…" : "₱" + balance.ToString("N0"));
        buyLabel.text = action; buy.interactable = canBuy;
        if (newlyOwned) buy.GetComponent<ButtonAnimator>()?.PlayConfirmation(.035f);
    }
    private void LateUpdate()
    {
        if (card == null || fitRoot == null) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Hide();
        var canvas = GetComponentInParent<Canvas>();
        float factor = canvas != null ? canvas.scaleFactor : 1;
        float fit = Mathf.Min(1, Mathf.Max(1, Screen.safeArea.width - 32) / Mathf.Max(1, factor * card.sizeDelta.x * Mathf.Abs(cardScale.x)),
            Mathf.Max(1, Screen.safeArea.height - 32) / Mathf.Max(1, factor * card.sizeDelta.y * Mathf.Abs(cardScale.y)));
        fitRoot.localScale = Vector3.one * fit;
        fitRoot.anchoredPosition = (Screen.safeArea.center - new Vector2(Screen.width, Screen.height) * .5f) / Mathf.Max(.001f, factor);
    }
    private void OnDisable() { Closed?.Invoke(); }
}
