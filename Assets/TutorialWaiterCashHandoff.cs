using UnityEngine;

[DefaultExecutionOrder(-200)]
public class TutorialWaiterCashHandoff : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CashierBoothInteractable cashierBooth;

    [Header("Handoff")]
    [SerializeField] private float handoffRadius = 1.5f;
    [SerializeField] private bool usePlanarDistance = true;
    [SerializeField] private bool autoCompleteCustomerLeave = true;
    [SerializeField] private bool registerPaymentCompletedInGameDay = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private float lastHandoffTime = -999f;

    private void Awake()
    {
        if (cashierBooth == null)
            cashierBooth = GetComponent<CashierBoothInteractable>();
    }

    private void Update()
    {
        if (!IsWaiterTutorialDay())
            return;

        if (Time.time - lastHandoffTime < 0.15f)
            return;

        var hands = WaiterHands.Instance;
        if (hands == null || !hands.HasMoney)
            return;

        if (RoleManager.Instance == null || !RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
            return;

        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null)
            return;

        Transform targetPoint = cashierBooth != null ? cashierBooth.StandPoint : transform;

        Vector3 a = mover.transform.position;
        Vector3 b = targetPoint.position;

        if (usePlanarDistance)
        {
            a.y = 0f;
            b.y = 0f;
        }

        float dist = Vector3.Distance(a, b);
        if (dist > handoffRadius)
            return;

        CompleteWaiterTutorialHandoff(hands);
    }

    private bool IsWaiterTutorialDay()
    {
        if (!TutorialSceneRuntimeMarker.IsTutorialRuntimeActive)
            return false;

        if (TutorialManager.Instance == null || !TutorialManager.Instance.TutorialStarted)
            return false;

        return TutorialManager.Instance.CurrentDay == TutorialManager.TutorialDay.Day2Waiter;
    }

    private void CompleteWaiterTutorialHandoff(WaiterHands hands)
    {
        if (hands == null || !hands.HasMoney)
            return;

        lastHandoffTime = Time.time;

        CustomerGroup paidGroup = hands.holdingMoneyFor;

        if (debugLogs)
            Debug.Log("[TutorialWaiterCashHandoff] Completing waiter tutorial cash handoff.");

        hands.ClearMoney();

        if (registerPaymentCompletedInGameDay)
            GameDayManager.Instance?.RegisterPaymentCompleted();

        if (autoCompleteCustomerLeave && paidGroup != null)
            paidGroup.PayAndLeave();

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnMoneyGivenToCashier(paidGroup);

        if (CashierRegisterUI.Instance != null && CashierRegisterUI.Instance.IsOpen)
            CashierRegisterUI.Instance.CloseRegister();
    }
}