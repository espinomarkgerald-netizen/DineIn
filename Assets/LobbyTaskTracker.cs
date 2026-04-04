using System;
using System.Reflection;
using UnityEngine;

public class LobbyTaskTracker : MonoBehaviour
{
    [Serializable]
    private struct TaskInfo
    {
        public string key;
        public string main;
        public string helper;

        public bool IsValid => !string.IsNullOrWhiteSpace(main);

        public TaskInfo(string key, string main, string helper)
        {
            this.key = key;
            this.main = main;
            this.helper = helper;
        }
    }

    [Header("UI")]
    [SerializeField] private LobbyTaskUI taskUI;
    [SerializeField] private bool showHelperText = true;

    [Header("Scene Sources")]
    [SerializeField] private WaiterHands waiterHands;
    [SerializeField] private MonoBehaviour roleManager;
    [SerializeField] private MonoBehaviour cashierBooth;
    [SerializeField] private MonoBehaviour[] boothSources;

    [Header("Rules")]
    [SerializeField] private bool showOnlyWhenWaiterIsActive = true;
    [SerializeField] private string waiterRoleToken = "Waiter";
    [SerializeField] private string cashierLabelOverride = "cashier booth";
    [SerializeField] private float refreshInterval = 0.15f;

    [Header("Task State Tokens")]
    [SerializeField] private string[] takeOrderStateTokens = { "Seated" };
    [SerializeField] private string[] deliverOrderStateTokens = { "OrderTaken", "WaitingFood", "WaitingForFood" };
    [SerializeField] private string[] pickUpBillStateTokens = { "BillRequested", "WaitingBill", "NeedsBill" };
    [SerializeField] private string[] collectPaymentStateTokens = { "BillDelivered", "WaitingPayment", "PaymentReady" };

    [Header("Read Names - WaiterHands")]
    [SerializeField] private string[] trayMemberNames = { "holdingTray", "heldTray" };
    [SerializeField] private string[] billMemberNames = { "holdingBill", "heldBill" };
    [SerializeField] private string[] moneyMemberNames = { "holdingMoney", "heldMoney", "moneyPickup" };

    [Header("Read Names - Booth")]
    [SerializeField] private string[] boothCurrentGroupMemberNames = { "CurrentGroup", "currentGroup" };
    [SerializeField] private string[] boothTableNumberMemberNames = { "tableNumber", "TableNumber", "tableNo", "TableNo", "boothNumber", "BoothNumber" };

    [Header("Read Names - Group")]
    [SerializeField] private string[] groupStateMemberNames = { "state", "State", "groupState", "CurrentState" };
    [SerializeField] private string[] groupBoothMemberNames = { "AssignedBooth", "assignedBooth", "Booth", "booth" };

    [Header("Read Names - Held Objects")]
    [SerializeField] private string[] objectTableNumberMemberNames =
    {
        "TargetTableNumber", "targetTableNumber",
        "TableNumber", "tableNumber",
        "AssignedTableNumber", "assignedTableNumber",
        "TargetTable", "targetTable",
        "TableNo", "tableNo",
        "OrderNumber", "orderNumber"
    };

    [SerializeField] private string[] objectGroupMemberNames =
    {
        "TargetGroup", "targetGroup",
        "OwnerGroup", "ownerGroup",
        "Group", "group",
        "customerGroup", "CustomerGroup"
    };

    [SerializeField] private string[] objectBoothMemberNames =
    {
        "TargetBooth", "targetBooth",
        "Booth", "booth",
        "assignedBooth", "AssignedBooth"
    };

    [Header("Task Text")]
    [SerializeField] private string deliverMoneyText = "Deliver money to {0}";
    [SerializeField] private string deliverMoneyHint = "Bring the payment to the cashier booth so it can be processed.";

    [SerializeField] private string deliverBillText = "Deliver bill to Table {0}";
    [SerializeField] private string deliverBillFallbackText = "Deliver bill";
    [SerializeField] private string deliverBillHint = "Bring the bill to Table {0}.";
    [SerializeField] private string deliverBillHintFallback = "Bring the bill to the correct table.";

