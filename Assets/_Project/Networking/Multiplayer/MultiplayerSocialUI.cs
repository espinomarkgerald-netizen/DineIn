using System;
using System.Text;
using TMPro;
using UnityEngine;

// All visuals and references are authored in the shared resource prefab.
[DefaultExecutionOrder(-250)]
public sealed partial class MultiplayerSocialUI : MonoBehaviour
{
    public MultiplayerChat chat;
    public UnityEngine.UI.Button chatButton, closeButton, sendButton;
    public UnityEngine.UI.Button[] phraseButtons, emojiButtons, emoteButtons;
    [Header("Option tray")]
    public UnityEngine.UI.Button[] categoryButtons;
    public Sprite selectedCategorySprite, unselectedCategorySprite;
    public Color optionTextColor = new(0.12f, 0.23f, 0.33f, 1f);
    public GameObject panel, unread, confirmation;
    public AnimatedPanel panelAnimation;
    public UnityEngine.UI.Button confirmButton, cancelButton;
    public TMP_Text confirmationText, feed, status;
    public TMP_InputField input;
    public UnityEngine.UI.ScrollRect scroll;
    public RectTransform safeArea;
    private MultiplayerSessionManager session;
    private CanvasGroup panelGroup;
    private string selectedAccount;
    private bool confirmingLeave;
    private static MultiplayerSocialUI active;
    private static int closedFrame = -10;
    public static bool InputBlocked => active != null && active.isActiveAndEnabled &&
        (active.panel.activeSelf || active.confirmation.activeSelf || active.wheelShield.activeSelf || active.touchId >= 0 || Time.frameCount <= closedFrame + 1 || active.TabRequested);
    public static void ShowChat() { if (active != null && active.isActiveAndEnabled) active.Open(); }
    public static void HideForExit() { if (active != null) active.gameObject.SetActive(false); }
    public static void ConfirmRemove(string account)
    {
        if (active == null || !active.isActiveAndEnabled || active.session == null || !active.session.CanRemove(account)) return;
        active.selectedAccount = account; active.confirmingLeave = false;
        var p = active.session.Run.participants.Find(m => m.accountId == account);
        active.confirmationText.richText = false;
        active.confirmationText.text = "Remove " + (p?.displayName ?? "player") + " from this session?";
        active.confirmation.SetActive(true);
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(active.confirmation, true);
    }
    public static void ConfirmLeave()
    {
        if (active == null || !active.isActiveAndEnabled || active.session == null) return;
        active.confirmingLeave = true; active.selectedAccount = null;
        active.confirmationText.text = active.session.IsHostConnection
            ? "End this session for everyone?" : "Leave this session? Your reserved place will be released.";
        active.confirmation.SetActive(true);
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(active.confirmation, true);
    }
    private void Awake()
    {
        active = this;
        panelGroup = panel.GetComponent<CanvasGroup>();
        panel.SetActive(false); confirmation.SetActive(false); unread.SetActive(false);
        InitializePresentation();
    }
    private void Start()
    {
        session = MultiplayerSessionManager.Instance;
        var catalog = chat.Catalog;
        feed.spriteAsset = catalog.emojiSprites;
        input.characterLimit = catalog.characterLimit;
        input.richText = false;
        input.lineType = TMP_InputField.LineType.SingleLine;
        chatButton.onClick.AddListener(ToggleChat);
        closeButton.onClick.AddListener(CloseChat);
        panelAnimation.OnClosed.AddListener(OnChatClosed);
        sendButton.onClick.AddListener(SendText);
        input.onSubmit.AddListener(_ => { if (input.isFocused && panel.activeSelf) SendText(); });
        cancelButton.onClick.AddListener(CloseConfirmation);
        confirmButton.onClick.AddListener(() =>
        {
            if (confirmingLeave) session.LeaveSession();
            else if (!session.RequestRemove(selectedAccount)) WarningSlideUI.Instance?.Show("This participant can no longer be removed.");
            CloseConfirmation();
        });

        Bind(emojiButtons, catalog.emojis, 2);
        Bind(emoteButtons, catalog.emotes, 3);
        for (int i = 0; i < categoryButtons.Length; i++)
        {
            int category = i;
            categoryButtons[i].onClick.AddListener(() => SelectCategory(category));
        }
        pickerButton.onClick.AddListener(TogglePicker);
        SelectCategory(0);
        optionTray.SetActive(false);
        scroll.onValueChanged.AddListener(OnHistoryScrolled);
        chat.Changed += Render;
        Render(false);
    }
    private void Bind(UnityEngine.UI.Button[] buttons, MultiplayerChatCatalog.Entry[] entries, int kind)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i]; bool exists = i < entries.Length;
            button.gameObject.SetActive(exists); if (!exists) continue;
            var entry = entries[i];
            var label = button.GetComponentInChildren<TMP_Text>();
            label.spriteAsset = chat.Catalog.emojiSprites;
            label.text = kind >= 2 ? "<sprite index=" + entry.spriteIndex + "> " + Escape(entry.label) : Escape(entry.label);
            button.onClick.AddListener(() =>
            {
                if (!chat.Send(kind, entry.id)) return;
                if (kind == 3) CloseChat(); // Reveal the emote; keep history and the unsent draft.
                else SetPicker(false);
            });
        }
    }
    private void SelectCategory(int category)
    {
        for (int i = 0; i < categoryButtons.Length; i++)
        {
            bool selected = i == category;
            categoryButtons[i].image.sprite = selected ? selectedCategorySprite : unselectedCategorySprite;
            categoryButtons[i].GetComponentInChildren<TMP_Text>().color = selected ? Color.white : optionTextColor;
        }
        for (int i = 0; i < emojiButtons.Length; i++) emojiButtons[i].gameObject.SetActive(category == 0 && i < chat.Catalog.emojis.Length);
        for (int i = 0; i < emoteButtons.Length; i++) emoteButtons[i].gameObject.SetActive(category == 1 && i < chat.Catalog.emotes.Length);
    }
    private void OnHistoryScrolled(Vector2 position)
    {
        if (panel.activeSelf && position.y <= 0.01f) unread.SetActive(false);
    }
    private void Update()
    {
        if (session == null) return;
        input.interactable = session.CanAct;
        sendButton.interactable = chat.CanSend && !string.IsNullOrWhiteSpace(input.text);
        foreach (var b in emojiButtons) b.interactable = chat.CanSend;
        if (panel.activeSelf)
        {
            var avatar = session.LocalManager;
            bool canEmote = avatar != null && avatar.TryGetComponent<MultiplayerAvatarEmote>(out var emote) && emote.CanPlay;
            foreach (var b in emoteButtons) b.interactable = chat.CanSend && canEmote;
        }
        confirmButton.interactable = confirmingLeave ? session.IsConnected || session.Ended : session.CanRemove(selectedAccount);
        status.text = session.CanAct ? (Application.isMobilePlatform ? string.Empty : "Enter to send  /  Esc to close") : "Chat unavailable while disconnected or synchronizing.";
        UpdatePresentationInput();
        if (InputBlocked && Input.GetKeyDown(KeyCode.Escape))
        { if (confirmation.activeSelf) CloseConfirmation(); else if (wheelShield.activeSelf || touchId >= 0) EndWheel(false, true); else if (optionTray.activeSelf) SetPicker(false); else if (panel.activeSelf) CloseChat();
            closedFrame = Time.frameCount; }
        Rect safe = Screen.safeArea;
        safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        RefreshWindowLayout();
    }
    private void Open()
    {
        if (session == null || session.Ended) return;
        EndWheel(false, true);
        SetPicker(false);
        CaptureCursor();
        panelAnimation.Open();
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(panel, true);
        unread.SetActive(false);
        if (!Application.isMobilePlatform) input.ActivateInputField();
        Render(false);
    }
    private void CloseChat()
    {
        pickerPending = false; // Keep the current geometry during the close animation.
        input.DeactivateInputField();
        panelAnimation.Close();
        // AnimatedPanel normally releases raycasts as its fade starts. Keep this
        // modal shield until OnClosed so pointer events cannot hit the HUD below.
        if (panel.activeInHierarchy) panelGroup.blocksRaycasts = true;
    }
    private void OnChatClosed()
    {
        SetPicker(false);
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(panel, false);
        closedFrame = Time.frameCount;
        RestoreCursor();
    }
    private void CloseConfirmation()
    {
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(confirmation, false);
        confirmation.SetActive(false); selectedAccount = null; closedFrame = Time.frameCount;
    }
    private void SendText()
    {
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && !input.isFocused) return;
        if (chat.Send(0, input.text)) { input.text = ""; input.ActivateInputField(); }
    }
    private void Render(bool notify)
    {
        bool followLatest = !notify || scroll.verticalNormalizedPosition <= 0.01f;
        Vector2 readingPosition = scroll.content.anchoredPosition;
        var lines = new StringBuilder();
        foreach (var message in chat.Messages)
        {
            string text = MessageBody(chat.Catalog, message);
            lines.Append("<b>").Append(Escape(message.sender)).Append(":</b> ").Append(text).Append('\n');
        }
        feed.text = lines.Length > 0 ? lines.ToString().TrimEnd() : "No messages yet.";
        if (panel.activeSelf)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            if (followLatest) scroll.verticalNormalizedPosition = 0f;
            else scroll.content.anchoredPosition = readingPosition;
        }
        if (notify && (!panel.activeSelf || !followLatest)) unread.SetActive(true);
    }
    public static string Escape(string value) => (value ?? "Player").Replace('<', '\uFF1C').Replace('>', '\uFF1E').Replace('\n', ' ').Replace('\r', ' ');
    public static string MessageBody(MultiplayerChatCatalog catalog, MultiplayerChat.Message message)
    {
        if (message.kind == 0) return Escape(message.value);
        var entry = catalog.Find(message.value, message.kind);
        if (entry == null) return "";
        return message.kind == 1 ? Escape(entry.label) : "<sprite index=" + entry.spriteIndex + ">" +
            (message.kind == 3 ? " " + Escape(entry.label) : "");
    }
    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        EndWheel(false, true);
        input.DeactivateInputField(); panel.SetActive(false); OnChatClosed(); CloseConfirmation();
    }
    private void OnDestroy()
    {
        if (chat != null) chat.Changed -= Render;
        if (scroll != null) scroll.onValueChanged.RemoveListener(OnHistoryScrolled);
        if (panelAnimation != null) panelAnimation.OnClosed.RemoveListener(OnChatClosed);
        if (active == this) active = null;
    }
}
