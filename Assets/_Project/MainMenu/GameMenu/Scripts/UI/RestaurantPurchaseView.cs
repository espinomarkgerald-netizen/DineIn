using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RestaurantPurchaseView : MonoBehaviour
{
    [Serializable]
    public sealed class Art
    {
        public Sprite frame, title, ribbon, ownedRibbon, chip, cash, calendar, track, fill;
        public Sprite backButton, buyButton, sparkle, fastFood, fineDining;
        public TMP_FontAsset headingFont, bodyFont;
    }

    private static readonly Color32 Ink = new(16, 33, 68, 255);
    private static readonly Color32 Cream = new(255, 249, 231, 255);
    private AnimatedPanel animationPanel;
    private TMP_Text heading, badge, status, priceText, balanceText, buyLabel;
    private Button buy;
    private Image preview, ribbon;
    private RectTransform card, fitRoot;
    private Vector3 cardScale;
    private ProgressRow casualRow, fastRow;
    private TMP_Text modes;
    private Art art;
    private Coroutine ribbonMotion;
    public Action Buy, Closed;
    public bool IsOpen => animationPanel != null && animationPanel.IsOpen;

    private sealed class ProgressRow
    {
        public Image Calendar, Track, Fill;
        public TMP_Text Label;

        public void Update(string name, int days, int required)
        {
            bool visible = required > 0;
            Calendar.gameObject.SetActive(visible);
            Track.gameObject.SetActive(visible);
            Label.gameObject.SetActive(visible);
            if (!visible) return;
            Label.text = $"{name} / {days} OF {required} SUCCESSFUL DAYS";
            Fill.fillAmount = Mathf.Clamp01((float)days / required);
        }
    }

    public void Configure(AnimatedPanel panel, TMP_Text title, TMP_Text nameText, TMP_Text detailText,
        TMP_Text statusText, TMP_Text confirmText, TMP_Text cancelText, Button confirm, Button cancel, Button close,
        Image icon, GameObject check, Art artwork)
    {
        art = artwork ?? new Art();
        animationPanel = panel;
        heading = title;
        badge = nameText;
        status = statusText;
        buyLabel = confirmText;
        buy = confirm;
        card = (RectTransform)heading.transform.parent;
        cardScale = card.localScale;
        fitRoot = new GameObject("Restaurant card safe area", typeof(RectTransform)).GetComponent<RectTransform>();
        fitRoot.SetParent(transform, false);
        fitRoot.anchorMin = Vector2.zero;
        fitRoot.anchorMax = Vector2.one;
        fitRoot.offsetMin = fitRoot.offsetMax = Vector2.zero;
        card.SetParent(fitRoot, false);
        card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f;
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = new Vector2(480, 492);
        if (art.frame != null) SetSprite(card.GetComponent<Image>(), art.frame, true);

        if (icon != null) icon.gameObject.SetActive(false);
        if (check != null) check.SetActive(false);
        var oldBacking = card.Find("Purchase Preview Backing");
        if (oldBacking != null) oldBacking.gameObject.SetActive(false);

        Graphic("Title plaque", art.title, 0, -45, 278, 52, true);
        Style(heading, 0, -45, 248, 40, 30, Cream, art.headingFont);
        heading.transform.SetAsLastSibling();
        Graphic("Left sparkle", art.sparkle, -121, -125, 33, 33);
        Graphic("Right sparkle", art.sparkle, 121, -125, 28, 28);
        preview = Graphic("Restaurant model render", art.fastFood, 0, -123, 306, 166);
        ribbon = Graphic("Access ribbon", art.ribbon, 0, -197, 258, 47);
        Style(badge, 0, 0, 195, 34, 27, Cream, art.headingFont);
        badge.rectTransform.SetParent(ribbon.rectTransform, false);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = Vector2.one * .5f;
        badge.rectTransform.anchoredPosition = Vector2.zero;

        priceText = MakeCashTile(detailText, "Price", -109);
        balanceText = MakeCashTile(detailText, "Cash balance", 109);
        casualRow = MakeProgressRow(detailText, -310);
        fastRow = MakeProgressRow(detailText, -351);
        modes = CopyText(detailText, "Eligible modes");
        modes.text = "Campaign / Multiplayer";
        Style(modes, 0, -385, 400, 21, 15, Ink, art.bodyFont);
        Style(status, 0, -414, 418, 36, 14, Ink, art.bodyFont);
        detailText.gameObject.SetActive(false);

        SkinButton(cancel, cancelText, art.backButton, -109);
        SkinButton(buy, buyLabel, art.buyButton, 109);
        cancelText.text = "BACK";
        if (close != null)
        {
            var rect = (RectTransform)close.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = new Vector2(-31, -31);
            rect.sizeDelta = new Vector2(48, 48);
            close.transform.SetAsLastSibling();
            close.onClick = new Button.ButtonClickedEvent();
            close.onClick.AddListener(Hide);
        }
        buy.onClick = new Button.ButtonClickedEvent();
        buy.onClick.AddListener(() => Buy?.Invoke());
        cancel.onClick = new Button.ButtonClickedEvent();
        cancel.onClick.AddListener(Hide);
        animationPanel.OnClosed = new UnityEngine.Events.UnityEvent();
        animationPanel.OnClosed.AddListener(() => gameObject.SetActive(false));
        animationPanel.OnOpened = new UnityEngine.Events.UnityEvent();
        animationPanel.OnOpened.AddListener(() =>
        {
            if (!LevelOneUIAccessibility.ReducedMotion) ribbonMotion = StartCoroutine(PopRibbon());
        });
        UISubtlePressFeedback.EnsureButtons(transform);
    }

    private IEnumerator PopRibbon()
    {
        const float duration = .22f;
        for (float elapsed = 0; elapsed < duration && IsOpen && !LevelOneUIAccessibility.ReducedMotion;
             elapsed += Time.unscaledDeltaTime)
        {
            ribbon.rectTransform.localScale = Vector3.one * (1f + .085f * Mathf.Sin(Mathf.PI * elapsed / duration));
            yield return null;
        }
        ribbon.rectTransform.localScale = Vector3.one;
        ribbonMotion = null;
    }

    private TMP_Text MakeCashTile(TMP_Text template, string name, float x)
    {
        Graphic(name + " tile", art.chip, x, -254, 202, 57, true);
        Graphic(name + " icon", art.cash, x - 68, -254, 39, 39);
        var value = CopyText(template, name);
        Style(value, x + 19, -254, 134, 51, 25, Ink, template.font);
        value.richText = true;
        return value;
    }

    private ProgressRow MakeProgressRow(TMP_Text template, float y)
    {
        var label = CopyText(template, "Successful days");
        Style(label, 24, y + 10, 348, 22, 17, Ink, art.bodyFont);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var track = Graphic("Progress track", art.track, 24, y - 9, 348, 13, true);
        var fill = Graphic("Progress fill", art.fill, 0, 0, 0, 0);
        fill.rectTransform.SetParent(track.rectTransform, false);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        if (art.fill != null)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
        }
        return new ProgressRow
        {
            Calendar = Graphic("Calendar", art.calendar, -187, y, 35, 35),
            Label = label,
            Track = track,
            Fill = fill
        };
    }

    private void SkinButton(Button button, TMP_Text label, Sprite sprite, float x)
    {
        const float width = 202f;
        Place((RectTransform)button.transform, x, -456, width, 47);
        if (button.targetGraphic is Image image && sprite != null) SetSprite(image, sprite, true);
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, .45f);
        button.colors = colors;
        Style(label, 0, 0, width - 18, 40, 22, Cream, art.headingFont);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.one * .5f;
        label.rectTransform.anchoredPosition = Vector2.zero;
    }

    private Image Graphic(string name, Sprite sprite, float x, float y, float width, float height, bool sliced = false)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(card, false);
        Place(image.rectTransform, x, y, width, height);
        image.raycastTarget = false;
        SetSprite(image, sprite, sliced);
        return image;
    }

    private static void SetSprite(Image image, Sprite sprite, bool sliced)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.color = Color.white;
        image.type = sliced && sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 4f;
        image.preserveAspect = !sliced;
    }

    private TMP_Text CopyText(TMP_Text template, string name)
    {
        var text = Instantiate(template, card, false);
        text.name = name;
        return text;
    }

    private static void Style(TMP_Text text, float x, float y, float width, float height, float size, Color color, TMP_FontAsset font)
    {
        Place(text.rectTransform, x, y, width, height);
        if (font != null) text.font = font;
        text.fontSharedMaterial = text.font.material;
        text.color = color;
        text.fontStyle = FontStyles.Normal;
        text.enableAutoSizing = true;
        text.fontSize = text.fontSizeMax = size;
        text.fontSizeMin = size * .68f;
        text.margin = Vector4.zero;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.autoSizeTextContainer = false;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = Vector2.one * .5f;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    public void Show()
    {
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        ribbon.rectTransform.localScale = Vector3.one;
        animationPanel.Open();
    }

    public void Hide()
    {
        if (ribbonMotion != null)
        {
            StopCoroutine(ribbonMotion);
            ribbonMotion = null;
            ribbon.rectTransform.localScale = Vector3.one;
        }
        animationPanel.Close();
    }

    public void Render(bool fine, int price, long balance, int casualDays, int casualRequired, int fastDays,
        int fastRequired, string message, string action, bool canBuy, bool owned, bool newlyOwned)
    {
        heading.text = fine ? "FINE DINING" : "FAST FOOD";
        var model = fine ? art.fineDining : art.fastFood;
        if (preview.sprite != model) SetSprite(preview, model, false);
        badge.text = owned ? "UNLOCKED" : "LOCKED";
        var ribbonSprite = owned ? art.ownedRibbon : art.ribbon;
        if (ribbon.sprite != ribbonSprite) SetSprite(ribbon, ribbonSprite, false);
        casualRow.Update("CASUAL DINING", casualDays, casualRequired);
        fastRow.Update("FAST FOOD", fastDays, fastRequired);
        modes.enabled = casualRequired > 0;
        status.text = message;
        priceText.text = "<size=62%>PRICE</size>\n₱" + price.ToString("N0");
        balanceText.text = "<size=62%>YOUR CASH</size>\n" + (balance < 0 ? "Loading…" : "₱" + balance.ToString("N0"));
        buyLabel.text = action;
        buy.interactable = canBuy;
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

    private void OnDisable()
    {
        ribbonMotion = null;
        Closed?.Invoke();
    }
}