    [SerializeField] private string deliverOrderText = "Deliver order to Table {0}";
    [SerializeField] private string deliverOrderFallbackText = "Deliver order";
    [SerializeField] private string deliverOrderHint = "Bring the finished order to Table {0}.";
    [SerializeField] private string deliverOrderHintFallback = "Bring the finished order to the correct table.";

    [SerializeField] private string pendingOrderText = "Deliver order to Table {0}";
    [SerializeField] private string pendingOrderHint = "Watch the counter and bring the finished meal to Table {0}.";
    [SerializeField] private string pendingOrderHintFallback = "Watch the counter and bring the finished meal to the correct table.";

    [SerializeField] private string takeOrderText = "Take order from Table {0}";
    [SerializeField] private string takeOrderHint = "Approach Table {0} and confirm what they want.";
    [SerializeField] private string takeOrderHintFallback = "Approach the table and confirm what they want.";

    [SerializeField] private string pickUpBillText = "Pick up bill for Table {0}";
    [SerializeField] private string pickUpBillHint = "Get the bill for Table {0}, then bring it to the customer.";
    [SerializeField] private string pickUpBillHintFallback = "Get the bill, then bring it to the table.";

    [SerializeField] private string collectPaymentText = "Collect payment from Table {0}";
    [SerializeField] private string collectPaymentHint = "Pick up the money from Table {0}.";
    [SerializeField] private string collectPaymentHintFallback = "Pick up the money after the bill is delivered.";

    private float refreshTimer;
    private string currentTaskKey = string.Empty;
    private string currentTaskMain = string.Empty;
    private string currentTaskHelper = string.Empty;

    private void OnEnable()
    {
        ForceRefresh();
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshInterval)
            return;

