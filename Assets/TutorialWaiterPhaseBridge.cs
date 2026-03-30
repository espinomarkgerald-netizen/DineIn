using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class TutorialWaiterPhaseBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialManager tutorialManager;

    [Header("Arrows")]
    [SerializeField] private GameObject customerBillRequestArrow;
    [SerializeField] private GameObject cashierBillPickupArrow;
    [SerializeField] private GameObject customerMoneyArrow;
    [SerializeField] private GameObject cashierBoothArrow;

    [Header("Behavior")]
    [SerializeField] private bool guidedFlowOnly = true;
    [SerializeField] private bool hideArrowsWhenInactive = true;

    private MethodInfo showAutoHintMethod;

    private CustomerGroup watchedGroup;
    private int watchedOrderNumber = -1;

    private bool servedFoodHintShown;
    private bool billRequestHintShown;
    private bool billReadyHintShown;
    private bool billPickedHintShown;
    private bool waitingForMoneyHintShown;
    private bool moneyReadyHintShown;
    private bool moneyPickedHintShown;

    private string lastHint;
    private float lastHintTime = -999f;

    private void Awake()
    {
        if (tutorialManager == null)
            tutorialManager = GetComponent<TutorialManager>();

        if (tutorialManager != null)
        {
            showAutoHintMethod = typeof(TutorialManager).GetMethod(
                "ShowAutoHint",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }

        HideAllArrows();
    }

    private void OnDisable()
    {
        ResetGroupTracking();
        HideAllArrows();
    }

    private void Update()
    {
        if (!IsWaiterGuidedFlowActive())
        {
            ResetGroupTracking();

            if (hideArrowsWhenInactive)
                HideAllArrows();

            return;
        }

        RefreshWatchedGroup();

        if (watchedGroup == null)
        {
            HideAllArrows();
            return;
        }

        HandleWaiterSubSteps();
    }

    private bool IsWaiterGuidedFlowActive()
    {
        if (!TutorialSceneRuntimeMarker.IsTutorialRuntimeActive)
            return false;

        if (tutorialManager == null)
            return false;

        if (!tutorialManager.TutorialStarted)
            return false;

        if (tutorialManager.CurrentDay != TutorialManager.TutorialDay.Day2Waiter)
            return false;

        if (tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.Complete)
            return false;

        if (guidedFlowOnly && tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.PracticeGameplay)
            return false;

        return true;
    }

    private void RefreshWatchedGroup()
    {
        CustomerGroup current = tutorialManager != null ? tutorialManager.ActiveTutorialGroup : null;
        int currentOrderNo = current != null ? current.currentOrderNumber : -1;

        if (current != watchedGroup || currentOrderNo != watchedOrderNumber)
        {
            watchedGroup = current;
            watchedOrderNumber = currentOrderNo;

            servedFoodHintShown = false;
            billRequestHintShown = false;
            billReadyHintShown = false;
            billPickedHintShown = false;
            waitingForMoneyHintShown = false;
            moneyReadyHintShown = false;
            moneyPickedHintShown = false;

            HideAllArrows();
        }
    }

    private void ResetGroupTracking()
    {
        watchedGroup = null;
        watchedOrderNumber = -1;

        servedFoodHintShown = false;
        billRequestHintShown = false;
        billReadyHintShown = false;
        billPickedHintShown = false;
        waitingForMoneyHintShown = false;
        moneyReadyHintShown = false;
        moneyPickedHintShown = false;
    }

    private void HandleWaiterSubSteps()
    {
        if (watchedGroup == null)
            return;

        switch (tutorialManager.CurrentPhase)
        {
            case TutorialManager.TutorialPhase.ServeFood:
                HandleServeFoodGuidance();
                break;

            case TutorialManager.TutorialPhase.PickupBill:
                HandlePickupBillGuidance();
                break;

            case TutorialManager.TutorialPhase.DeliverBill:
                HandleDeliverBillGuidance();
                break;

            case TutorialManager.TutorialPhase.PickupMoney:
                HandlePickupMoneyGuidance();
                break;

            case TutorialManager.TutorialPhase.CollectPayment:
                HandleCollectPaymentGuidance();
                break;
        }
    }

    private void HandleServeFoodGuidance()
    {
        if (servedFoodHintShown)
            return;

        if (watchedGroup.state != CustomerGroup.GroupState.Eating)
            return;

        servedFoodHintShown = true;
        HideAllArrows();
        SendHint("Well done. The meal has been served. Give the guests a moment to finish eating.");
    }

    private void HandlePickupBillGuidance()
    {
        if (!billRequestHintShown && watchedGroup.state == CustomerGroup.GroupState.NeedsBill)
        {
            billRequestHintShown = true;
            ShowOnlyArrow(customerBillRequestArrow);
            SendHint("The guests are ready to settle their table. Tap the bill request above them so the cashier can prepare it.");
            return;
        }

        if (!billReadyHintShown && FindBillForGroup(watchedGroup) != null)
        {
            billReadyHintShown = true;
            ShowOnlyArrow(cashierBillPickupArrow);
            SendHint("The bill is ready at the cashier station. Collect it from the pickup point.");
        }
    }

    private void HandleDeliverBillGuidance()
    {
        var hands = WaiterHands.Instance;
        if (hands == null)
            return;

        if (billPickedHintShown)
            return;

        if (!hands.HasBill || hands.holdingBillFor != watchedGroup)
            return;

        billPickedHintShown = true;
        ShowOnlyArrow(customerBillRequestArrow);
        SendHint("Great. Bring the bill back to the same table and hand it to the guests.");
    }

    private void HandlePickupMoneyGuidance()
    {
        if (!waitingForMoneyHintShown)
        {
            waitingForMoneyHintShown = true;
            HideAllArrows();
            SendHint("The bill has been delivered. Stay by the table and watch for the payment bubble.");
            return;
        }

        if (!moneyReadyHintShown && FindMoneyForGroup(watchedGroup) != null)
        {
            moneyReadyHintShown = true;
            ShowOnlyArrow(customerMoneyArrow);
            SendHint("The payment is ready. Tap the money bubble at the table and collect the cash.");
        }
    }

    private void HandleCollectPaymentGuidance()
    {
        var hands = WaiterHands.Instance;
        if (hands == null)
            return;

        if (moneyPickedHintShown)
            return;

        if (!hands.HasMoney || hands.holdingMoneyFor != watchedGroup)
            return;

        moneyPickedHintShown = true;
        ShowOnlyArrow(cashierBoothArrow);
        SendHint("Perfect. Take the payment to the cashier booth to complete the handoff.");
    }

    private void SendHint(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (message == lastHint && Time.time - lastHintTime < 0.35f)
            return;

        lastHint = message;
        lastHintTime = Time.time;

        if (tutorialManager != null && showAutoHintMethod != null)
        {
            showAutoHintMethod.Invoke(tutorialManager, new object[] { message });
            return;
        }

        Debug.Log("[TutorialWaiterPhaseBridge] " + message);
    }

    private Component FindBillForGroup(CustomerGroup targetGroup)
    {
        if (targetGroup == null)
            return null;

        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            MonoBehaviour mb = all[i];
            if (mb == null) continue;
            if (mb.GetType().Name != "BillPaper") continue;

            CustomerGroup owner = ReadCustomerGroupMember(mb, "TargetGroup");
            if (owner == null)
                owner = ReadCustomerGroupMember(mb, "targetGroup");

            if (owner == targetGroup)
                return mb;
        }

        return null;
    }

    private Component FindMoneyForGroup(CustomerGroup targetGroup)
    {
        if (targetGroup == null)
            return null;

        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            MonoBehaviour mb = all[i];
            if (mb == null) continue;
            if (mb.GetType().Name != "MoneyPickup") continue;

            CustomerGroup owner = ReadCustomerGroupMember(mb, "TargetGroup");
            if (owner == null)
                owner = ReadCustomerGroupMember(mb, "targetGroup");

            if (owner == targetGroup)
                return mb;
        }

        return null;
    }

    private CustomerGroup ReadCustomerGroupMember(Component component, string memberName)
    {
        if (component == null)
            return null;

        Type type = component.GetType();

        PropertyInfo prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && typeof(CustomerGroup).IsAssignableFrom(prop.PropertyType))
            return prop.GetValue(component) as CustomerGroup;

        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && typeof(CustomerGroup).IsAssignableFrom(field.FieldType))
            return field.GetValue(component) as CustomerGroup;

        return null;
    }

    private void ShowOnlyArrow(GameObject target)
    {
        SetArrow(customerBillRequestArrow, target == customerBillRequestArrow);
        SetArrow(cashierBillPickupArrow, target == cashierBillPickupArrow);
        SetArrow(customerMoneyArrow, target == customerMoneyArrow);
        SetArrow(cashierBoothArrow, target == cashierBoothArrow);
    }

    private void HideAllArrows()
    {
        SetArrow(customerBillRequestArrow, false);
        SetArrow(cashierBillPickupArrow, false);
        SetArrow(customerMoneyArrow, false);
        SetArrow(cashierBoothArrow, false);
    }

    private void SetArrow(GameObject arrow, bool state)
    {
        if (arrow != null)
            arrow.SetActive(state);
    }
}