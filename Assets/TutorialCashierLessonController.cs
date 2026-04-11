using System.Collections;
using TMPro;
using UnityEngine;

public class TutorialCashierLessonController : MonoBehaviour
{
    [Header("Main References")]
    [SerializeField] private CashierRegisterUI registerUI;
    [SerializeField] private TutorialCashierOrderRandomizer randomizer;
    [SerializeField] private TutorialCashierRegisterDisplayBridge displayBridge;

    [Header("Optional Visual Root Override")]
    [SerializeField] private GameObject registerVisualRoot;

    [Header("UI")]
    [SerializeField] private GameObject waitingIndicatorRoot;
    [SerializeField] private TMP_Text waitingIndicatorText;
    [SerializeField] private GameObject lessonMessageRoot;
    [SerializeField] private TMP_Text lessonMessageText;

    [Header("Role UI")]
    [SerializeField] private MonoBehaviour roleManagerTarget;
    [SerializeField] private GameObject[] hideOnCashierDay;
    [SerializeField] private GameObject[] showOnCashierDay;

    [Header("Timing")]
    [SerializeField] private float introDialogueReadSeconds = 3f;
    [SerializeField] private float guidedDelaySeconds = 1.5f;
    [SerializeField] private float practiceDelaySeconds = 5f;

    [Header("Counts")]
    [SerializeField] private bool useGuidedRound = true;
    [SerializeField] private int practicePaymentCount = 5;

    [Header("Tutorial Day Lock")]
    [SerializeField] private bool onlyRunOnDay3Cashier = true;

    private bool sessionRunning;
    private bool lessonStarted;
    private bool guidedRoundDone;
    private bool roundOpen;
    private int practiceCompleted;
    private Coroutine openRoutine;

    private void Awake()
    {
        if (registerUI == null)
            registerUI = FindFirstObjectByType<CashierRegisterUI>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        bool shouldRun = !onlyRunOnDay3Cashier || IsDay3CashierActive();

        if (shouldRun && !lessonStarted)
        {
            lessonStarted = true;
            BeginLesson();
        }
        else if (!shouldRun && lessonStarted)
        {
            EndLesson();
        }

        // If TutorialManager has completed Day 3 while the lesson is still running,
        // end the lesson immediately — do not wait for our own payment counter.
        if (sessionRunning && IsDayComplete())
        {
            Debug.Log("[LessonController] TutorialManager completed Day 3 — ending lesson.", this);
            EndLesson();
        }
    }

    /// <summary>
    /// Returns true when TutorialManager has moved to the Complete phase for Day 3,
    /// meaning all required payments have been registered and the day is over.
    /// </summary>
    private bool IsDayComplete()
    {
        TutorialManager tm = TutorialManager.Instance;
        return tm != null &&
               tm.CurrentDay == TutorialManager.TutorialDay.Day3Cashier &&
               tm.CurrentPhase == TutorialManager.TutorialPhase.Complete;
    }

    private bool IsDay3CashierActive()
    {
        TutorialManager tm = TutorialManager.Instance;
        return tm != null &&
            tm.TutorialStarted &&
            tm.CurrentDay == TutorialManager.TutorialDay.Day3Cashier &&
            tm.CurrentPhase != TutorialManager.TutorialPhase.Complete;
    }

    private void BeginLesson()
    {
        sessionRunning = true;
        guidedRoundDone = !useGuidedRound;
        roundOpen = false;
        practiceCompleted = 0;

        ApplyCashierDayVisuals(true);
        ForceCashierRole();
        HideWaitingIndicator();
        HidePersistentTip();

        if (!guidedRoundDone)
        {
            // Explain the auto-switch mechanic before the first register interaction.
            ShowWarningPopup("When a waiter gives money to the cashier, your role switches automatically!");
            ShowPersistentTip("This is the POS. Read the received amount, total, and change. Give the exact change, then press Confirm.");
            ScheduleNextOpen(introDialogueReadSeconds + guidedDelaySeconds, false);
        }
        else
        {
            ShowWarningPopup("Cashier practice started. Payments will be simulated automatically.");
            ScheduleNextOpen(practiceDelaySeconds, true);
        }
    }

