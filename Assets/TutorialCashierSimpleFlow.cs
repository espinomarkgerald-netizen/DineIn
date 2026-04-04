using UnityEngine;

public class TutorialCashierSimpleFlow : MonoBehaviour
{
    [Header("Scene Objects")]
    [SerializeField] private GameObject paymentToken;
    [SerializeField] private GameObject[] hideOnCashierDay;
    [SerializeField] private GameObject[] showOnCashierDay;
    [SerializeField] private MonoBehaviour roleManagerTarget;

    [Header("Timing")]
    [SerializeField] private float firstPaymentDelay = 0.75f;
    [SerializeField] private float nextPaymentDelay = 0.75f;

    private bool cashierDayWasActive;

    private void Update()
    {
        bool cashierDayActive = IsCashierDayActive();

        if (cashierDayActive != cashierDayWasActive)
        {
            cashierDayWasActive = cashierDayActive;
            ApplyCashierDayState(cashierDayActive);
        }
    }

    private bool IsCashierDayActive()
    {
        return TutorialManager.Instance != null &&
               TutorialManager.Instance.TutorialStarted &&
               TutorialManager.Instance.CurrentDay == TutorialManager.TutorialDay.Day3Cashier;
    }

    private void ApplyCashierDayState(bool active)
    {
        CancelInvoke();

        bool registerIsOpen = CashierRegisterUI.Instance != null && CashierRegisterUI.Instance.IsOpen;

        if (hideOnCashierDay != null)
        {
            for (int i = 0; i < hideOnCashierDay.Length; i++)
            {
                if (hideOnCashierDay[i] == null)
                    continue;

                bool next = !active;
                Debug.Log($"[SimpleFlow] hideOnCashierDay[{i}]={hideOnCashierDay[i].name} → SetActive({next})", hideOnCashierDay[i]);
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
                    Debug.LogWarning($"[SimpleFlow] SKIPPED SetActive(false) on showOnCashierDay[{i}]={showOnCashierDay[i].name} because CashierRegisterUI is open.", showOnCashierDay[i]);
                    continue;
                }

                Debug.Log($"[SimpleFlow] showOnCashierDay[{i}]={showOnCashierDay[i].name} → SetActive({active})", showOnCashierDay[i]);
                showOnCashierDay[i].SetActive(active);
            }
        }

        if (paymentToken != null)
            paymentToken.SetActive(false);

        if (!active)
            return;

        ForceCashierRole();
        Invoke(nameof(ShowFirstPayment), firstPaymentDelay);
    }

    private void ForceCashierRole()
    {
        if (roleManagerTarget == null)
            return;

        roleManagerTarget.SendMessage("SelectRoleByName", "Cashier", SendMessageOptions.DontRequireReceiver);
        roleManagerTarget.SendMessage("SwitchRoleByName", "Cashier", SendMessageOptions.DontRequireReceiver);
        roleManagerTarget.SendMessage("SetCurrentRoleByName", "Cashier", SendMessageOptions.DontRequireReceiver);
    }

    private void ShowFirstPayment()
    {
        if (!IsCashierDayActive())
            return;

        if (TutorialManager.Instance.CurrentPhase == TutorialManager.TutorialPhase.Complete)
            return;

        ShowPaymentToken();
    }

    private void ShowPaymentToken()
    {
        if (paymentToken != null)
            paymentToken.SetActive(true);
    }

    private void HidePaymentToken()
    {
        if (paymentToken != null)
            paymentToken.SetActive(false);
    }

    public void NotifyPaymentTokenClicked()
    {
        if (!IsCashierDayActive())
            return;

        var phase = TutorialManager.Instance.CurrentPhase;

        if (phase != TutorialManager.TutorialPhase.CashierWaitForMoney &&
            phase != TutorialManager.TutorialPhase.PracticeGameplay)
            return;

        HidePaymentToken();
        TutorialManager.Instance.OnMoneyGivenToCashier(null);
    }

    public void NotifyPosConfirmed()
    {
        if (!IsCashierDayActive())
            return;

        TutorialManager.Instance.OnCashierConfirmed(null);

        CancelInvoke(nameof(ShowNextPaymentIfNeeded));
        Invoke(nameof(ShowNextPaymentIfNeeded), nextPaymentDelay);
    }

    private void ShowNextPaymentIfNeeded()
    {
        if (!IsCashierDayActive())
            return;

        if (TutorialManager.Instance.CurrentPhase == TutorialManager.TutorialPhase.Complete)
            return;

        if (TutorialManager.Instance.CurrentPhase == TutorialManager.TutorialPhase.PracticeGameplay)
            ShowPaymentToken();
    }
}