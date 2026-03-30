using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TutorialManager))]
[RequireComponent(typeof(TutorialArrowManager))]
[DefaultExecutionOrder(200)]
public class TutorialWaiterGuidedDialogue : MonoBehaviour
{
    [Header("Behavior")]
    [SerializeField] private bool guidedFlowOnly = true;
    [SerializeField] private float duplicateCooldown = 0.4f;
    [SerializeField] private float fallbackDialogueDuration = 3.5f;

    private TutorialManager tutorialManager;
    private TutorialArrowManager arrowManager;
    private TutorialDialogueUI dialogueUI;

    private MethodInfo showAutoHintMethod;
    private MethodInfo showArrowMethod;

    private FieldInfo cashierMoneyTargetField;
    private FieldInfo currentTargetField;
    private FieldInfo currentArrowField;

    private CustomerGroup watchedGroup;
    private int watchedOrderNumber = -1;
    private TutorialManager.TutorialPhase lastPhase = TutorialManager.TutorialPhase.None;

    private bool serveFoodWaitHintShown;
    private bool servedFoodHintShown;
    private bool waitingForBillRequestHintShown;
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
        tutorialManager = GetComponent<TutorialManager>();
        arrowManager = GetComponent<TutorialArrowManager>();

        if (tutorialManager != null)
        {
            showAutoHintMethod = typeof(TutorialManager).GetMethod(
                "ShowAutoHint",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            FieldInfo dialogueField = typeof(TutorialManager).GetField(
                "dialogueUI",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (dialogueField != null)
                dialogueUI = dialogueField.GetValue(tutorialManager) as TutorialDialogueUI;
        }

        if (arrowManager != null)
        {
            showArrowMethod = typeof(TutorialArrowManager).GetMethod(
                "ShowArrow",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            cashierMoneyTargetField = typeof(TutorialArrowManager).GetField(
                "cashierMoneyTarget",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            currentTargetField = typeof(TutorialArrowManager).GetField(
                "currentTarget",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            currentArrowField = typeof(TutorialArrowManager).GetField(
                "currentArrow",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }

        HideArrowOverride();
    }

    private void OnDisable()
    {
        ResetTracking();
        HideArrowOverride();
    }

    private void Update()
    {
        if (!IsGuidedWaiterActive())
        {
            ResetTracking();
            HideArrowOverride();
            return;
        }

        RefreshWatchedGroup();

        if (watchedGroup == null && !ShouldKeepCashierGuidanceWithoutGroup())
        {
            HideArrowOverride();
            return;
        }

        HandleCurrentPhase();
    }

    private bool IsGuidedWaiterActive()
    {
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
        CustomerGroup current = tutorialManager.ActiveTutorialGroup;
        int currentOrderNo = current != null ? current.currentOrderNumber : -1;

        if (current != watchedGroup || currentOrderNo != watchedOrderNumber)
        {
            watchedGroup = current;
            watchedOrderNumber = currentOrderNo;
            ResetPhaseFlags();
            HideArrowOverride();
        }

        if (tutorialManager.CurrentPhase != lastPhase)
        {
            lastPhase = tutorialManager.CurrentPhase;
            ResetPhaseFlags();
            HideArrowOverride();
        }
    }

    private void ResetTracking()
    {
        watchedGroup = null;
        watchedOrderNumber = -1;
        lastPhase = TutorialManager.TutorialPhase.None;
        ResetPhaseFlags();
    }

    private void ResetPhaseFlags()
    {
        serveFoodWaitHintShown = false;
        servedFoodHintShown = false;
        waitingForBillRequestHintShown = false;
        billRequestHintShown = false;
        billReadyHintShown = false;
        billPickedHintShown = false;
        waitingForMoneyHintShown = false;
        moneyReadyHintShown = false;
        moneyPickedHintShown = false;
    }

    private bool ShouldKeepCashierGuidanceWithoutGroup()
    {
        if (tutorialManager == null)
            return false;

        if (tutorialManager.CurrentPhase != TutorialManager.TutorialPhase.CollectPayment)
            return false;

        var hands = WaiterHands.Instance;
        if (hands == null)
            return false;

        return hands.HasMoney;
    }

    private void HandleCurrentPhase()
    {
        switch (tutorialManager.CurrentPhase)
        {
            case TutorialManager.TutorialPhase.ServeFood:
                HandleServeFoodPhase();
                break;

            case TutorialManager.TutorialPhase.PickupBill:
                HandlePickupBillPhase();
                break;

            case TutorialManager.TutorialPhase.DeliverBill:
                HandleDeliverBillPhase();
                break;

            case TutorialManager.TutorialPhase.PickupMoney:
                HandlePickupMoneyPhase();
                break;

            case TutorialManager.TutorialPhase.CollectPayment:
                HandleCollectPaymentPhase();
                break;

            default:
                HideArrowOverride();
                break;
        }
    }

    private void HandleServeFoodPhase()
    {
        if (watchedGroup == null)
        {
            HideArrowOverride();
            return;
        }

        if (watchedGroup.state == CustomerGroup.GroupState.OrderTaken)
        {
            if (!serveFoodWaitHintShown)
            {
                serveFoodWaitHintShown = true;
                SendHint("The kitchen is working on the order now. Wait for the tray to appear, then deliver it to the correct table.");
            }

            return;
        }

        if (watchedGroup.state != CustomerGroup.GroupState.Eating)
            return;

        HideArrowOverride();

        if (servedFoodHintShown)
            return;

        servedFoodHintShown = true;
        SendHint("Excellent work. The food has been delivered. Stay with this table for a moment while the guests finish eating.");
    }

    private void HandlePickupBillPhase()
    {
        if (watchedGroup == null)
        {
            HideArrowOverride();
            return;
        }

        BillPaper bill = FindBillForGroup(watchedGroup);

        if (bill != null)
        {
            ShowArrowOverride(bill.transform);

            if (!billReadyHintShown)
            {
                billReadyHintShown = true;
                SendHint("The receipt is ready at the cashier station. Go there now and pick it up.");
            }

            return;
        }

        if (watchedGroup.state == CustomerGroup.GroupState.NeedsBill)
        {
            Transform target = watchedGroup.UIAnchor != null ? watchedGroup.UIAnchor : watchedGroup.transform;
            ShowArrowOverride(target);

            if (!billRequestHintShown)
            {
                billRequestHintShown = true;
                SendHint("The guests are ready to pay. Tap the bill request above the table so the cashier can prepare the receipt.");
            }

            return;
        }

        if (watchedGroup.state == CustomerGroup.GroupState.Eating)
        {
            HideArrowOverride();

            if (!waitingForBillRequestHintShown)
            {
                waitingForBillRequestHintShown = true;
                SendHint("The guests are still eating. Stay ready. Your next step is to request their bill once they finish.");
            }

            return;
        }

        HideArrowOverride();
    }

    private void HandleDeliverBillPhase()
    {
        if (watchedGroup == null)
        {
            HideArrowOverride();
            return;
        }

        var hands = WaiterHands.Instance;
        if (hands == null || !hands.HasBill || hands.holdingBillFor != watchedGroup)
        {
            HideArrowOverride();
            return;
        }

        Transform target = watchedGroup.UIAnchor != null ? watchedGroup.UIAnchor : watchedGroup.transform;
        ShowArrowOverride(target);

        if (!billPickedHintShown)
        {
            billPickedHintShown = true;
            SendHint("You have the receipt now. Bring it back to the same table and hand it to the guests.");
        }
    }

    private void HandlePickupMoneyPhase()
    {
        if (watchedGroup == null)
        {
            HideArrowOverride();
            return;
        }

        MoneyPickup money = FindMoneyForGroup(watchedGroup);

        if (money != null)
        {
            ShowArrowOverride(money.transform);

            if (!moneyReadyHintShown)
            {
                moneyReadyHintShown = true;
                SendHint("The payment is ready on the table. Tap the money bubble and collect the cash.");
            }

            return;
        }

        HideArrowOverride();

        if (!waitingForMoneyHintShown)
        {
            waitingForMoneyHintShown = true;
            SendHint("The receipt has been delivered. Wait by the table. Your next step is to collect the payment as soon as it appears.");
        }
    }

    private void HandleCollectPaymentPhase()
    {
        var hands = WaiterHands.Instance;
        if (hands == null || !hands.HasMoney)
        {
            HideArrowOverride();
            return;
        }

        Transform cashierTarget = GetCashierMoneyTarget();
        if (cashierTarget != null)
            ShowArrowOverride(cashierTarget);
        else
            HideArrowOverride();

        if (!moneyPickedHintShown)
        {
            moneyPickedHintShown = true;
            SendHint("Good. You are holding the payment now. Bring it to the cashier booth to complete the handoff.");
        }
    }

    private void SendHint(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (message == lastHint && Time.time - lastHintTime < duplicateCooldown)
            return;

        lastHint = message;
        lastHintTime = Time.time;

        if (tutorialManager != null && showAutoHintMethod != null)
        {
            showAutoHintMethod.Invoke(tutorialManager, new object[] { message });
            return;
        }

        if (dialogueUI != null)
        {
            dialogueUI.ShowAuto("Manager", message, fallbackDialogueDuration);
            return;
        }

        Debug.Log("[TutorialWaiterGuidedDialogue] " + message);
    }

    private void ShowArrowOverride(Transform target)
    {
        if (arrowManager == null || showArrowMethod == null || target == null)
            return;

        Transform currentTarget = currentTargetField != null
            ? currentTargetField.GetValue(arrowManager) as Transform
            : null;

        GameObject currentArrow = currentArrowField != null
            ? currentArrowField.GetValue(arrowManager) as GameObject
            : null;

        if (currentArrow != null && currentTarget == target)
            return;

        showArrowMethod.Invoke(arrowManager, new object[] { target });
    }

    private void HideArrowOverride()
    {
        if (arrowManager != null)
            arrowManager.ForceHide();
    }

    private Transform GetCashierMoneyTarget()
    {
        if (arrowManager == null || cashierMoneyTargetField == null)
            return null;

        return cashierMoneyTargetField.GetValue(arrowManager) as Transform;
    }

    private BillPaper FindBillForGroup(CustomerGroup group)
    {
        if (group == null)
            return null;

        BillPaper[] all = FindObjectsByType<BillPaper>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;

            if (BillMatchesGroup(all[i], group))
                return all[i];
        }

        return null;
    }

    private bool BillMatchesGroup(BillPaper bill, CustomerGroup group)
    {
        if (bill == null || group == null)
            return false;

        FieldInfo targetField = typeof(BillPaper).GetField(
            "targetGroup",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );

        if (targetField != null)
        {
            CustomerGroup owner = targetField.GetValue(bill) as CustomerGroup;
            if (owner == group)
                return true;
        }

        PropertyInfo targetProp = typeof(BillPaper).GetProperty(
            "TargetGroup",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );

        if (targetProp != null)
        {
            CustomerGroup owner = targetProp.GetValue(bill) as CustomerGroup;
            if (owner == group)
                return true;
        }

        return false;
    }

    private MoneyPickup FindMoneyForGroup(CustomerGroup group)
    {
        if (group == null)
            return null;

        MoneyPickup[] all = FindObjectsByType<MoneyPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].Matches(group))
                return all[i];
        }

        return null;
    }
}