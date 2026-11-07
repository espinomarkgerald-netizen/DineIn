using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation only: keeps the original Button, listeners, hit target and action owner.
/// Following/reveal and press motion remain owned by UIFollowWorldPoint/ButtonAnimator.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class GameplayActionButtonSkin : MonoBehaviour
{
    private Button button;
    private Image frame, icon, badge;
    private TMP_Text caption, identifier, detail;
    private RectTransform rect;
    private GameplayActionButtonArt art;
    private UIFollowWorldPoint follow;
    private CanvasGroup content;
    private float unit, fadeStarted;
    private bool world, numberOnly, fading, layingOut;
    private string colorName, iconName, previousCaption, previousNumber, previousDetail;
    private bool previousLarge;
    private Vector2 previousSize;
    private Slider patience;
    public float FineDiningVisualScale => art != null ? art.fineDiningVisualScale : 1f;

    public static GameplayActionButtonSkin TableNumber(Button target, TMP_Text number)
    {
        if (target == null || GameplayActionButtonArt.Shared == null) return null;
        var skin = target.GetComponent<GameplayActionButtonSkin>();
        if (skin == null) skin = target.gameObject.AddComponent<GameplayActionButtonSkin>();
        skin.Initialize();
        skin.ShowNumber(number);
        return skin;
    }

    public static GameplayActionButtonSkin Apply(Button target, string iconName, string label,
        string color = "Cyan", TMP_Text number = null, TMP_Text value = null, Sprite customIcon = null)
    {
        if (target == null || GameplayActionButtonArt.Shared == null) return null;
        var skin = target.GetComponent<GameplayActionButtonSkin>();
        if (skin == null) skin = target.gameObject.AddComponent<GameplayActionButtonSkin>();
        skin.Initialize();
        skin.Configure(iconName, label, color, number, value, customIcon);
        return skin;
    }

    public static void TryAttach(Button target)
    {
        if (target == null || target.GetComponent<GameplayActionButtonSkin>() != null) return;
        // Management reservations retain their original complete presentation.
        if (target.GetComponentInParent<ManagementReservationPanel>(true) != null) return;
        // Whole reservation cards are also Buttons. Their portraits, party details
        // and selection frames belong to the card presenter, not an action pill.
        if (target.GetComponent<ManagementReservationCard>() != null) return;
        // Only these existing restaurant panels opt into generic caption styling.
        // Customer bubbles are configured by their actual owners with their identifier refs.
        if (target.GetComponentInParent<FastFoodCookingView>(true) != null) return;
        var pour = target.GetComponentInParent<WinePouringUI>(true);
        bool cleaning = target.GetComponentInParent<BoothMessCleanUI>(true) != null;
        if (!cleaning && (pour == null || target != pour.backButton)) return;
        var text = target.GetComponentInChildren<TMP_Text>(true);
        if (text == null) return;
        string label = text.text;
        string lower = (target.name + " " + label).ToLowerInvariant();
        string sprite = lower.Contains("back") || lower.Contains("exit") || lower.Contains("cancel") ? "IconBack" :
            lower.Contains("decline") ? "IconDeclineRequest" : lower.Contains("accept") || lower.Contains("confirm") ? "IconAcceptReservation" :
            lower.Contains("clean") ? "IconClean" : lower.Contains("staff") ? "IconWaiting" :
            lower.Contains("restock") || lower.Contains("supplies") ? "IconRestock" :
            lower.Contains("grill") ? "IconGrill" : lower.Contains("fry") ? "IconFryer" :
            lower.Contains("assembl") ? "IconAssembly" : lower.Contains("serve") ? "IconServeFood" : "";
        string color = lower.Contains("decline") ? "Coral" : lower.Contains("accept") || lower.Contains("confirm") || lower.Contains("serve") ? "Green" : "Cyan";
        var skin = Apply(target, sprite, label, color);
        // These labels are edited by their panel (request counts, pending state, etc.).
        if (skin != null) skin.UseCaption(text);
    }

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (button != null) return;
        button = GetComponent<Button>();
        rect = (RectTransform)transform;
        frame = button.targetGraphic as Image;
        if (frame == null) frame = GetComponent<Image>();
        art = GameplayActionButtonArt.Shared;
        follow = GetComponentInParent<UIFollowWorldPoint>(true);
        world = follow != null || GetComponentInParent<BoothMessCleanUI>(true) != null;
        // Work in the prefab's authored units. Never resize a fixed ancestor or replace its scale.
        if (follow != null)
        {
            float scale = Mathf.Abs(follow.AuthoredScale.x);
            for (Transform cursor = transform; cursor != null && cursor != follow.transform; cursor = cursor.parent)
                scale *= Mathf.Abs(cursor.localScale.x);
            unit = art.worldVisualScale / Mathf.Max(.025f, scale);
        }
        else unit = world ? Mathf.Max(.025f, rect.rect.height / 96f) : Mathf.Clamp(rect.rect.height / 96f, .5f, 1.5f);
    }

    private void Configure(string nextIcon, string label, string color, TMP_Text number, TMP_Text value, Sprite custom)
    {
        if (art == null || frame == null) return;
        EnsureContent();
        bool changed = colorName != color || iconName != nextIcon || caption.text != label || numberOnly;
        numberOnly = false;
        colorName = color;
        iconName = nextIcon;
        identifier = number;
        detail = value;
        HideLegacyGraphics();
        caption.text = label;
        caption.gameObject.SetActive(true);
        icon.sprite = custom != null ? custom : art.Find(nextIcon);
        icon.enabled = icon.sprite != null;
        frame.enabled = true;
        frame.color = Color.white;
        frame.type = Image.Type.Sliced;
        frame.preserveAspect = false;
        frame.pixelsPerUnitMultiplier = 4f / unit;
        frame.sprite = art.Find("Button" + color + "Ready");
        frame.overrideSprite = null;
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState {
            highlightedSprite = art.Find("Button" + color + "Focused"),
            selectedSprite = art.Find("Button" + color + "Focused"),
            pressedSprite = art.Find("Button" + color + "Pressed"),
            disabledSprite = art.Find("Button" + color + "Disabled") };
        // SpriteSwap does not clear tint left on the CanvasRenderer by ColorTint.
        frame.CrossFadeColor(Color.white, 0f, true, true);
        var animator = GetComponent<ButtonAnimator>();
        if (animator != null) animator.SetBaseColor(Color.white);
        Layout();
        if (changed && isActiveAndEnabled) BeginFade();
    }

    public void ShowNumber(TMP_Text number)
    {
        if (art == null || frame == null) return;
        EnsureContent();
        bool changed = !numberOnly;
        numberOnly = true;
        identifier = number;
        detail = null;
        HideLegacyGraphics();
        caption.gameObject.SetActive(false);
        icon.enabled = false;
        badge.enabled = false;
        frame.enabled = true;
        frame.color = Color.white;
        frame.type = Image.Type.Simple;
        frame.preserveAspect = true;
        frame.sprite = art.Find("TableNumberBubble");
        frame.overrideSprite = null;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = ColorBlock.defaultColorBlock;
        float size = art.numberBubbleSize * unit;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
        if (identifier != null)
        {
            Style(identifier, true);
            Place(identifier.rectTransform, content.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(72, 60) * unit);
            identifier.fontSize = art.labelSize * 1.2f * unit;
        }
        RefreshBounds();
        if (changed && isActiveAndEnabled) BeginFade();
    }

    private void EnsureContent()
    {
        if (content != null) return;
        var root = new GameObject("Action artwork", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);
        var r = (RectTransform)root.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        content = root.GetComponent<CanvasGroup>();
        content.blocksRaycasts = false;
        icon = CreateImage("Action icon", content.transform);
        badge = CreateImage("Table badge", content.transform);
        badge.sprite = art.Find("TableBadge"); badge.type = Image.Type.Sliced;
        caption = new GameObject("Action label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        caption.gameObject.layer = gameObject.layer;
        caption.transform.SetParent(content.transform, false);
        caption.text = string.Empty;
        Style(caption, true);
    }

    public void UseCaption(TMP_Text existing)
    {
        if (existing == null || existing == caption || art == null || content == null) return;
        if (caption != null)
        {
        caption.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(caption.gameObject);
        else DestroyImmediate(caption.gameObject);
        }
        caption = existing;
        caption.gameObject.SetActive(true);
        Style(caption, true);
        Layout();
    }

    public void SetColor(string color)
    {
        if (numberOnly || colorName == color || caption == null) return;
        Configure(iconName, caption.text, color, identifier, detail, icon.sprite);
    }

    private void HideLegacyGraphics()
    {
        // Retain real progress controls and dynamic amount/ID fields. Decorative legacy
        // frame siblings can otherwise show through the transparent sprite margins.
        Transform scope = follow != null ? follow.transform : transform;
        foreach (var graphic in scope.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == frame || graphic == identifier || graphic == detail || graphic.transform.IsChildOf(content.transform)) continue;
            if (graphic.GetComponentInParent<Slider>() != null) continue;
            var ownerButton = graphic.GetComponentInParent<Button>();
            if (ownerButton != null && ownerButton != button) continue;
            if (graphic.name == "Task ownership" || (graphic.transform.parent != null && graphic.transform.parent.name == "Task ownership")) continue;
            graphic.enabled = false;
            if (graphic is TMP_Text) graphic.gameObject.SetActive(false);
        }
    }

    private void Style(TMP_Text text, bool heading)
    {
        if (text == null) return;
        text.enabled = true;
        text.raycastTarget = false;
        text.enableAutoSizing = false;
        text.font = heading ? art.headingFont : art.bodyFont;
        Material material = heading ? art.headingMaterial : art.bodyMaterial;
        if (material != null) text.fontSharedMaterial = material;
        text.extraPadding = true;
        text.UpdateMeshPadding();
        text.color = new Color(1f, .98f, .88f);
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero;
    }

    private void Layout()
    {
        if (layingOut || content == null || numberOnly) return;
        layingOut = true;
        bool large = LevelOneUIAccessibility.LargeText;
        caption.fontSize = art.labelSize * unit * (large ? 1.2f : 1f);
        caption.textWrappingMode = TextWrappingModes.NoWrap;
        float iconWidth = icon.enabled ? 34f * unit : 0f;
        float padding = 64f * unit;
        float preferred = caption.GetPreferredValues(caption.text).x;
        if (detail != null)
        {
            Style(detail, false);
            detail.fontSize = art.detailSize * unit * (large ? 1.2f : 1f);
            preferred = Mathf.Max(preferred, detail.GetPreferredValues(detail.text).x);
        }
        float width = rect.rect.width;
        if (world)
        {
            float maxWidth = art.maximumWorldWidth * unit;
            width = Mathf.Clamp(preferred + padding + iconWidth, art.worldButtonWidth * unit, maxWidth);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }
        float textWidth = Mathf.Max(20f * unit, width - padding - iconWidth);
        if (preferred > textWidth) caption.textWrappingMode = TextWrappingModes.Normal;
        float textHeight = caption.GetPreferredValues(caption.text, textWidth, 0f).y;
        bool hasDetail = detail != null && !string.IsNullOrEmpty(detail.text);
        float detailHeight = hasDetail ? Mathf.Max(28f * unit, detail.GetPreferredValues(detail.text).y + 4f * unit) : 0f;
        float captionHeight = textHeight + 4f * unit;
        float gap = 8f * unit;
        float height = Mathf.Max(art.worldButtonHeight * unit, captionHeight + (hasDetail ? detailHeight + gap : 0) + 16f * unit);
        // Panel buttons keep their authored allocation; grow only floating world prompts.
        if (world) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        else height = rect.rect.height;
        Place(icon.rectTransform, content.transform, new Vector2(0, .5f), new Vector2(30f * unit + iconWidth * .5f, 0), Vector2.one * 34f * unit);
        Place(caption.rectTransform, content.transform, new Vector2(.5f, .5f), new Vector2(iconWidth * .5f, hasDetail ? (detailHeight + gap) * .5f : 0), new Vector2(textWidth, captionHeight));
        if (detail != null)
        {
            Style(detail, false);
            detail.fontSize = art.detailSize * unit * (large ? 1.2f : 1f);
            Place(detail.rectTransform, content.transform, new Vector2(.5f, .5f), new Vector2(iconWidth * .5f, -(captionHeight + gap) * .5f), new Vector2(textWidth, detailHeight));
        }
        badge.enabled = identifier != null && !string.IsNullOrEmpty(identifier.text);
        if (identifier != null)
        {
            Style(identifier, true);
            identifier.fontSize = art.detailSize * unit;
            float badgeWidth = Mathf.Max(92f * unit, identifier.GetPreferredValues(identifier.text).x + 24f * unit);
            Place(badge.rectTransform, content.transform, new Vector2(.5f, 1f), new Vector2(0, 8f * unit), new Vector2(badgeWidth, 38f * unit));
            badge.pixelsPerUnitMultiplier = 4f / unit;
            identifier.fontSize = art.detailSize * unit;
            Place(identifier.rectTransform, content.transform, new Vector2(.5f, 1f), new Vector2(0, 8f * unit), new Vector2(badgeWidth - 20f * unit, 28f * unit));
            identifier.transform.SetAsLastSibling();
        }
        previousCaption = caption.text;
        previousNumber = identifier != null ? identifier.text : null;
        previousDetail = detail != null ? detail.text : null;
        previousLarge = large;
        previousSize = rect.rect.size;
        layingOut = false;
        PositionProgress();
        if (follow != null) follow.RefreshFineDiningActionMetrics();
        RefreshBounds();
    }

    private void RefreshBounds()
    {
        if (follow != null)
        {
            follow.ConfigureScreenSizing(frame, rect.rect.width / unit);
            follow.RefreshVisualBounds(); follow.KeepAttachedToTarget();
        }
        var focus = GetComponentInParent<PlayerTaskBubbleFocus>();
        if (focus != null) focus.RefreshPresentation();
    }

    public void PlaceProgress(Slider slider)
    {
        if (slider == null) return;
        patience = slider;
        slider.interactable = false;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        PositionProgress();
        Image background = slider.transform.Find("Background")?.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = art.Find("PatienceTrack");
            background.color = new Color(.04f, .09f, .2f, 1f);
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 2f / unit;
            Stretch(background.rectTransform, 0f);
        }
        if (slider.fillRect != null)
        {
            var area = slider.fillRect.parent as RectTransform;
            if (area != null && area != slider.transform) Stretch(area, 2f * unit);
            // Slider retains its normalized-value anchors and owns the real fill amount.
            slider.fillRect.offsetMin = slider.fillRect.offsetMax = Vector2.zero;
            var fill = slider.fillRect.GetComponent<Image>();
            if (fill != null)
            {
                // Neutral artwork preserves the existing green/yellow/red patience tint.
                fill.sprite = art.Find("PatienceTrack"); fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = 2f / unit;
            }
        }
        foreach (var graphic in slider.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        RefreshBounds();
    }

    private void PositionProgress()
    {
        if (patience == null) return;
        float height = art.patienceBarHeight * unit;
        float badgeSpace = identifier != null && !string.IsNullOrEmpty(identifier.text) ? 32f * unit : 0f;
        Place((RectTransform)patience.transform, transform, new Vector2(.5f, 1f),
            new Vector2(0, art.patienceBarGap * unit + badgeSpace + height * .5f),
            new Vector2(rect.rect.width - 64f * unit, height));
    }

    private static void Stretch(RectTransform r, float padding)
    {
        r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.one * padding; r.offsetMax = -Vector2.one * padding;
    }

    public static UnityEngine.UI.Image HintIcon(TMP_Text label, string spriteName)
    {
        var shared = GameplayActionButtonArt.Shared;
        if (label == null || shared == null) return null;
        var child = label.transform.Find("Action hint icon");
        var image = child != null ? child.GetComponent<UnityEngine.UI.Image>() : CreateImage("Action hint icon", label.transform);
        image.sprite = shared.Find(spriteName);
        image.enabled = image.sprite != null;
        float size = Mathf.Max(28f, label.fontSize * 1.5f);
        label.margin = new Vector4(size + 10f, 0, 0, 0);
        Place(image.rectTransform, label.transform, new Vector2(0, .5f), new Vector2(size * .5f, 0), Vector2.one * size);
        return image;
    }

    private void BeginFade()
    {
        // Alpha only: no second scale owner and no input/success/audio side effects.
        fadeStarted = Time.unscaledTime;
        fading = true;
        content.alpha = LevelOneUIAccessibility.ReducedMotion ? .9f : .65f;
    }

    private void LateUpdate()
    {
        if (content == null) return;
        if (!numberOnly && (previousSize != rect.rect.size || previousLarge != LevelOneUIAccessibility.LargeText ||
            previousCaption != caption.text || previousNumber != (identifier != null ? identifier.text : null) ||
            previousDetail != (detail != null ? detail.text : null))) Layout();
        if (!fading) return;
        float t = Mathf.Clamp01((Time.unscaledTime - fadeStarted) / Mathf.Max(.01f, art.changeFadeSeconds));
        content.alpha = Mathf.Lerp(LevelOneUIAccessibility.ReducedMotion ? .9f : .65f, 1f, t);
        if (t >= 1f) fading = false;
    }

    private void OnDisable() { fading = false; if (content != null) content.alpha = 1f; }

    private static Image CreateImage(string name, Transform parent)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<Image>();
        image.gameObject.layer = parent.gameObject.layer;
        image.transform.SetParent(parent, false); image.raycastTarget = false; image.preserveAspect = true; return image;
    }

    private static void Place(RectTransform r, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        r.SetParent(parent, false); r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        // A fixed center pivot is essential for symmetric icons and attached badges.
        r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = position; r.sizeDelta = size;
    }
}
