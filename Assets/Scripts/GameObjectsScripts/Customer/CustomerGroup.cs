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

    [Header("Runtime")]
    public GroupState state = GroupState.Spawning;
    public List<CustomerAgent> members = new List<CustomerAgent>();

    [Header("Selection")]
    public bool isSelected;
    public GameObject selectionVisual;

    [Header("Order Bubble Warning")]
    [Tooltip("When timeLeft <= this, the order bubble starts shaking (panic warning).")]
    public float shakeBeforeAngrySeconds = 1.5f;

    [HideInInspector] public Booth assignedBooth;

    public event Action<CustomerGroup> OnGroupAssignedToBooth;
    public event Action<CustomerGroup> OnGroupSeated;

    [Header("UI Prefabs (Screen Space under Canvas_Gameplay)")]
    public GameObject orderBubblePrefab;
    public GameObject billBubblePrefab;
    public GameObject angryBubblePrefab;
    public GameObject tableNumberPrefab;

    [Header("UI Offsets (world units)")]
    public Vector3 bubbleOffset = new Vector3(0, 2.2f, 0);
    public Vector3 tableNumberOffset = new Vector3(0, 1.6f, 0);

    [Header("Order Timing")]
    public float minOrderDelay = 2f;
    public float maxOrderDelay = 5f;

    [Tooltip("If player doesn't take order in this time, they get angry and leave.")]
    public float minOrderPatience = 5f;
    public float maxOrderPatience = 8f;

    [Header("Eating Timing")]
    public float minEatSeconds = 3f;
    public float maxEatSeconds = 5f;

    [Header("Sprites (optional)")]
    public Sprite billIcon;
    public Sprite angryIcon;

    [Header("Food Sprites")]
    public Sprite chickenSprite;
    public Sprite friesSprite;
    public Sprite burgerSprite;

    [Header("Drink Sprites")]
    public Sprite cokeSprite;
    public Sprite pineappleSprite;
    public Sprite iceTeaSprite;

    [Header("Leaving / Exit")]
    public Transform exitPoint;

    [Tooltip("Spacing between members while walking to exit.")]
    public float exitFormationSpacing = 0.8f;

    // Order data
    public FoodType chosenFood;
    public DrinkType chosenDrink;

    // Seating
    private bool hasBeenAssigned = false;
    private readonly HashSet<CustomerAgent> seatedMembers = new HashSet<CustomerAgent>();
    private Coroutine seatingRoutine;

    // UI runtime refs
    private Canvas gameplayCanvas;
    private Transform groupUiAnchor;

    private GameObject orderBubbleInstance;
    private GameObject billBubbleInstance;
    private GameObject angryBubbleInstance;
    private GameObject tableNumberInstance;

    public int currentOrderNumber = -1;
    public int Size => members.Count;

    private bool cleanupDone = false;
    private bool boothSeatsCleared = false;
    private bool leavingRoutineStarted = false;

    // ✅ PAUSE FLAG (for notepad open)
    private bool isOrderPaused = false;
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

    // =========================
    // Essentials
    // =========================
    private void ResolveCanvas()
    {
        if (gameplayCanvas != null) return;

        gameplayCanvas = UIRoot.GameplayCanvasOrNull();

        // fallback (in case UIRoot isn't set up)
        if (gameplayCanvas == null)
            gameplayCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        if (gameplayCanvas == null)
            Debug.LogError("[CustomerGroup] No Canvas found. Bubble UI cannot spawn.");
    }

    private Camera GetFollowCam()
    {
        var cam = UIRoot.GameplayCameraOrNull();
        if (cam != null) return cam;
        return Camera.main;
    }

    private void ResolveExitPoint()
    {
        if (exitPoint != null) return;

        exitPoint = ExitManager.ExitPointOrNull();
        if (exitPoint != null) return;

        GameObject tagged = null;
        try { tagged = GameObject.FindGameObjectWithTag("ExitPoint"); } catch { }
        if (tagged != null) { exitPoint = tagged.transform; return; }

        GameObject named = GameObject.Find("ExitPoint");
        if (named != null) { exitPoint = named.transform; return; }

        Debug.LogWarning("No ExitPoint found. Add ExitManager in scene and assign exitPoint (recommended).");
    }

    private void BuildGroupUIAnchor()
    {
        if (groupUiAnchor != null) return;

        GameObject a = new GameObject("GroupUIAnchor");
        groupUiAnchor = a.transform;
        groupUiAnchor.SetParent(transform, false);
        groupUiAnchor.position = GetMembersCenterWorld();
    }

    private void LateUpdate()
    {
        if (groupUiAnchor != null)
            groupUiAnchor.position = GetMembersCenterWorld();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionVisual != null)
            selectionVisual.SetActive(selected);
    }

    // =========================
    // Seating
    // =========================
    public void AssignToBooth(Booth booth)
    {
        if (booth == null) return;
        if (hasBeenAssigned) return;

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
            var m = members[i];
            if (m == null) continue;

            Transform seat = assignedBooth.GetSeat(i);
            if (seat == null) continue;

            seatTargets[i] = seat.position;
            m.WalkTo(seatTargets[i]);
        }

        while (seatedMembers.Count < members.Count)
        {
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m == null) continue;
                if (seatedMembers.Contains(m)) continue;

                Transform seat = assignedBooth.GetSeat(i);
                if (seat == null) continue;

                if (m.HasArrived(seatTargets[i]))
                {
                    if (SeatAnchor.TryOccupy(seat, m.gameObject))
                    {
                        Quaternion rot = assignedBooth.GetSeatedRotation(seat.position);
                        m.SnapToSeat(seat.position, rot);
                        seatedMembers.Add(m);
                    }
                }
            }

            yield return null;
        }

        state = GroupState.Seated;
        SetSelected(false);

        OnGroupSeated?.Invoke(this);

        if (assignedBooth != null)
            assignedBooth.SpawnMenuBook();

        StartCoroutine(ReadyToOrderFlow());
    }

    // =========================
    // Ready To Order
    // =========================
    private IEnumerator ReadyToOrderFlow()
    {
        state = GroupState.WaitingToOrder;

        float delay = UnityEngine.Random.Range(minOrderDelay, maxOrderDelay);
        yield return new WaitForSeconds(delay);

        GenerateRandomOrder();

        // ✅ Always try to spawn bubble and log why if it fails
        SpawnOrderBubble();

        state = GroupState.ReadyToOrder;

        float patience = UnityEngine.Random.Range(minOrderPatience, maxOrderPatience);
        float timeLeft = patience;

        UIShake shaker = null;
        if (orderBubbleInstance != null)
            shaker = orderBubbleInstance.GetComponentInChildren<UIShake>(true);

        bool startedShake = false;

        while (state == GroupState.ReadyToOrder)
        {
            if (!isOrderPaused)
                timeLeft -= Time.deltaTime;

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
        chosenFood = (FoodType)UnityEngine.Random.Range(0, Enum.GetValues(typeof(FoodType)).Length);
        chosenDrink = (DrinkType)UnityEngine.Random.Range(0, Enum.GetValues(typeof(DrinkType)).Length);
    }

    private Sprite GetFoodSprite()
    {
        switch (chosenFood)
        {
            case FoodType.Chicken: return chickenSprite;
            case FoodType.Fries: return friesSprite;
            case FoodType.Burger: return burgerSprite;
        }
        return null;
    }

    private Sprite GetDrinkSprite()
    {
        switch (chosenDrink)
        {
            case DrinkType.Coke: return cokeSprite;
            case DrinkType.Pineapple: return pineappleSprite;
            case DrinkType.IceTea: return iceTeaSprite;
        }
        return null;
    }

    private void SpawnOrderBubble()
    {
        if (orderBubblePrefab == null)
        {
            Debug.LogError("[CustomerGroup] orderBubblePrefab is NULL. Assign it on the CustomerGroup prefab.");
            return;
        }

        ResolveCanvas();
        if (gameplayCanvas == null)
        {
            Debug.LogError("[CustomerGroup] gameplayCanvas is NULL. Bubble cannot spawn.");
            return;
        }

        ClearOrderBubble();

        orderBubbleInstance = Instantiate(orderBubblePrefab, gameplayCanvas.transform);
        if (orderBubbleInstance == null)
        {
            Debug.LogError("[CustomerGroup] Instantiate(orderBubblePrefab) failed.");
            return;
        }

        var follow = orderBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(groupUiAnchor, bubbleOffset, GetFollowCam());
        else
            Debug.LogWarning("[CustomerGroup] UIFollowWorldPoint missing on bubble prefab.");

        var ui = orderBubbleInstance.GetComponentInChildren<OrderBubbleUI>(true);
        if (ui != null)
        {
            ui.SetOrder(GetFoodSprite(), GetDrinkSprite());
            ui.Init(this);
        }
        else
        {
            Debug.LogWarning("[CustomerGroup] OrderBubbleUI missing on bubble prefab.");
        }
    }

    // Called by OrderChecklistUI Confirm
    public void TakeOrderFromWaiter()
    {
        if (state != GroupState.ReadyToOrder) return;

        // Stop shake if shaking
        if (orderBubbleInstance != null)
        {
            var sh = orderBubbleInstance.GetComponentInChildren<UIShake>(true);
            if (sh != null) sh.StopShake(true);
        }

        int orderNum = (OrderNumberManager.Instance != null)
            ? OrderNumberManager.Instance.GetNextOrderNumber()
            : UnityEngine.Random.Range(100, 999);

        currentOrderNumber = orderNum;
        state = GroupState.OrderTaken;

        ClearOrderBubble();
        SpawnTableNumber();

        // Spawn ticket UI for waiter to deliver to cashier
        if (OrderFlowManager.Instance != null)
            OrderFlowManager.Instance.SpawnTicket(this);

        Debug.Log($"{name}: Order taken! #{currentOrderNumber} Food={chosenFood} Drink={chosenDrink}");
    }

    private void SpawnTableNumber()
    {
        if (tableNumberPrefab == null) return;
        ResolveCanvas();
        if (gameplayCanvas == null) return;

        if (tableNumberInstance != null) Destroy(tableNumberInstance);

        tableNumberInstance = Instantiate(tableNumberPrefab, gameplayCanvas.transform);

        Transform anchor = (assignedBooth != null && assignedBooth.tableNumberAnchor != null)
            ? assignedBooth.tableNumberAnchor
            : groupUiAnchor;

        var follow = tableNumberInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(anchor, tableNumberOffset, GetFollowCam());

        var num = tableNumberInstance.GetComponentInChildren<TableNumberUI>(true);
        if (num != null)
            num.SetNumber(currentOrderNumber);
    }

    // =========================
    // Leaving
    // =========================
    public void PayAndLeave()
    {
        if (state != GroupState.NeedsBill) return;

        state = GroupState.Leaving;
        StartLeaving(showAngryBubble: false);
    }

    private void BecomeAngryAndLeave()
    {
        state = GroupState.AngryLeft;

        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();

        SpawnAngryBubble();
        StartLeaving(showAngryBubble: true);
    }

    private void StartLeaving(bool showAngryBubble)
    {
        if (leavingRoutineStarted) return;
        leavingRoutineStarted = true;

        ResolveExitPoint();

        if (exitPoint == null)
        {
            Debug.LogWarning("ExitPoint missing. Despawning.");
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

        // Teleport to approach
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;

            m.Unseat();

            if (m.Agent != null) m.Agent.Warp(approach.position);
            else m.transform.position = approach.position;
        }

        yield return null;

        Vector3 baseExit = exitPoint.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(baseExit, out hit, 3f, NavMesh.AllAreas))
            baseExit = hit.position;

        Vector3 forward = (baseExit - approach.position);
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3[] targets = new Vector3[members.Count];

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;

            Vector3 offset = Vector3.zero;
            if (i == 1) offset = right * 0.6f;
            else if (i == 2) offset = -right * 0.6f;
            else if (i == 3) offset = -forward * 0.6f;

            targets[i] = baseExit + offset;

            if (NavMesh.SamplePosition(targets[i], out hit, 2f, NavMesh.AllAreas))
                targets[i] = hit.position;

            m.WalkTo(targets[i]);
        }

        float timeout = 12f;
        float t = 0f;

        while (t < timeout)
        {
            bool allArrived = true;

            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m == null) continue;

                if (!m.HasArrived(targets[i]))
                {
                    allArrived = false;
                    break;
                }
            }

            if (allArrived)
                break;

            t += Time.deltaTime;
            yield return null;
        }

        CleanupOnLeave();
        Destroy(gameObject);
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

    // =========================
    // Cleanup
    // =========================
    private void CleanupSeatsAndBoothOnly()
    {
        if (boothSeatsCleared) return;
        boothSeatsCleared = true;

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;
            SeatAnchor.VacateAllFor(m.gameObject);
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

        if (assignedBooth != null)
            assignedBooth.ClearBoothProps();

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;
            SeatAnchor.VacateAllFor(m.gameObject);
        }
    }

    public void ClearOrderBubble()
    {
        if (orderBubbleInstance != null)
        {
            Destroy(orderBubbleInstance);
            orderBubbleInstance = null;
        }
    }

    public void ClearBillBubble()
    {
        if (billBubbleInstance != null)
        {
            Destroy(billBubbleInstance);
            billBubbleInstance = null;
        }
    }

    public void ClearAngryBubble()
    {
        if (angryBubbleInstance != null)
        {
            Destroy(angryBubbleInstance);
            angryBubbleInstance = null;
        }
    }

    public void ClearTableNumber()
    {
        if (tableNumberInstance != null)
        {
            Destroy(tableNumberInstance);
            tableNumberInstance = null;
        }
    }

    private Vector3 GetMembersCenterWorld()
    {
        int count = 0;
        Vector3 sum = Vector3.zero;

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;
            sum += m.transform.position;
            count++;
        }

        return count > 0 ? sum / count : transform.position;
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

    public void ReceiveFoodFromWaiter()
    {
        if (state != GroupState.OrderTaken) return;

        if (assignedBooth != null)
            assignedBooth.ClearMenuBook();

        state = GroupState.Eating;
        StartCoroutine(EatThenNeedBill());
    }

    public void ReceiveBillFromWaiter()
    {
        if (state != GroupState.NeedsBill) return;

        ClearBillBubble();

        if (assignedBooth == null) return;

        var spawner = assignedBooth.GetComponent<BoothMoneySpawner>();
        if (spawner == null) return;

        int amount = 100;

        var cashier = FindFirstObjectByType<CashierBoothInteractable>();
        if (cashier != null)
            amount = cashier.GenerateSaleAmount();

        spawner.SpawnMoney(this, amount, spawner.MoneySpawnPoint);
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
    
}