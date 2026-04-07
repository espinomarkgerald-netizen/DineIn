using UnityEngine;

public class TutorialArrowManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera followCamera;

    [Header("Arrow Prefab")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Vector3 defaultOffset = new Vector3(0f, 10f, 0f);

    [Header("Scene Targets")]
    [SerializeField] private Transform greetTarget;
    [SerializeField] private Transform orderSubmitTarget;
    [SerializeField] private Transform cashierMoneyTarget;
    [SerializeField] private Transform sinkTarget;
    [SerializeField] private Transform notepadTarget;
    [SerializeField] private Transform cashierCounterTarget;
    [SerializeField] private Transform cashierWaitSpotTarget;
    [SerializeField] private Booth[] hostBooths;

    private GameObject currentArrow;
    private Transform currentTarget;
    private bool externalDriverActive;
    private string externalDriverOwner;

    public bool HasRuntimeArrow => currentArrow != null;
    public string ExternalDriverOwner => externalDriverOwner;

    private void Awake()
    {
        if (tutorialManager == null)
            tutorialManager = GetComponent<TutorialManager>();

        if (tutorialManager == null)
            tutorialManager = TutorialManager.Instance;

        if (targetCanvas == null)
            targetCanvas = FindFirstObjectByType<Canvas>();

        if (followCamera == null)
            followCamera = Camera.main;

        if (greetTarget == null)
        {
            LobbyLineManager lineManager = FindFirstObjectByType<LobbyLineManager>(FindObjectsInactive.Include);
            if (lineManager != null && lineManager.linePoints != null && lineManager.linePoints.Length > 0)
                greetTarget = lineManager.linePoints[0];
        }

        Debug.Log(
            $"[TutorialArrowManager] Awake | tutorialManager={(tutorialManager != null ? tutorialManager.name : "NULL")} " +
            $"canvas={(targetCanvas != null ? targetCanvas.name : "NULL")} " +
            $"camera={(followCamera != null ? followCamera.name : "NULL")} " +
            $"arrowPrefab={(arrowPrefab != null ? arrowPrefab.name : "NULL")}",
            this);
    }

    private void Update()
    {
        if (tutorialManager == null)
        {
            Debug.LogWarning("[TutorialArrowManager] Update aborted: tutorialManager is NULL", this);
            externalDriverActive = false;
            HideArrow();
            return;
        }

        if (!tutorialManager.TutorialStarted)
        {
            externalDriverActive = false;
            HideArrow();
            return;
        }

        if (tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.None ||
            tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.Intro ||
            tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.Complete)
        {
            externalDriverActive = false;
            HideArrow();
            return;
        }

        if (externalDriverActive)
            return;

        Transform target = ResolveTarget();

        if (target == null)
        {
            HideArrow();
            return;
        }

        if (currentTarget == null || currentArrow == null)
        {
            ShowArrow(target);
            return;
        }

        if (currentTarget != target)
            ShowArrow(target);
    }

    private Transform ResolveTarget()
    {
        if (tutorialManager == null)
            return null;

        CustomerGroup group = tutorialManager.ActiveTutorialGroup;
        CustomerGroup.GroupState state = group != null ? group.state : CustomerGroup.GroupState.Spawning;

        switch (tutorialManager.CurrentPhase)
        {
            case TutorialManager.TutorialPhase.GreetCustomer:
            {
                if (group != null)
                    return group.UIAnchor != null ? group.UIAnchor : group.transform;

                return greetTarget;
            }

            case TutorialManager.TutorialPhase.AssignTable:
            {
                if (group == null)
                    return null;

                if (group.assignedBooth != null)
                {
                    if (state == CustomerGroup.GroupState.WalkingToBooth)
                    {
                        return group.assignedBooth.tableNumberAnchor != null
                            ? group.assignedBooth.tableNumberAnchor
                            : group.assignedBooth.transform;
                    }

                    return null;
                }

                Booth booth = FindBestBoothForGroup(group);
                if (booth != null)
                    return booth.tableNumberAnchor != null ? booth.tableNumberAnchor : booth.transform;

                return null;
            }

            case TutorialManager.TutorialPhase.TakeOrder:
            {
                if (group != null)
                    return group.UIAnchor != null ? group.UIAnchor : group.transform;

                return null;
            }

            case TutorialManager.TutorialPhase.ConfirmOrder:
            {
                return notepadTarget;
            }

            case TutorialManager.TutorialPhase.SubmitOrder:
            {
                if (group == null)
                    return orderSubmitTarget;

                if (state == CustomerGroup.GroupState.OrderTaken)
                    return orderSubmitTarget;

                return null;
            }

            case TutorialManager.TutorialPhase.ServeFood:
            {
                if (group == null)
                    return null;

                bool waiterHoldingTray = WaiterHands.Instance != null && WaiterHands.Instance.HasTray;

                if (!waiterHoldingTray)
                {
                    FoodTray tray = FindFoodTrayForGroup(group);
                    if (tray != null)
                        return tray.transform;

                    return null;
                }

                if (state == CustomerGroup.GroupState.OrderTaken)
                    return group.UIAnchor != null ? group.UIAnchor : group.transform;

                return null;
            }

            case TutorialManager.TutorialPhase.DeliverBill:
            {
                if (group == null)
                    return null;

                if (state != CustomerGroup.GroupState.NeedsBill)
                    return null;

                return group.UIAnchor != null ? group.UIAnchor : group.transform;
            }

            case TutorialManager.TutorialPhase.CollectPayment:
            {
                if (group == null)
                    return null;

                bool waiterHoldingMoney = WaiterHands.Instance != null && WaiterHands.Instance.HasMoney;

                if (!waiterHoldingMoney)
                {
                    MoneyPickup money = FindMoneyPickupForGroup(group);
                    if (money != null)
                        return money.transform;

                    return null;
                }

                return cashierMoneyTarget;
            }

            case TutorialManager.TutorialPhase.CleanTray:
            {
                bool busserHoldingTray = BusserHands.Instance != null && BusserHands.Instance.HasTray;

                Debug.Log(
                    $"[TutorialArrowManager] ResolveTarget CleanTray | holding={busserHoldingTray} " +
                    $"sinkTarget={(sinkTarget != null ? sinkTarget.name : "NULL")} " +
                    $"externalDriverActive={externalDriverActive}",
                    this);

                if (!busserHoldingTray)
                {
                    FoodTray tray = group != null ? FindFoodTrayForGroup(group) : FindAnyCleanupTray();

                    Debug.Log(
                        $"[TutorialArrowManager] ResolveTarget CleanTray tray={(tray != null ? tray.name : "NULL")}",
                        this);

                    if (tray != null)
                        return tray.transform;

                    return null;
                }

                return sinkTarget;
            }

            case TutorialManager.TutorialPhase.AllTogetherGameplay:
            {
                if (BusserHands.Instance != null && BusserHands.Instance.HasTray)
                    return sinkTarget;

                FoodTray cleanupTray = FindAnyCleanupTray();
                if (cleanupTray != null)
                    return cleanupTray.transform;

                return null;
            }

            case TutorialManager.TutorialPhase.CashierWaitForMoney:
            {
                return cashierWaitSpotTarget;
            }

            case TutorialManager.TutorialPhase.CashierProcessPayment:
            {
                return cashierCounterTarget;
            }
        }

        return null;
    }

    private void ShowArrow(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[TutorialArrowManager] ShowArrow target is NULL", this);
            HideArrow();
            return;
        }

        HideArrow();

        Debug.Log(
            $"[TutorialArrowManager] ShowArrow -> target={target.name}, " +
            $"arrowPrefab={(arrowPrefab != null ? arrowPrefab.name : "NULL")}, " +
            $"canvas={(targetCanvas != null ? targetCanvas.name : "NULL")}, " +
            $"camera={(followCamera != null ? followCamera.name : "NULL")}",
            this);

        if (arrowPrefab == null || targetCanvas == null || followCamera == null)
        {
            Debug.LogWarning("[TutorialArrowManager] ShowArrow aborted because a reference is missing", this);
            return;
        }

        currentArrow = Instantiate(arrowPrefab, targetCanvas.transform, false);
        currentArrow.name = "TutorialArrowRuntime";
        currentArrow.SetActive(true);
        currentArrow.transform.SetAsLastSibling();

        RectTransform rootRect = currentArrow.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.localScale = Vector3.one;
            rootRect.localRotation = Quaternion.identity;
            rootRect.anchoredPosition3D = Vector3.zero;
        }

        CanvasGroup[] groups = currentArrow.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
            groups[i].alpha = 1f;

        currentTarget = target;

        BoothAssignArrowUI arrowUI = currentArrow.GetComponent<BoothAssignArrowUI>();
        if (arrowUI == null)
            arrowUI = currentArrow.GetComponentInChildren<BoothAssignArrowUI>(true);

        Debug.Log($"[TutorialArrowManager] arrowUI found={(arrowUI != null)}", this);

        if (arrowUI != null)
        {
            arrowUI.Init(target, defaultOffset, followCamera);
            Debug.Log("[TutorialArrowManager] Arrow initialized successfully", this);
        }
        else
        {
            Debug.LogWarning("[TutorialArrowManager] BoothAssignArrowUI component was not found on spawned arrow", this);
        }
    }

    private void HideArrow()
    {
        if (currentArrow != null)
        {
            Debug.Log($"[TutorialArrowManager] HideArrow destroying {currentArrow.name}", this);
            Destroy(currentArrow);
        }

        currentArrow = null;
        currentTarget = null;
    }

    public void BeginExternalControl(string owner)
    {
        Debug.Log($"[TutorialArrowManager] BeginExternalControl owner={owner}");
        externalDriverActive = true;
        externalDriverOwner = owner;
    }

    public void EndExternalControl(string owner)
    {
        Debug.Log($"[TutorialArrowManager] EndExternalControl owner={owner} currentOwner={externalDriverOwner}");
        externalDriverActive = false;
        externalDriverOwner = null;
    }

    public void PointToTransform(Transform target, string owner)
    {
        Debug.Log($"[TutorialArrowManager] PointToTransform owner={owner} target={(target != null ? target.name : "NULL")}");

        if (target != null)
            ShowArrow(target);
        else
            HideArrow();
    }

    public void ForceHide(string owner)
    {
        Debug.Log($"[TutorialArrowManager] ForceHide owner={owner} externalOwner={externalDriverOwner}");
        externalDriverActive = false;
        externalDriverOwner = null;
        HideArrow();
    }

    /// <summary>
    /// Hides the arrow without releasing external driver ownership.
    /// Use this when the owning driver wants to temporarily hide without giving up control.
    /// </summary>
    public void HideArrowKeepControl(string owner)
    {
        Debug.Log($"[TutorialArrowManager] HideArrowKeepControl owner={owner}");
        HideArrow();
    }

    private Booth FindBestBoothForGroup(CustomerGroup group)
    {
        if (group == null || hostBooths == null || hostBooths.Length == 0)
            return null;

        Booth best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hostBooths.Length; i++)
        {
            Booth booth = hostBooths[i];
            if (booth == null)
                continue;

            if (!IsBoothValidForGroup(booth, group))
                continue;

            float dist = Vector3.Distance(group.transform.position, booth.transform.position);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                best = booth;
            }
        }

        return best;
    }

    private bool IsBoothValidForGroup(Booth booth, CustomerGroup group)
    {
        if (booth == null || group == null)
            return false;

        return booth.IsAvailableFor(group.Size);
    }

    private FoodTray FindAnyCleanupTray()
    {
        FoodTray[] all = FindObjectsByType<FoodTray>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        Debug.Log($"[TutorialArrowManager] FindAnyCleanupTray count={all.Length}", this);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;

            FoodTrayInteractable interactable = all[i].GetComponent<FoodTrayInteractable>();
            if (interactable == null)
                interactable = all[i].GetComponentInChildren<FoodTrayInteractable>(false);

            Debug.Log(
                $"[TutorialArrowManager] Tray={all[i].name} interactable={(interactable != null)} " +
                $"cleanupPickable={(interactable != null && interactable.IsCleanupPickable)}",
                this);

            if (interactable != null && interactable.IsCleanupPickable)
                return all[i];
        }

        return null;
    }

    private FoodTray FindFoodTrayForGroup(CustomerGroup group)
    {
        if (group == null)
            return null;

        FoodTray[] all = FindObjectsByType<FoodTray>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].Matches(group))
                return all[i];
        }

        return null;
    }

    private MoneyPickup FindMoneyPickupForGroup(CustomerGroup group)
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