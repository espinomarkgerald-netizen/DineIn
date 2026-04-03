using UnityEngine;
using TMPro;
using System.Collections;

public class DeliveryFeedback : MonoBehaviour {
    public static DeliveryFeedback Instance;

    [Header("UI References")]
    public TextMeshProUGUI feedbackText;
    public CanvasGroup canvasGroup;

    private Coroutine activeCoroutine;
    private Vector3 originalPos;

    void Awake() {
        Instance = this;
    }

    void Start() {
        originalPos = feedbackText.transform.localPosition;
        canvasGroup.alpha = 0f;
    }

    // --- FOR WHEN THEY MESS UP ---
    public void ShowRejection(string message) {
        feedbackText.color = Color.red; // Force it Red
        TriggerAnimation(message);
    }

    // --- FOR WHEN THEY WIN ---
    public void ShowSuccess(string message) {
        feedbackText.color = Color.green; // Force it Green
        TriggerAnimation(message);
    }

    private void TriggerAnimation(string message) {
        if (activeCoroutine != null) {
            StopCoroutine(activeCoroutine);
        }
        activeCoroutine = StartCoroutine(AnimateFeedback(message));
    }

    private IEnumerator AnimateFeedback(string message) {
        feedbackText.text = message;
        canvasGroup.alpha = 1f;
        feedbackText.transform.localPosition = originalPos;

        // Float it straight up
        Vector3 endPos = originalPos + new Vector3(0, 1f, 0);

        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;

            if (percent > 0.5f) {
                canvasGroup.alpha = 1f - ((percent - 0.5f) * 2f);
            }

            feedbackText.transform.localPosition = Vector3.Lerp(originalPos, endPos, percent);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        feedbackText.transform.localPosition = originalPos;
    }
}