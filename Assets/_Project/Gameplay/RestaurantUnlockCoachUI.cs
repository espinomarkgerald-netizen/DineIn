using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Campaign orchestration of the tutorial's authored dialogue, mask and input cues.</summary>
public sealed class RestaurantUnlockCoachUI : MonoBehaviour
{
    public const string ResourcePath = "UI/RestaurantUnlockGuide";
    [SerializeField] private TutorialDialogueUI dialogue;
    [SerializeField] private TutorialHandIndicator hand;
    [SerializeField] private TutorialTargetIndicator worldIndicator;
    [SerializeField] private RectTransform safeArea, dialogueRoot, controlsRoot;
    [SerializeField] private TMP_Text caption, objective;
    [SerializeField] private UnityEngine.UI.Button laterButton, skipButton;
    [SerializeField, Min(0f)] private float safePadding = 24f;
    [SerializeField, Min(0f)] private float maskEntranceSeconds = .24f;
    [SerializeField] private Vector2 guideButtonSize = new Vector2(260f, 80f);
    [SerializeField, Min(0f)] private float guideButtonGap = 16f;
    private TutorialUIFocusMask mask;
    private TutorialUIAutoScroller scroller;
    private Canvas canvas;
    private CanvasGroup group;
    private CanvasGroup maskGroup;
    private Vector2 dialogueHome;
    private Vector3 dialogueScale;
    private UnityEngine.UI.Button reviewButton;
    private UnityEngine.UI.Button yesButton, noButton;
    private RectTransform choicesRoot;
    private Action reviewComplete;
    private Sprite portrait;
    private RectTransform target, hint;
    private Transform worldTarget;
    private readonly List<string> pages = new();
    private Action nextAction;
    private int page, generation, dismissedFrame = -1;
    private bool suspended, actionStep, waitingForUI, preparing;
    public bool IsExplaining { get; private set; }
    public Action Later { get; set; }
    public Action Skip { get; set; }