    private void EndLesson()
    {
        Debug.LogWarning($"[LessonController] EndLesson called — sessionRunning={sessionRunning} lessonStarted={lessonStarted}", this);

        sessionRunning = false;
        lessonStarted = false;
        roundOpen = false;
        practiceCompleted = 0;

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        HideWaitingIndicator();
        HidePersistentTip();
        ApplyCashierDayVisuals(false);
    }

    private void ApplyCashierDayVisuals(bool active)
    {
        bool registerIsOpen = CashierRegisterUI.Instance != null && CashierRegisterUI.Instance.IsOpen;

        if (hideOnCashierDay != null)
        {
            for (int i = 0; i < hideOnCashierDay.Length; i++)
            {
                if (hideOnCashierDay[i] == null)
                    continue;

                bool next = !active;
                Debug.Log($"[LessonController] hideOnCashierDay[{i}]={hideOnCashierDay[i].name} → SetActive({next})", hideOnCashierDay[i]);
                hideOnCashierDay[i].SetActive(next);
            }
        }

        if (showOnCashierDay != null)
        {
            for (int i = 0; i < showOnCashierDay.Length; i++)
            {
                if (showOnCashierDay[i] == null)
                    continue;

                // Never force-disable a UI object while the POS register is open.
                if (!active && registerIsOpen)
                {
                    Debug.LogWarning($"[LessonController] SKIPPED SetActive(false) on showOnCashierDay[{i}]={showOnCashierDay[i].name} because CashierRegisterUI is open.", showOnCashierDay[i]);
                    continue;
                }

                Debug.Log($"[LessonController] showOnCashierDay[{i}]={showOnCashierDay[i].name} → SetActive({active})", showOnCashierDay[i]);
                showOnCashierDay[i].SetActive(active);
            }
        }
    }

    private void ForceCashierRole()
    {
        if (roleManagerTarget == null)
            return;

        roleManagerTarget.SendMessage("SelectRoleByName", "Cashier", SendMessageOptions.DontRequireReceiver);
        roleManagerTarget.SendMessage("SwitchRoleByName", "Cashier", SendMessageOptions.DontRequireReceiver);
        roleManagerTarget.SendMessage("SetCurrentRoleByName", "Cashier", SendMessageOptions.DontRequireReceiver);
    }

    private void ScheduleNextOpen(float delay)
    {
        ScheduleNextOpen(delay, guidedRoundDone);
    }

    private void ScheduleNextOpen(float delay, bool showWaiting)
    {
        if (!sessionRunning)
            return;

        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(OpenAfterDelay(delay, showWaiting));
    }

    private IEnumerator OpenAfterDelay(float delay, bool showWaiting)
    {
        roundOpen = false;

        // Abort immediately if TutorialManager already completed Day 3.
        if (IsDayComplete())
            yield break;

        if (showWaiting)
        {
            ShowWaitingIndicator("Waiting for payments...");
            ShowWarningPopup("Waiting for payments...");
        }
        else
        {
            HideWaitingIndicator();
        }

        yield return new WaitForSeconds(delay);

        if (!sessionRunning)
            yield break;

        // Second guard after the wait: TutorialManager may have completed during the delay.
        if (IsDayComplete())
        {
            HideWaitingIndicator();
            yield break;
        }

        yield return StartCoroutine(OpenRandomPaymentRoutine());
        openRoutine = null;
    }

