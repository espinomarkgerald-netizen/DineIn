using UnityEngine;

[DefaultExecutionOrder(-150)]
public class TutorialWaiterRegisterGuard : MonoBehaviour
{
    [SerializeField] private bool autoCompleteCustomerLeave = true;
    [SerializeField] private bool registerPaymentCompletedInGameDay = true;
    [SerializeField] private bool debugLogs;

    private float lastResolveTime = -999f;

    private void Update()
    {
        if (!IsWaiterTutorialDay())
            return;

        if (CashierRegisterUI.Instance == null || !CashierRegisterUI.Instance.IsOpen)
            return;

        if (Time.time - lastResolveTime < 0.15f)
            return;

        lastResolveTime = Time.time;

        var hands = WaiterHands.Instance;
        CustomerGroup paidGroup = null;

        if (hands != null && hands.HasMoney)
        {
            paidGroup = hands.holdingMoneyFor;
            hands.ClearMoney();

            if (registerPaymentCompletedInGameDay)
                GameDayManager.Instance?.RegisterPaymentCompleted();

            if (autoCompleteCustomerLeave && paidGroup != null)
                paidGroup.PayAndLeave();

            if (TutorialManager.Instance != null)
                TutorialManager.Instance.OnMoneyGivenToCashier(paidGroup);

            if (debugLogs)
                Debug.Log("[TutorialWaiterRegisterGuard] POS was opened during Waiter day. Closed and resolved tutorial handoff.");
        }

        CashierRegisterUI.Instance.CloseRegister();
    }

    private bool IsWaiterTutorialDay()
    {
        if (!TutorialSceneRuntimeMarker.IsTutorialRuntimeActive)
            return false;

        if (TutorialManager.Instance == null || !TutorialManager.Instance.TutorialStarted)
            return false;

        return TutorialManager.Instance.CurrentDay == TutorialManager.TutorialDay.Day2Waiter;
    }
}