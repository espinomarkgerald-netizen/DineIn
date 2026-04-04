using System.Collections;
using UnityEngine;

public class TutorialCashierConfirmRelay : MonoBehaviour
{
    [SerializeField] private TutorialCashierLessonController lessonController;
    [SerializeField] private float successCheckDuration = 1.5f;

    private Coroutine notifyRoutine;

    private void Awake()
    {
        if (lessonController == null)
            lessonController = GetComponent<TutorialCashierLessonController>();

        if (lessonController == null)
            lessonController = GetComponentInParent<TutorialCashierLessonController>(true);

        if (lessonController == null)
            lessonController = FindFirstObjectByType<TutorialCashierLessonController>(FindObjectsInactive.Include);
    }

    public void OnConfirmClicked()
    {
        if (notifyRoutine != null)
            StopCoroutine(notifyRoutine);

        notifyRoutine = StartCoroutine(NotifyIfConfirmSucceeded());
    }

    private IEnumerator NotifyIfConfirmSucceeded()
    {
        float elapsed = 0f;

        while (elapsed < successCheckDuration)
        {
            if (CashierRegisterUI.Instance == null)
            {
                notifyRoutine = null;
                yield break;
            }

            if (!CashierRegisterUI.Instance.IsOpen)
            {
                if (lessonController != null)
                    lessonController.NotifySuccessfulConfirm();

                notifyRoutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        notifyRoutine = null;
    }
}