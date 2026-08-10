using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Utilities
{
    public class TopUpWebRedirect : MonoBehaviour
    {
        [Header("URL Configuration")]
        [SerializeField] private string baseWebsiteURL = "https://dine-in-website.vercel.app/";

        [Header("Notification Settings")]
        [Tooltip("Message displayed to the player before redirecting.")]
        [SerializeField] private string notificationMessage = "Redirecting to web store to purchase GC...";
        [SerializeField] private float notificationDisplayTime = 2.5f;

        [Header("Delay Settings")]
        [Tooltip("How long to wait (in seconds) after showing the popup before opening the browser.")]
        [SerializeField] private float redirectDelaySeconds = 2.0f;

        [Header("Optional Setup")]
        [Tooltip("Optional: Drag a Button component here. If left empty, GetComponent<Button>() will be used.")]
        [SerializeField] private Button targetButton;

        private Coroutine redirectCoroutine;

        private void Awake()
        {
            if (targetButton == null)
                targetButton = GetComponent<Button>();

            if (targetButton != null)
                targetButton.onClick.AddListener(OpenTopUpPage);
        }

        private void OnDestroy()
        {
            if (targetButton != null)
                targetButton.onClick.RemoveListener(OpenTopUpPage);
        }

        /// <summary>
        /// Public entry point for UI buttons. Starts the delayed redirect sequence.
        /// </summary>
        public void OpenTopUpPage()
        {
            if (redirectCoroutine != null)
                StopCoroutine(redirectCoroutine);

            redirectCoroutine = StartCoroutine(RedirectSequenceRoutine());
        }

        private IEnumerator RedirectSequenceRoutine()
        {
            // 1. Trigger the Info Popup
            if (NotificationPopupController.Instance != null)
            {
                NotificationPopupController.Instance.Show(
                    notificationMessage, 
                    NotificationPopupController.PopupType.Info, 
                    notificationDisplayTime
                );
            }
            else
            {
                Debug.LogWarning("[TopUpWebRedirect] NotificationPopupController Instance not found in scene!");
            }

            // 2. Validate URL before waiting
            if (string.IsNullOrEmpty(baseWebsiteURL))
            {
                Debug.LogError("[TopUpWebRedirect] Base Website URL is empty!");
                yield break;
            }

            // 3. Wait for the delay timer (using unscaled time so it works even if the game is paused)
            yield return new WaitForSecondsRealtime(redirectDelaySeconds);

            // 4. Open the web browser
            Application.OpenURL(baseWebsiteURL);

            redirectCoroutine = null;
        }
    }
}