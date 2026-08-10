using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class LoadingScreenUI : MonoBehaviour
{
    [Header("Text UI")]
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private TMP_Text tipsText;

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
        "Seat customers quickly to keep them happy.",
        "VIP customers lose patience faster, but they can give tips.",
        "Messy customers may leave a dirty table behind.",
        "Serve the correct order to avoid angry customers.",
        "Clean tables fast so new customers can sit down."
    };
    [SerializeField] private float tipRefreshSeconds = 2f;

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

        RefreshTip();
        UpdateLoadingText();
    }

    private void Update()
    {
        UpdateLoadingText();
        UpdateTableMotion();

        if (Time.unscaledTime >= nextTipTime)
            RefreshTip();
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

        float dt = Time.unscaledDeltaTime;
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

    private void RefreshTip()
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
        tipsText.text = tips[index];
    }
}