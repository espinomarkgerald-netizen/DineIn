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
    private TutorialUIFocusMask mask;
    private TutorialUIAutoScroller scroller;
    private Canvas canvas;
    private CanvasGroup group;
    private CanvasGroup maskGroup;
    private Vector2 dialogueHome;
    private Vector3 dialogueScale;
    private UnityEngine.UI.Button reviewButton;
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
        dialogueHome = dialogueRoot.anchoredPosition;
        dialogueScale = dialogueRoot.localScale;
        scroller = gameObject.AddComponent<TutorialUIAutoScroller>();
        laterButton.onClick.AddListener(() => Later?.Invoke());
        skipButton.onClick.AddListener(() => Skip?.Invoke());
        // Reuse the escape-button styling for an optional equipment review, without adding HUD prose.
        reviewButton = Instantiate(skipButton, controlsRoot);
        reviewButton.name = "Continue Review";
        reviewButton.onClick.RemoveAllListeners();
        reviewButton.onClick.AddListener(() => reviewComplete?.Invoke());
        reviewButton.GetComponentInChildren<TMP_Text>().text = "CONTINUE";
        ((RectTransform)reviewButton.transform).anchoredPosition += Vector2.down * 72f;
        reviewButton.gameObject.SetActive(false);
        caption.gameObject.SetActive(false);
        objective.gameObject.SetActive(false);
        dialogue.CanAdvanceAt = position => !OverControls(position) && !suspended;
    }

    public bool ConsumesPointer(Vector2 position) => dismissedFrame == Time.frameCount ||
        (isActiveAndEnabled && !suspended && (IsExplaining || preparing || OverControls(position) ||
         (waitingForUI && (target == null || !target.gameObject.activeInHierarchy)) ||
         (mask.isActiveAndEnabled && mask.Raycast(position, null))));

    private bool OverControls(Vector2 position) =>
        RectTransformUtility.RectangleContainsScreenPoint(controlsRoot, position, null) ||
        (reviewButton != null && reviewButton.gameObject.activeInHierarchy &&
         RectTransformUtility.RectangleContainsScreenPoint((RectTransform)reviewButton.transform, position, null));

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
        mask.SetDialogueInput(true);
        dialogue.HideDialogue();
        hand.HideHint();
        worldIndicator.Hide();
        dialogue.SetFocusTarget(null);
        dialogue.SetWorldFocusTarget(null);
        caption.text = title;
        objective.text = string.Empty;
        reviewButton.gameObject.SetActive(false);
        reviewComplete = null;
        actionStep = !isModal;
        nextAction = onAction;
        IsExplaining = true;
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, true);
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
        bool appearing = !mask.IsVisible || mask.CurrentTarget == null ||
            !mask.CurrentTarget.gameObject.activeInHierarchy;
        mask.Hold();
        mask.SetDialogueInput(true);
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
                mask.SetDialogueInput(IsExplaining || target == null);
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
        group.alpha = value ? 0f : 1f;
        group.blocksRaycasts = !value;
        // Keep the dialogue component enabled: OnDisable clears its pending Next callback.
        // CanvasGroup hides it, and CanAdvanceAt rejects input while suspended.
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, !value && IsExplaining);
        if (value) { scroller.Cancel(); mask.Hide(); hand.HideHint(); worldIndicator.Hide(); }
        else if (IsExplaining) mask.SetDialogueInput(true);
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

    private void LateUpdate()
    {
        if (suspended) return;
        maskGroup.alpha = LevelOneUIAccessibility.ReducedMotion || maskEntranceSeconds <= 0f
            ? 1f : Mathf.MoveTowards(maskGroup.alpha, 1f, Time.unscaledDeltaTime / maskEntranceSeconds);
        Layout();
        if (waitingForUI && !IsExplaining && !preparing)
        {
            // Rebuilt/hidden controls must never leave a clickable stale hole.
            bool missing = target == null || !target.gameObject.activeInHierarchy || !mask.IsVisible;
            mask.SetDialogueInput(missing);
            if (missing) hand.HideHint();
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
    }
}
