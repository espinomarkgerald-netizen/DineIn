using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class CustomerGroup : MonoBehaviour
{
    public enum GroupState
    {
        Spawning,
        WalkingToLobby,
        Waiting,
        WalkingToBooth,
        Seated,
        WaitingToOrder,
        ReadyToOrder,
        OrderTaken,
        Eating,
        NeedsBill,
        Leaving,
        AngryLeft,
        UnhappyLeft
    }

    public enum FoodType { Chicken, Fries, Burger }
    public enum DrinkType { Coke, Pineapple, IceTea }

    public enum FinalResult
    {
        None,
        Happy,
        Neutral,
        Angry
    }

    [Serializable]
    public class SimpleOrder
    {
        public string name;
        public int quantity = 1;
        public int unitPrice;
        public List<string> contents = new List<string>();

        public int TotalPrice => unitPrice * Mathf.Max(1, quantity);

        public string GetDisplayText()
        {
            return $"{quantity}x {name}";
        }

        public void Clear()
        {
            name = string.Empty;
            quantity = 1;
            unitPrice = 0;
            contents.Clear();
        }
    }

    [Header("Runtime")]
    public GroupState state = GroupState.Spawning;
    public List<CustomerAgent> members = new List<CustomerAgent>();

    [Header("Selection")]
    public bool isSelected;
    public GameObject selectionVisual;

    [Header("Order Bubble Warning")]
    [Tooltip("When timeLeft <= this, the order bubble starts shaking.")]
    public float shakeBeforeAngrySeconds = 1.5f;

    [Header("Payment UI")]
    [SerializeField] private GameObject moneyBubblePrefab;
    [SerializeField] private float moneyBubbleOffsetY = 2.2f;

    [Header("UI Prefabs")]
    public GameObject orderBubblePrefab;
    public GameObject billBubblePrefab;
    public GameObject tableNumberPrefab;

    [Header("Customer Thoughts")]
    [SerializeField] private GameObject thoughtBubblePrefab;
    [SerializeField] private Vector3 thoughtBubbleOffset = new Vector3(0f, 2.8f, 0f);
    [SerializeField] private float thoughtBubbleDuration = 1.5f;

    [Header("Mood Face Sprites")]
    [SerializeField] private Sprite happyFaceSprite;
    [SerializeField] private Sprite unhappyFaceSprite;
    [SerializeField] private Sprite angryFaceSprite;

    [Header("Happy Comments")]
    [SerializeField] private string[] happyComments =
    {
        "That was great!",
        "Nice service!",
        "Everything was perfect.",
        "We enjoyed it!",
        "We'll come back again."
    };

    [Header("Unhappy Comments")]
    [SerializeField] private string[] unhappyComments =
    {
        "We've been waiting too long.",
        "No one took our order.",
        "Let's just leave.",
        "This is taking forever.",
        "We're done waiting."
    };

    [Header("Angry Comments")]
    [SerializeField] private string[] angryComments =
    {
        "This isn't what we ordered!",
        "Wrong order!",
        "This service is terrible!",
        "That's not our food!",
        "Unbelievable."
    };

    [Header("UI Offsets")]
    public Vector3 bubbleOffset = new Vector3(0, 2.2f, 0);
    public Vector3 tableNumberOffset = new Vector3(0, 1.6f, 0);

    [Header("Order Timing")]
    public float minOrderDelay = 2f;
    public float maxOrderDelay = 5f;
    public float minOrderPatience = 5f;
    public float maxOrderPatience = 8f;

    [Header("Eating Timing")]
    public float minEatSeconds = 3f;
    public float maxEatSeconds = 5f;

    [Header("Legacy Sprites")]
    public Sprite billIcon;
    public Sprite chickenSprite;
    public Sprite friesSprite;
    public Sprite burgerSprite;
    public Sprite cokeSprite;
    public Sprite pineappleSprite;
    public Sprite iceTeaSprite;

    [Header("Leaving / Exit")]
    public Transform exitPoint;
    public float exitFormationSpacing = 0.8f;

    [Header("Remake")]
    [SerializeField] private float remakeBubbleDelay = 1.2f;
    [SerializeField] private int maxWrongDeliveriesBeforeLeave = 3;

    [Header("Simple Bundle Orders")]
    public SimpleOrder currentOrder = new SimpleOrder();

    [Header("Submitted Order")]
    public SimpleOrder submittedOrder = new SimpleOrder();

    [Header("Eating UI")]
    [SerializeField] private GameObject eatingBubblePrefab;
    [SerializeField] private Vector3 eatingBubbleOffset = new Vector3(0f, 2.4f, 0f);

private GameObject eatingBubbleInstance;

    private bool waitingForRemake;
    private bool angryResultLocked;
    private bool firstDeliveryCompleted;
    private int wrongDeliveryCount;

    [HideInInspector] public Booth assignedBooth;

    public event Action<CustomerGroup> OnGroupAssignedToBooth;
    public event Action<CustomerGroup> OnGroupSeated;

    public FoodType chosenFood;
    public DrinkType chosenDrink;
    public FoodType confirmedFood;
    public DrinkType confirmedDrink;

    public int currentOrderNumber = -1;
    public int Size => members.Count;

    private bool hasConfirmedOrder;
    private bool hasBeenAssigned;
    private bool cleanupDone;
    private bool boothSeatsCleared;
    private bool leavingRoutineStarted;
    private bool isOrderPaused;

    private bool receivedWrongOrder;
    private bool shouldShowAngryThoughtOnLeave;

    private bool finalResultReported;
    private FinalResult finalResult = FinalResult.None;

    private readonly HashSet<CustomerAgent> seatedMembers = new HashSet<CustomerAgent>();
    private Coroutine seatingRoutine;
    private Coroutine thoughtRoutine;

    private Canvas gameplayCanvas;
    private Transform groupUiAnchor;

    private GameObject orderBubbleInstance;
    private GameObject billBubbleInstance;
    private GameObject tableNumberInstance;
    private GameObject moneyBubbleInstance;
    private GameObject thoughtBubbleInstance;

    public bool HasBeenAssigned => hasBeenAssigned;
    public Transform UIAnchor => groupUiAnchor;

    private int pendingPaymentAmount;

    [HideInInspector] public bool hasBeenGreeted = false;

    public void SetOrderPause(bool paused) => isOrderPaused = paused;

    private void Awake()
    {
        ResolveCanvas();
        BuildGroupUIAnchor();
        ResolveExitPoint();

        if (currentOrder == null)
            currentOrder = new SimpleOrder();
    }

    private void OnDestroy()
    {
        CleanupOnLeave();
    }

    private void LateUpdate()
    {
        if (groupUiAnchor != null)
            groupUiAnchor.position = GetMembersCenterWorld();
    }

    private void SetState(GroupState newState)
    {
        if (state == newState) return;
        state = newState;
        Debug.Log($"[CustomerGroup] {name} -> {state}");
    }

    private void ResolveCanvas()
    {
        if (gameplayCanvas != null) return;

        gameplayCanvas = UIRoot.GameplayCanvasOrNull();

        if (gameplayCanvas == null)
            gameplayCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        if (gameplayCanvas == null)
            Debug.LogError("[CustomerGroup] No Canvas found.");
    }

    private Camera GetFollowCam()
    {
        var cam = UIRoot.GameplayCameraOrNull();
        return cam != null ? cam : Camera.main;
    }

    private void ResolveExitPoint()
    {
        if (exitPoint != null) return;

        exitPoint = ExitManager.ExitPointOrNull();
        if (exitPoint != null) return;

        GameObject tagged = null;
        try { tagged = GameObject.FindGameObjectWithTag("ExitPoint"); } catch { }

        if (tagged != null)
        {
            exitPoint = tagged.transform;
            return;
        }

        GameObject named = GameObject.Find("ExitPoint");
        if (named != null)
        {
            exitPoint = named.transform;
            return;
        }

        Debug.LogWarning("No ExitPoint found.");
    }

    private void BuildGroupUIAnchor()
    {
        if (groupUiAnchor != null) return;

        GameObject anchor = new GameObject("GroupUIAnchor");
        groupUiAnchor = anchor.transform;
        groupUiAnchor.SetParent(transform, false);
        groupUiAnchor.position = GetMembersCenterWorld();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionVisual != null)
            selectionVisual.SetActive(selected);
    }

    public void AssignToBooth(Booth booth)
    {
        if (booth == null || hasBeenAssigned) return;

        hasBeenAssigned = true;
        assignedBooth = booth;
        seatedMembers.Clear();
        SetState(GroupState.WalkingToBooth);

        OnGroupAssignedToBooth?.Invoke(this);

        if (seatingRoutine != null)
            StopCoroutine(seatingRoutine);

        seatingRoutine = StartCoroutine(SeatMembersFlow());

        assignedBooth.SetCurrentGroup(this);
    }

    private IEnumerator SeatMembersFlow()
    {
        if (assignedBooth == null)
            yield break;

        Vector3[] seatTargets = new Vector3[members.Count];

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null) continue;

            Transform seat = assignedBooth.GetSeat(i);
            if (seat == null) continue;

            seatTargets[i] = seat.position;
            member.WalkTo(seatTargets[i]);
        }

        while (seatedMembers.Count < members.Count)
        {
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || seatedMembers.Contains(member)) continue;

                Transform seat = assignedBooth.GetSeat(i);
                if (seat == null) continue;

                if (member.HasArrived(seatTargets[i]) && SeatAnchor.TryOccupy(seat, member.gameObject))
                {
                    Quaternion rot = assignedBooth.GetSeatedRotation(seat.position);
                    member.SnapToSeat(seat.position, rot);
                    seatedMembers.Add(member);
                }
            }

            yield return null;
        }

        SetState(GroupState.Seated);
        SetSelected(false);

        OnGroupSeated?.Invoke(this);
        GameDayManager.Instance?.RegisterGroupSeated();

        if (assignedBooth != null)
            assignedBooth.SpawnMenuBook();

        StartCoroutine(ReadyToOrderFlow());
    }

    private IEnumerator ReadyToOrderFlow()
    {
        SetState(GroupState.WaitingToOrder);

        float delay = UnityEngine.Random.Range(minOrderDelay, maxOrderDelay);
        yield return new WaitForSeconds(delay);

        GenerateRandomOrder();

        if (currentOrderNumber < 0)
        {
            currentOrderNumber = OrderNumberManager.Instance != null
                ? OrderNumberManager.Instance.GetNextOrderNumber()
                : UnityEngine.Random.Range(100, 999);
        }

        SetState(GroupState.ReadyToOrder);
        SpawnOrderBubble();

        float patience = UnityEngine.Random.Range(minOrderPatience, maxOrderPatience);
        float timeLeft = patience;

        OrderBubbleUI bubbleUI = orderBubbleInstance != null
            ? orderBubbleInstance.GetComponentInChildren<OrderBubbleUI>(true)
            : null;

        UIShake shaker = orderBubbleInstance != null
            ? orderBubbleInstance.GetComponentInChildren<UIShake>(true)
            : null;

        bool startedShake = false;

        while (state == GroupState.ReadyToOrder)
        {
            if (!isOrderPaused)
            {
                timeLeft -= Time.deltaTime;

                if (bubbleUI != null)
                    bubbleUI.SetPatience(Mathf.Clamp01(timeLeft / patience));
            }

            if (!startedShake && timeLeft <= shakeBeforeAngrySeconds)
            {
                startedShake = true;
                if (shaker != null) shaker.StartShake();
            }

            if (timeLeft <= 0f)
            {
                if (shaker != null) shaker.StopShake(true);
                BecomeUnhappyAndLeave();
                yield break;
            }

            yield return null;
        }
    }

    private void GenerateRandomOrder()
    {
        ResetOrderFlags();

        if (submittedOrder != null)
            submittedOrder.Clear();

        GenerateSimpleBundleOrder();
        SyncLegacyOrderFieldsFromCurrentOrder();
    }

    private void ResetOrderFlags()
    {
        hasConfirmedOrder = false;
        receivedWrongOrder = false;
        waitingForRemake = false;
        angryResultLocked = false;
        shouldShowAngryThoughtOnLeave = false;
        firstDeliveryCompleted = false;
        wrongDeliveryCount = 0;
    }

    private void GenerateSimpleBundleOrder()
    {
        if (currentOrder == null)
            currentOrder = new SimpleOrder();

        currentOrder.Clear();

        int random = UnityEngine.Random.Range(0, 6);

        switch (random)
        {
            // Solo food
            case 0:
                currentOrder.name = "Chicken";
                currentOrder.unitPrice = 299;
                currentOrder.contents.Add("Chicken");
                break;

            case 1:
                currentOrder.name = "Fries";
                currentOrder.unitPrice = 79;
                currentOrder.contents.Add("Fries");
                break;

            case 2:
                currentOrder.name = "Burger";
                currentOrder.unitPrice = 119;
                currentOrder.contents.Add("Burger");
                break;

            // Bundle food
            case 3:
                currentOrder.name = "Chicken + Fries";
                currentOrder.unitPrice = 375;
                currentOrder.contents.Add("Chicken");
                currentOrder.contents.Add("Fries");
                break;

            case 4:
                currentOrder.name = "Chicken + Burger";
                currentOrder.unitPrice = 415;
                currentOrder.contents.Add("Chicken");
                currentOrder.contents.Add("Burger");
                break;

            case 5:
                currentOrder.name = "Burger + Fries";
                currentOrder.unitPrice = 195;
                currentOrder.contents.Add("Burger");
                currentOrder.contents.Add("Fries");
                break;
        }

        // ALWAYS add a drink
        currentOrder.contents.Add(GetRandomDrinkName());

        currentOrder.quantity = 1;
    }

    private string GetRandomDrinkName()
    {
        int r = UnityEngine.Random.Range(0, 3);

        switch (r)
        {
            case 0: return "Coke";
            case 1: return "Pineapple";
            case 2: return "Ice Tea";
        }

        return "Coke";
    }

    private void SyncLegacyOrderFieldsFromCurrentOrder()
    {
        if (currentOrder == null || currentOrder.contents.Count == 0)
        {
            chosenFood = FoodType.Chicken;
            chosenDrink = DrinkType.Coke;
            confirmedFood = chosenFood;
            confirmedDrink = chosenDrink;
            return;
        }

        if (currentOrder.contents.Contains("Burger"))
            chosenFood = FoodType.Burger;
        else if (currentOrder.contents.Contains("Fries"))
            chosenFood = FoodType.Fries;
        else
            chosenFood = FoodType.Chicken;

        bool hasDrink = false;
        chosenDrink = DrinkType.Coke;

        foreach (var item in currentOrder.contents)
        {
            if (item == "Coke")
            {
                chosenDrink = DrinkType.Coke;
                hasDrink = true;
            }
            else if (item == "Pineapple")
            {
                chosenDrink = DrinkType.Pineapple;
                hasDrink = true;
            }
            else if (item == "Ice Tea")
            {
                chosenDrink = DrinkType.IceTea;
                hasDrink = true;
            }
        }

        if (!hasDrink)
            chosenDrink = DrinkType.Coke;

        hasConfirmedOrder = false;
    }

    public string GetCurrentOrderSummary()
    {
        if (currentOrder == null)
            return "No Order";

        string result = "";

        for (int i = 0; i < currentOrder.contents.Count; i++)
        {
            result += currentOrder.contents[i];

            if (i < currentOrder.contents.Count - 1)
                result += ", ";
        }

        return result;
    }

    public List<string> GetCurrentOrderContents()
    {
        if (currentOrder == null)
            return new List<string>();

        return new List<string>(currentOrder.contents);
    }

    private bool CurrentOrderHasDrink()
    {
        if (currentOrder == null) return false;

        for (int i = 0; i < currentOrder.contents.Count; i++)
        {
            string item = currentOrder.contents[i];
            if (item == "Coke" || item == "Pineapple" || item == "Ice Tea")
                return true;
        }

        return false;
    }

    private bool IsCorrectDeliveredOrder(List<string> deliveredContents)
    {
        if (currentOrder == null || currentOrder.contents == null)
            return false;

        if (deliveredContents == null)
            return false;

        // Count must match
        if (currentOrder.contents.Count != deliveredContents.Count)
            return false;

        // Compare ignoring order
        List<string> expected = new List<string>(currentOrder.contents);
        List<string> delivered = new List<string>(deliveredContents);

        expected.Sort();
        delivered.Sort();

        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i] != delivered[i])
                return false;
        }

        return true;
    }

    private int GetOrderTotal()
    {
        if (currentOrder == null)
            return 0;

        int total = currentOrder.unitPrice * Mathf.Max(1, currentOrder.quantity);

        for (int i = 0; i < currentOrder.contents.Count; i++)
        {
            string item = currentOrder.contents[i];

            if (item == "Coke" || item == "Pineapple" || item == "Ice Tea")
                total += 39 * Mathf.Max(1, currentOrder.quantity);
        }

        return total;
    }

    private void SpawnOrderBubble()
    {
        if (orderBubblePrefab == null)
        {
            Debug.LogWarning($"[CustomerGroup] orderBubblePrefab missing on {name}");
            return;
        }

        ResolveCanvas();
        if (gameplayCanvas == null)
        {
            Debug.LogWarning($"[CustomerGroup] gameplayCanvas missing on {name}");
            return;
        }

        ClearOrderBubble();

        orderBubbleInstance = Instantiate(orderBubblePrefab, gameplayCanvas.transform);
        orderBubbleInstance.name = $"{name}_OrderBubble";
        orderBubbleInstance.SetActive(true);
        orderBubbleInstance.transform.SetAsLastSibling();

        RectTransform rootRect = orderBubbleInstance.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.localScale = Vector3.one;
            rootRect.anchoredPosition3D = Vector3.zero;
        }

        CanvasGroup[] canvasGroups = orderBubbleInstance.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < canvasGroups.Length; i++)
        {
            canvasGroups[i].alpha = 1f;
            canvasGroups[i].interactable = true;
            canvasGroups[i].blocksRaycasts = true;
        }

        Image[] images = orderBubbleInstance.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
            images[i].enabled = true;

        var follow = orderBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
        {
            follow.enabled = true;
            follow.Init(groupUiAnchor, bubbleOffset, GetFollowCam());
        }
        else
        {
            Debug.LogWarning($"[CustomerGroup] UIFollowWorldPoint missing on order bubble prefab for {name}");
        }

        var ui = orderBubbleInstance.GetComponentInChildren<OrderBubbleUI>(true);
        if (ui != null)
        {
            ui.gameObject.SetActive(true);
            ui.Init(this);
            ui.SetAlert();
            ui.SetPatience(1f);
        }
        else
        {
            Debug.LogWarning($"[CustomerGroup] OrderBubbleUI missing on order bubble prefab for {name}");
        }

        Canvas.ForceUpdateCanvases();

        Debug.Log($"[CustomerGroup] Spawned order alert bubble for {name} | order={GetCurrentOrderSummary()}");
    }

    public void TakeOrderFromWaiter(FoodType food, DrinkType drink)
    {
        if (state != GroupState.ReadyToOrder) return;

        ConfirmOrder(food, drink);

        if (orderBubbleInstance != null)
        {
            var shaker = orderBubbleInstance.GetComponentInChildren<UIShake>(true);
            if (shaker != null) shaker.StopShake(true);
        }


        SetState(GroupState.OrderTaken);

        ClearOrderBubble();
        SpawnTableNumber();

        if (!waitingForRemake)
            GameDayManager.Instance?.RegisterOrderTaken();

        if (OrderFlowManager.Instance != null)
            OrderFlowManager.Instance.SpawnTicket(this);

        waitingForRemake = false;
    }

    public void ConfirmOrder(FoodType food, DrinkType drink)
    {
        confirmedFood = food;
        confirmedDrink = drink;
        hasConfirmedOrder = true;
    }

    

    private void SpawnTableNumber()
    {
        if (tableNumberPrefab == null) return;
        ResolveCanvas();
        if (gameplayCanvas == null) return;

        ClearTableNumber();

        tableNumberInstance = Instantiate(tableNumberPrefab, gameplayCanvas.transform);

        Transform anchor = assignedBooth != null && assignedBooth.tableNumberAnchor != null
            ? assignedBooth.tableNumberAnchor
            : groupUiAnchor;

        var follow = tableNumberInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(anchor, tableNumberOffset, GetFollowCam());

        var num = tableNumberInstance.GetComponentInChildren<TableNumberUI>(true);
        if (num != null)
        {
            num.SetNumber(currentOrderNumber);
            num.SetBooth(assignedBooth);
        }
    }

    private IEnumerator ShowRemakeOrderAfterDelay()
    {
        yield return new WaitForSeconds(remakeBubbleDelay);

        if (state != GroupState.ReadyToOrder)
            yield break;

        SpawnOrderBubble();
    }

    public void ReceiveFoodFromWaiter(List<string> deliveredContents)
    {
        if (state != GroupState.OrderTaken)
            return;

        bool isCorrectOrder = IsCorrectDeliveredOrder(deliveredContents);

        if (assignedBooth != null)
            assignedBooth.ClearMenuBook();

        ClearTableNumber();

        firstDeliveryCompleted = true;

        if (!isCorrectOrder)
        {
            HandleWrongDelivery();
            return;
        }

        waitingForRemake = false;
        SetState(GroupState.Eating);
        SpawnEatingBubble();

        GameDayManager.Instance?.RegisterFoodDelivered();
        StartCoroutine(EatThenNeedBill());
    }

    public void ReceiveWrongFoodFromWaiter()
    {
        if (state != GroupState.OrderTaken && state != GroupState.Eating)
            return;

        HandleWrongDelivery();
    }

    private void HandleWrongDelivery()
    {
        receivedWrongOrder = true;
        waitingForRemake = true;
        shouldShowAngryThoughtOnLeave = true;
        wrongDeliveryCount++;

        if (!angryResultLocked)
        {
            angryResultLocked = true;
            ReportFinalResult(FinalResult.Angry);
        }

        if (wrongDeliveryCount >= maxWrongDeliveriesBeforeLeave)
        {
            ShowThought(angryComments, angryFaceSprite);
            SetState(GroupState.AngryLeft);

            ClearOrderBubble();
            ClearBillBubble();
            ClearTableNumber();
            ClearMoneyBubble();
            ClearEatingBubble();

            StartLeaving(false);
            return;
        }

        if (assignedBooth != null)
            assignedBooth.ClearMenuBook();

        ClearTableNumber();
        SetState(GroupState.ReadyToOrder);
        ShowThought(angryComments, angryFaceSprite);

        ClearOrderBubble();
        StartCoroutine(ShowRemakeOrderAfterDelay());
    }

    public void ReceiveBillFromWaiter()
    {
        if (state != GroupState.NeedsBill) return;

        ClearBillBubble();
        GameDayManager.Instance?.RegisterBillDelivered();
        StartCoroutine(SpawnMoneyBubbleAfterDelay());
    }

    public void RequestBillFromCashier()
    {
        if (state != GroupState.NeedsBill) return;
        if (BillManager.Instance == null) return;

        BillManager.Instance.RequestBill(this);
    }

    private IEnumerator EatThenNeedBill()
    {
        float eat = UnityEngine.Random.Range(minEatSeconds, maxEatSeconds);
        yield return new WaitForSeconds(eat);

        ClearEatingBubble();
        SetState(GroupState.NeedsBill);
        SpawnBillBubble();
    }

    private IEnumerator SpawnMoneyBubbleAfterDelay()
    {
        yield return new WaitForSeconds(0.6f);

        if (state != GroupState.NeedsBill) yield break;
        if (moneyBubblePrefab == null) yield break;
        if (assignedBooth == null) yield break;

        int total = GetOrderTotal();
        int amount = GetCustomerPaymentAmount(total);

        pendingPaymentAmount = amount;

        var spawner = assignedBooth.GetComponent<BoothMoneySpawner>();
        if (spawner == null) yield break;

        var money = spawner.SpawnMoney(this, amount, null);
        if (money == null) yield break;

        ResolveCanvas();
        if (gameplayCanvas == null) yield break;

        ClearMoneyBubble();

        moneyBubbleInstance = Instantiate(moneyBubblePrefab, gameplayCanvas.transform);

        var follow = moneyBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
        {
            Vector3 offset = bubbleOffset;
            offset.y = moneyBubbleOffsetY;
            follow.Init(groupUiAnchor, offset, GetFollowCam());
        }

        var ui = moneyBubbleInstance.GetComponentInChildren<MoneyBubbleUI>(true);
        if (ui != null)
            ui.Init(amount, money);
    }

    private int GetCustomerPaymentAmount(int total)
    {
        int[] validAmounts = { 1, 5, 10, 20, 50, 100, 200, 500, 1000 };

        for (int i = 0; i < validAmounts.Length; i++)
        {
            if (validAmounts[i] == total)
                return total;
        }

        for (int i = 0; i < validAmounts.Length; i++)
        {
            if (validAmounts[i] > total)
                return validAmounts[i];
        }

        return total;
    }

    public void PayAndLeave()
    {
        if (state != GroupState.NeedsBill) return;

        if (angryResultLocked || receivedWrongOrder)
        {
            ShowThought(angryComments, angryFaceSprite);
        }
        else
        {
            ReportFinalResult(FinalResult.Happy);
            ShowThought(happyComments, happyFaceSprite);
        }
       
        ClearEatingBubble();
        SetState(GroupState.Leaving);
        StartLeaving(false);
    }

    public bool IsWaitingForRemake()
    {
        return waitingForRemake;
    }

    private void BecomeUnhappyAndLeave()
    {
        ReportFinalResult(FinalResult.Neutral);
        ShowThought(unhappyComments, unhappyFaceSprite);

        SetState(GroupState.UnhappyLeft);

        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();
        ClearMoneyBubble();
        ClearEatingBubble();

        StartLeaving(false);
    }

    private void StartLeaving(bool unused)
    {
        if (leavingRoutineStarted) return;
        leavingRoutineStarted = true;

        if (shouldShowAngryThoughtOnLeave && state == GroupState.Leaving)
            ShowThought(angryComments, angryFaceSprite);

        ResolveExitPoint();

        if (exitPoint == null)
        {
            CleanupOnLeave();
            Destroy(gameObject);
            return;
        }

        CleanupSeatsAndBoothOnly();

        if (assignedBooth != null)
            assignedBooth.ClearCurrentGroup();

        StartCoroutine(LeaveToExitFlow());
    }

    private IEnumerator LeaveToExitFlow()
    {
        if (assignedBooth == null)
        {
            CleanupOnLeave();
            Destroy(gameObject);
            yield break;
        }

        Transform approach = assignedBooth.approachPoint;
        if (approach == null)
        {
            CleanupOnLeave();
            Destroy(gameObject);
            yield break;
        }

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null) continue;

            member.Unseat();

            if (member.Agent != null) member.Agent.Warp(approach.position);
            else member.transform.position = approach.position;
        }

        yield return null;

        Vector3 baseExit = exitPoint.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(baseExit, out hit, 3f, NavMesh.AllAreas))
            baseExit = hit.position;

        Vector3 forward = baseExit - approach.position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3[] targets = new Vector3[members.Count];

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null) continue;

            Vector3 offset = Vector3.zero;
            if (i == 1) offset = right * 0.6f;
            else if (i == 2) offset = -right * 0.6f;
            else if (i == 3) offset = -forward * 0.6f;

            targets[i] = baseExit + offset;

            if (NavMesh.SamplePosition(targets[i], out hit, 2f, NavMesh.AllAreas))
                targets[i] = hit.position;

            member.WalkTo(targets[i]);
        }

        float timeout = 12f;
        float t = 0f;

        while (t < timeout)
        {
            bool allArrived = true;

            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null) continue;

                if (!member.HasArrived(targets[i]))
                {
                    allArrived = false;
                    break;
                }
            }

            if (allArrived) break;

            t += Time.deltaTime;
            yield return null;
        }

        CleanupOnLeave();
        Destroy(gameObject);
    }

    private void SpawnBillBubble()
    {
        if (billBubblePrefab == null) return;
        ResolveCanvas();
        if (gameplayCanvas == null) return;

        ClearBillBubble();

        billBubbleInstance = Instantiate(billBubblePrefab, gameplayCanvas.transform);

        var follow = billBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(groupUiAnchor, bubbleOffset, GetFollowCam());

        var ui = billBubbleInstance.GetComponentInChildren<BillBubbleUI>(true);
        if (ui != null)
            ui.Init(this);
    }

    private void ShowThought(string[] comments, Sprite faceSprite)
    {
        if (thoughtBubblePrefab == null) return;
        ResolveCanvas();
        if (gameplayCanvas == null) return;

        string message = GetRandomComment(comments);
        if (string.IsNullOrWhiteSpace(message)) return;

        if (thoughtRoutine != null)
            StopCoroutine(thoughtRoutine);

        ClearThoughtBubble();

        thoughtBubbleInstance = Instantiate(thoughtBubblePrefab, gameplayCanvas.transform);

        var follow = thoughtBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(groupUiAnchor, thoughtBubbleOffset, GetFollowCam());

        TMP_Text text = thoughtBubbleInstance.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = message;

        Transform moodsHolder = FindChildRecursive(thoughtBubbleInstance.transform, "Moods");
        if (moodsHolder != null)
        {
            Image moodImage = moodsHolder.GetComponent<Image>();

            if (moodImage == null)
                moodImage = moodsHolder.GetComponentInChildren<Image>(true);

            if (moodImage != null)
            {
                moodImage.sprite = faceSprite;
                moodImage.enabled = true;
            }
            else
            {
                Debug.LogWarning("[CustomerGroup] No Image found in Moods");
            }
        }
        else
        {
            Debug.LogWarning("[CustomerGroup] Moods object not found in prefab");
        }

        thoughtRoutine = StartCoroutine(HideThoughtBubbleAfterDelay());
    }

    private IEnumerator HideThoughtBubbleAfterDelay()
    {
        yield return new WaitForSeconds(thoughtBubbleDuration);
        ClearThoughtBubble();
        thoughtRoutine = null;
    }

    private string GetRandomComment(string[] comments)
    {
        if (comments == null || comments.Length == 0)
            return string.Empty;

        List<string> valid = new List<string>();
        for (int i = 0; i < comments.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(comments[i]))
                valid.Add(comments[i]);
        }

        if (valid.Count == 0)
            return string.Empty;

        return valid[UnityEngine.Random.Range(0, valid.Count)];
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null) return null;
        if (root.name == childName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void ReportFinalResult(FinalResult result)
    {
        if (finalResultReported)
            return;

        finalResultReported = true;
        finalResult = result;

        switch (result)
        {
            case FinalResult.Happy:
                GameDayManager.Instance?.RegisterHappyCustomer();
                break;

            case FinalResult.Neutral:
                GameDayManager.Instance?.RegisterNeutralCustomer();
                break;

            case FinalResult.Angry:
                GameDayManager.Instance?.RegisterAngryCustomer();
                break;
        }
    }

    private void CleanupSeatsAndBoothOnly()
    {
        if (boothSeatsCleared) return;
        boothSeatsCleared = true;

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null) continue;
            SeatAnchor.VacateAllFor(member.gameObject);
        }

        if (assignedBooth != null)
            assignedBooth.ClearBoothProps();
    }

    private void CleanupOnLeave()
    {
        if (cleanupDone) return;
        cleanupDone = true;

        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();
        ClearMoneyBubble();
        ClearThoughtBubble();
        ClearEatingBubble();

        if (assignedBooth != null)
            assignedBooth.ClearBoothProps();

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null) continue;
            SeatAnchor.VacateAllFor(member.gameObject);
        }
    }

    public void ClearOrderBubble()
    {
        if (orderBubbleInstance == null) return;
        Destroy(orderBubbleInstance);
        orderBubbleInstance = null;
    }

    public void ClearBillBubble()
    {
        if (billBubbleInstance == null) return;
        Destroy(billBubbleInstance);
        billBubbleInstance = null;
    }

    public void ClearTableNumber()
    {
        if (tableNumberInstance == null) return;
        Destroy(tableNumberInstance);
        tableNumberInstance = null;
    }

    private void ClearMoneyBubble()
    {
        if (moneyBubbleInstance == null) return;
        Destroy(moneyBubbleInstance);
        moneyBubbleInstance = null;
    }

    private void ClearThoughtBubble()
    {
        if (thoughtBubbleInstance == null) return;
        Destroy(thoughtBubbleInstance);
        thoughtBubbleInstance = null;
    }

    private Vector3 GetMembersCenterWorld()
    {
        int count = 0;
        Vector3 sum = Vector3.zero;

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null) continue;

            sum += member.transform.position;
            count++;
        }

        return count > 0 ? sum / count : transform.position;
    }

    public bool CanBeGreeted()
    {
        return state == GroupState.Waiting || state == GroupState.WalkingToLobby;
    }

    public void MarkGreeted()
    {
        hasBeenGreeted = true;
    }

    private void ClearEatingBubble()
    {
        if (eatingBubbleInstance == null) return;
        Destroy(eatingBubbleInstance);
        eatingBubbleInstance = null;
    }

    private void SpawnEatingBubble()
    {
        if (eatingBubblePrefab == null) return;

        ResolveCanvas();
        if (gameplayCanvas == null) return;

        ClearEatingBubble();

        eatingBubbleInstance = Instantiate(eatingBubblePrefab, gameplayCanvas.transform);
        eatingBubbleInstance.name = $"{name}_EatingBubble";

        var follow = eatingBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(groupUiAnchor, eatingBubbleOffset, GetFollowCam());

        var ui = eatingBubbleInstance.GetComponentInChildren<EatingBubbleUI>(true);
        if (ui != null)
            ui.SetBaseText("Eating");
    }
}