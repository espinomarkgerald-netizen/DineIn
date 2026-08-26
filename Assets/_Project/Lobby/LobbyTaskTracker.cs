using System;
using System.Reflection;
using UnityEngine;

public class LobbyTaskTracker : MonoBehaviour
{
    private const string GuidanceSource = "lobby-service";

    [Serializable]
    private struct TaskInfo
    {
        public string key;
        public string main;
        public string helper;
        public UnityEngine.Object target;

        public bool IsValid => !string.IsNullOrWhiteSpace(main);

        public TaskInfo(string key, string main, string helper, UnityEngine.Object target = null)
        {
            this.key = key;
            this.main = main;
            this.helper = helper;
            this.target = target;
        }
    }

    [Header("UI")]
    [SerializeField] private bool showHelperText = true;

    [Header("Scene Sources")]
    [SerializeField] private WaiterHands waiterHands;
    [SerializeField] private MonoBehaviour roleManager;
    [SerializeField] private MonoBehaviour cashierBooth;
    [SerializeField] private MonoBehaviour[] boothSources;

    [Header("Rules")]
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

    private float refreshTimer;
    private string currentTaskKey = string.Empty;
    private string currentTaskMain = string.Empty;
    private string currentTaskHelper = string.Empty;

    private void OnEnable()
    {
        ForceRefresh();
    }

    private void OnDisable()
    {
        PlayerTaskGuidance.ClearTask(GuidanceSource);
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
        TaskInfo task = ResolveTask();

        if (!task.IsValid)
        {
            PlayerTaskGuidance.ClearTask(GuidanceSource);
            if (!string.IsNullOrEmpty(currentTaskKey))
            {
                currentTaskKey = string.Empty;
                currentTaskMain = string.Empty;
                currentTaskHelper = string.Empty;
            }

            return;
        }

        if (task.key == currentTaskKey && task.main == currentTaskMain && task.helper == currentTaskHelper)
            return;

        currentTaskKey = task.key;
        currentTaskMain = task.main;
        currentTaskHelper = task.helper;

        PlayerTaskGuidance.SetTask(
            GuidanceSource,
            task.key,
            task.main,
            showHelperText ? task.helper : string.Empty,
            100,
            task.target,
            PlayerTaskCategory.Service);
    }

