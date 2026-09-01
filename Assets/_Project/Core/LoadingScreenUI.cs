using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class LoadingScreenUI : MonoBehaviour
{
    [Header("Text UI")]
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private TMP_Text tipsText;
    [SerializeField] private CanvasGroup tipsCanvasGroup;
    [SerializeField] private RectTransform tipsSafeAreaRoot;

    [Header("Loading Text Dots")]
    [SerializeField] private string baseLoadingText = "Loading";
    [SerializeField] private int textDotCount = 3;
    [SerializeField] private float textDotBounceSpeed = 6f;
    [SerializeField] private float textDotBounceHeight = 8f;
    [SerializeField] private float textDotPhaseOffset = 0.25f;

    [Header("Tips")]
    [TextArea(2, 4)]
    [SerializeField] private List<string> tips = new List<string>
    {
        "TIP — Seat waiting groups quickly before their patience falls.",
        "GUIDE — Only one employee per role can be active during a shift.",
        "MANAGEMENT NOTE — Payroll is deducted automatically at the end of the day.",
        "DID YOU KNOW? — Messy diners may leave both trays and spills behind.",
        "LORE — Alien diners evaluate Earth restaurants through service and satisfaction.",
        "TIP — Serve the correct order to avoid angry customers.",
        "GUIDE — Clean used tables quickly so the receptionist can seat another group."
    };
    [Tooltip("A second tip is shown only when a loading screen remains visible this long.")]
    [SerializeField, Min(3f)] private float tipRefreshSeconds = 5f;
    [SerializeField, Min(0f)] private float tipFadeSeconds = 0.22f;
    [SerializeField] private bool rotateTipsDuringLongLoads = true;
    [SerializeField] private bool respectDeviceSafeArea = true;
    [Tooltip("Extra inset inside the device safe area: X left, Y right, Z top, W bottom.")]
    [SerializeField] private Vector4 safeAreaPadding = new Vector4(18f, 18f, 0f, 12f);

    [Header("Table Approach")]
    [SerializeField] private RectTransform table;
    [SerializeField] private RectTransform tableHitTarget;
    [SerializeField] private float cycleDuration = 2.4f;
    [SerializeField] private float arriveBeforeReset = 0.8f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector2 tableStartPos;
    private float tableTimer;
    private float nextTipTime;
    private int lastTipIndex = -1;
    private Coroutine tipFadeRoutine;
    private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
    private Vector2Int lastScreenSize = new Vector2Int(-1, -1);

    private void Awake()
    {
        if (table == null)
            table = GetComponent<RectTransform>();

        if (table != null)
            tableStartPos = table.anchoredPosition;
    }

    private void OnEnable()
    {
        tableTimer = 0f;
        nextTipTime = 0f;

        if (table != null)
            table.anchoredPosition = tableStartPos;

        RefreshSafeArea(true);
        RefreshTip(true);
        UpdateLoadingText();
    }

    private void OnDisable()
    {
        if (tipFadeRoutine != null)
        {
            StopCoroutine(tipFadeRoutine);
            tipFadeRoutine = null;
        }
    }

    private void Update()
    {
        UpdateLoadingText();
        UpdateTableMotion();
        RefreshSafeArea(false);

        if (rotateTipsDuringLongLoads && Time.unscaledTime >= nextTipTime)
            RefreshTip(false);
    }

    private void UpdateLoadingText()
    {
        if (loadingText == null) return;

        float t = Time.unscaledTime * textDotBounceSpeed;
        StringBuilder sb = new StringBuilder(baseLoadingText);

        for (int i = 0; i < textDotCount; i++)
        {
            float offset = Mathf.Sin(t - (i * textDotPhaseOffset)) * textDotBounceHeight;
            sb.Append("<voffset=");
            sb.Append(offset.ToString("0.0"));
            sb.Append("px>.</voffset>");
        }

        loadingText.text = sb.ToString();
    }

    private void UpdateTableMotion()
    {
        if (table == null || tableHitTarget == null) return;

        float dt = LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
        tableTimer += dt;

        float safeCycle = Mathf.Max(0.1f, cycleDuration);
        float moveDuration = Mathf.Max(0.05f, safeCycle - arriveBeforeReset);
        float loopTime = tableTimer % safeCycle;

        if (loopTime >= moveDuration)
        {
            table.anchoredPosition = tableHitTarget.anchoredPosition;
            return;
        }

        float t = Mathf.Clamp01(loopTime / moveDuration);
        float eased = moveCurve.Evaluate(t);
        table.anchoredPosition = Vector2.LerpUnclamped(tableStartPos, tableHitTarget.anchoredPosition, eased);
    }

    private void RefreshTip(bool immediate)
    {
        nextTipTime = Time.unscaledTime + Mathf.Max(0.1f, tipRefreshSeconds);

        if (tipsText == null || tips == null || tips.Count == 0)
            return;

        int index = 0;

        if (tips.Count > 1)
        {
            do
            {
                index = Random.Range(0, tips.Count);
            }
            while (index == lastTipIndex);
        }

        lastTipIndex = index;
        string message = tips[index];

        if (immediate || tipFadeSeconds <= 0f || LevelOneUIAccessibility.ReducedMotion)
        {
            if (tipFadeRoutine != null)
            {
                StopCoroutine(tipFadeRoutine);
                tipFadeRoutine = null;
            }
            tipsText.text = message;
            if (tipsCanvasGroup != null)
                tipsCanvasGroup.alpha = 1f;
            return;
        }

        if (tipFadeRoutine != null)
            StopCoroutine(tipFadeRoutine);
        tipFadeRoutine = StartCoroutine(CrossFadeTip(message));
    }

    private IEnumerator CrossFadeTip(string message)
    {
        if (tipsCanvasGroup == null && tipsText != null)
            tipsCanvasGroup = tipsText.GetComponent<CanvasGroup>();

        if (tipsCanvasGroup == null)
        {
            tipsText.text = message;
            tipFadeRoutine = null;
            yield break;
        }

        float halfDuration = Mathf.Max(0.01f, tipFadeSeconds * 0.5f);
        for (float elapsed = 0f; elapsed < halfDuration; elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
        {
            tipsCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / halfDuration);
            yield return null;
        }

        tipsCanvasGroup.alpha = 0f;
        tipsText.text = message;
        for (float elapsed = 0f; elapsed < halfDuration; elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
        {
            tipsCanvasGroup.alpha = Mathf.Clamp01(elapsed / halfDuration);
            yield return null;
        }

        tipsCanvasGroup.alpha = 1f;
        tipFadeRoutine = null;
    }

    private void RefreshSafeArea(bool force)
    {
        if (tipsSafeAreaRoot == null)
            return;

        Rect safe = respectDeviceSafeArea
            ? Screen.safeArea
            : new Rect(0f, 0f, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        if (!force && safe == lastSafeArea && size == lastScreenSize)
            return;

        lastSafeArea = safe;
        lastScreenSize = size;
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);
        tipsSafeAreaRoot.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
        tipsSafeAreaRoot.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
        tipsSafeAreaRoot.offsetMin = new Vector2(safeAreaPadding.x, safeAreaPadding.w);
        tipsSafeAreaRoot.offsetMax = new Vector2(-safeAreaPadding.y, -safeAreaPadding.z);
    }

#if UNITY_EDITOR
    public void ConfigureTipsForEditor(
        TMP_Text configuredTipsText,
        CanvasGroup configuredTipsGroup,
        RectTransform configuredSafeAreaRoot)
    {
        tipsText = configuredTipsText;
        tipsCanvasGroup = configuredTipsGroup;
        tipsSafeAreaRoot = configuredSafeAreaRoot;
    }
#endif
}
