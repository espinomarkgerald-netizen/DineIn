using System;
using Photon.Pun;
using TMPro;
using UnityEngine;

// A few authored slots on the existing social canvas; no per-message objects or canvases.
public sealed class MultiplayerChatFeedback : MonoBehaviour
{
    [Serializable] public sealed class Popup
    {
        public CanvasGroup group;
        public TMP_Text text;
        [NonSerialized] public float started;
        [NonSerialized] public Vector2 textPosition;
        [NonSerialized] public float maximumHeight;
    }
    [Serializable] public sealed class Bubble
    {
        public UIFollowWorldPoint follow;
        public TMP_Text text;
        [NonSerialized] public float started;
        [NonSerialized] public int actor;
    }
    [SerializeField] private MultiplayerChat chat;
    [SerializeField] private Popup[] popups;
    [SerializeField] private Bubble[] bubbles;
    [SerializeField, Min(1f)] private float messageSeconds = 5f;
    [SerializeField, Min(0.5f)] private float bubbleSeconds = 2.5f;
    [SerializeField, Min(0.05f)] private float fadeSeconds = 0.2f;
    [SerializeField] private Vector3 bubbleOffset = new(0f, 0.4f, 0f);
    [Header("HUD presentation")]
    [Tooltip("Optional HUD anchor. When empty, uses the existing approval row. Future restaurant HUDs can assign their own row.")]
    [SerializeField] private RectTransform popupAnchor;
    [SerializeField, Range(1, 3)] private int popupLines = 2;
    [SerializeField, Min(0f)] private float popupGap = 12f;
    [SerializeField, Min(0f)] private float popupSpacing = 8f;
    [SerializeField, Min(0f)] private float popupSlideDistance = 8f;
    [Tooltip("Frozen session roster order, so colors remain consistent after reconnects and departures.")]
    [SerializeField] private Color[] usernameColors;
    private Canvas canvas, anchorCanvas;
    private RectTransform boundAnchor;
    private readonly Vector3[] anchorCorners = new Vector3[4];
    private MultiplayerSessionManager session;
    private string runId;
    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        foreach (var popup in popups)
        {
            popup.text.spriteAsset = chat.Catalog.emojiSprites;
            popup.text.maxVisibleLines = popupLines;
            popup.maximumHeight = ((RectTransform)popup.group.transform).rect.height;
            popup.textPosition = popup.text.rectTransform.anchoredPosition;
        }
        foreach (var bubble in bubbles) bubble.text.spriteAsset = chat.Catalog.emojiSprites;
        Clear();
    }
    private void OnEnable() { chat.MessageReceived += Received; }
    private void Start() { session = MultiplayerSessionManager.Instance; runId = session?.RunId; }
    private void OnDisable() { chat.MessageReceived -= Received; Clear(); }
    private void Clear()
    {
        foreach (var popup in popups) { popup.started = -100f; popup.group.gameObject.SetActive(false); }
        foreach (var bubble in bubbles) { bubble.actor = 0; bubble.follow.gameObject.SetActive(false); }
    }
    private void Received(MultiplayerChat.Message message)
    {
        if (session == null || !session.CanAct || PhotonNetwork.Time - message.sentAt > messageSeconds) return;
        // Fixed, reusable slots keep newest messages at the top without layout rebuilds.
        for (int i = popups.Length - 1; i > 0; i--)
        {
            popups[i].started = popups[i - 1].started;
            popups[i].text.text = popups[i - 1].text.text;
        }
        if (popups.Length > 0)
        {
            popups[0].started = Time.unscaledTime;
            int index = session.Run.participants.FindIndex(p => p.accountId == message.account);
            Color color = usernameColors != null && usernameColors.Length > 0
                ? usernameColors[Mathf.Max(0, index) % usernameColors.Length] : Color.white;
            popups[0].text.text = "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" +
                MultiplayerSocialUI.Escape(message.sender) + "</color><color=#FFFFFF>: " +
                MultiplayerSocialUI.MessageBody(chat.Catalog, message) + "</color>";
        }
        if (message.kind < 2) return;
        var member = session.Run.participants.Find(p => p.accountId == message.account);
        if (member == null || !session.ValidActor(member.actor) || !session.TryGetManager(member.actor, out var avatar)) return;
        var emote = avatar.GetComponent<MultiplayerAvatarEmote>();
        if (message.kind == 3 && message.animate && emote != null) emote.Play(chat.Catalog.Find(message.value, 3), message.sentAt);
        Bubble slot = Array.Find(bubbles, b => b.actor == member.actor) ?? Array.Find(bubbles, b => b.actor == 0);
        if (slot == null) return;
        slot.actor = member.actor; slot.started = Time.unscaledTime;
        slot.text.text = MultiplayerSocialUI.MessageBody(chat.Catalog, message);
        slot.follow.gameObject.SetActive(true);
        var nameplate = avatar.GetComponentInChildren<NameTagBillboard>(true);
        slot.follow.Init(nameplate != null ? nameplate.transform : emote != null ? emote.BubbleAnchor : avatar.transform,
            nameplate != null ? Vector3.zero : bubbleOffset,
            nameplate != null && nameplate.ViewCamera != null ? nameplate.ViewCamera : Camera.main);
        slot.follow.SetTaskFocus(0f, false);
    }
    private float Alpha(float age, float lifetime) => Mathf.Clamp01(age / fadeSeconds) * Mathf.Clamp01((lifetime - age) / fadeSeconds);
    private void Update()
    {
        if (session == null || !session.IsConnected || session.Ended || session.RunId != runId)
        { Clear(); runId = session?.RunId; return; }
        foreach (var popup in popups)
        {
            float age = Time.unscaledTime - popup.started;
            bool visible = age < messageSeconds && !MultiplayerSocialUI.InputBlocked && !GameplayUIBlocker.IsBlocked();
            popup.group.gameObject.SetActive(visible);
            if (!visible) continue;
            popup.group.alpha = Alpha(age, messageSeconds);
            float slide = LevelOneUIAccessibility.ReducedMotion ? 0f
                : popupSlideDistance * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / fadeSeconds)));
            popup.text.rectTransform.anchoredPosition = popup.textPosition + Vector2.up * slide;
        }
        foreach (var bubble in bubbles)
        {
            if (bubble.actor == 0) continue;
            float age = Time.unscaledTime - bubble.started;
            if (age >= bubbleSeconds || !session.ValidActor(bubble.actor) || bubble.follow.target == null)
            { bubble.actor = 0; bubble.follow.gameObject.SetActive(false); continue; }
            bubble.follow.SetTaskFocus(Alpha(age, bubbleSeconds), false);
        }
    }
    private void LateUpdate()
    {
        // Follow the HUD's completed responsive layout; never move or restyle the HUD itself.
        if (!Array.Exists(popups, p => p.group.gameObject.activeSelf)) return;
        if (popupAnchor == null) popupAnchor = CasualDiningProgressHUD.Instance?.ApprovalPopupAnchor;
        if (popupAnchor == null || canvas == null) return;
        if (boundAnchor != popupAnchor)
        { boundAnchor = popupAnchor; anchorCanvas = popupAnchor.GetComponentInParent<Canvas>(); }
        Camera sourceCamera = anchorCanvas != null && anchorCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? anchorCanvas.worldCamera : null;
        Camera destinationCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        popupAnchor.GetWorldCorners(anchorCorners);
        float offset = popupGap;
        foreach (var popup in popups)
        {
            var rect = (RectTransform)popup.group.transform;
            var parent = (RectTransform)rect.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    RectTransformUtility.WorldToScreenPoint(sourceCamera, anchorCorners[0]), destinationCamera, out var left) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    RectTransformUtility.WorldToScreenPoint(sourceCamera, anchorCorners[3]), destinationCamera, out var right)) continue;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, right.x - left.x));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(popup.maximumHeight, popup.text.preferredHeight + 8f));
            rect.position = parent.TransformPoint(new Vector3(
                (left.x + right.x) * 0.5f - rect.rect.width * 0.5f, Mathf.Min(left.y, right.y) - offset, 0f));
            offset += rect.rect.height + popupSpacing;
        }
    }
}
