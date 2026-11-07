using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Artwork/layout for the existing customer thought prefab. Owns no outcome or lifetime.</summary>
[DisallowMultipleComponent]
public sealed class CustomerCommentBubble : MonoBehaviour
{
    private GameplayActionButtonArt art;
    private UIFollowWorldPoint follow;
    private FineDiningReservations settings;
    private TMP_Text message;
    private Image panel, tail, badge, face;
    private float unit;
    private bool largeText;
    private string previousMessage;

    public static CustomerCommentBubble Apply(GameObject root, TMP_Text message, int mood,
        Sprite originalFace, FineDiningReservations settings)
    {
        var art = GameplayActionButtonArt.Shared;
        if (root == null || message == null || art == null || art.bodyFont == null ||
            art.Find("CommentHappyPanel") == null) return null;
        var view = root.GetComponent<CustomerCommentBubble>();
        if (view == null) view = root.AddComponent<CustomerCommentBubble>();
        view.Configure(art, message, Mathf.Clamp(mood, 0, 2), originalFace, settings);
        return view;
    }

    private void Configure(GameplayActionButtonArt artwork, TMP_Text label, int mood,
        Sprite originalFace, FineDiningReservations profile)
    {
        art = artwork;
        message = label;
        settings = profile;
        follow = GetComponentInChildren<UIFollowWorldPoint>(true);
        if (panel == null)
        {
            // Keep the original message field and prefab/follow hierarchy. Only legacy
            // decorative Images are replaced; the game still owns messages and removal.
            foreach (var image in GetComponentsInChildren<Image>(true))
            { image.enabled = false; image.raycastTarget = false; }
            Transform parent = follow != null ? follow.transform : transform;
            unit = 1f / Mathf.Max(.025f, follow != null ? Mathf.Abs(follow.AuthoredScale.x) : 1f);
            tail = CreateImage("Comment tail", parent);
            panel = CreateImage("Comment panel", parent);
            badge = CreateImage("Comment mood badge", panel.transform);
            face = CreateImage("Comment mood face", badge.transform);
            message.transform.SetParent(panel.transform, false);
        }
        string name = mood == 2 ? "Angry" : mood == 1 ? "Neutral" : "Happy";
        panel.sprite = art.Find("Comment" + name + "Panel");
        panel.type = Image.Type.Sliced;
        panel.preserveAspect = false;
        panel.pixelsPerUnitMultiplier = 2f / unit;
        tail.sprite = art.Find("Comment" + name + "Tail");
        badge.sprite = art.Find("Comment" + name + "Badge");
        face.sprite = art.Find("Comment" + name + "Face") ?? originalFace;
        message.font = art.bodyFont;
        message.fontSharedMaterial = art.bodyFont.material;
        message.fontStyle = FontStyles.Normal;
        message.color = new Color(.04f, .09f, .2f, 1f);
        message.raycastTarget = false;
        message.enableAutoSizing = false;
        message.textWrappingMode = TextWrappingModes.Normal;
        message.overflowMode = TextOverflowModes.Overflow;
        message.alignment = TextAlignmentOptions.MidlineLeft;
        message.margin = Vector4.zero;
        message.richText = false;
        message.extraPadding = true;
        message.UpdateMeshPadding();
        Layout();
    }

    private void Layout()
    {
        largeText = LevelOneUIAccessibility.LargeText;
        previousMessage = message.text;
        float padding = 18f * unit;
        float badgeSize = 48f * unit;
        message.fontSize = art.commentFontSize * unit * (largeText ? 1.2f : 1f);
        float textWidth = art.commentWidth * unit - padding * 2f - badgeSize - 16f * unit;
        // A long tip amount is an atomic value, even with Large Text enabled.
        // Give it enough room rather than allowing TMP to wrap its final digit.
        foreach (string word in (message.text ?? string.Empty).Split(' '))
            if (word.IndexOf('₱') >= 0)
                textWidth = Mathf.Max(textWidth, message.GetPreferredValues(word).x + 8f * unit);
        float width = textWidth + padding * 2f + badgeSize + 16f * unit;
        float textHeight = message.GetPreferredValues(message.text, textWidth, 0f).y;
        float height = Mathf.Max(art.commentMinimumHeight * unit, textHeight + padding * 2f);
        Place(panel.rectTransform, Vector2.zero, new Vector2(width, height));
        Place(tail.rectTransform, new Vector2(24f * unit, -height * .5f - 12f * unit), new Vector2(80f, 56f) * unit);
        Place(badge.rectTransform, new Vector2(-width * .5f + padding + badgeSize * .5f, 0f), Vector2.one * badgeSize);
        Place(face.rectTransform, Vector2.zero, Vector2.one * 40f * unit);
        Place(message.rectTransform, new Vector2(width * .5f - padding - textWidth * .5f, 0f), new Vector2(textWidth, height - padding * 2f));
        if (follow != null)
        {
            follow.RefreshVisualBounds();
            follow.ConfigureCommentSizing(settings, panel, width / unit);
            follow.KeepAttachedToTarget();
        }
    }

    private void LateUpdate()
    {
        if (message != null && panel != null && (largeText != LevelOneUIAccessibility.LargeText || previousMessage != message.text)) Layout();
    }

    private static Image CreateImage(string name, Transform parent)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.gameObject.layer = parent.gameObject.layer;
        image.transform.SetParent(parent, false);
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}
