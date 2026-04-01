using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Explains the Total, Received, and Change fields on the register UI
/// before the player is guided to press money buttons.
///
/// Place this on TutorialCashierRuntime.
/// Call BeginExplanation() from TutorialCashierLessonController after the
/// register opens. It fires OnExplanationComplete when done so the lesson
/// controller can start button guidance.
/// </summary>
public class TutorialCashierFieldExplainer : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Lesson Message UI")]
    [Tooltip("The panel that shows lesson text. Same root used by LessonController.")]
    [SerializeField] private GameObject lessonMessageRoot;
    [SerializeField] private TMP_Text lessonMessageText;

    [Header("Register Field Highlights")]
    [Tooltip("Graphic on or around the Total field — will pulse yellow while explaining it.")]
    [SerializeField] private Graphic totalFieldHighlight;

    [Tooltip("Graphic on or around the Received field.")]
    [SerializeField] private Graphic receivedFieldHighlight;

    [Tooltip("Graphic on or around the Change field.")]
    [SerializeField] private Graphic changeFieldHighlight;

    [Header("Timing")]
    [Tooltip("Seconds each explanation step is shown before auto-advancing.")]
    [SerializeField] private float stepDuration = 2.8f;

    [Tooltip("If true the player must tap anywhere (on a Next button) to advance. " +
             "If false steps auto-advance after stepDuration.")]
    [SerializeField] private bool requireTapToAdvance = false;

    [Header("Optional Next Button")]
    [Tooltip("Only used when requireTapToAdvance is true.")]
    [SerializeField] private Button nextButton;
    [SerializeField] private GameObject nextButtonRoot;

    [Header("Highlight Style")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private float pulseScale  = 0.08f;
    [SerializeField] private float pulseSpeed  = 6f;

    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    /// <summary>Fired when all explanation steps are done.</summary>
    public System.Action OnExplanationComplete;

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    private bool explaining;
    private bool stepAdvanceRequested;
    private Graphic activePulseGraphic;
    private Vector3 activePulseBase;
    private bool hasPulseBase;

    // shadow components added at runtime so we don't need to pre-attach them
    private Shadow totalShadow;
    private Shadow receivedShadow;
    private Shadow changeShadow;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        totalShadow    = EnsureShadow(totalFieldHighlight);
        receivedShadow = EnsureShadow(receivedFieldHighlight);
        changeShadow   = EnsureShadow(changeFieldHighlight);

        DisableAllHighlights();

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextButtonPressed);

        ShowNextButton(false);
    }

    private void Update()
    {
        if (!explaining || activePulseGraphic == null || !hasPulseBase)
            return;

        float wave  = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseScale;
        activePulseGraphic.transform.localScale = activePulseBase * wave;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Start the field explanation sequence.
    /// Safe to call even if already running — it restarts cleanly.
    /// </summary>
    public void BeginExplanation()
    {
        if (explaining)
            StopAllCoroutines();

        explaining = true;
        StartCoroutine(ExplanationRoutine());
    }

    // -------------------------------------------------------------------------
    // Coroutine
    // -------------------------------------------------------------------------

    private IEnumerator ExplanationRoutine()
    {
        // Step 1 – Received
        yield return ShowStep(
            receivedFieldHighlight,
            receivedShadow,
            "This is the amount the customer gave you.",
            stepDuration);

        // Step 2 – Total
        yield return ShowStep(
            totalFieldHighlight,
            totalShadow,
            "This is the total bill the customer has to pay.",
            stepDuration);

        // Step 3 – Change
        yield return ShowStep(
            changeFieldHighlight,
            changeShadow,
            "This is the exact change you must give back.\n" +
            "Use the bill and coin buttons below to build that amount.",
            stepDuration);

        // Step 4 – Button guidance reminder
        yield return ShowStep(
            null,
            null,
            "Use Undo if you press the wrong button.\n" +
            "Press Confirm only when the entered change matches.",
            stepDuration);

        // Done
        DisableAllHighlights();
        ResetActivePulse();
        HideLessonMessage();
        ShowNextButton(false);

        explaining = false;
        OnExplanationComplete?.Invoke();
    }

    private IEnumerator ShowStep(Graphic highlight, Shadow shadow, string message, float duration)
    {
        // Swap highlight
        DisableAllHighlights();
        ResetActivePulse();

        if (highlight != null && shadow != null)
        {
            shadow.effectColor    = highlightColor;
            shadow.effectDistance = new Vector2(10f, 10f);
            shadow.useGraphicAlpha = false;
            shadow.enabled        = true;

            activePulseGraphic = highlight;
            activePulseBase    = highlight.transform.localScale;
            hasPulseBase       = true;
        }
        else
        {
            activePulseGraphic = null;
            hasPulseBase = false;
        }

        ShowLessonMessage(message);

        if (requireTapToAdvance)
        {
            ShowNextButton(true);
            stepAdvanceRequested = false;

            while (!stepAdvanceRequested)
                yield return null;

            ShowNextButton(false);
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }
    }

    // -------------------------------------------------------------------------
    // Button Callback
    // -------------------------------------------------------------------------

    private void OnNextButtonPressed()
    {
        stepAdvanceRequested = true;
    }

    // -------------------------------------------------------------------------
    // Highlight Helpers
    // -------------------------------------------------------------------------

    private Shadow EnsureShadow(Graphic graphic)
    {
        if (graphic == null) return null;

        Shadow s = graphic.GetComponent<Shadow>();
        if (s == null) s = graphic.gameObject.AddComponent<Shadow>();

        s.enabled = false;
        return s;
    }

    private void DisableAllHighlights()
    {
        if (totalShadow    != null) totalShadow.enabled    = false;
        if (receivedShadow != null) receivedShadow.enabled = false;
        if (changeShadow   != null) changeShadow.enabled   = false;
    }

    private void ResetActivePulse()
    {
        if (activePulseGraphic != null && hasPulseBase)
            activePulseGraphic.transform.localScale = activePulseBase;

        activePulseGraphic = null;
        hasPulseBase       = false;
    }

    // -------------------------------------------------------------------------
    // Message Helpers
    // -------------------------------------------------------------------------

    private void ShowLessonMessage(string text)
    {
        if (lessonMessageRoot != null)
            lessonMessageRoot.SetActive(true);

        if (lessonMessageText != null)
            lessonMessageText.text = text;
    }

    private void HideLessonMessage()
    {
        if (lessonMessageRoot != null)
            lessonMessageRoot.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Next Button
    // -------------------------------------------------------------------------

    private void ShowNextButton(bool show)
    {
        if (nextButtonRoot != null)
            nextButtonRoot.SetActive(show);
        else if (nextButton != null)
            nextButton.gameObject.SetActive(show);
    }
}
