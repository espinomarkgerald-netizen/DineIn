using System;
using System.Reflection;
using UnityEngine;

public class TutorialWaiterStepGuidance : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private MonoBehaviour dialoguePresenter;

    [Header("Arrow Objects")]
    [SerializeField] private GameObject customerTableArrow;
    [SerializeField] private GameObject cashierBillArrow;
    [SerializeField] private GameObject cashierBoothArrow;

    [Header("Dialogue")]
    [SerializeField] private string speakerName = "Manager";
    [SerializeField] private float dialogueDuration = 3.8f;
    [SerializeField] private bool guidedFlowOnly = true;

    private CustomerGroup watchedGroup;
    private int watchedOrderNumber = -1;

    private bool servedFoodShown;
    private bool needsBillShown;
    private bool billReadyShown;
    private bool billPickedShown;
    private bool moneyReadyShown;
    private bool moneyPickedShown;

    private string lastHint;
    private float lastHintTime = -999f;

    private void Awake()
    {
        if (tutorialManager == null)
            tutorialManager = GetComponent<TutorialManager>();

        HideAllArrows();
    }

    private void OnDisable()
    {
        HideAllArrows();
        ResetFlags();
    }

    private void Update()
    {
        if (!IsWaiterTutorialActive())
        {
            HideAllArrows();
            watchedGroup = null;
            watchedOrderNumber = -1;
            ResetFlags();
            return;
        }

        RefreshWatchedGroup();

        if (watchedGroup == null)
        {
            HideAllArrows();
            return;
        }

        HandleGuidance();
    }

    private bool IsWaiterTutorialActive()
    {
        if (!TutorialSceneRuntimeMarker.IsTutorialRuntimeActive)
            return false;

        if (tutorialManager == null)
            return false;

        if (!tutorialManager.TutorialStarted)
            return false;

        if (tutorialManager.CurrentDay != TutorialManager.TutorialDay.Day2Waiter)
            return false;

        if (guidedFlowOnly && tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.PracticeGameplay)
            return false;

        if (tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.Complete)
            return false;

        return true;
    }

    private void RefreshWatchedGroup()
    {
        CustomerGroup current = tutorialManager != null ? tutorialManager.ActiveTutorialGroup : null;
        int currentOrder = current != null ? current.currentOrderNumber : -1;

        if (current != watchedGroup || currentOrder != watchedOrderNumber)
        {
            watchedGroup = current;
            watchedOrderNumber = currentOrder;
            ResetFlags();
            HideAllArrows();
        }
    }

    private void HandleGuidance()
    {
        if (watchedGroup == null)
            return;

        if (!servedFoodShown && watchedGroup.state == CustomerGroup.GroupState.Eating)
        {
            servedFoodShown = true;
            HideAllArrows();
            ShowHint("Nicely done. The order has been delivered. Stay nearby while the guests finish their meal.");
            return;
        }

        if (!needsBillShown && watchedGroup.state == CustomerGroup.GroupState.NeedsBill)
        {
            needsBillShown = true;
            ShowOnlyArrow(customerTableArrow);
            ShowHint("The guests are ready to settle their table. Tap the bill request above them so the cashier can prepare it.");
            return;
        }

        if (!billReadyShown && FindBillForGroup(watchedGroup) != null)
        {
            billReadyShown = true;
            ShowOnlyArrow(cashierBillArrow != null ? cashierBillArrow : cashierBoothArrow);
            ShowHint("The bill is ready at the cashier station. Use the pickup button there to collect it.");
            return;
        }

        var hands = WaiterHands.Instance;

        if (!billPickedShown && hands != null && hands.HasBill && hands.holdingBillFor == watchedGroup)
        {
            billPickedShown = true;
            ShowOnlyArrow(customerTableArrow);
            ShowHint("Great. Bring the bill back to the same table and hand it to the guests.");
            return;
        }

        if (!moneyReadyShown && FindMoneyForGroup(watchedGroup) != null)
        {
            moneyReadyShown = true;
            ShowOnlyArrow(customerTableArrow);
            ShowHint("Payment is now available at the table. Collect the cash from the guests.");
            return;
        }

        if (!moneyPickedShown && hands != null && hands.HasMoney && hands.holdingMoneyFor == watchedGroup)
        {
            moneyPickedShown = true;
            ShowOnlyArrow(cashierBoothArrow);
            ShowHint("Perfect. Take the payment to the cashier booth to complete the handoff.");
            return;
        }

        if (moneyPickedShown && hands != null && !hands.HasMoney)
            HideAllArrows();
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

    private void ShowHint(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (message == lastHint && Time.time - lastHintTime < 0.35f)
            return;

        lastHint = message;
        lastHintTime = Time.time;

        if (dialoguePresenter == null)
            return;

        MethodInfo method = dialoguePresenter.GetType().GetMethod(
            "ShowAuto",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(string), typeof(string), typeof(float) },
            null
        );

        if (method != null)
        {
            method.Invoke(dialoguePresenter, new object[] { speakerName, message, dialogueDuration });
            return;
        }

        Debug.Log("[TutorialWaiterStepGuidance] " + message);
    }

    private void ResetFlags()
    {
        servedFoodShown = false;
        needsBillShown = false;
        billReadyShown = false;
        billPickedShown = false;
        moneyReadyShown = false;
        moneyPickedShown = false;
    }

    private void ShowOnlyArrow(GameObject arrowToShow)
    {
        SetArrow(customerTableArrow, arrowToShow == customerTableArrow);
        SetArrow(cashierBillArrow, arrowToShow == cashierBillArrow);
        SetArrow(cashierBoothArrow, arrowToShow == cashierBoothArrow);
    }

    private void HideAllArrows()
    {
        SetArrow(customerTableArrow, false);
        SetArrow(cashierBillArrow, false);
        SetArrow(cashierBoothArrow, false);
    }

    private void SetArrow(GameObject arrow, bool state)
    {
        if (arrow != null)
            arrow.SetActive(state);
    }
}