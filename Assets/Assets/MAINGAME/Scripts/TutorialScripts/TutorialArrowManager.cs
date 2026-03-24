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
    [SerializeField] private Transform orderSubmitTarget;
    [SerializeField] private Transform cashierMoneyTarget;
    [SerializeField] private Transform sinkTarget;
    [SerializeField] private Booth[] hostBooths;

    private GameObject currentArrow;
    private Transform currentTarget;

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
    }

    private void Update()
    {
        if (tutorialManager == null || !tutorialManager.TutorialStarted)
        {
            HideArrow();
            return;
        }

        if (tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.None ||
            tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.Intro ||
            tutorialManager.CurrentPhase == TutorialManager.TutorialPhase.Complete)
        {
            HideArrow();
            return;
        }

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
        CustomerGroup group = tutorialManager.ActiveTutorialGroup;
        CustomerGroup.GroupState state = group != null ? group.state : CustomerGroup.GroupState.Spawning;

        switch (tutorialManager.CurrentPhase)
        {
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

                    // once seated, hide arrow and let phase advance naturally
                    return null;
                }

                Booth booth = FindBestBoothForGroup(group);
                if (booth != null)
                    return booth.tableNumberAnchor != null ? booth.tableNumberAnchor : booth.transform;

                return null;
            }

            case TutorialManager.TutorialPhase.SubmitOrder:
            {
                if (group == null)
                    return orderSubmitTarget;

                // only point to counter while the order is already taken
                if (state == CustomerGroup.GroupState.OrderTaken)
                    return orderSubmitTarget;

                return null;
            }

            case TutorialManager.TutorialPhase.ServeFood:
            {
                if (group == null)
                    return null;

                bool waiterHoldingTray = WaiterHands.Instance != null && WaiterHands.Instance.HasTray;

                // show tray first
                if (!waiterHoldingTray)
                {
                    FoodTray tray = FindFoodTrayForGroup(group);
                    if (tray != null)
                        return tray.transform;

                    return null;
                }

                // after pickup, point to customer head
                if (state == CustomerGroup.GroupState.OrderTaken)
                    return group.UIAnchor != null ? group.UIAnchor : group.transform;

                // once they are eating, hide arrow
                return null;
            }

            case TutorialManager.TutorialPhase.DeliverBill:
            {
                if (group == null)
                    return null;

                // do not show arrow while they are still eating
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

                if (!busserHoldingTray)
                {
                    FoodTray tray = FindFoodTrayForGroup(group);
                    if (tray != null)
                        return tray.transform;

                    return null;
                }

                return sinkTarget;
            }
        }

        return null;
    }

    private void ShowArrow(Transform target)
    {
        if (target == null)
        {
            HideArrow();
            return;
        }

        HideArrow();

        if (arrowPrefab == null || targetCanvas == null || followCamera == null)
            return;

        currentArrow = Instantiate(arrowPrefab, targetCanvas.transform);
        currentTarget = target;

        BoothAssignArrowUI arrowUI = currentArrow.GetComponent<BoothAssignArrowUI>();
        if (arrowUI == null)
            arrowUI = currentArrow.GetComponentInChildren<BoothAssignArrowUI>(true);

        if (arrowUI != null)
            arrowUI.Init(target, defaultOffset, followCamera);
    }

    private void HideArrow()
    {
        if (currentArrow != null)
            Destroy(currentArrow);

        currentArrow = null;
        currentTarget = null;
    }

    public void ForceHide()
    {
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