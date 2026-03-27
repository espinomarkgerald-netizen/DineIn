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
        // Save exactly where you placed it in the editor so it doesn't float into space
        originalPos = feedbackText.transform.localPosition;
        canvasGroup.alpha = 0f; // Hide it at the start of the game
    }

    public void ShowRejection(string message) {
        // If they spam the window, cancel the old animation and restart it
        if (activeCoroutine != null) {
            StopCoroutine(activeCoroutine);
        }
        activeCoroutine = StartCoroutine(AnimateFeedback(message));
    }

    private IEnumerator AnimateFeedback(string message) {
        feedbackText.text = message;
        canvasGroup.alpha = 1f;
        feedbackText.transform.localPosition = originalPos;

        // Float it straight up by 1 unit
        Vector3 endPos = originalPos + new Vector3(0, 1f, 0);

        float duration = 1.5f; // Lasts 1.5 seconds
        float elapsed = 0f;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;

            // Start fading out when the animation is halfway done
            if (percent > 0.5f) {
                canvasGroup.alpha = 1f - ((percent - 0.5f) * 2f);
            }

            // Smoothly move it up
            feedbackText.transform.localPosition = Vector3.Lerp(originalPos, endPos, percent);
            yield return null;
        }

        // Reset it when it's done
        canvasGroup.alpha = 0f;
        feedbackText.transform.localPosition = originalPos;
    }
}