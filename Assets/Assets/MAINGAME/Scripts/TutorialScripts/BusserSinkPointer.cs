using TMPro;
using UnityEngine;

/// <summary>
/// World-space sink pointer shown during the Day 4 Busser guided clean-tray step.
/// Place this GameObject above the sink. Wire a SpriteRenderer on the same object
/// as the arrow (Arrow sprite), optionally add a child SpriteRenderer for the glow
/// and a child TMP_Text for the label. The whole object starts inactive in the scene.
/// TutorialManager calls Show() when the busser picks up the tray and Hide() when cleaned.
/// </summary>
public class BusserSinkPointer : MonoBehaviour
{
    [Header("Bob")]
    [SerializeField] private Transform bobTarget;
    [SerializeField] private float bobHeight = 0.18f;
    [SerializeField] private float bobSpeed  = 2.2f;

    [Header("Glow / Pulse")]
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private float glowMinAlpha = 0.15f;
    [SerializeField] private float glowMaxAlpha = 0.65f;
    [SerializeField] private float glowPulseSpeed = 1.8f;

    [Header("Label")]
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private string labelString = "CLEAN TRAY HERE";

    private Vector3 bobOrigin;
    private bool bobOriginCaptured;
    private bool isShowing;

    private void Awake()
    {
        // Resolve bob target to self if not set.
        if (bobTarget == null)
            bobTarget = transform;

        // Cache the origin before any bob animation touches localPosition.
        bobOrigin = bobTarget.localPosition;
        bobOriginCaptured = true;

        if (labelText != null)
        {
            labelText.text = labelString;
            labelText.gameObject.SetActive(!string.IsNullOrWhiteSpace(labelString));
        }

        // Always start hidden — will be activated by Show().
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // Snap bob position back to origin each time it becomes active.
        // bobOrigin may not be set yet if the object starts inactive in the scene
        // (Awake has not run). Capture it here as a safe fallback.
        if (!bobOriginCaptured && bobTarget != null)
        {
            bobOrigin = bobTarget.localPosition;
            bobOriginCaptured = true;
        }

        if (bobTarget != null)
            bobTarget.localPosition = bobOrigin;
    }

    private void Update()
    {
        if (!isShowing)
            return;

        ApplyBob();
        ApplyGlowPulse();
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    /// <summary>Make the pointer visible. Safe to call multiple times.</summary>
    public void Show()
    {
        if (isShowing)
            return;

        isShowing = true;

        // Activate first so OnEnable fires and captures bobOrigin if Awake never ran.
        gameObject.SetActive(true);

        if (bobTarget != null)
            bobTarget.localPosition = bobOrigin;
    }

    /// <summary>Hide the pointer and reset state. Safe to call multiple times.</summary>
    public void Hide()
    {
        if (!isShowing)
            return;

        isShowing = false;
        gameObject.SetActive(false);

        if (bobTarget != null)
            bobTarget.localPosition = bobOrigin;
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private void ApplyBob()
    {
        if (bobTarget == null)
            return;

        Vector3 pos = bobOrigin;
        pos.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        bobTarget.localPosition = pos;
    }

    private void ApplyGlowPulse()
    {
        if (glowRenderer == null)
            return;

        float t = Mathf.Sin(Time.time * glowPulseSpeed) * 0.5f + 0.5f; // 0-1
        float alpha = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, t);

        Color c = glowRenderer.color;
        c.a = alpha;
        glowRenderer.color = c;
    }
}