    private TaskInfo ResolveTask()
    {
        // Never use the scene's legacy waiterHands reference here: in Lobby1 it
        // points at the autonomous waiter. Guidance must only inspect the
        // permanent player-controlled Manager's inventory.
        WaiterHands hands = ManagerPlayer.Active != null
            ? ManagerPlayer.Active.GetComponent<WaiterHands>()
            : null;
        if (hands != null && hands.HasMoney)
        {
            CustomerGroup group = hands.HeldMoney != null ? hands.HeldMoney.TargetGroup : hands.holdingMoneyFor;
            int tableNumber = group != null ? group.currentOrderNumber : -1;
            return new TaskInfo(
                tableNumber > 0 ? "held_money_" + tableNumber : "held_money",
                "TAKE PAYMENT  >  CASHIER",
                tableNumber > 0 ? "FROM TABLE " + tableNumber : "PROCESS THE CUSTOMER PAYMENT",
                hands.HeldMoney != null ? hands.HeldMoney : group
            );
        }

        if (hands != null && hands.HasBill)
        {
            CustomerGroup group = hands.holdingBillFor;
            int tableNumber = group != null ? group.currentOrderNumber : -1;

            return new TaskInfo(
                tableNumber > 0 ? "held_bill_" + tableNumber : "held_bill",
                tableNumber > 0 ? "GIVE BILL  >  TABLE " + tableNumber : "GIVE BILL  >  CORRECT TABLE",
                "MATCH THE TABLE NUMBER",
                group
            );
        }

        if (hands != null && hands.HasTray)
        {
            FoodTray tray = hands.holdingTray;
            int tableNumber = tray != null ? tray.orderNumber : -1;

            return new TaskInfo(
                tableNumber > 0 ? "held_tray_" + tableNumber : "held_tray",
                tableNumber > 0 ? "DELIVER ORDER  >  TABLE " + tableNumber : "DELIVER ORDER  >  CORRECT TABLE",
                tableNumber > 0 ? "ORDER #" + tableNumber : "MATCH THE ORDER NUMBER",
                tray
            );
        }

        if (TakeoutBagInteractable.PlayerHasHeldBag)
        {
            TakeoutBagInteractable bag = TakeoutBagInteractable.HeldBag;
            int number = bag != null ? bag.OrderNumber : -1;
            return new TaskInfo(
                bag != null ? "deliver_takeout_" + bag.GetInstanceID() : "deliver_takeout",
                number > 0 ? "DELIVER TAKEOUT  >  ORDER #" + number : "DELIVER TAKEOUT ORDER",
                "FIND THE WAITING TAKEOUT CUSTOMER",
                bag);
        }

        UnityEngine.Object activeTarget = RestaurantTaskClaim.ActivePlayerTarget;
        if (activeTarget == null)
            return default;

        if (activeTarget is CustomerGroup groupTarget)
        {
            int number = groupTarget.currentOrderNumber;
            string table = number > 0 ? "TABLE " + number : "CUSTOMER GROUP";

            if (!groupTarget.hasBeenGreeted)
            {
                return new TaskInfo(
                    "greet_group_" + groupTarget.GetInstanceID(),
                    "GREET WAITING GROUP",
                    "MEET THEM AT THE RECEPTION AREA",
                    groupTarget);
            }

            if (groupTarget.IsReceptionClaimedByPlayer ||
                groupTarget.state == CustomerGroup.GroupState.Waiting)
            {
                return new TaskInfo(
                    "seat_group_" + groupTarget.GetInstanceID(),
                    "CHOOSE A TABLE",
                    "SEAT THE WAITING CUSTOMER GROUP",
                    groupTarget);
            }

            if (groupTarget.state == CustomerGroup.GroupState.ReadyToOrder ||
                groupTarget.state == CustomerGroup.GroupState.WaitingToOrder ||
                groupTarget.state == CustomerGroup.GroupState.Seated)
            {
                return new TaskInfo(
                    "take_order_" + groupTarget.GetInstanceID(),
                    "TAKE ORDER  >  " + table,
                    "CONFIRM THE CUSTOMER'S ORDER",
                    groupTarget);
            }

            if (groupTarget.state == CustomerGroup.GroupState.NeedsBill)
            {
                BillPaper paper = FindBillPaperFor(groupTarget);
                if (paper != null)
                {
                    return new TaskInfo(
                        "pickup_bill_" + groupTarget.GetInstanceID(),
                        "PICK UP BILL  >  CASHIER",
                        number > 0 ? "FOR TABLE " + number : "CHECK THE TABLE NUMBER",
                        groupTarget);
                }

                return new TaskInfo(
                    "request_bill_" + groupTarget.GetInstanceID(),
                    "REQUEST BILL  >  " + table,
                    "THEN PICK IT UP AT THE CASHIER",
                    groupTarget);
            }

            return new TaskInfo(
                "help_group_" + groupTarget.GetInstanceID(),
                "HELP  >  " + table,
                "FINISH THE CURRENT CUSTOMER STEP",
                groupTarget);
        }

        if (activeTarget is FoodTray trayTarget)
        {
            int number = trayTarget.orderNumber;
            return new TaskInfo(
                "pickup_tray_" + trayTarget.GetInstanceID(),
                "PICK UP ORDER  >  COUNTER",
                number > 0 ? "FOR TABLE " + number : "CHECK THE ORDER NUMBER",
                trayTarget);
        }

        if (activeTarget is MoneyPickup moneyTarget)
        {
            int number = moneyTarget.TargetGroup != null ? moneyTarget.TargetGroup.currentOrderNumber : -1;
            return new TaskInfo(
                "collect_money_" + moneyTarget.GetInstanceID(),
                number > 0 ? "COLLECT PAYMENT  >  TABLE " + number : "COLLECT CUSTOMER PAYMENT",
                "THEN TAKE IT TO THE CASHIER",
                moneyTarget);
        }

        if (activeTarget is Booth boothTarget)
        {
            return new TaskInfo(
                "clean_booth_" + boothTarget.GetInstanceID(),
                "CLEAN DIRTY TABLE",
                "HOLD THE CLEAN BUTTON UNTIL COMPLETE",
                boothTarget);
        }

        if (activeTarget is TakeoutBagInteractable takeoutTarget)
        {
            int number = takeoutTarget.OrderNumber;
            return new TaskInfo(
                "takeout_" + takeoutTarget.GetInstanceID(),
                number > 0 ? "PICK UP TAKEOUT  >  ORDER #" + number : "PICK UP TAKEOUT ORDER",
                "DELIVER IT TO THE WAITING CUSTOMER",
                takeoutTarget);
        }

        return default;
    }

    private static BillPaper FindBillPaperFor(CustomerGroup group)
    {
        if (group == null)
            return null;

        BillPaper[] papers = FindObjectsByType<BillPaper>(FindObjectsSortMode.None);
        for (int i = 0; i < papers.Length; i++)
        {
            BillPaper paper = papers[i];
            if (paper != null && paper.TargetGroup == group)
                return paper;
        }

        return null;
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
