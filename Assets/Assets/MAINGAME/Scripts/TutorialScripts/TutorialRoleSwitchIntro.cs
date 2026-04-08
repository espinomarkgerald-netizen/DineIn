using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Guided role-switch introduction that runs before Day 1 Host gameplay begins.
///
/// Walks through all four roles — Host, Waiter, Cashier, Busser — one at a time.
/// For each role:
///   1. The active button is highlighted (tint); all other buttons are dimmed.
///   2. The pointer arrow moves above that button.
///   3. Dialogue describes what that role does.
///   4. The player MUST press that button to advance to the next role.
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

        [TextArea(2, 4)]
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

    [Header("Role Intro Steps  (Host → Waiter → Cashier → Busser)")]
    [SerializeField] private RoleIntroEntry[] roles = new RoleIntroEntry[4];

    [Header("Screen Dim")]
    [Tooltip("Full-screen CanvasGroup (black image, alpha 0, blocksRaycasts false at start).")]
    [SerializeField] private CanvasGroup dimOverlay;
    [SerializeField] [Range(0f, 1f)] private float dimTargetAlpha = 0.6f;
    [SerializeField] private float dimFadeDuration = 0.3f;

    [Header("Button Panel Highlight")]
    [Tooltip("Glow/border CanvasGroup placed behind the Buttons group. Pulsed automatically.")]
    [SerializeField] private CanvasGroup buttonPanelHighlight;
    [SerializeField] private float pulseMin  = 0.3f;
    [SerializeField] private float pulseMax  = 1f;
    [SerializeField] private float pulseSpeed = 2.5f;

    [Header("Active Button Tint")]
    [SerializeField] private Color activeButtonTint   = new Color(1f, 0.92f, 0.2f, 1f);
    [SerializeField] private Color inactiveButtonTint = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("Pointer Arrow (optional)")]
    [Tooltip("UI arrow RectTransform repositioned above each active button.")]
    [SerializeField] private RectTransform pointerArrow;
    [SerializeField] private Vector2 pointerOffset = new Vector2(0f, 34f);

    [Header("Closing")]
    [TextArea(2, 4)]
    [SerializeField] private string closingLine =
        "Good! You now know all four roles. Let's begin — you're starting as the Host!";

    // -----------------------------------------------------------------------
    // Runtime
    // -----------------------------------------------------------------------

    private Action onComplete;
    private Coroutine runRoutine;
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

        for (int i = 0; i < roles.Length; i++)
        {
            RoleIntroEntry entry = roles[i];
            if (entry == null) continue;

            ApplyButtonTints(i);
            MovePointer(entry.button);
            if (entry.roleLabel != null) entry.roleLabel.SetActive(true);

            // Dialogue — player presses Next to continue.
            bool dialogueDone = false;
            string speaker = string.IsNullOrWhiteSpace(entry.roleName) ? speakerName : entry.roleName;

            if (dialogueUI != null && !string.IsNullOrWhiteSpace(entry.description))
                dialogueUI.ShowManual(speaker, entry.description, () => dialogueDone = true);
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

                yield return new WaitForSeconds(0.25f);
            }

            if (entry.roleLabel != null) entry.roleLabel.SetActive(false);
        }

        // Clean up.
        ResetButtonTints();
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
    // Visual helpers
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

    private void ApplyButtonTints(int activeIndex)
    {
        for (int i = 0; i < roles.Length; i++)
        {
            if (roles[i]?.button == null) continue;
            Image img = roles[i].button.GetComponent<Image>();
            if (img != null) img.color = (i == activeIndex) ? activeButtonTint : inactiveButtonTint;
        }
    }

    private void ResetButtonTints()
    {
        foreach (RoleIntroEntry entry in roles)
        {
            if (entry?.button == null) continue;
            Image img = entry.button.GetComponent<Image>();
            if (img != null) img.color = Color.white;
        }
    }

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