    public static RestaurantUnlockCoachUI Create(Sprite bossPortrait)
    {
        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError("[UnlockCoach] Missing tutorial presentation. Run Dine In/Tutorial/Create Campaign Guide Presentation outside Play mode.");
            return null;
        }
        var root = Instantiate(prefab);
        DontDestroyOnLoad(root);
        root.SetActive(true);
        var view = root.GetComponent<RestaurantUnlockCoachUI>();
        view.portrait = bossPortrait;
        view.Hide();
        return view;
    }

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        group = GetComponent<CanvasGroup>();
        mask = TutorialUIFocusMask.Create(transform);
        maskGroup = mask.gameObject.AddComponent<CanvasGroup>();
        // Campaign highlights are visual only, including during transitions.
        maskGroup.blocksRaycasts = false;
        mask.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled = false;
        dialogueHome = dialogueRoot.anchoredPosition;
        dialogueScale = dialogueRoot.localScale;
        scroller = gameObject.AddComponent<TutorialUIAutoScroller>();
        laterButton.onClick.AddListener(() => Later?.Invoke());
        skipButton.onClick.AddListener(() => Skip?.Invoke());
        // Reuse the escape-button styling for an optional equipment review, without adding HUD prose.
        reviewButton = Instantiate(skipButton, controlsRoot);
        reviewButton.name = "Continue Review";
        reviewButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        reviewButton.onClick.AddListener(() => reviewComplete?.Invoke());
        reviewButton.GetComponentInChildren<TMP_Text>().text = "CONTINUE";
        reviewButton.gameObject.SetActive(false);
        choicesRoot = new GameObject("Tour Choice", typeof(RectTransform)).GetComponent<RectTransform>();
        choicesRoot.SetParent(controlsRoot.parent, false);
        choicesRoot.anchorMin = choicesRoot.anchorMax = new Vector2(.5f, 0f);
        choicesRoot.pivot = new Vector2(.5f, 0f);
        yesButton = Instantiate(skipButton, choicesRoot);
        noButton = Instantiate(skipButton, choicesRoot);
        yesButton.name = "Yes Show Me";
        noButton.name = "No Thanks";
        yesButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        noButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        choicesRoot.gameObject.SetActive(false);
        ConfigureButtons();
        caption.gameObject.SetActive(false);
        objective.gameObject.SetActive(false);
        dialogue.CanAdvanceAt = position => !OverControls(position) && !suspended &&
            !choicesRoot.gameObject.activeSelf && OverDialogue(position);
    }

    public bool ConsumesPointer(Vector2 position) => dismissedFrame == Time.frameCount ||
        (isActiveAndEnabled && (OverControls(position) || (IsExplaining && OverDialogue(position))));

    private bool OverControls(Vector2 position) =>
        OverButton(laterButton, position) || OverButton(skipButton, position) ||
        OverButton(reviewButton, position) || OverButton(yesButton, position) || OverButton(noButton, position);

    private static bool OverButton(UnityEngine.UI.Button button, Vector2 position) =>
        button != null && button.gameObject.activeInHierarchy &&
        RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, position, null);

    private bool OverDialogue(Vector2 position) => dialogue.IsVisible &&
        RectTransformUtility.RectangleContainsScreenPoint(dialogue.BodyText.transform.parent as RectTransform, position, null);

    public void ShowOffer(string title, string message, Action accept, Action decline)
    {
        const string question = "Would you like me to show you?";
        Show(title, message + " " + question, null, () =>
        {
            dialogue.ShowWaiting("Big Boss", question, portrait);
            dialogue.SetMessage(question);
            choicesRoot.gameObject.SetActive(true);
            yesButton.onClick.RemoveAllListeners();
            noButton.onClick.RemoveAllListeners();
            yesButton.onClick.AddListener(() => { choicesRoot.gameObject.SetActive(false); accept?.Invoke(); });
            noButton.onClick.AddListener(() => { choicesRoot.gameObject.SetActive(false); decline?.Invoke(); });
        }, true, null);
    }

    public void Show(string title, string message, string action, Action onAction, bool isModal, Sprite icon)
    {
        generation++;
        StopAllCoroutines();
        scroller.Cancel();
        gameObject.SetActive(true);
        suspended = false;
        group.alpha = 1f;
        group.blocksRaycasts = true;
        dialogue.enabled = true;
        target = hint = null;
        worldTarget = null;
        waitingForUI = preparing = false;
        // Dialogue and actions are separate phases, just like the main tutorial.
        mask.Hide();
        dialogue.HideDialogue();
        hand.HideHint();
        worldIndicator.Hide();
        dialogue.SetFocusTarget(null);
        dialogue.SetWorldFocusTarget(null);
        caption.text = title;
        objective.text = string.Empty;
        reviewButton.gameObject.SetActive(false);
        choicesRoot.gameObject.SetActive(false);
        reviewComplete = null;
        actionStep = !isModal;
        nextAction = onAction;
        IsExplaining = true;
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, false);
        Layout();
        Paginate(message);
        page = 0;
        PresentPage();
    }

    // Measure the actual authored body instead of shrinking long equipment explanations to unreadable text.
    private void Paginate(string message)
    {
        pages.Clear();
        TMP_Text body = dialogue.BodyText;
        float width = body.rectTransform.rect.width - body.margin.x - body.margin.z;
        float height = body.rectTransform.rect.height - body.margin.y - body.margin.w;
        bool autoSize = body.enableAutoSizing;
        body.enableAutoSizing = false;
        body.fontSize = body.fontSizeMax;
        string current = "";
        foreach (string word in (message ?? "").Split(' '))
        {
            string proposed = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && body.GetPreferredValues(proposed, Mathf.Max(100f, width), 0f).y > height)
            { pages.Add(current); current = word; }
            else current = proposed;
        }
        pages.Add(current);
        body.enableAutoSizing = autoSize;
    }

    private void PresentPage()
    {
        dialogue.ShowManual("Big Boss", pages[page], portrait, () =>
        {
            if (++page < pages.Count) { PresentPage(); return; }
            // Consume the entire advance gesture before enabling a real gameplay action.
            StartCoroutine(AfterAdvance(generation));
        });
        if (LevelOneUIAccessibility.ReducedMotion) dialogue.SetMessage(pages[page]);
    }

    private IEnumerator AfterAdvance(int version)
    {
        while (suspended || Input.GetMouseButton(0) || Input.touchCount > 0) yield return null;
        yield return null; // The release frame belongs to the dialogue, including raw-input fallbacks.
        if (version != generation) yield break;
        if (!actionStep) { nextAction?.Invoke(); yield break; }
        dialogue.HideDialogue();
        IsExplaining = false;
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, false);
        objective.text = "Follow the highlighted target. You can choose LATER at any time.";
        ShowActionFocus();
    }

    public void SetTarget(RectTransform value, RectTransform inputHint = null)
    {
        if (waitingForUI && target == value && hint == (inputHint != null ? inputHint : value)) return;
        worldTarget = null;
        worldIndicator.Hide();
        target = value;
        hint = inputHint != null ? inputHint : value;
        waitingForUI = true;
        dialogue.SetWorldFocusTarget(null);
        dialogue.SetFocusTarget(value);
        if (!IsExplaining) PrepareFocus();
    }

    private void PrepareFocus()
    {
        hand.HideHint();
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            scroller.Cancel();
            preparing = false;
            mask.Hide();
            Later?.Invoke();
            return;
        }
        bool appearing = !mask.IsVisible || mask.CurrentTarget == null ||
            !mask.CurrentTarget.gameObject.activeInHierarchy;
        mask.Hold();
        scroller.Cancel();
        preparing = true;
        int version = generation;
        RectTransform requested = target;
        scroller.Prepare(hint != null ? hint : target, () =>
        {
            if (version != generation || requested != target || suspended) return;
            if (appearing) maskGroup.alpha = LevelOneUIAccessibility.ReducedMotion ? 1f : .001f;
            mask.TransitionTo(target, !IsExplaining, () =>
            {
                if (version != generation || requested != target || suspended) return;
                preparing = false;
                mask.SetDialogueInput(false);
                if (!IsExplaining && hint != null) hand.ShowTapHint(hint);
            });
        });
    }

    public void SetWorldTarget(Transform value)
    {
        if (worldTarget == value) return;
        target = hint = null;
        waitingForUI = preparing = false;
        scroller.Cancel();
        worldTarget = value;
        dialogue.SetFocusTarget(null);
        dialogue.SetWorldFocusTarget(value);
        if (!IsExplaining) ShowActionFocus();
    }

    private void ShowActionFocus()
    {
        if (waitingForUI)
        {
            PrepareFocus();
            return;
        }
        mask.Hide(); // Walking/camera controls remain usable on the way to the real computer.
        worldIndicator.Show(worldTarget);
        if (worldTarget != null) hand.ShowTapHint(worldTarget);
        objective.text = "Go to the highlighted computer and interact with it.";
    }

    public void SetSuspended(bool value)
    {
        if (suspended == value) return;
        suspended = value;
        // Exit controls remain available even when another modal pauses the guide.
        group.alpha = 1f;
        group.blocksRaycasts = true;
        // Keep the dialogue component enabled: OnDisable clears its pending Next callback.
        // CanAdvanceAt rejects dialogue input while suspended; Skip remains interactive.
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, false);
        if (value) { scroller.Cancel(); mask.Hide(); hand.HideHint(); worldIndicator.Hide(); }
        else if (IsExplaining) mask.Hide();
        else if (waitingForUI) PrepareFocus();
        else ShowActionFocus();
    }

    public void Hide()
    {
        generation++;
        dismissedFrame = Time.frameCount;
        StopAllCoroutines();
        scroller.Cancel();
        mask.Hide();
        hand.HideHint();
        worldIndicator.Hide();
        dialogue.HideDialogue();
        IsExplaining = false;
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, false);
        gameObject.SetActive(false);
    }

    public void BeginOptionalReview(Action onComplete)
    {
        dialogue.HideDialogue();
        IsExplaining = false;
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, false);
        reviewComplete = onComplete;
        reviewButton.gameObject.SetActive(true);
        ShowActionFocus();
    }

    private void OnDestroy() => GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, false);

    private void OnDisable()
    {
        generation++;
        StopAllCoroutines();
        scroller?.Cancel();
        mask?.Hide();
        hand?.HideHint();
        worldIndicator?.Hide();
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, false);
    }

    private void LateUpdate()
    {
        Layout();
        if (suspended) return;
        maskGroup.alpha = LevelOneUIAccessibility.ReducedMotion || maskEntranceSeconds <= 0f
            ? 1f : Mathf.MoveTowards(maskGroup.alpha, 1f, Time.unscaledDeltaTime / maskEntranceSeconds);
        if (waitingForUI && !IsExplaining && !preparing)
        {
            // Rebuilt/hidden controls must never leave a clickable stale hole.
            bool missing = target == null || !target.gameObject.activeInHierarchy || !mask.IsVisible;
            if (missing) { mask.Hide(); hand.HideHint(); }
            else if (!hand.IsVisible && hint != null) hand.ShowTapHint(hint);
        }
    }

    private void Layout()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        // Preserve the tutorial artwork's proportions, with a safe-area fit on narrow screens.
        float scale = Mathf.Min(1f, (safe.width / canvas.scaleFactor - safePadding * 2f) / 1050f);
        scale = Mathf.Min(scale, (safe.height / canvas.scaleFactor - 140f) / 380f);
        scale = Mathf.Max(.25f, scale);
        dialogueRoot.localScale = dialogueScale * scale;
        // Match the tutorial's authored bottom dialogue. Its portrait already switches sides around targets.
        dialogueRoot.anchoredPosition = dialogueHome * scale;
        choicesRoot.anchoredPosition = new Vector2(0f, 390f * scale + safePadding);
        float buttonScale = Mathf.Min(1f, (safe.width / canvas.scaleFactor - safePadding * 2f) /
            (guideButtonSize.x * 2f + guideButtonGap));
        controlsRoot.localScale = choicesRoot.localScale = Vector3.one * Mathf.Max(.25f, buttonScale);
    }

    private void ConfigureButtons()
    {
        controlsRoot.sizeDelta = new Vector2(guideButtonSize.x * 2f + guideButtonGap, guideButtonSize.y * 2f + guideButtonGap);
        controlsRoot.anchoredPosition = new Vector2(-safePadding, -safePadding);
        PositionButton(laterButton, Vector2.zero, "LATER");
        PositionButton(skipButton, new Vector2(guideButtonSize.x + guideButtonGap, 0f), "SKIP TUTORIAL");
        PositionButton(reviewButton, new Vector2(guideButtonSize.x + guideButtonGap, -guideButtonSize.y - guideButtonGap), "CONTINUE");
        choicesRoot.sizeDelta = new Vector2(guideButtonSize.x * 2f + guideButtonGap, guideButtonSize.y);
        PositionButton(yesButton, Vector2.zero, "YES, SHOW ME");
        PositionButton(noButton, new Vector2(guideButtonSize.x + guideButtonGap, 0f), "NO THANKS");
    }

    private void PositionButton(UnityEngine.UI.Button button, Vector2 position, string text)
    {
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = guideButtonSize;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        label.text = text;
        label.fontSize = 30f;
    }
}
