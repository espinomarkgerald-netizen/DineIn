using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class CustomerGroup : MonoBehaviour
{
    public enum ReceptionTaskOwner
    {
        None,
        Player,
        Receptionist
    }

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

    public enum ServiceType
    {
        DineIn,
        Takeout
    }

    public enum TakeoutQueueState
    {
        None,
        WalkingToQueueSlot,
        WaitingInQueue,
        WalkingToOrderPoint,
        AtOrderPoint
    }

    public enum CustomerType
    {
        Green,
        Pink,
        Blue
    }

    [Serializable]
    public class OrderLine
    {
        [Tooltip("Stable Recipe product ID or MenuCatalog bundle ID.")]
        public string itemId;
        public bool isBundle;
        public string displayName;
        public int quantity = 1;
        public int unitPrice;
        [Tooltip("Resolved product IDs for one copy of this line.")]
        public List<string> productIds = new List<string>();

        public int TotalPrice => Mathf.Max(0, unitPrice) * Mathf.Max(1, quantity);

        public void SetProduct(Recipe product, int lineQuantity = 1)
        {
            productIds ??= new List<string>();
            itemId = product != null ? product.ProductId : string.Empty;
            isBundle = false;
            displayName = product != null ? product.DisplayName : string.Empty;
            quantity = Mathf.Max(1, lineQuantity);
            unitPrice = product != null ? product.EffectiveSellPrice : 0;
            productIds.Clear();
            if (product != null)
                productIds.Add(product.ProductId);
        }

        public void SetBundle(MenuBundle bundle, int lineQuantity = 1)
        {
            productIds ??= new List<string>();
            itemId = bundle != null ? bundle.bundleId : string.Empty;
            isBundle = true;
            displayName = bundle != null ? bundle.displayName : string.Empty;
            quantity = Mathf.Max(1, lineQuantity);
            unitPrice = bundle != null ? bundle.GetPrice() : 0;
            productIds.Clear();

            if (bundle == null)
                return;

            for (int i = 0; i < bundle.products.Count; i++)
            {
                Recipe product = bundle.products[i];
                if (product != null)
                    productIds.Add(product.ProductId);
            }
        }

        public List<Recipe> ResolveProducts(MenuCatalog catalog = null)
        {
            catalog ??= MenuCatalog.Default;
            if (catalog == null)
                return new List<Recipe>();

            productIds ??= new List<string>();

            List<Recipe> resolved = new List<Recipe>();
            if (isBundle)
            {
                MenuBundle bundle = catalog.FindBundle(itemId);
                if (bundle != null)
                {
                    displayName = bundle.displayName;
                    unitPrice = bundle.GetPrice();
                    resolved.AddRange(bundle.products);
                }
            }
            else
            {
                Recipe product = catalog.FindProduct(itemId);
                if (product != null)
                {
                    displayName = product.DisplayName;
                    unitPrice = product.EffectiveSellPrice;
                    resolved.Add(product);
                }
            }

            if (resolved.Count == 0 && productIds.Count > 0)
                resolved.AddRange(catalog.ResolveProducts(productIds));

            productIds.Clear();
            productIds.AddRange(catalog.GetProductIds(resolved));
            quantity = Mathf.Max(1, quantity);
            return resolved;
        }

        public bool IsDrink(MenuCatalog catalog = null)
        {
            List<Recipe> products = ResolveProducts(catalog);
            return products.Count > 0 &&
                products[0].category == MenuProductCategory.Drink;
        }

        public OrderLine Clone()
        {
            return new OrderLine
            {
                itemId = itemId,
                isBundle = isBundle,
                displayName = displayName,
                quantity = quantity,
                unitPrice = unitPrice,
                productIds = productIds != null
                    ? new List<string>(productIds)
                    : new List<string>()
            };
        }
    }

    [Serializable]
    public class SimpleOrder
    {
        public string name;
        public int quantity = 1;
        public int unitPrice;
        [Tooltip("Stable menu product IDs. These are the authoritative order contents.")]
        public List<string> productIds = new List<string>();
        [Tooltip("Display-name snapshot kept for legacy UI and older saved orders.")]
        public List<string> contents = new List<string>();
        [Tooltip("Quantity-aware menu lines. Bundle lines preserve their MenuCatalog bundle ID.")]
        public List<OrderLine> lines = new List<OrderLine>();

        public int TotalPrice => unitPrice * Mathf.Max(1, quantity);

        public string GetDisplayText()
        {
            return $"{quantity}x {name}";
        }

        public void Clear()
        {
            productIds ??= new List<string>();
            contents ??= new List<string>();
            lines ??= new List<OrderLine>();
            name = string.Empty;
            quantity = 1;
            unitPrice = 0;
            productIds.Clear();
            contents.Clear();
            lines.Clear();
        }

        public List<Recipe> ResolveProducts(MenuCatalog catalog = null)
        {
            catalog ??= MenuCatalog.Default;
            if (catalog == null)
                return new List<Recipe>();

            productIds ??= new List<string>();
            contents ??= new List<string>();
            lines ??= new List<OrderLine>();

            if (lines != null && lines.Count > 0)
            {
                List<Recipe> lineProducts = new List<Recipe>();
                productIds.Clear();
                contents.Clear();
                unitPrice = 0;

                for (int i = 0; i < lines.Count; i++)
                {
                    OrderLine line = lines[i];
                    if (line == null)
                        continue;

                    List<Recipe> products = line.ResolveProducts(catalog);
                    int lineQuantity = Mathf.Max(1, line.quantity);
                    unitPrice += line.TotalPrice;

                    for (int copy = 0; copy < lineQuantity; copy++)
                    {
                        lineProducts.AddRange(products);
                        productIds.AddRange(catalog.GetProductIds(products));
                        contents.AddRange(catalog.GetDisplayNames(products));
                    }
                }

                quantity = 1;
                name = lines.Count == 1 ? lines[0].displayName : "Group Order";
                return lineProducts;
            }

            List<Recipe> resolved = productIds.Count > 0
                ? catalog.ResolveProducts(productIds)
                : catalog.ResolveProducts(contents);

            // Migrate legacy runtime/saved orders the first time they are read.
            if (productIds.Count == 0 && resolved.Count > 0)
                productIds.AddRange(catalog.GetProductIds(resolved));

            if (resolved.Count > 0)
            {
                // Refresh the display snapshot so renaming or repricing a product asset
                // is immediately reflected by orders that already carry stable IDs.
                contents.Clear();
                contents.AddRange(catalog.GetDisplayNames(resolved));

                List<Recipe> foods = resolved.FindAll(
                    product => product.category == MenuProductCategory.Food);
                MenuBundle bundle = catalog.FindBundle(foods);
                if (bundle != null)
                    name = bundle.displayName;
                else if (foods.Count > 0)
                    name = foods[0].DisplayName;

                unitPrice = catalog.GetOrderTotal(productIds);
            }

            return resolved;
        }

        public void SetProducts(IReadOnlyList<Recipe> products, string orderName, int price)
        {
            productIds ??= new List<string>();
            contents ??= new List<string>();
            lines ??= new List<OrderLine>();
            productIds.Clear();
            contents.Clear();
            lines.Clear();

            if (products != null)
            {
                for (int i = 0; i < products.Count; i++)
                {
                    Recipe product = products[i];
                    if (product == null) continue;
                    productIds.Add(product.ProductId);
                    contents.Add(product.DisplayName);
                }
            }

            name = orderName;
            unitPrice = Mathf.Max(0, price);
        }

        public void SetLines(IReadOnlyList<OrderLine> orderLines, MenuCatalog catalog = null)
        {
            lines ??= new List<OrderLine>();
            productIds ??= new List<string>();
            contents ??= new List<string>();
            lines.Clear();
            productIds.Clear();
            contents.Clear();
            name = string.Empty;
            unitPrice = 0;
            if (orderLines != null)
            {
                for (int i = 0; i < orderLines.Count; i++)
                {
                    if (orderLines[i] != null)
                        lines.Add(orderLines[i].Clone());
                }
            }

            quantity = 1;
            ResolveProducts(catalog);
        }
    }

    private Coroutine readyToOrderRoutine;
    private bool tutorialDisableAutoOrderFlow;

    [Header("Runtime")]
    public GroupState state = GroupState.Spawning;
    public List<CustomerAgent> members = new List<CustomerAgent>();

    [Header("Customer Type")]
    [SerializeField] private CustomerType customerType = CustomerType.Green;

    [Header("Type Profiles")]
    [SerializeField] private CustomerTypeProfile profileGreen;
    [SerializeField] private CustomerTypeProfile profilePink;
    [SerializeField] private CustomerTypeProfile profileBlue;

    [Header("Tip Popup")]
    [SerializeField] private GameObject tipPopupPrefab;

    private CustomerTypeProfile Profile
    {
        get
        {
            switch (customerType)
            {
                case CustomerType.Pink:
                    return profilePink != null ? profilePink : profileGreen;

                case CustomerType.Blue:
                    return profileBlue != null ? profileBlue : profileGreen;

                default:
                    return profileGreen;
            }
        }
    }

    public CustomerType CurrentCustomerType => customerType;
    public bool IsMessy => Profile != null && Profile.isMessy;

    public string CustomerTypeDisplayName
    {
        get
        {
            if (Profile != null && !string.IsNullOrWhiteSpace(Profile.displayName))
                return Profile.displayName;

            return customerType.ToString();
        }
    }

    [Header("Selection")]
    public bool isSelected;
    public GameObject selectionVisual;

    [Header("Order Bubble Warning")]
    [Tooltip("When timeLeft <= this, the order bubble starts shaking.")]
    public float shakeBeforeAngrySeconds = 1.5f;

    [Header("Payment UI")]
    [SerializeField] private GameObject moneyBubblePrefab;

    [Header("UI Prefabs")]
    public GameObject orderBubblePrefab;
    public GameObject billBubblePrefab;
    public GameObject tableNumberPrefab;

    [Header("Customer Thoughts")]
    [SerializeField] private GameObject thoughtBubblePrefab;
    [SerializeField] private float thoughtBubbleDuration = 1.5f;

    [Header("Mood Face Sprites")]
    [SerializeField] private Sprite happyFaceSprite;
    [SerializeField] private Sprite unhappyFaceSprite;
    [SerializeField] private Sprite angryFaceSprite;

    [Header("Line Patience")]
    [SerializeField] private GameObject linePatiencePrefab;
    [SerializeField] private float linePatienceSeconds = 60f;
    [SerializeField] private float greetedLinePatienceDrainMultiplier = 0.5f;

    /// <summary>
    /// Overrides the line patience timer. Called by ShiftScaler before the group is spawned.
    /// Clamps to a minimum of 10 seconds.
    /// </summary>
    public void SetPatienceSeconds(float seconds)
    {
        linePatienceSeconds = Mathf.Max(10f, seconds);
        linePatienceRemaining = linePatienceSeconds;
    }

    [Header("DEBUG - Line Patience")]
    [SerializeField] private bool debugForceShowLinePatience;
    [SerializeField] private float debugPatienceValue = 1f;

    [Header("Takeout")]
    [SerializeField] private ServiceType serviceType = ServiceType.DineIn;
    [SerializeField] private TakeoutQueueState takeoutQueueState = TakeoutQueueState.None;
    [SerializeField] private GameObject deliveryHighlightVisual;

    public bool IsTakeout => serviceType == ServiceType.Takeout;
    public TakeoutQueueState CurrentTakeoutQueueState => takeoutQueueState;

    private const float TakeoutDestinationSampleRadius = 2f;
    private readonly Dictionary<CustomerAgent, Vector3> takeoutMemberDestinations = new();

    private GameObject linePatienceInstance;
    private LinePatienceUI linePatienceUI;
    private float linePatienceRemaining;
    private bool linePatienceExpired;
    private bool hasNotifiedLeftLine;

    private bool hasLineSlotTarget;
    private Vector3 currentLineSlotTarget;

    [Header("Happy Comments")]
    [SerializeField]
    private string[] happyComments =
    {
        "That was great!",
        "Nice service!",
        "Everything was perfect.",
        "We enjoyed it!",
        "We'll come back again."
    };

    [Header("VIP Tip Comments")]
    [SerializeField]
    private string[] vipTipComments =
    {
        "Excellent service. Here's a tip!",
        "You handled us well. Keep the change.",
        "Very impressive service. A little tip for you.",
        "That was fast and clean. Here's a tip!",
        "You earned this tip. Thank you!"
    };

    [Header("Unhappy Comments")]
    [SerializeField]
    private string[] unhappyComments =
    {
        "We've been waiting too long.",
        "No one took our order.",
        "Let's just leave.",
        "This is taking forever.",
        "We're done waiting."
    };

    [Header("Angry Comments")]
    [SerializeField]
    private string[] angryComments =
    {
        "This isn't what we ordered!",
        "Wrong order!",
        "This service is terrible!",
        "That's not our food!",
        "Unbelievable."
    };

    [Header("Price Rejection Comments")]
    [SerializeField] private string[] priceRejectionComments =
    {
        "That's too expensive!",
        "I'm not paying that much.",
        "Those prices are ridiculous!",
        "I'll eat somewhere cheaper.",
        "Not worth that price.",
        "That's way over my budget."
    };

    [Header("Line Waiting Comments")]
    [SerializeField]
    private string[] lineAngryComments =
    {
        "This line is too long.",
        "We've been waiting forever.",
        "Let's go somewhere else.",
        "No one is assisting us.",
        "This place is too slow."
    };

    [SerializeField, HideInInspector]
    private float bubbleEdgeGapPixels = 8f;
    [SerializeField, HideInInspector] private float bubbleStackGapPixels = 6f;
    [SerializeField, HideInInspector] private float fallbackVisualHeight = 2.2f;

    [Header("Order Timing")]
    public float minOrderDelay = 2f;
    public float maxOrderDelay = 5f;
    public float minOrderPatience = 5f;
    public float maxOrderPatience = 8f;

    [Header("Eating Timing")]
    public float minEatSeconds = 3f;
    public float maxEatSeconds = 5f;

    [Header("Bill UI")]
    public Sprite billIcon;

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

    private GameObject eatingBubbleInstance;

    private bool waitingForRemake;
    private bool angryResultLocked;
    private bool firstDeliveryCompleted;
    private int wrongDeliveryCount;
    private FoodTray activeFoodTray;
    private FoodTray complaintFoodTray;
    private bool managerComplaintRetryUsed;
    private bool priceRejectionHandled;
    private Coroutine eatingRoutine;

    [HideInInspector] public Booth assignedBooth;

    public event Action<CustomerGroup> OnGroupAssignedToBooth;
    public event Action<CustomerGroup> OnGroupSeated;
    public event Action<CustomerGroup> OnGroupLeftLine;

    public FoodType chosenFood;
    public DrinkType chosenDrink;
    public FoodType confirmedFood;
    public DrinkType confirmedDrink;

    public int currentOrderNumber = -1;
    public int Size => members.Count;

    private bool hasConfirmedOrder;
    private bool isPlayerReviewingOrder;

    public bool HasConfirmedOrder => hasConfirmedOrder;
    public bool IsPlayerReviewingOrder => isPlayerReviewingOrder;
    private bool hasBeenAssigned;
    private bool cleanupDone;
    private bool boothSeatsCleared;
    private bool leavingRoutineStarted;
    private bool isOrderPaused;

    private bool receivedWrongOrder;
    private bool shouldShowAngryThoughtOnLeave;
    private bool managerComplaintPending;

    private bool finalResultReported;
    private FinalResult finalResult = FinalResult.None;

    private readonly HashSet<CustomerAgent> seatedMembers = new HashSet<CustomerAgent>();
    private Coroutine seatingRoutine;
    private Coroutine thoughtRoutine;

    private const int PrimaryBubblePriority = 0;
    private const int PatienceBubblePriority = 100;

    private Transform groupUiAnchor;
    [SerializeField, HideInInspector] private GroupSpawner bubbleLayoutSource;
    private readonly List<Renderer> groupUiRenderers = new();
    private readonly List<UIFollowWorldPoint> activeCustomerBubbles = new();
    private int cachedRendererMemberCount = -1;

    private GameObject orderBubbleInstance;
    private GameObject billBubbleInstance;
    private GameObject tableNumberInstance;
    private GameObject moneyBubbleInstance;
    private bool hasReceivedBill;
    private GameObject thoughtBubbleInstance;

    public bool HasBeenAssigned => hasBeenAssigned;
    public bool CanBeSeated =>
        !IsTakeout &&
        !linePatienceExpired &&
        !hasBeenAssigned &&
        !leavingRoutineStarted &&
        (state == GroupState.Waiting || state == GroupState.WalkingToLobby);
    public Transform UIAnchor => groupUiAnchor;
    public Transform ManagerComplaintAnchor
    {
        get
        {
            if (groupUiAnchor != null)
                return groupUiAnchor;

            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] == null) continue;
                if (members[i].HeadAnchor != null)
                    return members[i].HeadAnchor;
                return members[i].transform;
            }

            return transform;
        }
    }
    public bool CanReceiveManagerComplaint =>
        gameObject.activeInHierarchy &&
        !IsTakeout &&
        !managerComplaintPending &&
        !leavingRoutineStarted &&
        assignedBooth != null &&
        members != null &&
        members.Count > 0 &&
        state != GroupState.Leaving &&
        state != GroupState.UnhappyLeft &&
        state != GroupState.AngryLeft;

    private int pendingPaymentAmount;

    [HideInInspector] public bool hasBeenGreeted = false;
    [SerializeField, HideInInspector] private ReceptionTaskOwner receptionTaskOwner;

    public ReceptionTaskOwner CurrentReceptionTaskOwner => receptionTaskOwner;
    public bool IsReceptionClaimedByPlayer => receptionTaskOwner == ReceptionTaskOwner.Player;
    public bool IsReceptionClaimedByBot => receptionTaskOwner == ReceptionTaskOwner.Receptionist;

    public void SetOrderPause(bool paused) => isOrderPaused = paused;

    public bool BeginPlayerOrderReview()
    {
        if (state != GroupState.ReadyToOrder || hasConfirmedOrder)
            return false;

        if (isPlayerReviewingOrder)
            return true;

        isPlayerReviewingOrder = true;
        isOrderPaused = true;
        return true;
    }

    public void EndPlayerOrderReview()
    {
        isPlayerReviewingOrder = false;
        isOrderPaused = false;
    }

    public void SetCustomerType(CustomerType type)
    {
        customerType = type;

        if (Profile == null)
        {
            Debug.LogWarning($"[CustomerGroup] Missing profile for type {type} on {name}.");
            return;
        }

        Debug.Log(
            $"[CustomerGroup] {name} set to {CustomerTypeDisplayName} ({customerType}) | " +
            $"order x{Profile.orderPatienceMultiplier}, " +
            $"line x{Profile.linePatienceMultiplier}, " +
            $"eat x{Profile.eatDurationMultiplier}, " +
            $"tip={Profile.tipAmount}, " +
            $"messy={Profile.isMessy}"
        );
    }

    private void Awake()
    {
        receptionTaskOwner = ReceptionTaskOwner.None;
        isOrderPaused = false;
        BuildGroupUIAnchor();
        ResolveExitPoint();

        if (currentOrder == null)
            currentOrder = new SimpleOrder();

        linePatienceRemaining = Mathf.Max(1f, linePatienceSeconds);
    }

    private void OnDestroy()
    {
        RestaurantTaskClaim.Complete(this);
        NotifyLeftLineIfNeeded();
        ClearLinePatienceUI();
        CleanupOnLeave();
    }

    private void LateUpdate()
    {
        if (groupUiAnchor != null)
            groupUiAnchor.position = GetMembersHeadAnchorWorld();

        ApplyBubbleHeightSetting();

        UpdateWaitingStateFromLineTarget();
        UpdateLinePatience();
    }

    private void SetState(GroupState newState)
    {
        if (state == newState) return;

        bool eatingVisualChanged =
            (state == GroupState.Eating) != (newState == GroupState.Eating);
        state = newState;

        if (eatingVisualChanged)
            SetMembersEating(state == GroupState.Eating);

        RefreshMemberProceduralState();
        PlayStateReaction(newState);

        Debug.Log($"[CustomerGroup] {name} -> {state}");
    }

    private void SetMembersEating(bool eating)
    {
        for (int i = 0; i < members.Count; i++)
            members[i]?.SetEating(eating, eating ? activeFoodTray : null, i);

        if (!eating)
            activeFoodTray = null;
    }

    private void RefreshMemberProceduralState()
    {
        CustomerProceduralState visualState = state switch
        {
            GroupState.Waiting => CustomerProceduralState.QueueWaiting,
            GroupState.Seated => CustomerProceduralState.Conversation,
            GroupState.WaitingToOrder => CustomerProceduralState.BrowseMenu,
            GroupState.ReadyToOrder => CustomerProceduralState.RequestOrder,
            GroupState.OrderTaken => CustomerProceduralState.WaitingForFood,
            GroupState.Eating => CustomerProceduralState.Eating,
            GroupState.NeedsBill => CustomerProceduralState.RequestBill,
            GroupState.Leaving or GroupState.AngryLeft or GroupState.UnhappyLeft =>
                CustomerProceduralState.Leaving,
            _ => CustomerProceduralState.None
        };

        int memberCount = members != null ? members.Count : 0;
        for (int i = 0; i < memberCount; i++)
        {
            CustomerAgent member = members[i];
            if (member == null)
                continue;

            Transform partner = null;
            if (memberCount > 1)
            {
                CustomerAgent next = members[(i + 1) % memberCount];
                if (next != null)
                    partner = next.transform;
            }

            member.SetProceduralGroupContext(i, memberCount, partner);
            member.SetProceduralServiceState(visualState);
        }
    }

    private void SetMembersProceduralPatience(float normalizedRemaining)
    {
        for (int i = 0; i < members.Count; i++)
            members[i]?.SetProceduralPatience(normalizedRemaining);
    }

    private void PlayMembersReaction(CustomerProceduralReaction reaction)
    {
        for (int i = 0; i < members.Count; i++)
            members[i]?.PlayProceduralReaction(reaction);
    }

    private void PlayStateReaction(GroupState newState)
    {
        if (newState == GroupState.AngryLeft ||
            (newState == GroupState.Leaving && finalResult == FinalResult.Angry))
        {
            PlayMembersReaction(CustomerProceduralReaction.Angry);
        }
        else if (newState == GroupState.UnhappyLeft ||
                 (newState == GroupState.Leaving && finalResult == FinalResult.Neutral))
        {
            PlayMembersReaction(CustomerProceduralReaction.Neutral);
        }
        else if (newState == GroupState.Leaving && finalResult == FinalResult.Happy)
        {
            PlayMembersReaction(CustomerProceduralReaction.Positive);
        }
    }

    private Camera GetFollowCam()
    {
        var cam = UIRoot.GameplayCameraOrNull();
        return cam != null ? cam : Camera.main;
    }

    public void ConfigureCustomerBubble(UIFollowWorldPoint follow, Camera followCamera = null)
    {
        ConfigureCustomerBubble(follow, PrimaryBubblePriority, followCamera);
    }

    private void ConfigureCustomerBubble(
        UIFollowWorldPoint follow,
        int stackPriority,
        Camera followCamera = null)
    {
        if (follow == null || groupUiAnchor == null)
            return;

        Camera resolvedCamera = followCamera != null ? followCamera : GetFollowCam();
        follow.enabled = true;
        follow.InitAboveTarget(
            groupUiAnchor,
            Vector3.zero,
            resolvedCamera,
            ResolveBubbleOffsetPixels(),
            stackPriority,
            bubbleStackGapPixels);

        TrackCustomerBubble(follow);
    }

    private void TrackCustomerBubble(UIFollowWorldPoint follow)
    {
        if (follow != null && !activeCustomerBubbles.Contains(follow))
            activeCustomerBubbles.Add(follow);
    }

    private void ApplyBubbleHeightSetting()
    {
        float offsetPixels = ResolveBubbleOffsetPixels();
        for (int i = activeCustomerBubbles.Count - 1; i >= 0; i--)
        {
            UIFollowWorldPoint follow = activeCustomerBubbles[i];
            if (follow == null)
            {
                activeCustomerBubbles.RemoveAt(i);
                continue;
            }

            follow.SetAboveTargetGap(offsetPixels);
        }
    }

    public void SetBubbleLayoutSource(GroupSpawner source)
    {
        bubbleLayoutSource = source;
        ApplyBubbleHeightSetting();
    }

    private float ResolveBubbleOffsetPixels()
    {
        if (bubbleLayoutSource == null)
            bubbleLayoutSource = GroupSpawner.Instance;

        return bubbleLayoutSource != null
            ? bubbleLayoutSource.CurrentBubbleOffsetPixels
            : bubbleEdgeGapPixels;
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
        groupUiAnchor.position = GetMembersCenterWorld() + Vector3.up * fallbackVisualHeight;
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionVisual != null)
            selectionVisual.SetActive(selected);
    }

    public void AssignToBooth(Booth booth)
    {
        if (booth == null || !CanBeSeated) return;

        hasBeenAssigned = true;
        hasLineSlotTarget = false;
        StopLinePatience();
        NotifyLeftLineIfNeeded();

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

        Vector3 approachCenter = assignedBooth.GetNavigableApproachPosition();
        Vector3 towardBooth = assignedBooth.transform.position - approachCenter;
        towardBooth.y = 0f;
        if (towardBooth.sqrMagnitude < 0.0001f)
            towardBooth = assignedBooth.transform.forward;
        towardBooth.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, towardBooth).normalized;
        Vector3[] approachTargets = new Vector3[members.Count];
        int validMemberCount = 0;

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null) continue;

            Transform seat = assignedBooth.GetSeat(i);
            if (seat == null) continue;

            float centeredIndex = i - (members.Count - 1) * 0.5f;
            Vector3 desiredApproach = approachCenter + right * centeredIndex * 0.45f;
            if (NavMesh.SamplePosition(desiredApproach, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                desiredApproach = hit.position;

            approachTargets[i] = desiredApproach;
            member.WalkTo(desiredApproach);
            validMemberCount++;
        }

        float approachTimeout = 12f;
        float elapsed = 0f;
        while (seatedMembers.Count < validMemberCount && elapsed < approachTimeout)
        {
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || seatedMembers.Contains(member)) continue;

                Transform seat = assignedBooth.GetSeat(i);
                if (seat == null) continue;

                if (member.HasArrived(approachTargets[i]) && SeatAnchor.TryOccupy(seat, member.gameObject))
                {
                    Quaternion rot = assignedBooth.GetSeatedRotation(seat.position);
                    member.SnapToSeat(seat.position, rot);
                    seatedMembers.Add(member);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // A partial path must not deadlock the restaurant. The final seat snap is
        // intentional because booth interiors are carved out of the walkable mesh.
        for (int i = 0; i < members.Count; i++)
        {
            CustomerAgent member = members[i];
            Transform seat = assignedBooth.GetSeat(i);
            if (member == null || seat == null || seatedMembers.Contains(member))
                continue;

            if (!SeatAnchor.TryOccupy(seat, member.gameObject))
                continue;

            member.SnapToSeat(seat.position, assignedBooth.GetSeatedRotation(seat.position));
            seatedMembers.Add(member);
        }

        SetState(GroupState.Seated);
        StopLinePatience();
        SetSelected(false);

        OnGroupSeated?.Invoke(this);
        GameDayManager.Instance?.RegisterGroupSeated();

        if (assignedBooth != null)
            assignedBooth.SpawnMenuBook();

        if (!tutorialDisableAutoOrderFlow)
            StartReadyToOrderFlow();
    }

    private IEnumerator ReadyToOrderFlow()
    {
        SetState(GroupState.WaitingToOrder);

        float delay = UnityEngine.Random.Range(minOrderDelay, maxOrderDelay);
        yield return new WaitForSeconds(delay);

        GenerateRandomOrder();

        if (currentOrder == null || currentOrder.contents == null ||
            currentOrder.contents.Count == 0 || currentOrder.name == "No Food Available")
        {
            WarnAndLeaveForMissingStock();
            yield break;
        }

        if (TryRejectCurrentOrderForPrice())
            yield break;

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
                float mult = Profile != null
                    ? Mathf.Max(0.01f, Profile.orderPatienceMultiplier)
                    : 1f;

                timeLeft -= Time.deltaTime * mult;

                float normalizedPatience = Mathf.Clamp01(timeLeft / patience);
                SetMembersProceduralPatience(normalizedPatience);
                if (bubbleUI != null)
                    bubbleUI.SetPatience(normalizedPatience);
            }

            if (!startedShake && timeLeft <= shakeBeforeAngrySeconds)
            {
                startedShake = true;
                if (shaker != null) shaker.StartShake();
            }

            if (timeLeft <= 0f)
            {
                if (shaker != null) shaker.StopShake(true);
                CasualDiningPolishManager.EnsureInstance().RegisterIncident(
                    DailyIncidentType.WaitedTooLong);
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
        isPlayerReviewingOrder = false;
        receivedWrongOrder = false;
        waitingForRemake = false;
        angryResultLocked = false;
        shouldShowAngryThoughtOnLeave = false;
        firstDeliveryCompleted = false;
        wrongDeliveryCount = 0;
        complaintFoodTray = null;
        managerComplaintRetryUsed = false;
        priceRejectionHandled = false;
    }

    private bool TryRejectCurrentOrderForPrice()
    {
        if (priceRejectionHandled || currentOrder == null ||
            currentOrder.lines == null || currentOrder.lines.Count == 0)
            return false;

        float rejectionChance = MenuPriceValueService.GetOrderRejectionChance(
            currentOrder.lines);
        if (rejectionChance <= 0f || UnityEngine.Random.value >= rejectionChance)
            return false;

        priceRejectionHandled = true;
        string message = GetRandomComment(priceRejectionComments);
        if (string.IsNullOrWhiteSpace(message))
            message = "That's too expensive!";
        ShowCustomThought(message, unhappyFaceSprite);

        currentOrder.Clear();
        submittedOrder?.Clear();
        isPlayerReviewingOrder = false;
        isOrderPaused = false;
        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();
        ClearMoneyBubble();
        ClearEatingBubble();
        SetState(GroupState.UnhappyLeft);
        StartLeaving(false);
        return true;
    }

    private void GenerateSimpleBundleOrder()
    {
        if (currentOrder == null)
            currentOrder = new SimpleOrder();

        currentOrder.Clear();
        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog == null)
        {
            Debug.LogError("[CustomerGroup] MenuCatalog is missing from a Resources folder.");
            currentOrder.name = "No Food Available";
            return;
        }

        List<OrderLine> validMeals = new List<OrderLine>();
        bool simpleMealsOnly =
        TutorialManager.Instance != null &&
        TutorialManager.Instance.TutorialStarted &&
        TutorialManager.Instance.CurrentDay == TutorialManager.TutorialDay.Day2Waiter;

        List<Recipe> foods = catalog.GetProducts(MenuProductCategory.Food);
        for (int i = 0; i < foods.Count; i++)
        {
            if (HasProductStock(foods[i]))
            {
                OrderLine line = new OrderLine();
                line.SetProduct(foods[i]);
                validMeals.Add(line);
            }
        }

        if (!simpleMealsOnly)
        {
            List<MenuBundle> bundles = catalog.GetFoodBundles();
            for (int i = 0; i < bundles.Count; i++)
            {
                if (HasAllProductStock(bundles[i].products))
                {
                    OrderLine line = new OrderLine();
                    line.SetBundle(bundles[i]);
                    validMeals.Add(line);
                }
            }
        }

        List<Recipe> availableDrinks = catalog.GetProducts(MenuProductCategory.Drink);
        availableDrinks.RemoveAll(drinkProduct => !HasProductStock(drinkProduct));
        bool restaurantServesDrinks = catalog.GetProducts(MenuProductCategory.Drink, false).Count > 0;

        if (validMeals.Count == 0 || (restaurantServesDrinks && availableDrinks.Count == 0))
        {
            currentOrder.name = "No Food Available";
            currentOrder.quantity = 1;
            currentOrder.unitPrice = 0;
            return;
        }

        int dinerCount = Mathf.Max(1, Size);
        int generationAttempts = Mathf.Max(16, dinerCount * 16);
        List<OrderLine> generatedLines = null;

        for (int attempt = 0; attempt < generationAttempts; attempt++)
        {
            List<OrderLine> mealLines = new List<OrderLine>();
            List<OrderLine> drinkLines = new List<OrderLine>();
            List<Recipe> allProducts = new List<Recipe>();
            bool usesSharedPitchers = restaurantServesDrinks &&
                                      catalog.RestaurantType == RestaurantType.CasualDining;
            Recipe sharedPitcher = usesSharedPitchers
                ? availableDrinks[UnityEngine.Random.Range(0, availableDrinks.Count)]
                : null;

            for (int memberIndex = 0; memberIndex < dinerCount; memberIndex++)
            {
                OrderLine meal = validMeals[UnityEngine.Random.Range(0, validMeals.Count)];
                AddOrIncrementOrderLine(mealLines, meal);
                allProducts.AddRange(meal.ResolveProducts(catalog));

                if (restaurantServesDrinks && !usesSharedPitchers)
                {
                    Recipe drink = availableDrinks[UnityEngine.Random.Range(0, availableDrinks.Count)];
                    OrderLine drinkLine = new OrderLine();
                    drinkLine.SetProduct(drink);
                    AddOrIncrementOrderLine(drinkLines, drinkLine);
                    allProducts.Add(drink);
                }
            }

            if (usesSharedPitchers && sharedPitcher != null)
            {
                int pitcherQuantity = GetCasualDiningPitcherQuantity(dinerCount);
                OrderLine pitcherLine = new OrderLine();
                pitcherLine.SetProduct(sharedPitcher, pitcherQuantity);
                drinkLines.Add(pitcherLine);
                for (int pitcher = 0; pitcher < pitcherQuantity; pitcher++)
                    allProducts.Add(sharedPitcher);
            }

            if (LobbyStockBridge.Instance != null &&
                !LobbyStockBridge.Instance.HasOrderStock(allProducts))
            {
                continue;
            }

            generatedLines = mealLines;
            generatedLines.AddRange(drinkLines);
            break;
        }

        if (generatedLines == null || generatedLines.Count == 0)
        {
            currentOrder.name = "No Food Available";
            currentOrder.quantity = 1;
            currentOrder.unitPrice = 0;
            return;
        }

        currentOrder.SetLines(generatedLines, catalog);
    }

    public static int GetCasualDiningPitcherQuantity(int groupSize)
    {
        return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1, groupSize) / 2f), 1, 2);
    }

    private static void AddOrIncrementOrderLine(
        List<OrderLine> destination,
        OrderLine source)
    {
        if (destination == null || source == null)
            return;

        for (int i = 0; i < destination.Count; i++)
        {
            OrderLine existing = destination[i];
            if (existing == null || existing.isBundle != source.isBundle)
                continue;

            if (string.Equals(existing.itemId, source.itemId, StringComparison.OrdinalIgnoreCase))
            {
                existing.quantity += Mathf.Max(1, source.quantity);
                return;
            }
        }

        destination.Add(source.Clone());
    }

    private void SyncLegacyOrderFieldsFromCurrentOrder()
    {
        List<Recipe> products = currentOrder != null
            ? currentOrder.ResolveProducts()
            : new List<Recipe>();

        if (products.Count == 0)
        {
            chosenFood = FoodType.Chicken;
            chosenDrink = DrinkType.Coke;
            confirmedFood = chosenFood;
            confirmedDrink = chosenDrink;
            return;
        }

        bool hasFood = false;
        bool hasDrink = false;
        chosenFood = FoodType.Chicken;
        chosenDrink = DrinkType.Coke;

        for (int i = 0; i < products.Count; i++)
        {
            Recipe product = products[i];

            if (!hasFood && product.category == MenuProductCategory.Food)
            {
                chosenFood = ToLegacyFoodType(product);
                hasFood = true;
            }
            else if (!hasDrink && product.category == MenuProductCategory.Drink)
            {
                chosenDrink = ToLegacyDrinkType(product);
                hasDrink = true;
            }
        }

        if (!hasDrink)
            chosenDrink = DrinkType.Coke;

        hasConfirmedOrder = false;
    }

    public string GetCurrentOrderSummary()
    {
        if (currentOrder == null) return "No Order";
        currentOrder.ResolveProducts();

        if (currentOrder.lines != null && currentOrder.lines.Count > 0)
        {
            List<string> lineSummaries = new List<string>();
            for (int i = 0; i < currentOrder.lines.Count; i++)
            {
                OrderLine line = currentOrder.lines[i];
                if (line != null)
                    lineSummaries.Add($"{Mathf.Max(1, line.quantity)}x {line.displayName}");
            }

            return string.Join(", ", lineSummaries);
        }

        string result = "";

        for (int i = 0; i < currentOrder.contents.Count; i++)
        {
            result += currentOrder.contents[i];

            if (i < currentOrder.contents.Count - 1)
                result += ", ";
        }

        return result;
    }

    public IReadOnlyList<OrderLine> GetCurrentOrderLines()
    {
        if (currentOrder == null)
            return Array.Empty<OrderLine>();

        currentOrder.ResolveProducts();
        return currentOrder.lines ?? (IReadOnlyList<OrderLine>)Array.Empty<OrderLine>();
    }

    public List<string> GetCurrentOrderContents()
    {
        if (currentOrder == null) return new List<string>();
        currentOrder.ResolveProducts();
        return new List<string>(currentOrder.contents);
    }

    public List<string> GetCurrentOrderProductIds()
    {
        if (currentOrder == null) return new List<string>();
        currentOrder.ResolveProducts();
        return new List<string>(currentOrder.productIds);
    }

    private bool CurrentOrderHasDrink()
    {
        if (currentOrder == null) return false;

        List<Recipe> products = currentOrder.ResolveProducts();
        for (int i = 0; i < products.Count; i++)
            if (products[i].category == MenuProductCategory.Drink)
                return true;

        return false;
    }

    private bool IsCorrectDeliveredOrder(List<string> deliveredContents)
    {
        if (currentOrder == null || currentOrder.contents == null) return false;
        if (deliveredContents == null) return false;

        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog != null)
        {
            currentOrder.ResolveProducts(catalog);
            List<Recipe> deliveredProducts = catalog.ResolveProducts(deliveredContents);
            List<string> deliveredIds = catalog.GetProductIds(deliveredProducts);

            if (currentOrder.productIds.Count != deliveredIds.Count) return false;

            List<string> expectedIds = new List<string>(currentOrder.productIds);
            expectedIds.Sort(StringComparer.Ordinal);
            deliveredIds.Sort(StringComparer.Ordinal);

            for (int i = 0; i < expectedIds.Count; i++)
                if (!string.Equals(expectedIds[i], deliveredIds[i], StringComparison.Ordinal))
                    return false;

            return true;
        }

        if (currentOrder.contents.Count != deliveredContents.Count) return false;

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

    public int GetCurrentOrderTotal()
    {
        if (currentOrder == null) return 0;

        currentOrder.ResolveProducts();
        if (currentOrder.lines != null && currentOrder.lines.Count > 0)
            return currentOrder.unitPrice;

        int quantity = Mathf.Max(1, currentOrder.quantity);

        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog != null)
        {
            currentOrder.ResolveProducts(catalog);
            return catalog.GetOrderTotal(currentOrder.productIds) * quantity;
        }

        return currentOrder.unitPrice * quantity;
    }

    public int GetCurrentOrderCategoryTotal(MenuProductCategory category)
    {
        if (currentOrder == null)
            return 0;

        MenuCatalog catalog = MenuCatalog.Default;
        currentOrder.ResolveProducts(catalog);

        if (currentOrder.lines != null && currentOrder.lines.Count > 0)
        {
            int total = 0;
            for (int i = 0; i < currentOrder.lines.Count; i++)
            {
                OrderLine line = currentOrder.lines[i];
                if (line == null)
                    continue;

                bool isDrink = line.IsDrink(catalog);
                if ((category == MenuProductCategory.Drink && isDrink) ||
                    (category == MenuProductCategory.Food && !isDrink))
                {
                    total += line.TotalPrice;
                }
            }

            return total;
        }

        List<Recipe> products = currentOrder.ResolveProducts(catalog);
        if (category == MenuProductCategory.Food && catalog != null)
        {
            List<Recipe> foods = products.FindAll(
                product => product != null && product.category == MenuProductCategory.Food);
            return catalog.GetOrderTotal(catalog.GetProductIds(foods)) *
                Mathf.Max(1, currentOrder.quantity);
        }

        int legacyTotal = 0;
        for (int i = 0; i < products.Count; i++)
        {
            if (products[i] != null && products[i].category == category)
                legacyTotal += products[i].EffectiveSellPrice;
        }

        return legacyTotal * Mathf.Max(1, currentOrder.quantity);
    }

    private void SpawnOrderBubble()
    {
        if (orderBubblePrefab == null)
        {
            Debug.LogWarning($"[CustomerGroup] orderBubblePrefab missing on {name}");
            return;
        }

        ClearOrderBubble();

        orderBubbleInstance = Instantiate(orderBubblePrefab);
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
            ConfigureCustomerBubble(follow);
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

    public bool TakeOrderFromWaiter(FoodType food, DrinkType drink)
    {
        if (state != GroupState.ReadyToOrder || hasConfirmedOrder)
            return false;

        if (isPlayerReviewingOrder)
        {
            Debug.LogWarning(
                $"[CustomerGroup] Automated order attempt ignored for {name} while the player is reviewing the notepad.",
                this);
            return false;
        }

        if (LobbyStockBridge.Instance != null)
        {
            List<Recipe> products = currentOrder != null
                ? currentOrder.ResolveProducts()
                : new List<Recipe>();

            if (products.Count == 0 ||
                !LobbyStockBridge.Instance.TryUseOrderStock(products))
            {
                Debug.LogWarning(
                    $"[CustomerGroup] Automated order for {name} could not reserve its full quantity from stock.",
                    this);
                WarnAndLeaveForMissingStock();
                return false;
            }
        }

        return CompleteOrderTaking(food, drink, spawnTicket: true);
    }

    public bool ConfirmPlayerReviewedOrder(FoodType food, DrinkType drink)
    {
        if (!isPlayerReviewingOrder || state != GroupState.ReadyToOrder)
            return false;

        isPlayerReviewingOrder = false;
        isOrderPaused = false;
        // Manager-assisted notepad orders go straight to the kitchen after
        // confirmation. They do not create a cashier ticket for the player.
        return CompleteOrderTaking(food, drink, spawnTicket: false);
    }

    private bool CompleteOrderTaking(FoodType food, DrinkType drink, bool spawnTicket)
    {
        if (state != GroupState.ReadyToOrder)
            return false;

        // A complaint remake is a distinct kitchen job. Reusing the delivered
        // order number makes KitchenManager correctly reject it as already
        // completed, which used to leave the customer waiting forever.
        if (waitingForRemake)
            AssignFreshOrderNumberForRemake();

        ConfirmOrder(food, drink);

        if (orderBubbleInstance != null)
        {
            var shaker = orderBubbleInstance.GetComponentInChildren<UIShake>(true);
            if (shaker != null) shaker.StopShake(true);
        }

        SetState(GroupState.OrderTaken);
        ClearOrderBubble();

        if (!waitingForRemake)
            GameDayManager.Instance?.RegisterOrderTaken();

        waitingForRemake = false;

        if (IsTakeout)
        {
            TakeoutFlowManager.Instance?.NotifyOrderTaken(this);
            return true;
        }

        SpawnTableNumber();

        if (spawnTicket && OrderFlowManager.Instance != null)
            OrderFlowManager.Instance.SpawnTicket(this);

        return true;
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

        ClearTableNumber();

        tableNumberInstance = Instantiate(tableNumberPrefab);

        var follow = tableNumberInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            ConfigureCustomerBubble(follow);

        var num = tableNumberInstance.GetComponentInChildren<TableNumberUI>(true);
        if (num != null)
        {
            num.SetGroup(this);
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

    public void ReceiveFoodFromWaiter(
        List<string> deliveredContents,
        FoodTray sourceTray = null)
    {
        if (state != GroupState.OrderTaken || !hasConfirmedOrder || isPlayerReviewingOrder)
            return;

        bool isCorrectOrder = IsCorrectDeliveredOrder(deliveredContents);
        bool isBurntFood = sourceTray != null && sourceTray.ContainsBurntFood;
        if (!isBurntFood && deliveredContents != null)
        {
            for (int i = 0; i < deliveredContents.Count; i++)
            {
                string delivered = deliveredContents[i];
                if (!string.IsNullOrWhiteSpace(delivered) &&
                    (delivered.IndexOf("burnt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     delivered.IndexOf("burned", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    isBurntFood = true;
                    break;
                }
            }
        }

        if (assignedBooth != null)
            assignedBooth.ClearMenuBook();

        ClearTableNumber();

        firstDeliveryCompleted = true;

        if (isBurntFood)
        {
            complaintFoodTray = sourceTray;
            HandleBurntDelivery();
            return;
        }

        if (!isCorrectOrder)
        {
            complaintFoodTray = sourceTray;
            HandleWrongDelivery();
            return;
        }

        waitingForRemake = false;
        activeFoodTray = sourceTray;
        SetState(GroupState.Eating);
        ClearEatingBubble();

        GameDayManager.Instance?.RegisterFoodDelivered();
        if (eatingRoutine != null)
            StopCoroutine(eatingRoutine);
        eatingRoutine = StartCoroutine(EatThenNeedBill());
    }

    public void ReceiveWrongFoodFromWaiter()
    {
        if (state != GroupState.OrderTaken && state != GroupState.Eating)
            return;

        if (complaintFoodTray == null)
            complaintFoodTray = activeFoodTray;
        HandleWrongDelivery();
    }

    /// <summary>
    /// Starts one of the day's rolled complaint encounters while this group is
    /// eating. This uses the normal complaint request path, so real mistakes and
    /// scheduled encounters share the same daily allowance and pacing rules.
    /// </summary>
    public bool TryBeginScheduledManagerComplaint(ManagerComplaintType type)
    {
        if (state != GroupState.Eating || !CanReceiveManagerComplaint)
            return false;

        complaintFoodTray = activeFoodTray;
        ManagerComplaintSystem complaintSystem = ManagerComplaintSystem.EnsureInstance();
        if (complaintSystem == null || !complaintSystem.TryRequestComplaint(this, type))
        {
            complaintFoodTray = null;
            return false;
        }

        StopEatingRoutineForServiceFailure();
        waitingForRemake = true;
        shouldShowAngryThoughtOnLeave = true;

        if (type == ManagerComplaintType.WrongOrder)
        {
            receivedWrongOrder = true;
            wrongDeliveryCount++;
            CasualDiningPolishManager.EnsureInstance().RegisterIncident(
                DailyIncidentType.WrongOrder);
        }
        else
        {
            CasualDiningPolishManager.EnsureInstance().RegisterIncident(
                DailyIncidentType.OrderFailed);
        }

        return true;
    }

    private void AssignFreshOrderNumberForRemake()
    {
        int previousOrderNumber = currentOrderNumber;
        if (OrderNumberManager.Instance != null)
        {
            int nextOrderNumber = OrderNumberManager.Instance.GetNextOrderNumber();
            if (nextOrderNumber == previousOrderNumber)
                nextOrderNumber = OrderNumberManager.Instance.GetNextOrderNumber();
            currentOrderNumber = nextOrderNumber;
            return;
        }

        // Match the existing fallback policy while guaranteeing that this retry
        // cannot collide with its own completed kitchen job.
        int fallback;
        do
        {
            fallback = UnityEngine.Random.Range(100, 999);
        }
        while (fallback == previousOrderNumber);

        currentOrderNumber = fallback;
    }

    private void HandleWrongDelivery()
    {
        StopEatingRoutineForServiceFailure();
        CasualDiningPolishManager.EnsureInstance().RegisterIncident(
            DailyIncidentType.WrongOrder);
        receivedWrongOrder = true;
        waitingForRemake = true;
        shouldShowAngryThoughtOnLeave = true;
        wrongDeliveryCount++;

        if (managerComplaintRetryUsed)
        {
            EndFailedFinalComplaintRetry();
            return;
        }

        ManagerComplaintSystem complaintSystem = ManagerComplaintSystem.EnsureInstance();
        if (complaintSystem != null && complaintSystem.TryRequestComplaint(
                this,
                ManagerComplaintType.WrongOrder))
            return;

        ContinueUnresolvedDeliveryFailure();
    }

    private void HandleBurntDelivery()
    {
        StopEatingRoutineForServiceFailure();
        CasualDiningPolishManager.EnsureInstance().RegisterIncident(
            DailyIncidentType.OrderFailed);
        waitingForRemake = true;
        shouldShowAngryThoughtOnLeave = true;

        if (managerComplaintRetryUsed)
        {
            EndFailedFinalComplaintRetry();
            return;
        }

        ManagerComplaintSystem complaintSystem = ManagerComplaintSystem.EnsureInstance();
        if (complaintSystem != null && complaintSystem.TryRequestComplaint(
                this,
                ManagerComplaintType.BurntFood))
            return;

        ContinueUnresolvedDeliveryFailure();
    }

    private void StopEatingRoutineForServiceFailure()
    {
        if (eatingRoutine == null)
            return;

        StopCoroutine(eatingRoutine);
        eatingRoutine = null;
    }

    private void ContinueUnresolvedDeliveryFailure()
    {
        MarkComplaintTrayForCleanup();
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
        hasConfirmedOrder = false;
        isPlayerReviewingOrder = false;
        SetState(GroupState.ReadyToOrder);
        ShowThought(angryComments, angryFaceSprite);

        ClearOrderBubble();
        StartCoroutine(ShowRemakeOrderAfterDelay());
    }

    public void BeginManagerComplaint(ManagerComplaintType _)
    {
        managerComplaintPending = true;
        isOrderPaused = true;
        SetMembersEating(false);
        ClearThoughtBubble();
        SetManagerCallAnimation(true);
    }

    public void SetManagerCallAnimation(bool calling)
    {
        bool assigned = false;
        for (int i = 0; i < members.Count; i++)
        {
            CustomerAgent member = members[i];
            if (member == null) continue;
            bool shouldCall = calling && !assigned;
            member.SetCallingManager(shouldCall);
            assigned |= shouldCall;
        }
    }

    public void CancelManagerComplaint()
    {
        managerComplaintPending = false;
        isOrderPaused = false;
        SetManagerCallAnimation(false);
    }

    public void ResolveManagerComplaint(
        ManagerComplaintResponseQuality quality,
        ManagerComplaintType type)
    {
        if (!managerComplaintPending)
            return;

        managerComplaintPending = false;
        isOrderPaused = false;
        SetManagerCallAnimation(false);
        PlayMembersReaction(quality switch
        {
            ManagerComplaintResponseQuality.Professional =>
                CustomerProceduralReaction.Positive,
            ManagerComplaintResponseQuality.Acceptable =>
                CustomerProceduralReaction.Neutral,
            _ => CustomerProceduralReaction.Angry
        });

        if (quality == ManagerComplaintResponseQuality.Professional)
        {
            MarkComplaintTrayForCleanup();
            managerComplaintRetryUsed = true;

            angryResultLocked = false;
            receivedWrongOrder = false;
            shouldShowAngryThoughtOnLeave = false;
            waitingForRemake = true;
            hasConfirmedOrder = false;
            isPlayerReviewingOrder = false;

            if (assignedBooth != null)
                assignedBooth.ClearMenuBook();

            ClearOrderBubble();
            ClearBillBubble();
            ClearTableNumber();
            ClearMoneyBubble();
            ClearEatingBubble();
            SetState(GroupState.ReadyToOrder);
            ShowCustomThought("Thank you for taking this seriously.", happyFaceSprite);
            StartCoroutine(ShowRemakeOrderAfterDelay());
            return;
        }

        MarkComplaintTrayForCleanup();
        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();
        ClearMoneyBubble();
        ClearEatingBubble();

        if (quality == ManagerComplaintResponseQuality.Acceptable)
        {
            ReportFinalResult(FinalResult.Neutral);
            ShowCustomThought("The refund helps, but this was disappointing.", unhappyFaceSprite);
            SetState(GroupState.Leaving);
        }
        else
        {
            if (!angryResultLocked)
            {
                angryResultLocked = true;
                ReportFinalResult(FinalResult.Angry);
            }
            ShowThought(angryComments, angryFaceSprite);
            SetState(GroupState.AngryLeft);
        }

        StartLeaving(false);
    }

    private void MarkComplaintTrayForCleanup()
    {
        if (complaintFoodTray == null)
            return;

        FoodTrayInteractable interactable =
            complaintFoodTray.GetComponent<FoodTrayInteractable>();
        interactable?.MarkForComplaintRemoval();
        complaintFoodTray = null;
    }

    private void EndFailedFinalComplaintRetry()
    {
        MarkComplaintTrayForCleanup();
        if (!angryResultLocked)
        {
            angryResultLocked = true;
            ReportFinalResult(FinalResult.Angry);
        }

        ShowThought(angryComments, angryFaceSprite);
        SetState(GroupState.AngryLeft);
        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();
        ClearMoneyBubble();
        ClearEatingBubble();
        StartLeaving(false);
    }

    public void ShowRefundPopup(int amount)
    {
        if (tipPopupPrefab == null || amount <= 0 || groupUiAnchor == null)
            return;

        GameObject instance = Instantiate(tipPopupPrefab);
        instance.name = $"{name}_RefundPopup";

        RectTransform rootRect = instance.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.localScale = Vector3.one;
            rootRect.anchoredPosition3D = Vector3.zero;
        }

        CanvasGroup[] canvasGroups = instance.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < canvasGroups.Length; i++)
        {
            canvasGroups[i].alpha = 1f;
            canvasGroups[i].interactable = false;
            canvasGroups[i].blocksRaycasts = false;
        }

        UIFollowWorldPoint follow = instance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            ConfigureCustomerBubble(follow);

        TipPopupUI ui = instance.GetComponentInChildren<TipPopupUI>(true);
        ui?.ShowLoss(amount);
    }

    public void ReceiveBillFromWaiter()
    {
        if (state != GroupState.NeedsBill) return;

        hasReceivedBill = true;
        RestaurantTaskClaim.Complete(this);
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
        float mult = Profile != null ? Mathf.Max(0.1f, Profile.eatDurationMultiplier) : 1f;
        float eat = UnityEngine.Random.Range(minEatSeconds, maxEatSeconds) * mult;
        yield return new WaitForSeconds(eat);

        eatingRoutine = null;
        ClearEatingBubble();
        hasReceivedBill = false;
        SetState(GroupState.NeedsBill);
        SpawnBillBubble();
    }

    private IEnumerator SpawnMoneyBubbleAfterDelay()
    {
        yield return new WaitForSeconds(0.6f);

        if (state != GroupState.NeedsBill) yield break;
        if (moneyBubblePrefab == null) yield break;
        if (assignedBooth == null) yield break;

        int total = GetCurrentOrderTotal();
        bool useCardPayment = CardPaymentService.ShouldUseCardPayment();
        int amount = useCardPayment ? total : GetCustomerPaymentAmount(total);

        pendingPaymentAmount = amount;

        var spawner = assignedBooth.GetComponent<BoothMoneySpawner>();
        if (spawner == null) yield break;

        Transform paymentApproach = assignedBooth.approachPoint != null
            ? assignedBooth.approachPoint
            : assignedBooth.transform;
        var money = spawner.SpawnMoney(this, amount, paymentApproach, useCardPayment);
        if (money == null) yield break;

        ClearMoneyBubble();

        moneyBubbleInstance = Instantiate(moneyBubblePrefab);

        var follow = moneyBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            ConfigureCustomerBubble(follow);

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

        return Mathf.CeilToInt(total / 1000f) * 1000;
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

            if (ShouldShowVipTip())
            {
                GameDayManager.Instance?.RegisterTip(Profile.tipAmount);
                SpawnTipPopup(Profile.tipAmount);
                Debug.Log($"[CustomerGroup] {name} ({customerType}) left a tip of {Profile.tipAmount}.");
            }
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

    private void WarnAndLeaveForMissingStock()
    {
        CasualDiningPolishManager.EnsureInstance().RegisterIncident(
            DailyIncidentType.StockoutRefusal);
        WarningSlideUI.Instance?.Show(
            "No stocked food and drinks are available. This group is leaving.");
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

        CancelOutstandingGroupTask();

        NotifyTrayGroupLeaving();

        NotifyLeftLineIfNeeded();

        if (shouldShowAngryThoughtOnLeave && state == GroupState.Leaving)
            ShowThought(angryComments, angryFaceSprite);

        if (IsTakeout)
        {
            TakeoutQueueManager qm = TakeoutQueueManager.Instance;
            if (qm != null)
                qm.ReleaseGroup(this);
            else
                Destroy(gameObject);

            TakeoutFlowManager.Instance?.ForceRelease(this);
            return;
        }

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

    private void NotifyTrayGroupLeaving()
    {
        FoodTrayInteractable[] trays = FindObjectsByType<FoodTrayInteractable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < trays.Length; i++)
        {
            if (trays[i] == null) continue;

            FoodTray foodTray = trays[i].GetComponentInChildren<FoodTray>(true);
            if (foodTray == null) foodTray = trays[i].GetComponent<FoodTray>();
            if (foodTray == null) continue;

            if (foodTray.TargetGroup == this)
            {
                trays[i].NotifyGroupLeaving();

                if (TutorialManager.Instance != null)
                    TutorialManager.Instance.RegisterGroupLeftTable(foodTray);
            }
        }
    }

    private IEnumerator LeaveToExitFlow()
    {
        if (IsTakeout)
            yield break;

        Vector3 departurePosition = GetMembersCenterWorld();

        if (assignedBooth != null)
        {
            departurePosition = assignedBooth.GetNavigableApproachPosition();

            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null) continue;

                member.Unseat();

                if (member.Agent != null) member.Agent.Warp(departurePosition);
                else member.transform.position = departurePosition;
            }
        }

        yield return null;

        Vector3 baseExit = exitPoint.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(baseExit, out hit, 3f, NavMesh.AllAreas))
            baseExit = hit.position;

        Vector3 forward = baseExit - departurePosition;
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

            if (member.TryWalkTo(targets[i], out Vector3 resolvedTarget))
                targets[i] = resolvedTarget;
        }

        const float departureTimeout = 12f;
        float elapsed = 0f;
        float repathTimer = 0f;

        while (elapsed < departureTimeout)
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

            elapsed += Time.deltaTime;
            repathTimer += Time.deltaTime;
            if (repathTimer >= 2f)
            {
                repathTimer = 0f;
                for (int i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    if (member == null || member.HasArrived(targets[i]))
                        continue;

                    if (member.TryWalkTo(targets[i], out Vector3 resolvedTarget))
                        targets[i] = resolvedTarget;
                }
            }

            yield return null;
        }

        // Never remove a customer away from the exit. If navigation remained
        // unavailable after several retries, place only the failed members on
        // their sampled exit targets before completing the leave flow.
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null || member.HasArrived(targets[i]))
                continue;

            bool warped = member.Agent != null && member.Agent.enabled &&
                          member.Agent.Warp(targets[i]);
            if (!warped)
                member.transform.position = targets[i];
        }

        CleanupOnLeave();
        Destroy(gameObject);
    }

    private void SpawnBillBubble()
    {
        if (billBubblePrefab == null) return;

        ClearBillBubble();

        billBubbleInstance = Instantiate(billBubblePrefab);

        var follow = billBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            ConfigureCustomerBubble(follow);

        var ui = billBubbleInstance.GetComponentInChildren<BillBubbleUI>(true);
        if (ui != null)
            ui.Init(this);
    }

    private void ShowThought(string[] comments, Sprite faceSprite)
    {
        if (TutorialSystem.IsTutorialMode) return;
        if (thoughtBubblePrefab == null) return;

        string message = GetRandomComment(comments);
        if (string.IsNullOrWhiteSpace(message)) return;

        if (thoughtRoutine != null)
            StopCoroutine(thoughtRoutine);

        ClearThoughtBubble();

        thoughtBubbleInstance = Instantiate(thoughtBubblePrefab);

        var follow = thoughtBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            ConfigureCustomerBubble(follow);

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

    private void ShowCustomThought(string message, Sprite faceSprite)
    {
        if (thoughtBubblePrefab == null) return;
        if (string.IsNullOrWhiteSpace(message)) return;

        if (thoughtRoutine != null)
            StopCoroutine(thoughtRoutine);

        ClearThoughtBubble();

        thoughtBubbleInstance = Instantiate(thoughtBubblePrefab);

        var follow = thoughtBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            ConfigureCustomerBubble(follow);

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

    private bool ShouldShowVipTipBubble()
    {
        return customerType == CustomerType.Pink &&
               Profile != null &&
               Profile.tipAmount > 0;
    }

    private void ShowHappyOrTipThought()
    {
        if (ShouldShowVipTipBubble())
        {
            string message = GetRandomComment(vipTipComments);

            if (!string.IsNullOrWhiteSpace(message))
            {
                ShowCustomThought($"{message} (+₱{Profile.tipAmount} tip)", happyFaceSprite);
                return;
            }
        }

        ShowThought(happyComments, happyFaceSprite);
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
        if (finalResultReported) return;

        finalResultReported = true;
        finalResult = result;

        switch (result)
        {
            case FinalResult.Happy:
                GameDayManager.Instance?.RegisterHappyCustomer();
                DailyRevenueTracker.Instance?.RecordOrderCompleted();
                break;

            case FinalResult.Neutral:
                GameDayManager.Instance?.RegisterNeutralCustomer();
                DailyRevenueTracker.Instance?.RecordOrderCompleted();
                break;

            case FinalResult.Angry:
                GameDayManager.Instance?.RegisterAngryCustomer();
                DailyRevenueTracker.Instance?.RecordOrderFailed();
                break;
        }

        AlienApprovalManager.Instance?.RegisterGroupResult(result);
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
        ClearLinePatienceUI();

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

    public void RestoreOrderBubbleIfWaiting()
    {
        if (state == GroupState.ReadyToOrder && orderBubbleInstance == null)
            SpawnOrderBubble();
    }

    public bool HasReceivedBill => hasReceivedBill;

    public void SetOrderTaskClaimedByStaff(bool claimed)
    {
        if (orderBubbleInstance != null)
            orderBubbleInstance.SetActive(!claimed);
        else if (!claimed)
            RestoreOrderBubbleIfWaiting();
    }

    public void SetBillTaskClaimedByStaff(bool claimed)
    {
        if (billBubbleInstance != null)
            billBubbleInstance.SetActive(!claimed);
    }

    private Vector3 GetMembersHeadAnchorWorld()
    {
        Vector3 center = GetMembersCenterWorld();
        Camera followCamera = GetFollowCam();
        if (followCamera == null)
            return center + Vector3.up * fallbackVisualHeight;

        if (TryGetHeadAnchorWorld(followCamera, center, out Vector3 headAnchorWorld))
            return headAnchorWorld;

        RefreshGroupUIRenderers();

        bool foundVisual = false;
        float minScreenX = float.PositiveInfinity;
        float maxScreenX = float.NegativeInfinity;
        float maxScreenY = float.NegativeInfinity;

        for (int i = 0; i < groupUiRenderers.Count; i++)
        {
            Renderer visual = groupUiRenderers[i];
            if (visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = visual.bounds;
            for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                Vector3 corner = bounds.center + new Vector3(
                    (cornerIndex & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                    (cornerIndex & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                    (cornerIndex & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                Vector3 screenPoint = followCamera.WorldToScreenPoint(corner);
                if (screenPoint.z <= 0f)
                    continue;

                foundVisual = true;
                minScreenX = Mathf.Min(minScreenX, screenPoint.x);
                maxScreenX = Mathf.Max(maxScreenX, screenPoint.x);
                maxScreenY = Mathf.Max(maxScreenY, screenPoint.y);
            }
        }

        Vector3 centerScreen = followCamera.WorldToScreenPoint(center);
        if (!foundVisual || centerScreen.z <= 0f)
            return center + Vector3.up * fallbackVisualHeight;

        Vector3 visualTopScreen = new Vector3(
            (minScreenX + maxScreenX) * 0.5f,
            maxScreenY,
            centerScreen.z);
        return followCamera.ScreenToWorldPoint(visualTopScreen);
    }

    private bool TryGetHeadAnchorWorld(
        Camera followCamera,
        Vector3 groupCenter,
        out Vector3 headAnchorWorld)
    {
        bool foundHead = false;
        float minScreenX = float.PositiveInfinity;
        float maxScreenX = float.NegativeInfinity;
        float maxScreenY = float.NegativeInfinity;

        for (int i = 0; i < members.Count; i++)
        {
            CustomerAgent member = members[i];
            Transform head = member != null ? member.HeadAnchor : null;
            if (head == null || !head.gameObject.activeInHierarchy)
                continue;

            Vector3 screenPoint = followCamera.WorldToScreenPoint(head.position);
            if (screenPoint.z <= 0f)
                continue;

            foundHead = true;
            minScreenX = Mathf.Min(minScreenX, screenPoint.x);
            maxScreenX = Mathf.Max(maxScreenX, screenPoint.x);
            maxScreenY = Mathf.Max(maxScreenY, screenPoint.y);
        }

        Vector3 centerScreen = followCamera.WorldToScreenPoint(groupCenter);
        if (!foundHead || centerScreen.z <= 0f)
        {
            headAnchorWorld = default;
            return false;
        }

        Vector3 headScreen = new Vector3(
            (minScreenX + maxScreenX) * 0.5f,
            maxScreenY,
            centerScreen.z);
        headAnchorWorld = followCamera.ScreenToWorldPoint(headScreen);
        return true;
    }

    private void RefreshGroupUIRenderers()
    {
        if (cachedRendererMemberCount == members.Count && !HasMissingGroupUIRenderer())
            return;

        groupUiRenderers.Clear();
        for (int i = 0; i < members.Count; i++)
        {
            CustomerAgent member = members[i];
            if (member == null)
                continue;

            Renderer[] renderers = member.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer ||
                    renderer is LineRenderer)
                {
                    continue;
                }

                groupUiRenderers.Add(renderer);
            }
        }

        cachedRendererMemberCount = members.Count;
    }

    private bool HasMissingGroupUIRenderer()
    {
        for (int i = 0; i < groupUiRenderers.Count; i++)
        {
            if (groupUiRenderers[i] == null)
                return true;
        }

        return false;
    }

    public Vector3 GetCurrentWorldCenter() => GetMembersCenterWorld();

    public bool CanBeGreeted()
    {
        return CanBeSeated;
    }

    public void MarkGreeted()
    {
        hasBeenGreeted = true;
    }

    public bool TryClaimReceptionForPlayer()
    {
        if (!CanBeSeated || receptionTaskOwner == ReceptionTaskOwner.Receptionist)
            return false;

        receptionTaskOwner = ReceptionTaskOwner.Player;
        return true;
    }

    public bool TryClaimReceptionForBot()
    {
        if (!CanBeSeated || receptionTaskOwner == ReceptionTaskOwner.Player)
            return false;

        receptionTaskOwner = ReceptionTaskOwner.Receptionist;
        return true;
    }

    public void ReleasePlayerReceptionTask()
    {
        if (receptionTaskOwner == ReceptionTaskOwner.Player)
            receptionTaskOwner = ReceptionTaskOwner.None;
    }

    public void ReleaseBotReceptionTask()
    {
        if (receptionTaskOwner == ReceptionTaskOwner.Receptionist)
            receptionTaskOwner = ReceptionTaskOwner.None;
    }

    public void CompleteReceptionTask()
    {
        receptionTaskOwner = ReceptionTaskOwner.None;
    }

    private void ClearEatingBubble()
    {
        if (eatingBubbleInstance == null) return;
        Destroy(eatingBubbleInstance);
        eatingBubbleInstance = null;
    }

    private bool HasAllProductStock(IReadOnlyList<Recipe> products)
    {
        if (products == null || products.Count == 0)
            return false;

        for (int i = 0; i < products.Count; i++)
        {
            if (!HasProductStock(products[i]))
                return false;
        }

        return true;
    }

    private bool HasProductStock(Recipe product)
    {
        if (LobbyStockBridge.Instance == null)
            return true;

        return LobbyStockBridge.Instance.HasProductStock(product);
    }

    private static FoodType ToLegacyFoodType(Recipe product)
    {
        if (product == null)
            return FoodType.Chicken;

        switch (product.kitchenItemType)
        {
            case ItemTypeKitchen.Fries:  return FoodType.Fries;
            case ItemTypeKitchen.Burger: return FoodType.Burger;
            default:                     return FoodType.Chicken;
        }
    }

    private static DrinkType ToLegacyDrinkType(Recipe product)
    {
        if (product == null)
            return DrinkType.Coke;

        switch (product.kitchenItemType)
        {
            case ItemTypeKitchen.Pineapple: return DrinkType.Pineapple;
            case ItemTypeKitchen.IcedTea:   return DrinkType.IceTea;
            default:                        return DrinkType.Coke;
        }
    }

    private void UpdateLinePatience()
    {
        if (debugForceShowLinePatience)
        {
            EnsureLinePatienceUI();

            if (linePatienceInstance != null && !linePatienceInstance.activeSelf)
                linePatienceInstance.SetActive(true);

            if (linePatienceUI != null)
                linePatienceUI.SetProgress(Mathf.Clamp01(debugPatienceValue));

            return;
        }

        if (linePatienceExpired)
        {
            if (linePatienceInstance != null)
                linePatienceInstance.SetActive(false);
            return;
        }

        bool shouldShow = CanUseLinePatience();

        if (!shouldShow)
        {
            if (linePatienceInstance != null)
                linePatienceInstance.SetActive(false);
            return;
        }

        EnsureLinePatienceUI();

        if (linePatienceInstance != null && !linePatienceInstance.activeSelf)
            linePatienceInstance.SetActive(true);

        float baseDrain = hasBeenGreeted ? greetedLinePatienceDrainMultiplier : 1f;
        float typeMult = Profile != null ? Mathf.Max(0.01f, Profile.linePatienceMultiplier) : 1f;
        linePatienceRemaining -= Time.deltaTime * baseDrain * typeMult;

        float normalized = Mathf.Clamp01(linePatienceRemaining / Mathf.Max(1f, linePatienceSeconds));

        SetMembersProceduralPatience(normalized);

        if (linePatienceUI != null)
            linePatienceUI.SetProgress(normalized);

        if (linePatienceRemaining > 0f)
            return;

        linePatienceRemaining = 0f;
        linePatienceExpired = true;
        HandleLinePatienceExpired();
    }

    private bool CanUseLinePatience()
    {
        if (IsTakeout) return false;
        if (linePatienceExpired) return false;
        if (hasBeenAssigned) return false;
        if (!hasLineSlotTarget) return false;
        return state == GroupState.Waiting;
    }

    private void EnsureLinePatienceUI()
    {
        if (linePatienceInstance != null)
            return;

        if (linePatiencePrefab == null)
        {
            Debug.LogWarning("[CustomerGroup] linePatiencePrefab is missing on " + name);
            return;
        }

        if (groupUiAnchor == null)
        {
            Debug.LogWarning("[CustomerGroup] groupUiAnchor is missing on " + name);
            return;
        }

        linePatienceInstance = Instantiate(linePatiencePrefab);
        linePatienceInstance.name = name + "_LinePatienceUI";
        // Keep the prefab invisible until its scale, follow target, and fill
        // value are final. This prevents one-frame full-size patience bars.
        linePatienceInstance.SetActive(false);
        linePatienceInstance.transform.SetAsLastSibling();

        RectTransform rootRect = linePatienceInstance.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.localScale = Vector3.one;
            rootRect.anchoredPosition3D = Vector3.zero;
        }

        CanvasGroup[] canvasGroups = linePatienceInstance.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < canvasGroups.Length; i++)
        {
            canvasGroups[i].alpha = 1f;
            canvasGroups[i].interactable = true;
            canvasGroups[i].blocksRaycasts = false;
        }

        Image[] images = linePatienceInstance.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
            images[i].enabled = true;

        linePatienceUI = linePatienceInstance.GetComponent<LinePatienceUI>();
        if (linePatienceUI == null)
            linePatienceUI = linePatienceInstance.GetComponentInChildren<LinePatienceUI>(true);

        if (linePatienceUI == null)
        {
            Debug.LogWarning("[CustomerGroup] LinePatienceUI component missing on prefab for " + name);
            Destroy(linePatienceInstance);
            linePatienceInstance = null;
            return;
        }

        linePatienceUI.gameObject.SetActive(true);
        linePatienceUI.InitAboveTarget(
            groupUiAnchor,
            GetFollowCam(),
            ResolveBubbleOffsetPixels(),
            PatienceBubblePriority,
            bubbleStackGapPixels);
        TrackCustomerBubble(
            linePatienceInstance.GetComponentInChildren<UIFollowWorldPoint>(true));
        linePatienceUI.SetProgress(Mathf.Clamp01(linePatienceRemaining / Mathf.Max(1f, linePatienceSeconds)));

        Canvas.ForceUpdateCanvases();
        linePatienceInstance.SetActive(true);

        Debug.Log("[CustomerGroup] Spawned line patience UI for " + name);
    }

    private void ClearLinePatienceUI()
    {
        if (linePatienceInstance != null)
            Destroy(linePatienceInstance);

        linePatienceInstance = null;
        linePatienceUI = null;
    }

    private void StopLinePatience()
    {
        ClearLinePatienceUI();
    }

    private void HandleLinePatienceExpired()
    {
        StopLinePatience();

        CasualDiningPolishManager.EnsureInstance().RegisterIncident(
            DailyIncidentType.Unaccommodated);

        if (!angryResultLocked)
        {
            angryResultLocked = true;
            ReportFinalResult(FinalResult.Angry);
        }

        ShowThought(lineAngryComments, angryFaceSprite);

        SetState(GroupState.AngryLeft);

        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();
        ClearMoneyBubble();
        ClearEatingBubble();

        StartLeaving(false);
    }

    private void CancelOutstandingGroupTask()
    {
        bool claimedByPlayer = IsReceptionClaimedByPlayer ||
                               RestaurantTaskClaim.IsClaimedByPlayer(this);

        if (claimedByPlayer)
        {
            PlayerMovement movement = ManagerPlayer.Active != null
                ? ManagerPlayer.Active.Movement
                : RoleManager.Instance != null
                    ? RoleManager.Instance.GetActivePlayerMovement()
                    : null;

            if (movement != null && movement.IsTaskLocked)
                movement.CancelLockedTask();
        }

        receptionTaskOwner = ReceptionTaskOwner.None;
        RestaurantTaskClaim.Complete(this);
        SetSelected(false);
    }

    private void NotifyLeftLineIfNeeded()
    {
        if (hasNotifiedLeftLine)
            return;

        if (hasBeenAssigned)
            return;

        if (state != GroupState.Waiting &&
            state != GroupState.WalkingToLobby &&
            state != GroupState.AngryLeft &&
            state != GroupState.UnhappyLeft &&
            state != GroupState.Leaving)
            return;

        hasNotifiedLeftLine = true;
        OnGroupLeftLine?.Invoke(this);
    }

    public void SetLineSlotTarget(Vector3 target)
    {
        currentLineSlotTarget = target;
        hasLineSlotTarget = true;
        hasNotifiedLeftLine = false;

        if (!hasBeenAssigned)
            SetState(GroupState.WalkingToLobby);
    }

    private void UpdateWaitingStateFromLineTarget()
    {
        if (hasBeenAssigned)
            return;

        if (!hasLineSlotTarget)
            return;

        if (state == GroupState.AngryLeft || state == GroupState.UnhappyLeft || state == GroupState.Leaving)
            return;

        if (IsGroupAtLineTarget())
        {
            if (state != GroupState.Waiting)
                SetState(GroupState.Waiting);
        }
        else
        {
            if (state != GroupState.WalkingToLobby)
                SetState(GroupState.WalkingToLobby);
        }
    }

    private bool IsGroupAtLineTarget()
    {
        if (members == null || members.Count == 0)
            return false;

        Vector3 target = currentLineSlotTarget;
        target.y = 0f;

        float centerThreshold = 1.35f;
        float memberThreshold = 1.75f;

        Vector3 center = GetMembersCenterWorld();
        center.y = 0f;

        if (Vector3.Distance(center, target) <= centerThreshold)
            return true;

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null)
                continue;

            Vector3 memberPos = member.transform.position;
            memberPos.y = 0f;

            if (Vector3.Distance(memberPos, target) <= memberThreshold)
                return true;
        }

        return false;
    }

    private void StartReadyToOrderFlow()
    {
        StopReadyToOrderFlow();
        readyToOrderRoutine = StartCoroutine(ReadyToOrderFlow());
    }

    private void StopReadyToOrderFlow()
    {
        if (readyToOrderRoutine != null)
        {
            StopCoroutine(readyToOrderRoutine);
            readyToOrderRoutine = null;
        }
    }

    public void SetTutorialDisableAutoOrderFlow(bool disabled)
    {
        tutorialDisableAutoOrderFlow = disabled;

        if (!disabled)
            return;

        StopReadyToOrderFlow();
        TutorialClearServiceUI();

        if (state == GroupState.WaitingToOrder ||
            state == GroupState.ReadyToOrder ||
            state == GroupState.OrderTaken ||
            state == GroupState.Eating ||
            state == GroupState.NeedsBill)
        {
            SetState(GroupState.Seated);
        }
    }

    public void TutorialClearServiceUI()
    {
        StopReadyToOrderFlow();
        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();
        ClearMoneyBubble();
        ClearEatingBubble();
    }

    public void TutorialBeginWaiterFlow(float delay = 0.25f)
    {
        tutorialDisableAutoOrderFlow = false;
        StopReadyToOrderFlow();
        TutorialClearServiceUI();
        StartCoroutine(TutorialBeginWaiterFlowRoutine(delay));
    }

    private IEnumerator TutorialBeginWaiterFlowRoutine(float delay)
    {
        if (state != GroupState.Seated)
            SetState(GroupState.Seated);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tutorialDisableAutoOrderFlow)
            yield break;

        StartReadyToOrderFlow();
    }

    public void TutorialPlaceGroupAtBooth(Booth booth, bool startWaiterFlow, float orderDelay = 0.25f, bool markGreeted = true)
    {
        if (booth == null)
            return;

        if (seatingRoutine != null)
        {
            StopCoroutine(seatingRoutine);
            seatingRoutine = null;
        }

        StopReadyToOrderFlow();
        TutorialClearServiceUI();

        assignedBooth = booth;
        hasBeenAssigned = true;
        hasLineSlotTarget = false;

        if (markGreeted)
            hasBeenGreeted = true;

        StopLinePatience();
        NotifyLeftLineIfNeeded();

        seatedMembers.Clear();
        assignedBooth.SetCurrentGroup(this);

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null)
                continue;

            Transform seat = assignedBooth.GetSeat(i);
            if (seat == null)
                continue;

            SeatAnchor.TryOccupy(seat, member.gameObject);

            Quaternion rot = assignedBooth.GetSeatedRotation(seat.position);

            if (member.Agent != null)
                member.Agent.Warp(seat.position);
            else
                member.transform.position = seat.position;

            member.SnapToSeat(seat.position, rot);
            seatedMembers.Add(member);
        }

        SetState(GroupState.Seated);
        SetSelected(false);

        OnGroupAssignedToBooth?.Invoke(this);
        OnGroupSeated?.Invoke(this);

        if (assignedBooth != null)
            assignedBooth.SpawnMenuBook();

        tutorialDisableAutoOrderFlow = !startWaiterFlow;

        if (startWaiterFlow)
            TutorialBeginWaiterFlow(orderDelay);
    }

    public void SetServiceType(ServiceType value)
    {
        serviceType = value;
    }

    public void SetTakeoutQueueState(TakeoutQueueState value)
    {
        if (takeoutQueueState == value)
            return;

        takeoutQueueState = value;

        if (value != TakeoutQueueState.AtOrderPoint && value != TakeoutQueueState.WaitingInQueue)
            return;

        for (int i = 0; i < members.Count; i++)
            members[i]?.StopAtCurrentPosition();
    }

    public void SetDeliveryHighlight(bool active)
    {
        if (!IsTakeout)
            return;

        if (deliveryHighlightVisual != null)
        {
            deliveryHighlightVisual.SetActive(active);
            return;
        }

        if (selectionVisual != null)
            selectionVisual.SetActive(active);
    }

    public void MoveToTakeoutPoint(Vector3 worldPoint)
    {
        MoveToTakeoutPoint(worldPoint, transform.forward, 1.1f, 1f);
    }

    public void MoveToTakeoutPoint(Vector3 worldPoint, Vector3 forward, float sideSpacing, float rowSpacing)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        takeoutMemberDestinations.Clear();

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null)
                continue;

            member.SetFormationPriorityOffset(i);

            Vector3 desiredTarget = GetTakeoutFormationTarget(
                worldPoint,
                forward,
                right,
                i,
                members.Count,
                sideSpacing,
                rowSpacing);

            if (!TryResolveTakeoutDestination(desiredTarget, worldPoint, out Vector3 resolvedTarget))
            {
                member.StopAtCurrentPosition();
                Debug.LogWarning(
                    $"[TakeoutQueue] {name} member '{member.name}' could not resolve a NavMesh destination near {desiredTarget}.",
                    member);
                continue;
            }

            if (member.TryWalkTo(resolvedTarget, out Vector3 actualDestination) ||
                member.TryWalkTo(worldPoint, out actualDestination))
            {
                takeoutMemberDestinations[member] = actualDestination;
                continue;
            }

            member.StopAtCurrentPosition();
            Debug.LogWarning(
                $"[TakeoutQueue] {name} member '{member.name}' has no complete path to its assigned queue position.",
                member);
        }
    }

    public bool HasReachedTakeoutPoint(Vector3 worldPoint, float threshold = 0.6f)
    {
        return HasReachedTakeoutPoint(worldPoint, transform.forward, 1.1f, 1f, threshold);
    }

    public bool HasReachedTakeoutPoint(
        Vector3 worldPoint,
        Vector3 forward,
        float sideSpacing,
        float rowSpacing,
        float threshold = 0.6f)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        int validMembers = 0;

        for (int i = 0; i < members.Count; i++)
        {
            CustomerAgent member = members[i];
            if (member == null)
                continue;

            validMembers++;
            Vector3 target;
            if (!takeoutMemberDestinations.TryGetValue(member, out target))
            {
                target = GetTakeoutFormationTarget(
                    worldPoint,
                    forward,
                    right,
                    i,
                    members.Count,
                    sideSpacing,
                    rowSpacing);
            }

            Vector3 memberPosition = member.transform.position;
            memberPosition.y = 0f;
            target.y = 0f;

            if (!member.HasArrived(target) && Vector3.Distance(memberPosition, target) > threshold)
                return false;
        }

        return validMembers > 0;
    }

    private static Vector3 GetTakeoutFormationTarget(
        Vector3 center,
        Vector3 forward,
        Vector3 right,
        int index,
        int memberCount,
        float sideSpacing,
        float rowSpacing)
    {
        sideSpacing = Mathf.Max(0.5f, sideSpacing);
        rowSpacing = Mathf.Max(0.5f, rowSpacing);

        if (memberCount <= 1)
            return center;

        if (memberCount == 2)
        {
            float side = index == 0 ? -0.5f : 0.5f;
            return center + right * side * sideSpacing;
        }

        if (memberCount == 3)
        {
            if (index == 0)
                return center;

            float side = index == 1 ? -0.5f : 0.5f;
            return center + right * side * sideSpacing - forward * rowSpacing;
        }

        int row = index / 2;
        int column = index % 2;
        float centeredSide = column == 0 ? -0.5f : 0.5f;

        return center + right * centeredSide * sideSpacing - forward * row * rowSpacing;
    }

    private static bool TryResolveTakeoutDestination(
        Vector3 desiredTarget,
        Vector3 groupCenter,
        out Vector3 resolvedTarget)
    {
        if (NavMesh.SamplePosition(
                desiredTarget,
                out NavMeshHit desiredHit,
                TakeoutDestinationSampleRadius,
                NavMesh.AllAreas))
        {
            resolvedTarget = desiredHit.position;
            return true;
        }

        if (NavMesh.SamplePosition(
                groupCenter,
                out NavMeshHit centerHit,
                TakeoutDestinationSampleRadius * 1.5f,
                NavMesh.AllAreas))
        {
            resolvedTarget = centerHit.position;
            return true;
        }

        resolvedTarget = desiredTarget;
        return false;
    }

    public void BeginTakeoutOrderFlow(float delay = 0.15f)
    {
        if (!IsTakeout)
            return;

        tutorialDisableAutoOrderFlow = false;
        StopReadyToOrderFlow();
        ClearOrderBubble();

        StartCoroutine(BeginTakeoutOrderFlowRoutine(delay));
    }

    private IEnumerator BeginTakeoutOrderFlowRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!IsTakeout)
            yield break;

        if (state != GroupState.Waiting &&
            state != GroupState.WaitingToOrder &&
            state != GroupState.ReadyToOrder &&
            state != GroupState.OrderTaken)
        {
            SetState(GroupState.Waiting);
        }

        StartReadyToOrderFlow();
    }

    public bool ReceiveTakeoutBagFromWaiter(List<string> deliveredContents)
    {
        if (!IsTakeout)
            return false;

        bool validState = state == GroupState.OrderTaken
                    || state == GroupState.Waiting
                    || state == GroupState.WaitingToOrder;

        if (!validState)
        {
            Debug.LogWarning($"[TakeoutDelivery] {name} cannot receive bag in state {state}.");
            return false;
        }

        bool skipContentsCheck = currentOrder == null
                            || currentOrder.contents == null
                            || currentOrder.contents.Count == 0;
        bool isCorrectOrder = skipContentsCheck || IsCorrectDeliveredOrder(deliveredContents);

        if (!isCorrectOrder)
        {
            ShowThought(angryComments, angryFaceSprite);
            return false;
        }

        GameDayManager.Instance?.RegisterFoodDelivered();
        ReportFinalResult(FinalResult.Happy);
        ShowThought(happyComments, happyFaceSprite);

        if (ShouldShowVipTip())
        {
            GameDayManager.Instance?.RegisterTip(Profile.tipAmount);
            SpawnTipPopup(Profile.tipAmount);
            Debug.Log($"[CustomerGroup] {name} ({customerType}) left a takeout tip of {Profile.tipAmount}.");
        }

        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();
        ClearMoneyBubble();
        ClearEatingBubble();

        SetState(GroupState.Leaving);

        TakeoutFlowManager.Instance?.NotifyBagDelivered(this);
        return true;
    }

    public void FailTakeoutService(string reason)
    {
        if (!IsTakeout || leavingRoutineStarted)
            return;

        Debug.LogWarning($"[TakeoutDelivery] {name} could not be completed: {reason}", this);
        CasualDiningPolishManager.EnsureInstance().RegisterIncident(
            DailyIncidentType.TakeoutFailure);
        BecomeUnhappyAndLeave();
    }

    public void FailTakeoutTravel(string reason)
    {
        if (!IsTakeout || leavingRoutineStarted)
            return;

        Debug.LogWarning($"[TakeoutQueue] {name} could not join the queue: {reason}", this);
        CasualDiningPolishManager.EnsureInstance().RegisterIncident(
            DailyIncidentType.TakeoutFailure);
        ReportFinalResult(FinalResult.Neutral);
        SetState(GroupState.Leaving);
        ClearOrderBubble();
        ClearBillBubble();
        ClearTableNumber();
        ClearMoneyBubble();
        ClearEatingBubble();
        StartLeaving(false);
    }

    public string GetCustomerTypeName()
    {
        if (Profile != null && !string.IsNullOrWhiteSpace(Profile.displayName))
            return Profile.displayName;

        return customerType.ToString();
    }

    public Sprite GetCustomerTypeImage()
    {
        return Profile != null ? Profile.customerImage : null;
    }

    public string GetCustomerOpeningMessage()
    {
        if (Profile == null)
            return string.Empty;

        return Profile.GetRandomOpeningMessage();
    }

    private bool ShouldShowVipTip()
    {
        return customerType == CustomerType.Pink &&
            Profile != null &&
            Profile.tipAmount > 0;
    }

    private void SpawnTipPopup(int amount)
    {
        if (tipPopupPrefab == null || amount <= 0)
            return;

        if (groupUiAnchor == null)
            return;

        GameObject instance = Instantiate(tipPopupPrefab);
        instance.name = $"{name}_TipPopup";

        RectTransform rootRect = instance.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.localScale = Vector3.one;
            rootRect.anchoredPosition3D = Vector3.zero;
        }

        CanvasGroup[] canvasGroups = instance.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < canvasGroups.Length; i++)
        {
            canvasGroups[i].alpha = 1f;
            canvasGroups[i].interactable = false;
            canvasGroups[i].blocksRaycasts = false;
        }

        var follow = instance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            ConfigureCustomerBubble(follow);

        var ui = instance.GetComponentInChildren<TipPopupUI>(true);
        if (ui != null)
            ui.Show(amount);
    }
}
