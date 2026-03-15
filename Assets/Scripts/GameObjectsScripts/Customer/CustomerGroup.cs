using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
        AngryLeft
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
    public GameObject angryBubblePrefab;
    public GameObject tableNumberPrefab;

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

    [Header("Sprites")]
    public Sprite billIcon;
    public Sprite angryIcon;
    public Sprite chickenSprite;
    public Sprite friesSprite;
    public Sprite burgerSprite;
    public Sprite cokeSprite;
    public Sprite pineappleSprite;
    public Sprite iceTeaSprite;

    [Header("Leaving / Exit")]
    public Transform exitPoint;
    public float exitFormationSpacing = 0.8f;

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

    private bool finalResultReported;
    private FinalResult finalResult = FinalResult.None;

    private readonly HashSet<CustomerAgent> seatedMembers = new HashSet<CustomerAgent>();
    private Coroutine seatingRoutine;

    private Canvas gameplayCanvas;
    private Transform groupUiAnchor;

    private GameObject orderBubbleInstance;
    private GameObject billBubbleInstance;
    private GameObject angryBubbleInstance;
    private GameObject tableNumberInstance;
    private GameObject moneyBubbleInstance;

    private int pendingPaymentAmount;

    public void SetOrderPause(bool paused) => isOrderPaused = paused;

    private void Awake()
    {
        ResolveCanvas();
        BuildGroupUIAnchor();
        ResolveExitPoint();
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
        state = GroupState.WalkingToBooth;

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

        state = GroupState.Seated;
        SetSelected(false);

        OnGroupSeated?.Invoke(this);
        GameDayManager.Instance?.RegisterGroupSeated();

        if (assignedBooth != null)
            assignedBooth.SpawnMenuBook();

        StartCoroutine(ReadyToOrderFlow());
    }

    private IEnumerator ReadyToOrderFlow()
    {
        state = GroupState.WaitingToOrder;

        float delay = UnityEngine.Random.Range(minOrderDelay, maxOrderDelay);
        yield return new WaitForSeconds(delay);

        GenerateRandomOrder();
        SpawnOrderBubble();

        state = GroupState.ReadyToOrder;

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
                BecomeAngryAndLeave();
                yield break;
            }

            yield return null;
        }
    }

    private void GenerateRandomOrder()
    {
        hasConfirmedOrder = false;

        chosenFood = (FoodType)UnityEngine.Random.Range(0, Enum.GetValues(typeof(FoodType)).Length);
        chosenDrink = (DrinkType)UnityEngine.Random.Range(0, Enum.GetValues(typeof(DrinkType)).Length);

        confirmedFood = chosenFood;
        confirmedDrink = chosenDrink;
    }

    private Sprite GetFoodSprite()
    {
        switch (chosenFood)
        {
            case FoodType.Chicken: return chickenSprite;
            case FoodType.Fries: return friesSprite;
            case FoodType.Burger: return burgerSprite;
            default: return null;
        }
    }

    private Sprite GetDrinkSprite()
    {
        switch (chosenDrink)
        {
            case DrinkType.Coke: return cokeSprite;
            case DrinkType.Pineapple: return pineappleSprite;
            case DrinkType.IceTea: return iceTeaSprite;
            default: return null;
        }
    }

    private void SpawnOrderBubble()
    {
        if (orderBubblePrefab == null) return;
        ResolveCanvas();
        if (gameplayCanvas == null) return;

        ClearOrderBubble();

        orderBubbleInstance = Instantiate(orderBubblePrefab, gameplayCanvas.transform);

        var follow = orderBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(groupUiAnchor, bubbleOffset, GetFollowCam());

        var ui = orderBubbleInstance.GetComponentInChildren<OrderBubbleUI>(true);
        if (ui != null)
        {
            ui.SetOrder(GetFoodSprite(), GetDrinkSprite());
            ui.Init(this);
        }
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

        currentOrderNumber = OrderNumberManager.Instance != null
            ? OrderNumberManager.Instance.GetNextOrderNumber()
            : UnityEngine.Random.Range(100, 999);

        state = GroupState.OrderTaken;

        ClearOrderBubble();
        SpawnTableNumber();

        GameDayManager.Instance?.RegisterOrderTaken();

        if (OrderFlowManager.Instance != null)
            OrderFlowManager.Instance.SpawnTicket(this);
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
            num.SetNumber(currentOrderNumber);
    }

    public void ReceiveFoodFromWaiter()
    {
        if (state != GroupState.OrderTaken) return;

        if (assignedBooth != null)
            assignedBooth.ClearMenuBook();

        ClearTableNumber();

        state = GroupState.Eating;

        GameDayManager.Instance?.RegisterFoodDelivered();

        StartCoroutine(EatThenNeedBill());
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

        state = GroupState.NeedsBill;
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

    private int GetOrderTotal()
    {
        return GetFoodPrice(confirmedFood) + GetDrinkPrice(confirmedDrink);
    }

    private int GetFoodPrice(FoodType food)
    {
        switch (food)
        {
            case FoodType.Chicken: return 99;
            case FoodType.Fries: return 79;
            case FoodType.Burger: return 79;
            default: return 0;
        }
    }

    private int GetDrinkPrice(DrinkType drink)
    {
        switch (drink)
        {
            case DrinkType.Coke:
            case DrinkType.Pineapple:
            case DrinkType.IceTea:
                return 39;
            default:
                return 0;
        }
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

        ReportFinalResult(FinalResult.Happy);

        state = GroupState.Leaving;
        StartLeaving(false);
    }

    private void BecomeAngryAndLeave()
    {
        ReportFinalResult(FinalResult.Angry);

        state = GroupState.AngryLeft;

        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();

        SpawnAngryBubble();
        StartLeaving(true);
    }

    private void StartLeaving(bool showAngryBubble)
    {
        if (leavingRoutineStarted) return;
        leavingRoutineStarted = true;

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

        if (!showAngryBubble)
            ClearAngryBubble();

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

    private void SpawnAngryBubble()
    {
        if (angryBubblePrefab == null) return;
        ResolveCanvas();
        if (gameplayCanvas == null) return;

        ClearAngryBubble();

        angryBubbleInstance = Instantiate(angryBubblePrefab, gameplayCanvas.transform);

        var follow = angryBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(groupUiAnchor, bubbleOffset, GetFollowCam());

        var ui = angryBubbleInstance.GetComponentInChildren<AngryBubbleUI>(true);
        if (ui != null && angryIcon != null)
            ui.SetIcon(angryIcon);
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
        ClearAngryBubble();
        ClearTableNumber();
        ClearMoneyBubble();

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

    public void ClearAngryBubble()
    {
        if (angryBubbleInstance == null) return;
        Destroy(angryBubbleInstance);
        angryBubbleInstance = null;
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
}