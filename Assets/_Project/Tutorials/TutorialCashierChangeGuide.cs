using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialCashierChangeGuide : MonoBehaviour
{
    [System.Serializable]
    private class MoneyRef
    {
        public int value;
        public Button button;
        public Graphic graphic;
        public RectTransform rect;
    }

    private enum GuideStage
    {
        None,
        DemoWrongInput,
        WaitingForDemoUndo,
        FollowPlan,
        WaitingForMistakeUndo,
        WaitConfirm
    }

    [Header("Mode")]
    [SerializeField] private bool onlyRunOnDay3Cashier = true;
    [SerializeField] private bool guideEveryRound = true;
    [SerializeField] private bool teachUndoOnFirstRound = false;

    [Header("Register References")]
    [SerializeField] private CashierRegisterUI registerUI;
    [SerializeField] private TMP_Text expectedChangeText;
    [SerializeField] private TMP_Text currentInputText;

    [Header("Guide UI")]
    [SerializeField] private GameObject guideRoot;
    [SerializeField] private TMP_Text guideText;

    [Header("Money Buttons")]
    [SerializeField] private Button bill1000Button;
    [SerializeField] private Button bill500Button;
    [SerializeField] private Button bill200Button;
    [SerializeField] private Button bill100Button;
    [SerializeField] private Button bill50Button;
    [SerializeField] private Button coin20Button;
    [SerializeField] private Button coin10Button;
    [SerializeField] private Button coin5Button;
    [SerializeField] private Button coin1Button;

    [Header("Other Buttons")]
    [SerializeField] private Button undoButton;
    [SerializeField] private Button confirmButton;

    [Header("Optional Manual Highlight Override")]
    [SerializeField] private Graphic undoHighlightGraphic;
    [SerializeField] private RectTransform undoPulseTarget;
    [SerializeField] private Graphic confirmHighlightGraphic;
    [SerializeField] private RectTransform confirmPulseTarget;

    [Header("Undo Demo")]
    [SerializeField] private int demoWrongValue = 1;

    [Header("Highlight")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.95f, 0.45f, 1f);
    [SerializeField] [Range(0f, 1f)] private float tintStrength = 0.35f;
    [SerializeField] [Range(0.01f, 0.25f)] private float pulseScale = 0.08f;
    [SerializeField] private float pulseSpeed = 7f;

    private readonly List<MoneyRef> moneyRefs = new List<MoneyRef>();
    private readonly Dictionary<int, MoneyRef> moneyByValue = new Dictionary<int, MoneyRef>();
    private readonly Dictionary<Graphic, Color> baseGraphicColors = new Dictionary<Graphic, Color>();
    private readonly Dictionary<RectTransform, Vector3> baseScaleCache = new Dictionary<RectTransform, Vector3>();
    private readonly List<int> currentPlan = new List<int>();

    private GuideStage stage = GuideStage.None;

    private Graphic currentGraphic;
    private RectTransform currentPulseTarget;

    private bool undoTaught;
    private bool roundGuideActive;
    private bool wasOpen;

    private int planIndex;
    private int lastObservedInput;

    private void Awake()
    {
        ResolveReferences();
        BuildMoneyRefs();
        CacheVisuals();
        ClearHighlights();
        SetGuideText(string.Empty, false);
    }

    private void Update()
    {
        ResolveReferences();

        bool canRun = CanRunGuide();
        bool isOpen = registerUI != null && registerUI.IsOpen;

        if (!canRun)
        {
            if (wasOpen)
            {
                EndRound();
                wasOpen = false;
            }
            return;
        }

        if (isOpen && !wasOpen)
            BeginRound();

        if (!isOpen && wasOpen)
        {
            EndRound();
        }

        wasOpen = isOpen;

        if (!isOpen)
            return;

        int currentInput = ParseMoneyText(currentInputText != null ? currentInputText.text : "0");
        ProcessInputState(currentInput);
        AnimateCurrentHighlight();
    }

    private bool CanRunGuide()
    {
        if (!onlyRunOnDay3Cashier)
            return true;

        return TutorialManager.Instance != null &&
               TutorialManager.Instance.TutorialStarted &&
               TutorialManager.Instance.CurrentDay == TutorialManager.TutorialDay.Day3Cashier;
    }

    private void ResolveReferences()
    {
        if (registerUI == null)
            registerUI = FindFirstObjectByType<CashierRegisterUI>(FindObjectsInactive.Include);

        if (undoHighlightGraphic == null && undoButton != null)
            undoHighlightGraphic = GetButtonGraphic(undoButton);

        if (undoPulseTarget == null && undoButton != null)
            undoPulseTarget = undoButton.transform as RectTransform;

        if (confirmHighlightGraphic == null && confirmButton != null)
            confirmHighlightGraphic = GetButtonGraphic(confirmButton);

        if (confirmPulseTarget == null && confirmButton != null)
            confirmPulseTarget = confirmButton.transform as RectTransform;
    }

    private void BuildMoneyRefs()
    {
        moneyRefs.Clear();
        moneyByValue.Clear();

        AddMoneyRef(1000, bill1000Button);
        AddMoneyRef(500, bill500Button);
        AddMoneyRef(200, bill200Button);
        AddMoneyRef(100, bill100Button);
        AddMoneyRef(50, bill50Button);
        AddMoneyRef(20, coin20Button);
        AddMoneyRef(10, coin10Button);
        AddMoneyRef(5, coin5Button);
        AddMoneyRef(1, coin1Button);
    }

    private void AddMoneyRef(int value, Button button)
    {
        if (button == null)
            return;

        MoneyRef moneyRef = new MoneyRef
        {
            value = value,
            button = button,
            graphic = GetButtonGraphic(button),
            rect = button.transform as RectTransform
        };

        moneyRefs.Add(moneyRef);

        if (!moneyByValue.ContainsKey(value))
            moneyByValue.Add(value, moneyRef);
    }

    private Graphic GetButtonGraphic(Button button)
    {
        if (button == null)
            return null;

        if (button.targetGraphic != null)
            return button.targetGraphic;

        Graphic graphic = button.GetComponent<Graphic>();
        if (graphic != null)
            return graphic;

        return button.GetComponentInChildren<Graphic>(true);
    }

    private void CacheVisuals()
    {
        for (int i = 0; i < moneyRefs.Count; i++)
        {
            CacheGraphic(moneyRefs[i].graphic);
            CachePulseTarget(moneyRefs[i].rect);
        }

        CacheGraphic(undoHighlightGraphic);
        CachePulseTarget(undoPulseTarget);

        CacheGraphic(confirmHighlightGraphic);
        CachePulseTarget(confirmPulseTarget);
    }

    private void CacheGraphic(Graphic graphic)
    {
        if (graphic == null || baseGraphicColors.ContainsKey(graphic))
            return;

        baseGraphicColors.Add(graphic, graphic.color);
    }

    private void CachePulseTarget(RectTransform target)
    {
        if (target == null || baseScaleCache.ContainsKey(target))
            return;

        baseScaleCache.Add(target, target.localScale);
    }

    private void BeginRound()
    {
        stage = GuideStage.None;
        planIndex = 0;
        currentPlan.Clear();
        ClearHighlights();

        lastObservedInput = ParseMoneyText(currentInputText != null ? currentInputText.text : "0");
        roundGuideActive = guideEveryRound || (!undoTaught && teachUndoOnFirstRound);

        if (!roundGuideActive)
        {
            SetGuideText(string.Empty, false);
            return;
        }

        if (teachUndoOnFirstRound && !undoTaught)
        {
            stage = GuideStage.DemoWrongInput;
            HighlightMoneyValue(GetDemoWrongValue());
            SetGuideText("Tap the highlighted amount once. Then press Undo.", true);
            return;
        }

        StartPlanGuide();
    }

    private void EndRound()
    {
        stage = GuideStage.None;
        planIndex = 0;
        currentPlan.Clear();
        roundGuideActive = false;
        lastObservedInput = 0;

        ClearHighlights();
        SetGuideText(string.Empty, false);
    }

    private void StartPlanGuide()
    {
        BuildPlanFromExpectedChange();

        if (currentPlan.Count == 0)
        {
            stage = GuideStage.WaitConfirm;
            HighlightConfirm();
            SetGuideText("No change is needed. Press Confirm.", true);
            return;
        }

        stage = GuideStage.FollowPlan;
        planIndex = 0;
        HighlightCurrentPlannedButton();
    }

    private void BuildPlanFromExpectedChange()
    {
        currentPlan.Clear();

        int remaining = ParseMoneyText(expectedChangeText != null ? expectedChangeText.text : "0");
        if (remaining <= 0)
            return;

        List<int> values = new List<int>();
        for (int i = 0; i < moneyRefs.Count; i++)
            values.Add(moneyRefs[i].value);

        values.Sort((a, b) => b.CompareTo(a));

        for (int i = 0; i < values.Count; i++)
        {
            int value = values[i];

            while (remaining >= value)
            {
                currentPlan.Add(value);
                remaining -= value;
            }
        }

        currentPlan.Reverse();
    }

    private void ProcessInputState(int currentInput)
    {
        if (!roundGuideActive)
        {
            lastObservedInput = currentInput;
            return;
        }

        switch (stage)
        {
            case GuideStage.DemoWrongInput:
                ProcessDemoWrongInput(currentInput);
                break;

            case GuideStage.WaitingForDemoUndo:
                ProcessDemoUndo(currentInput);
                break;

            case GuideStage.FollowPlan:
                ProcessPlannedInput(currentInput);
                break;

            case GuideStage.WaitingForMistakeUndo:
                ProcessMistakeUndo(currentInput);
                break;

            case GuideStage.WaitConfirm:
                ProcessConfirmState(currentInput);
                break;
        }

        lastObservedInput = currentInput;
    }

    private void ProcessDemoWrongInput(int currentInput)
    {
        if (currentInput == lastObservedInput)
            return;

        if (currentInput > 0)
        {
            stage = GuideStage.WaitingForDemoUndo;
            HighlightUndo();
            SetGuideText("Good. Now press Undo to clear the input.", true);
        }
    }

    private void ProcessDemoUndo(int currentInput)
    {
        if (currentInput == 0 && lastObservedInput != 0)
        {
            undoTaught = true;
            StartPlanGuide();
        }
    }

    private void ProcessPlannedInput(int currentInput)
    {
        if (currentInput == lastObservedInput)
            return;

        if (currentInput == 0)
        {
            planIndex = 0;
            HighlightCurrentPlannedButton();
            return;
        }

        if (currentInput < lastObservedInput)
        {
            planIndex = 0;
            HighlightCurrentPlannedButton();
            return;
        }

        if (planIndex < 0 || planIndex >= currentPlan.Count)
        {
            stage = GuideStage.WaitConfirm;
            HighlightConfirm();
            SetGuideText("Good. The change is complete. Press Confirm.", true);
            return;
        }

        int delta = currentInput - lastObservedInput;
        int expectedValue = currentPlan[planIndex];

        if (delta != expectedValue)
        {
            stage = GuideStage.WaitingForMistakeUndo;
            HighlightUndo();
            SetGuideText("Wrong amount. Press Undo, then follow the highlight again.", true);
            return;
        }

        planIndex++;
        HighlightCurrentPlannedButton();
    }

    private void ProcessMistakeUndo(int currentInput)
    {
        if (currentInput == 0 && lastObservedInput != 0)
            StartPlanGuide();
    }

    private void ProcessConfirmState(int currentInput)
    {
        if (currentInput != ParseMoneyText(expectedChangeText != null ? expectedChangeText.text : "0"))
        {
            planIndex = 0;
            StartPlanGuide();
        }
    }

    private void HighlightCurrentPlannedButton()
    {
        if (planIndex < 0 || planIndex >= currentPlan.Count)
        {
            stage = GuideStage.WaitConfirm;
            HighlightConfirm();
            SetGuideText("Good. The change is complete. Press Confirm.", true);
            return;
        }

        int nextValue = currentPlan[planIndex];
        HighlightMoneyValue(nextValue);
        SetGuideText("Tap ₱" + nextValue + ".", true);
    }

    private void HighlightMoneyValue(int value)
    {
        ClearHighlights();

        MoneyRef moneyRef;
        if (!moneyByValue.TryGetValue(value, out moneyRef) || moneyRef == null)
            return;

        currentGraphic = moneyRef.graphic;
        currentPulseTarget = moneyRef.rect;

        EnableGraphicHighlight(currentGraphic, true);
        ResetPulseTarget(currentPulseTarget);
    }

    private void HighlightUndo()
    {
        ClearHighlights();

        currentGraphic = undoHighlightGraphic;
        currentPulseTarget = undoPulseTarget;

        EnableGraphicHighlight(currentGraphic, true);
        ResetPulseTarget(currentPulseTarget);
    }

    private void HighlightConfirm()
    {
        ClearHighlights();

        currentGraphic = confirmHighlightGraphic;
        currentPulseTarget = confirmPulseTarget;

        EnableGraphicHighlight(currentGraphic, true);
        ResetPulseTarget(currentPulseTarget);
    }

    private int GetDemoWrongValue()
    {
        if (moneyByValue.ContainsKey(demoWrongValue))
            return demoWrongValue;

        int smallest = int.MaxValue;

        for (int i = 0; i < moneyRefs.Count; i++)
        {
            if (moneyRefs[i].value < smallest)
                smallest = moneyRefs[i].value;
        }

        return smallest == int.MaxValue ? 1 : smallest;
    }

    private void ClearHighlights()
    {
        foreach (var pair in baseGraphicColors)
        {
            if (pair.Key != null)
                pair.Key.color = pair.Value;
        }

        foreach (var pair in baseScaleCache)
        {
            if (pair.Key != null)
                pair.Key.localScale = pair.Value;
        }

        currentGraphic = null;
        currentPulseTarget = null;
    }

    private void EnableGraphicHighlight(Graphic graphic, bool enabled)
    {
        if (graphic == null)
            return;

        Color baseColor;
        if (!baseGraphicColors.TryGetValue(graphic, out baseColor))
            return;

        graphic.color = enabled
            ? Color.Lerp(baseColor, highlightColor, tintStrength)
            : baseColor;
    }

    private void ResetPulseTarget(RectTransform target)
    {
        if (target == null)
            return;

        Vector3 baseScale;
        if (!baseScaleCache.TryGetValue(target, out baseScale))
            return;

        target.localScale = baseScale;
    }

    private void AnimateCurrentHighlight()
    {
        if (currentPulseTarget == null)
            return;

        Vector3 baseScale;
        if (!baseScaleCache.TryGetValue(currentPulseTarget, out baseScale))
            return;

        float wave = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseScale;
        currentPulseTarget.localScale = baseScale * wave;
    }

    private void SetGuideText(string message, bool visible)
    {
        if (guideRoot != null)
            guideRoot.SetActive(visible && !string.IsNullOrWhiteSpace(message));

        if (guideText != null)
            guideText.text = visible ? message : string.Empty;
    }

    private int ParseMoneyText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        string cleaned = text.Replace("₱", "").Replace(",", "").Trim();

        float parsedFloat;
        if (float.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedFloat))
            return Mathf.RoundToInt(parsedFloat);

        string digitsOnly = string.Empty;
        for (int i = 0; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            if (char.IsDigit(c))
                digitsOnly += c;
        }

        int parsedInt;
        return int.TryParse(digitsOnly, out parsedInt) ? parsedInt : 0;
    }
}