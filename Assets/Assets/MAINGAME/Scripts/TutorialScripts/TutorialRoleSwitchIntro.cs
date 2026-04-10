using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactive role-switch tutorial step that runs before Day 1 Host gameplay.
///
/// Flow:
///   • Begin() is called the same frame the video screen activates.
///   • Host button immediately starts glowing + bouncing — no yield before first cue.
///   • Player clicks Host → Busser → Cashier → Waiter in order.
///   • Wrong clicks are silently ignored.
///   • After all 4 clicks onComplete fires → TutorialManager hides the video and starts Day 1.
///
/// Wiring (Inspector):
///   roles[0..3] — HostButton, BusserButton, CashierButton, WaiterButton
///   dialogueUI  — shared TutorialDialogueUI on TutorialManager (optional)
/// </summary>
public class TutorialRoleSwitchIntro : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Data
    // -----------------------------------------------------------------------

    [Serializable]
    public class RoleIntroEntry
    {
        [Tooltip("Display name — used only in editor logs.")]
        public string roleName;

        [Tooltip("The scene Button the player must click to advance.")]
        public Button button;
    }

    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Dialogue (optional)")]
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private string speakerName = "Manager";

    [Header("Preamble (shown automatically when sequence starts)")]
    [TextArea(2, 5)]
    [SerializeField] private string preambleLine =
        "Use the buttons on the left to switch between roles. Click each one when it glows!";

    [Header("Role Buttons  (Host \u2192 Busser \u2192 Cashier \u2192 Waiter)")]
    [SerializeField] private RoleIntroEntry[] roles = new RoleIntroEntry[4];

    [Header("Button Glow")]
    [Tooltip("Peak color pulsed on the active button's Image.")]
    [SerializeField] private Color glowColor = new Color(1f, 0.92f, 0.16f, 1f);
    [Tooltip("Glow pulse cycles per second.")]
    [SerializeField] private float glowPulseSpeed = 2.5f;

    [Header("Button Bounce")]
    [Tooltip("Peak scale multiplier during bounce (1 = no bounce).")]
    [SerializeField] private float bounceScale = 1.18f;
    [Tooltip("Bounce cycles per second.")]
    [SerializeField] private float bounceSpeed = 2.8f;

    [Header("Closing (optional)")]
    [TextArea(2, 4)]
    [SerializeField] private string closingLine =
        "Good! You know all four roles. Let's begin \u2014 you're starting as the Host!";

    // -----------------------------------------------------------------------
    // Runtime
    // -----------------------------------------------------------------------

    private Action onComplete;
    private Coroutine runRoutine;
    private Coroutine glowRoutine;
    private Coroutine bounceRoutine;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Starts the guided sequence. The first button starts glowing and bouncing on this
    /// exact frame so the effect appears the instant the video screen activates.
    /// Calls <paramref name="onComplete"/> after the last correct button is clicked.
    /// </summary>
    public void Begin(Action onComplete)
    {
        this.onComplete = onComplete;

        if (runRoutine != null)
            StopCoroutine(runRoutine);

        // Fire preamble dialogue immediately alongside the glow — informational only,
        // auto-dismissed after a few seconds so the player is not blocked by a Next button.
        if (dialogueUI != null && !string.IsNullOrWhiteSpace(preambleLine))
            dialogueUI.ShowAuto(speakerName, preambleLine, 4f);

        // Activate the first button on this same frame — no yield, no delay.
        if (roles.Length > 0 && roles[0] != null && roles[0].button != null)
            ActivateButtonEffect(roles[0].button);

        runRoutine = StartCoroutine(RunSequence());
    }

    // -----------------------------------------------------------------------
    // Sequence
    // -----------------------------------------------------------------------

    private IEnumerator RunSequence()
    {
        for (int i = 0; i < roles.Length; i++)
        {
            RoleIntroEntry entry = roles[i];
            if (entry == null || entry.button == null) continue;

            // First button was already activated synchronously in Begin().
            // Activate subsequent buttons here, after the previous click.
            if (i > 0)
                ActivateButtonEffect(entry.button);

            // Block until exactly this button is clicked — all others do nothing.
            // DeactivateButtonEffect and the role switch both happen inside the handler
            // so the glow/bounce are gone and the role is applied on the same frame as
            // the click — no one-frame lag where the button still looks active after pressing.
            bool pressed = false;
            RoleIntroEntry captured = entry;
            UnityEngine.Events.UnityAction handler = () =>
            {
                DeactivateButtonEffect(captured.button);
                SwitchToRole(captured.roleName);
                pressed = true;
            };
            entry.button.onClick.AddListener(handler);
            while (!pressed)
                yield return null;
            entry.button.onClick.RemoveListener(handler);

            // Short pause so the transition between buttons feels satisfying.
            yield return new WaitForSeconds(0.18f);
        }

        // Optional closing line — auto-dismissed, does not block Day 1 from starting.
        if (dialogueUI != null && !string.IsNullOrWhiteSpace(closingLine))
            dialogueUI.ShowAuto(speakerName, closingLine, 2f);

        runRoutine = null;
        onComplete?.Invoke();
    }

    // -----------------------------------------------------------------------
    // Role switch helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Switches the player to the role matching <paramref name="roleName"/> immediately.
    /// Uses <see cref="RoleManager.Instance"/> — the same path TutorialManager uses.
    /// </summary>
    private static void SwitchToRole(string roleName)
    {
        if (RoleManager.Instance == null || string.IsNullOrWhiteSpace(roleName))
            return;

        switch (roleName.Trim().ToLowerInvariant())
        {
            case "host":    RoleManager.Instance.SwitchToHost();    break;
            case "busser":  RoleManager.Instance.SwitchToBusser();  break;
            case "cashier": RoleManager.Instance.SwitchToCashier(); break;
            case "waiter":  RoleManager.Instance.SwitchToWaiter();  break;
        }
    }

    // -----------------------------------------------------------------------
    // Per-button effects — glow + bounce run in parallel
    // -----------------------------------------------------------------------

    private void ActivateButtonEffect(Button target)
    {
        if (target == null) return;

        StopEffectRoutines();

        Image img = target.GetComponent<Image>();
        if (img != null)
            glowRoutine = StartCoroutine(PulseColor(img));

        bounceRoutine = StartCoroutine(PulseScale(target.transform));
    }

    private void DeactivateButtonEffect(Button target)
    {
        StopEffectRoutines();

        if (target == null) return;

        Image img = target.GetComponent<Image>();
        if (img != null)
            img.color = Color.white;

        target.transform.localScale = Vector3.one;
    }

    private void StopEffectRoutines()
    {
        if (glowRoutine != null) { StopCoroutine(glowRoutine); glowRoutine = null; }
        if (bounceRoutine != null) { StopCoroutine(bounceRoutine); bounceRoutine = null; }
    }

    private IEnumerator PulseColor(Image img)
    {
        while (img != null)
        {
            float t = (Mathf.Sin(Time.unscaledTime * glowPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            img.color = Color.Lerp(Color.white, glowColor, t);
            yield return null;
        }
    }

    private IEnumerator PulseScale(Transform target)
    {
        // Phase-offset by π/2 so the bounce peak lands between glow peaks — feels lively.
        while (target != null)
        {
            float t = (Mathf.Sin(Time.unscaledTime * bounceSpeed * Mathf.PI * 2f + Mathf.PI * 0.5f) + 1f) * 0.5f;
            float s = Mathf.Lerp(1f, bounceScale, t);
            target.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    // -----------------------------------------------------------------------
    // Cleanup — safety net for scene reloads and day resets
    // -----------------------------------------------------------------------

    private void OnDisable()
    {
        StopEffectRoutines();

        if (runRoutine != null) { StopCoroutine(runRoutine); runRoutine = null; }

        foreach (RoleIntroEntry entry in roles)
        {
            if (entry?.button == null) continue;

            Image img = entry.button.GetComponent<Image>();
            if (img != null) img.color = Color.white;
            entry.button.transform.localScale = Vector3.one;
        }
    }
}