        refreshTimer = 0f;
        RefreshTask();
    }

    public void ForceRefresh()
    {
        refreshTimer = 0f;
        RefreshTask();
    }

    private void RefreshTask()
    {
        if (taskUI == null)
            return;

        TaskInfo task = ResolveTask();

        if (!task.IsValid)
        {
            if (!string.IsNullOrEmpty(currentTaskKey))
            {
                currentTaskKey = string.Empty;
                currentTaskMain = string.Empty;
                currentTaskHelper = string.Empty;
                taskUI.HideTask();
            }

            return;
        }

        if (task.key == currentTaskKey && task.main == currentTaskMain && task.helper == currentTaskHelper)
            return;

        currentTaskKey = task.key;
        currentTaskMain = task.main;
        currentTaskHelper = task.helper;

        taskUI.ShowTask(task.main, showHelperText ? task.helper : string.Empty);
    }

    private TaskInfo ResolveTask()
    {
        if (showOnlyWhenWaiterIsActive && roleManager != null && !IsWaiterRoleActive())
            return default;

        UnityEngine.Object heldMoney = ReadFirstUnityObject(waiterHands, moneyMemberNames);
        if (heldMoney != null)
        {
            string cashierLabel = ResolveCashierLabel();
            return new TaskInfo(
                "held_money",
                FormatText(deliverMoneyText, cashierLabel, "Deliver money to cashier booth"),
                CleanText(deliverMoneyHint)
            );
        }

        UnityEngine.Object heldBill = ReadFirstUnityObject(waiterHands, billMemberNames);
        if (heldBill != null)
        {
            int tableNumber = ResolveHeldBillTable(heldBill);

            return new TaskInfo(
                tableNumber > 0 ? "held_bill_" + tableNumber : "held_bill",
                tableNumber > 0 ? FormatText(deliverBillText, tableNumber, deliverBillFallbackText) : CleanText(deliverBillFallbackText),
                FormatTableHint(deliverBillHint, tableNumber, deliverBillHintFallback)
            );
        }

        UnityEngine.Object heldTray = ReadFirstUnityObject(waiterHands, trayMemberNames);
        if (heldTray != null)
        {
            int tableNumber = ResolveHeldTrayTable(heldTray);

            return new TaskInfo(
                tableNumber > 0 ? "held_tray_" + tableNumber : "held_tray",
                tableNumber > 0 ? FormatText(deliverOrderText, tableNumber, deliverOrderFallbackText) : CleanText(deliverOrderFallbackText),
                FormatTableHint(deliverOrderHint, tableNumber, deliverOrderHintFallback)
            );
        }

        int collectPaymentTable = FindFirstTableWithState(collectPaymentStateTokens);
        if (collectPaymentTable > 0)
        {
            return new TaskInfo(
                "collect_payment_" + collectPaymentTable,
                FormatText(collectPaymentText, collectPaymentTable, "Collect payment"),
                FormatTableHint(collectPaymentHint, collectPaymentTable, collectPaymentHintFallback)
            );
        }

        int billPickupTable = FindFirstTableWithState(pickUpBillStateTokens);
        if (billPickupTable > 0)
        {
            return new TaskInfo(
                "pickup_bill_" + billPickupTable,
                FormatText(pickUpBillText, billPickupTable, "Pick up bill"),
                FormatTableHint(pickUpBillHint, billPickupTable, pickUpBillHintFallback)
            );
        }

        int pendingOrderTable = FindFirstTableWithState(deliverOrderStateTokens);
        if (pendingOrderTable > 0)
        {
            return new TaskInfo(
                "pending_order_" + pendingOrderTable,
                FormatText(pendingOrderText, pendingOrderTable, "Deliver order"),
                FormatTableHint(pendingOrderHint, pendingOrderTable, pendingOrderHintFallback)
            );
        }

        int takeOrderTable = FindFirstTableWithState(takeOrderStateTokens);
        if (takeOrderTable > 0)
        {
            return new TaskInfo(
                "take_order_" + takeOrderTable,
                FormatText(takeOrderText, takeOrderTable, "Take order"),
                FormatTableHint(takeOrderHint, takeOrderTable, takeOrderHintFallback)
            );
        }

        return default;
    }

    private int ResolveHeldTrayTable(object heldTray)
    {
        // Prefer the direct typed path: waiterHands.holdingTray.orderNumber — same source
        // used by FoodTrayInteractable's pickup button, so it is always authoritative.
        int tableNumber = ResolveTrayTableNumberDirect();
        if (tableNumber > 0)
            return tableNumber;

        // Reflection fallback for the held object itself.
        tableNumber = ResolveTableNumberFromObject(heldTray);
        if (tableNumber > 0)
            return tableNumber;

        // Last resort: infer from booth state.
        tableNumber = FindFirstTableWithState(deliverOrderStateTokens);
        if (tableNumber > 0)
            return tableNumber;

        return -1;
    }

    /// <summary>
    /// Reads the order/table number directly from waiterHands.holdingTray.orderNumber,
    /// the exact same field FoodTrayInteractable uses for its pickup button label.
    /// </summary>
    private int ResolveTrayTableNumberDirect()
    {
        if (waiterHands == null)
            return -1;

        FoodTray tray = waiterHands.holdingTray;
        if (tray == null)
            return -1;

        return tray.orderNumber > 0 ? tray.orderNumber : -1;
    }

    private int ResolveHeldBillTable(object heldBill)
    {
        int tableNumber = ResolveTableNumberFromObject(heldBill);
        if (tableNumber > 0)
            return tableNumber;

        tableNumber = FindFirstTableWithState(pickUpBillStateTokens);
        if (tableNumber > 0)
            return tableNumber;

        tableNumber = FindFirstTableWithState(collectPaymentStateTokens);
        if (tableNumber > 0)
            return tableNumber;

        return -1;
    }

    private int FindFirstTableWithState(string[] stateTokens)
    {
        if (boothSources == null || boothSources.Length == 0)
            return -1;

        for (int i = 0; i < boothSources.Length; i++)
        {
            MonoBehaviour booth = boothSources[i];
            if (booth == null)
                continue;

            object group = ReadFirstValue(booth, boothCurrentGroupMemberNames);
            if (group == null)
                continue;

            string stateToken = ReadStateToken(group);
            if (!MatchesAnyToken(stateToken, stateTokens))
                continue;

            int tableNumber = ResolveTableNumberFromBooth(booth);
            if (tableNumber > 0)
                return tableNumber;
        }

        return -1;
    }

    private bool IsWaiterRoleActive()
    {
        object roleValue = ReadFirstValue(
            roleManager,
            "CurrentRole",
            "currentRole",
            "SelectedRole",
            "selectedRole",
            "ActiveRole",
            "activeRole",
            "currentSelectedRole"
        );

        if (roleValue == null)
            return true;

        string roleToken = Normalize(roleValue.ToString());
        string waiterToken = Normalize(waiterRoleToken);

        return !string.IsNullOrEmpty(roleToken) && roleToken.Contains(waiterToken);
    }

    private string ResolveCashierLabel()
    {
        if (!string.IsNullOrWhiteSpace(cashierLabelOverride))
            return cashierLabelOverride;

        if (cashierBooth == null)
            return "cashier booth";

        return cashierBooth.gameObject.name;
    }

    private int ResolveTableNumberFromObject(object source)
    {
        if (source == null)
            return -1;

        int directTableNumber = ReadFirstInt(source, objectTableNumberMemberNames);
        if (directTableNumber > 0)
            return directTableNumber;

        object group = ReadFirstValue(source, objectGroupMemberNames);
        if (group != null)
        {
            object booth = ReadFirstValue(group, groupBoothMemberNames);
            int groupBoothTable = ResolveTableNumberFromBooth(booth);
            if (groupBoothTable > 0)
                return groupBoothTable;
        }

        object targetBooth = ReadFirstValue(source, objectBoothMemberNames);
        int boothTable = ResolveTableNumberFromBooth(targetBooth);
        if (boothTable > 0)
            return boothTable;

        return -1;
    }

    private int ResolveTableNumberFromBooth(object booth)
    {
        if (booth == null)
            return -1;

        return ReadFirstInt(booth, boothTableNumberMemberNames);
    }

    private string ReadStateToken(object source)
    {
        object value = ReadFirstValue(source, groupStateMemberNames);
        return value != null ? value.ToString() : string.Empty;
    }

    private string FormatTableHint(string template, int tableNumber, string fallback)
    {
        if (tableNumber > 0)
            return FormatText(template, tableNumber, fallback);

        return CleanText(fallback);
    }

    private string FormatText(string template, object value, string fallback)
    {
        string cleanedTemplate = CleanText(template);

        if (!string.IsNullOrWhiteSpace(cleanedTemplate))
        {
            try
            {
                return string.Format(cleanedTemplate, value);
            }
            catch
            {
            }
        }

        return CleanText(fallback);
    }

    private string CleanText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("{{0}}", "{0}");
    }

    private bool MatchesAnyToken(string value, string[] tokens)
    {
        string normalizedValue = Normalize(value);
        if (string.IsNullOrEmpty(normalizedValue) || tokens == null || tokens.Length == 0)
            return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = Normalize(tokens[i]);
            if (string.IsNullOrEmpty(token))
                continue;

            if (normalizedValue == token || normalizedValue.Contains(token))
                return true;
        }

        return false;
    }

    private string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    private UnityEngine.Object ReadFirstUnityObject(object source, string[] memberNames)
    {
        object value = ReadFirstValue(source, memberNames);
        return value as UnityEngine.Object;
    }

    private int ReadFirstInt(object source, string[] memberNames)
    {
        object value = ReadFirstValue(source, memberNames);
        if (value == null)
            return -1;

        if (value is int intValue)
            return intValue;

        if (value is Enum enumValue)
            return Convert.ToInt32(enumValue);

        return int.TryParse(value.ToString(), out int parsed) ? parsed : -1;
    }

    private object ReadFirstValue(object source, params string[] memberNames)
    {
        if (source == null || memberNames == null)
            return null;

        Type type = source.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < memberNames.Length; i++)
        {
            string name = memberNames[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null)
                return property.GetValue(source, null);

            FieldInfo field = type.GetField(name, flags);
            if (field != null)
                return field.GetValue(source);
        }

        return null;
    }
}