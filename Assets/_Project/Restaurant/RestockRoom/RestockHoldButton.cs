using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Reusable mouse/touch hold control used to collect truck deliveries.</summary>
public sealed class RestockHoldButton : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [SerializeField] private Image progress;
    [SerializeField] private TMP_Text label;
    [SerializeField, Min(0.25f)] private float holdSeconds = 1.4f;

    private bool holding;
    private float heldFor;
    private Action completed;

    public void Configure(Image configuredProgress, TMP_Text configuredLabel)
    {
        progress = configuredProgress;
        label = configuredLabel;
    }

    public void Begin(Action onCompleted)
    {
        completed = onCompleted;
        holding = false;
        heldFor = 0f;
        Refresh();
    }

    private void Update()
    {
        if (!holding)
            return;

        heldFor += Time.unscaledDeltaTime;
        Refresh();
        if (heldFor < holdSeconds)
            return;

        holding = false;
        Action callback = completed;
        completed = null;
        callback?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (completed != null)
            holding = true;
    }

    public void OnPointerUp(PointerEventData eventData) => CancelHold();
    public void OnPointerExit(PointerEventData eventData) => CancelHold();

    private void CancelHold()
    {
        if (!holding)
            return;

        holding = false;
        heldFor = 0f;
        Refresh();
    }

    private void Refresh()
    {
        float normalized = Mathf.Clamp01(heldFor / Mathf.Max(0.25f, holdSeconds));
        if (progress != null)
            progress.fillAmount = normalized;

        if (label != null)
            label.text = normalized > 0f
                ? "KEEP HOLDING  " + Mathf.RoundToInt(normalized * 100f) + "%"
                : "HOLD TO COLLECT";
    }
}
