using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Guided role-switch introduction that runs before Day 1 Host gameplay begins.
///
/// Step 0  — Preamble: explains what the four switch buttons do and how switching works.
/// Steps 1–4 — One per role (Host → Waiter → Cashier → Busser):
///   1. Active button bounces continuously so it stands out without changing colours.
///   2. Pointer arrow moves above that button.
///   3. Dialogue describes the role AND tells the player to press that button.
///   4. Player MUST press the button to advance — bounce stops on press.
///
/// After all four roles the screen un-dims, a closing line plays, and onComplete fires.
///
/// Wiring (Inspector):
///   • roles[0–3] — HostButton, WaiterButton, CashierButton, BusserButton (real scene buttons)
///   • dimOverlay — full-screen black CanvasGroup (alpha 0, blocksRaycasts false)
///   • buttonPanelHighlight — glow CanvasGroup behind the Buttons group
///   • pointerArrow — UI RectTransform arrow/hand icon
///   • dialogueUI  — shared TutorialDialogueUI
/// </summary>
public class TutorialRoleSwitchIntro : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Role definition
    // -----------------------------------------------------------------------

    [Serializable]
    public class RoleIntroEntry
    {
        [Tooltip("Speaker name shown in the dialogue bubble.")]
        public string roleName;

        [TextArea(2, 5)]
        [Tooltip("What this role does. A 'press this button now' prompt is appended automatically.")]
        public string description;

        [Tooltip("The actual scene Button the player must press to advance.")]
        public Button button;

        [Tooltip("Optional badge/label for this role — shown during its step, hidden after. Leave unassigned to skip.")]
        public GameObject roleLabel;
    }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Dialogue")]
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private string speakerName = "Manager";

    [Header("Preamble")]
    [TextArea(2, 5)]
    [Tooltip("Shown once before walking through each role. Explains what the switch buttons do.")]
    [SerializeField] private string preambleLine =
        "See those four buttons at the top? Each one switches you into a different staff role.\n" +
        "Each role has a different job — Host seats guests, Waiter takes orders, " +
        "Cashier handles payment, and Busser cleans up.\n" +
        "I'll walk you through each one now. Press each bouncing button when I point to it.";

    [Header("Role Intro Steps  (Host → Waiter → Cashier → Busser)")]
    [SerializeField] private RoleIntroEntry[] roles = new RoleIntroEntry[4];

    [Header("Press-Button Prompt")]
    [Tooltip("Appended to each role's description so the player knows what to do next.")]
    [SerializeField] private string pressPromptSuffix = "\n\nPress this button now to continue.";

    [Header("Button Bounce")]
    [Tooltip("Peak scale of the active button during its bounce (1 = no bounce).")]
    [SerializeField] private float bounceScale = 1.15f;
    [Tooltip("Bounces per second.")]
    [SerializeField] private float bounceSpeed = 2.2f;

    [Header("Screen Dim")]
    [Tooltip("Full-screen CanvasGroup (black image, alpha 0, blocksRaycasts false at start).")]
    [SerializeField] private CanvasGroup dimOverlay;
    [SerializeField] [Range(0f, 1f)] private float dimTargetAlpha = 0.55f;
    [SerializeField] private float dimFadeDuration = 0.3f;

    [Header("Button Panel Highlight")]
    [Tooltip("Glow/border CanvasGroup placed behind the Buttons group. Pulsed automatically.")]
    [SerializeField] private CanvasGroup buttonPanelHighlight;
    [SerializeField] private float pulseMin  = 0.3f;
    [SerializeField] private float pulseMax  = 1f;
    [SerializeField] private float pulseSpeed = 2.5f;

    [Header("Pointer Arrow (optional)")]
    [Tooltip("UI arrow RectTransform repositioned above each active button.")]
    [SerializeField] private RectTransform pointerArrow;
    [SerializeField] private Vector2 pointerOffset = new Vector2(0f, 34f);

    [Header("Closing")]
    [TextArea(2, 4)]
    [SerializeField] private string closingLine =
        "Great — you know all four roles now.\n" +
        "In mastery gameplay you can switch between them freely at any time.\n" +
        "Let's begin! You're the Host first — greet the incoming customers!";

    // -----------------------------------------------------------------------
    // Runtime
    // -----------------------------------------------------------------------

    private Action onComplete;
    private Coroutine runRoutine;
    private Coroutine bounceRoutine;
    private Coroutine pulseRoutine;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Starts the four-role guided intro. Invokes <paramref name="onComplete"/> when done.
    /// </summary>
    public void Begin(Action onComplete)
    {
        this.onComplete = onComplete;

        if (runRoutine != null)
            StopCoroutine(runRoutine);

        runRoutine = StartCoroutine(RunSequence());
    }

    // -----------------------------------------------------------------------
    // Sequence
    // -----------------------------------------------------------------------

    private IEnumerator RunSequence()
    {
        yield return StartCoroutine(FadeDim(0f, dimTargetAlpha));
        StartPanelPulse();

        // Step 0 — Preamble: explain what the buttons are before touching any of them.
        if (dialogueUI != null && !string.IsNullOrWhiteSpace(preambleLine))
        {
            bool preambleDone = false;
            dialogueUI.ShowManual(speakerName, preambleLine, () => preambleDone = true);
            while (!preambleDone)
                yield return null;
        }

        // Steps 1–N — One per role.
        for (int i = 0; i < roles.Length; i++)
        {
            RoleIntroEntry entry = roles[i];
            if (entry == null) continue;

            StartBounce(entry.button);
            MovePointer(entry.button);
            if (entry.roleLabel != null) entry.roleLabel.SetActive(true);

            // Build the dialogue: role description + explicit press-button prompt.
            bool dialogueDone = false;
            string speaker = string.IsNullOrWhiteSpace(entry.roleName) ? speakerName : entry.roleName;
            string body = entry.description ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(pressPromptSuffix))
                body = body.TrimEnd() + pressPromptSuffix;

            if (dialogueUI != null && !string.IsNullOrWhiteSpace(body))
                dialogueUI.ShowManual(speaker, body, () => dialogueDone = true);
            else
                dialogueDone = true;

            while (!dialogueDone)
                yield return null;

            // Wait for the player to press this role's actual button.
            if (entry.button != null)
            {
                bool pressed = false;
                UnityEngine.Events.UnityAction handler = () => pressed = true;
                entry.button.onClick.AddListener(handler);
                while (!pressed) yield return null;
                entry.button.onClick.RemoveListener(handler);

                StopBounce(entry.button);
                yield return new WaitForSeconds(0.25f);
            }
            else
            {
                StopBounce(null);
            }

            if (entry.roleLabel != null) entry.roleLabel.SetActive(false);
        }

        // Clean up after all roles.
        StopPanelPulse();
        HidePointer();
        yield return StartCoroutine(FadeDim(dimTargetAlpha, 0f));

        // Closing line.
        bool closingDone = false;
        if (dialogueUI != null && !string.IsNullOrWhiteSpace(closingLine))
            dialogueUI.ShowManual(speakerName, closingLine, () => closingDone = true);
        else
            closingDone = true;

        while (!closingDone)
            yield return null;

        runRoutine = null;
        onComplete?.Invoke();
    }

    // -----------------------------------------------------------------------
    // Bounce
    // -----------------------------------------------------------------------

    private void StartBounce(Button target)
    {
        if (bounceRoutine != null)
            StopCoroutine(bounceRoutine);

        if (target == null) return;

        bounceRoutine = StartCoroutine(BounceButton(target.transform));
    }

    private void StopBounce(Button target)
    {
        if (bounceRoutine != null)
        {
            StopCoroutine(bounceRoutine);
            bounceRoutine = null;
        }

        // Always restore identity scale so the button sits exactly as it was before.
        if (target != null)
            target.transform.localScale = Vector3.one;
    }

    private IEnumerator BounceButton(Transform buttonTransform)
    {
        while (buttonTransform != null)
        {
            float t = (Mathf.Sin(Time.unscaledTime * bounceSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f, bounceScale, t);
            buttonTransform.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
    }

    // -----------------------------------------------------------------------
    // Screen dim
    // -----------------------------------------------------------------------

    private IEnumerator FadeDim(float from, float to)
    {
        if (dimOverlay == null) yield break;

        dimOverlay.gameObject.SetActive(true);
        dimOverlay.alpha = from;

        float elapsed = 0f;
        while (elapsed < dimFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            dimOverlay.alpha = Mathf.Lerp(from, to, elapsed / dimFadeDuration);
            yield return null;
        }

        dimOverlay.alpha = to;
        if (to <= 0f) dimOverlay.gameObject.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Panel pulse
    // -----------------------------------------------------------------------

    private void StartPanelPulse()
    {
        if (buttonPanelHighlight == null) return;
        buttonPanelHighlight.gameObject.SetActive(true);
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseHighlight());
    }

    private void StopPanelPulse()
    {
        if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
        if (buttonPanelHighlight != null) buttonPanelHighlight.gameObject.SetActive(false);
    }

    private IEnumerator PulseHighlight()
    {
        while (buttonPanelHighlight != null && buttonPanelHighlight.gameObject.activeSelf)
        {
            float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI) + 1f) * 0.5f;
            buttonPanelHighlight.alpha = Mathf.Lerp(pulseMin, pulseMax, t);
            yield return null;
        }
    }

    // -----------------------------------------------------------------------
    // Pointer arrow
    // -----------------------------------------------------------------------

    private void MovePointer(Button targetButton)
    {
        if (pointerArrow == null || targetButton == null) return;

        RectTransform targetRect = targetButton.GetComponent<RectTransform>();
        if (targetRect == null) return;

        pointerArrow.gameObject.SetActive(true);
        pointerArrow.SetParent(targetRect.parent, false);
        pointerArrow.anchoredPosition = targetRect.anchoredPosition + pointerOffset;
        pointerArrow.SetAsLastSibling();
    }

    private void HidePointer()
    {
        if (pointerArrow != null) pointerArrow.gameObject.SetActive(false);
    }
}