    private IEnumerator OpenRandomPaymentRoutine()
    {
        if (registerUI == null || randomizer == null || displayBridge == null)
            yield break;

        var order = randomizer.Generate();

        ForceShowRegisterHierarchy();
        yield return null;

        registerUI.OpenForPayment(null, order.received, order.total);
        displayBridge.Apply(order);

        ForceShowRegisterHierarchy();

        roundOpen = true;
        HideWaitingIndicator();

        ShowPersistentTip("Tip: Use the bills and coins to enter the exact change, then press Confirm.");

        if (guidedRoundDone)
            ShowWarningPopup("A new payment arrived. Process it in the POS.");
    }

    private void ForceShowRegisterHierarchy()
    {
        if (registerUI == null)
            return;

        SetParentsActive(registerUI.transform);

        if (registerVisualRoot != null)
            SetHierarchyActive(registerVisualRoot.transform);
        else
            SetHierarchyActive(registerUI.transform);

        registerUI.transform.SetAsLastSibling();

        CanvasGroup[] groups = registerUI.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null)
                continue;

            groups[i].alpha = 1f;
            groups[i].interactable = true;
            groups[i].blocksRaycasts = true;
        }
    }

    private void SetParentsActive(Transform child)
    {
        Transform current = child;
        while (current != null)
        {
            current.gameObject.SetActive(true);
            current = current.parent;
        }
    }

    private void SetHierarchyActive(Transform root)
    {
        if (root == null)
            return;

        root.gameObject.SetActive(true);

        for (int i = 0; i < root.childCount; i++)
            SetHierarchyActive(root.GetChild(i));
    }

    public void NotifySuccessfulConfirm()
    {
        if (!sessionRunning || !roundOpen)
            return;

        roundOpen = false;

        if (!guidedRoundDone)
        {
            guidedRoundDone = true;

            if (TutorialManager.Instance != null)
                TutorialManager.Instance.OnCashierConfirmed(null);

            if (IsDayComplete())
            {
                EndLesson();
                return;
            }

            ShowWarningPopup("Good. Now process payments on your own.");
            ScheduleNextOpen(practiceDelaySeconds, true);
            return;
        }

        practiceCompleted++;

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.RegisterCashierPaymentProcessed(null);

        if (practiceCompleted >= practicePaymentCount)
        {
            ShowWarningPopup("Cashier practice complete.");
            HideWaitingIndicator();
            HidePersistentTip();

            if (TutorialManager.Instance != null &&
                TutorialManager.Instance.CurrentPhase != TutorialManager.TutorialPhase.Complete)
            {
                TutorialManager.Instance.SetPhase(TutorialManager.TutorialPhase.Complete);
            }

            EndLesson();
            return;
        }

        if (IsDayComplete())
        {
            EndLesson();
            return;
        }

        ShowWarningPopup("Good. Get ready for the next payment.");
        ScheduleNextOpen(practiceDelaySeconds, true);
    }

    private void ShowWaitingIndicator(string text)
    {
        if (waitingIndicatorRoot != null)
            waitingIndicatorRoot.SetActive(true);

        if (waitingIndicatorText != null)
            waitingIndicatorText.text = text;
    }

    private void HideWaitingIndicator()
    {
        if (waitingIndicatorRoot != null)
            waitingIndicatorRoot.SetActive(false);

        if (waitingIndicatorText != null)
            waitingIndicatorText.text = string.Empty;
    }

    private void ShowPersistentTip(string text)
    {
        if (lessonMessageRoot != null)
            lessonMessageRoot.SetActive(!string.IsNullOrWhiteSpace(text));

        if (lessonMessageText != null)
            lessonMessageText.text = text;
    }

    private void HidePersistentTip()
    {
        if (lessonMessageRoot != null)
            lessonMessageRoot.SetActive(false);

        if (lessonMessageText != null)
            lessonMessageText.text = string.Empty;
    }

    private void ShowWarningPopup(string text)
    {
        if (WarningSlideUI.Instance != null && !string.IsNullOrWhiteSpace(text))
            WarningSlideUI.Instance.Show(text);
    }
}