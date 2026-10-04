using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Presentation and input for the existing shared chat. Sending still goes through MultiplayerChat.
public sealed partial class MultiplayerSocialUI
{
    [Header("Compact chat")]
    public RectTransform window;
    public UnityEngine.UI.Button pickerButton;
    public GameObject optionTray;
    public TMP_Text newMessages;
    public Vector2 desktopSize = new(760f, 460f);
    public Vector2 desktopInset = new(240f, 250f);
    public Vector2 mobileSize = new(1100f, 620f);
    [Min(220f)] public float minimumMobileHeight = 264f;
    [Range(.2f, .7f)] public float keyboardFallbackFraction = .42f;
    public float historyBottom = 100f, pickerHeight = 196f;
    [Header("Hold wheel")]
    public GameObject wheelShield;
    public RectTransform wheel;
    public CanvasGroup wheelGroup;
    public TMP_Text wheelCaption;
    public UnityEngine.UI.Image holdProgress;
    public string[] wheelLabels = { "Help", "Orders", "Cook", "Serve", "Clean", "Restock", "Thanks", "Good job" };
    [Min(.1f)] public float holdSeconds = .35f;
    [Min(100f)] public float wheelRadius = 224f;
    [Min(0f)] public float deadZone = 90f;
    [Min(1f)] public float deliberateDrag = 14f;
    [Min(.01f)] public float wheelFadeSeconds = .15f;
    [Range(1f, 1.15f)] public float selectedScale = 1.05f;
    private Canvas uiCanvas;
    private TMP_Text[] wheelTexts;
    private int touchId = -1, highlighted = -1, suppressedClickFrame = -10;
    private float pressedAt, wheelStarted, wheelClosedAt;
    private bool wheelOpen, wheelClosing, deliberate, cursorCaptured, previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private Vector2 selectionOrigin, heldPosition, lastSafeSize;
    private Rect lastSafeRect;
    private float lastKeyboard = -1f;
    private string draftRun;
    private bool hasFocus = true, pickerPending, suppressHudClick;
    private float closingAlpha;
    private Camera UICamera => uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCanvas.worldCamera;
    private bool TabRequested => !Application.isMobilePlatform && Input.GetKeyDown(KeyCode.Tab) && EligibleForWheel();
    private static bool Typing()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selected != null && (selected.GetComponentInParent<TMP_InputField>()?.isFocused == true ||
            selected.GetComponentInParent<UnityEngine.UI.InputField>()?.isFocused == true);
    }
    private bool EligibleForWheel() => hasFocus && session != null && session.CanAct &&
        !panel.activeSelf && !confirmation.activeSelf && !wheelShield.activeSelf && !Typing() &&
        !LobbyPauseMenu.IsAnyOpen && !DevSettingsConsole.IsWindowOpen && !GameplayUIBlocker.IsBlocked();
    private void InitializePresentation()
    {
        uiCanvas = GetComponent<Canvas>();
        wheelShield.SetActive(false); optionTray.SetActive(false); holdProgress.gameObject.SetActive(false);
        wheelTexts = new TMP_Text[phraseButtons.Length];
        for (int i = 0; i < phraseButtons.Length; i++) wheelTexts[i] = phraseButtons[i].GetComponentInChildren<TMP_Text>();
        // Leave the one HUD toggle reachable above the transparent chat input shield.
        chatButton.transform.SetAsLastSibling();
    }
    private void ToggleChat()
    {
        if (suppressHudClick || Time.frameCount <= suppressedClickFrame || wheelShield.activeSelf || touchId >= 0) return;
        if (panel.activeSelf) CloseChat(); else Open();
    }
    private void TogglePicker()
    {
        bool show = !optionTray.activeSelf;
        input.DeactivateInputField();
        if (show && Application.isMobilePlatform && TouchScreenKeyboard.visible)
        {
            pickerPending = true;
            if (input.touchScreenKeyboard != null) input.touchScreenKeyboard.active = false;
        }
        else SetPicker(show);
    }
    private void SetPicker(bool show)
    {
        pickerPending = false;
        optionTray.SetActive(show);
        scroll.GetComponent<RectTransform>().offsetMin = new Vector2(24f, historyBottom + (show ? pickerHeight : 0f));
    }
    private void CaptureCursor()
    {
        if (cursorCaptured || Application.isMobilePlatform) return;
        previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible; cursorCaptured = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    private void RestoreCursor()
    {
        if (!cursorCaptured) return;
        Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible; cursorCaptured = false;
    }
    public void BeginHudHold(int pointerId, Vector2 position)
    {
        if (!Application.isMobilePlatform || pointerId < 0 || touchId >= 0 || wheelShield.activeSelf) return;
        // Only a new physical press clears suppression; a late release after focus loss cannot toggle chat.
        suppressHudClick = false;
        if (!EligibleForWheel()) return;
        touchId = pointerId; heldPosition = position; pressedAt = Time.unscaledTime;
        holdProgress.fillAmount = 0f; holdProgress.gameObject.SetActive(true);
    }
    public void EndHudHold(int pointerId, Vector2 position, bool canceled)
    {
        if (pointerId != touchId) return;
        if (wheelOpen)
        {
            UpdateSelection(position);
            suppressedClickFrame = Time.frameCount + 1;
            EndWheel(!canceled && !Input.GetKeyDown(KeyCode.Escape), false);
        }
        else if (canceled) { suppressedClickFrame = Time.frameCount + 1; suppressHudClick = true; }
        touchId = -1; holdProgress.gameObject.SetActive(false);
        closedFrame = Time.frameCount;
    }
    private void UpdatePresentationInput()
    {
        if (pickerPending && !TouchScreenKeyboard.visible && panel.activeSelf) SetPicker(true);
        if (draftRun != session.RunId)
        {
            draftRun = session.RunId; input.text = ""; unread.SetActive(false); EndWheel(false, true);
        }
        if (!session.CanAct || !hasFocus || (wheelOpen && GameplayUIBlocker.IsBlockedExcept(wheelShield)) ||
            (touchId >= 0 && !wheelOpen && !wheelClosing && GameplayUIBlocker.IsBlocked()))
        {
            if (touchId >= 0 || wheelShield.activeSelf) EndWheel(false, true);
        }
        else
        {
            if (TabRequested) OpenWheel(Input.mousePosition, false);
            if (touchId >= 0)
            {
                bool found = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId != touchId) continue;
                    found = true; heldPosition = touch.position;
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    { EndHudHold(touchId, heldPosition, touch.phase == TouchPhase.Canceled); break; }
                    if (!wheelOpen && !wheelClosing)
                    {
                        holdProgress.fillAmount = Mathf.Clamp01((Time.unscaledTime - pressedAt) / holdSeconds);
                        if (Time.unscaledTime - pressedAt >= holdSeconds)
                        { OpenWheel(heldPosition, true); suppressedClickFrame = Time.frameCount + 1; }
                    }
                    if (wheelOpen) UpdateSelection(heldPosition);
                    break;
                }
                if (!found) EndHudHold(touchId, heldPosition, true);
            }
            else if (wheelOpen)
            {
                UpdateSelection(Input.mousePosition);
                if (Input.GetKeyDown(KeyCode.Escape)) EndWheel(false, false);
                else if (Input.GetKeyUp(KeyCode.Tab)) EndWheel(true, false);
                else if (!Input.GetKey(KeyCode.Tab)) EndWheel(false, false);
            }
        }
        if (wheelOpen)
        {
            wheelGroup.alpha = Mathf.Clamp01((Time.unscaledTime - wheelStarted) / wheelFadeSeconds);
        }
        else if (wheelClosing)
        {
            wheelGroup.alpha = closingAlpha * (1f - Mathf.Clamp01((Time.unscaledTime - wheelClosedAt) / wheelFadeSeconds));
            if (wheelGroup.alpha <= 0f) FinishWheel();
        }
        newMessages.gameObject.SetActive(panel.activeSelf && unread.activeSelf);
    }
    private void OpenWheel(Vector2 screenPoint, bool mobile)
    {
        if (panel.activeSelf || confirmation.activeSelf || !session.CanAct || Typing()) return;
        CaptureCursor();
        wheelShield.SetActive(true); wheelShield.transform.SetAsLastSibling();
        wheelOpen = true; wheelClosing = false; wheelStarted = Time.unscaledTime;
        wheelGroup.alpha = 0f; wheelGroup.blocksRaycasts = true;
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(wheelShield, true);
        var parent = (RectTransform)wheel.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, UICamera, out var point);
        Vector2 extent = Vector2.zero;
        for (int i = 0; i < phraseButtons.Length; i++)
        {
            float angle = (90f - i * 45f) * Mathf.Deg2Rad;
            var option = (RectTransform)phraseButtons[i].transform;
            option.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * wheelRadius;
            Vector2 edge = new Vector2(Mathf.Abs(option.anchoredPosition.x), Mathf.Abs(option.anchoredPosition.y)) + option.rect.size * (selectedScale * .5f);
            extent = Vector2.Max(extent, edge);
        }
        extent += Vector2.one * 12f;
        wheel.sizeDelta = extent * 2f;
        if (mobile) point.x -= extent.x; // inward from the right-hand HUD button
        Rect bounds = parent.rect;
        point.x = Mathf.Clamp(point.x, bounds.xMin + extent.x + 12f, bounds.xMax - extent.x - 12f);
        point.y = Mathf.Clamp(point.y, bounds.yMin + extent.y + 12f, bounds.yMax - extent.y - 12f);
        wheel.localPosition = new Vector3(point.x, point.y, 0f);
        for (int i = 0; i < phraseButtons.Length; i++)
        {
            float angle = (90f - i * 45f) * Mathf.Deg2Rad;
            ((RectTransform)phraseButtons[i].transform).anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * wheelRadius;
            wheelTexts[i].text = wheelLabels[i];
        }
        selectionOrigin = screenPoint; deliberate = false; highlighted = -2; SetWheelSelection(-1);
        holdProgress.gameObject.SetActive(false);
    }
    private void UpdateSelection(Vector2 screenPoint)
    {
        float scale = Mathf.Max(.01f, uiCanvas.scaleFactor);
        if ((screenPoint - selectionOrigin).sqrMagnitude >= deliberateDrag * deliberateDrag * scale * scale) deliberate = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(wheel, screenPoint, UICamera, out var local);
        int choice = -1;
        if (deliberate && local.magnitude >= deadZone)
            for (int i = 0; i < phraseButtons.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)phraseButtons[i].transform, screenPoint, UICamera))
                { choice = i; break; }
        SetWheelSelection(choice);
    }
    private void SetWheelSelection(int choice)
    {
        if (choice == highlighted) return;
        highlighted = choice;
        for (int i = 0; i < phraseButtons.Length; i++)
        {
            bool selected = i == choice;
            phraseButtons[i].image.sprite = selected ? selectedCategorySprite : unselectedCategorySprite;
            wheelTexts[i].color = selected ? Color.white : optionTextColor;
            phraseButtons[i].transform.localScale = Vector3.one * (selected && !LevelOneUIAccessibility.ReducedMotion ? selectedScale : 1f);
        }
        wheelCaption.text = choice >= 0 ? chat.Catalog.phrases[choice].label :
            Application.isMobilePlatform ? "Drag to choose\nRelease to send" : "Move to choose\nRelease Tab to send";
    }
    private void EndWheel(bool send, bool immediate)
    {
        bool wasOpen = wheelOpen;
        if (wasOpen || touchId >= 0) closedFrame = Time.frameCount;
        int choice = highlighted;
        wheelOpen = false; highlighted = -1; deliberate = false;
        if (touchId >= 0) { suppressedClickFrame = Time.frameCount + 1; suppressHudClick = true; }
        touchId = -1;
        if (holdProgress != null) holdProgress.gameObject.SetActive(false);
        if (wasOpen && send && hasFocus && session != null && session.CanAct && choice >= 0 && choice < chat.Catalog.phrases.Length)
            chat.Send(1, chat.Catalog.phrases[choice].id);
        if (wheelShield == null || !wheelShield.activeSelf) return;
        closedFrame = Time.frameCount;
        if (immediate) FinishWheel();
        else { wheelClosing = true; wheelClosedAt = Time.unscaledTime; closingAlpha = wheelGroup.alpha; }
    }
    private void FinishWheel()
    {
        wheelClosing = false; wheelShield.SetActive(false);
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(wheelShield, false);
        closedFrame = Time.frameCount; RestoreCursor();
    }
    private void RefreshWindowLayout()
    {
        Rect safe = Screen.safeArea;
        float keyboard = Application.isMobilePlatform && TouchScreenKeyboard.visible
            ? (TouchScreenKeyboard.area.height > 0 ? TouchScreenKeyboard.area.height : Screen.height * keyboardFallbackFraction) : 0f;
        Vector2 size = safeArea.rect.size;
        if (lastSafeRect == safe && lastSafeSize == size && Mathf.Approximately(lastKeyboard, keyboard)) return;
        if (wheelShield.activeSelf) EndWheel(false, true); // orientation/safe-area changes cancel rather than reuse stale geometry
        lastSafeRect = safe; lastSafeSize = size; lastKeyboard = keyboard;
        float keyboardInset = Mathf.Max(0f, keyboard - safe.yMin) / Mathf.Max(.01f, uiCanvas.scaleFactor);
        if (Application.isMobilePlatform)
        {
            window.anchorMin = window.anchorMax = new Vector2(.5f, 0f); window.pivot = new Vector2(.5f, 0f);
            float available = size.y - keyboardInset - 32f;
            float reserveTop = Mathf.Min(desktopInset.y, Mathf.Max(0f, available - minimumMobileHeight));
            window.sizeDelta = new Vector2(Mathf.Min(mobileSize.x, size.x - 32f), Mathf.Min(mobileSize.y, available - reserveTop));
            window.anchoredPosition = new Vector2(0f, keyboardInset + 16f);
        }
        else
        {
            window.anchorMin = window.anchorMax = Vector2.one; window.pivot = Vector2.one;
            window.sizeDelta = new Vector2(Mathf.Min(desktopSize.x, size.x - 32f), Mathf.Min(desktopSize.y, size.y - desktopInset.y - 16f));
            window.anchoredPosition = new Vector2(-Mathf.Min(desktopInset.x, Mathf.Max(16f, size.x - window.rect.width - 16f)), -desktopInset.y);
        }
    }
    private void OnEnable() => UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
    private void OnSceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next)
    {
        EndWheel(false, true);
        if (panel.activeSelf) { SetPicker(false); input.DeactivateInputField(); panel.SetActive(false); OnChatClosed(); }
    }
    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;
        if (!focused) { EndWheel(false, true); if (panel.activeSelf) { input.DeactivateInputField(); panel.SetActive(false); OnChatClosed(); } }
    }
    private void OnApplicationPause(bool paused) { if (paused) OnApplicationFocus(false); else hasFocus = true; }
}
