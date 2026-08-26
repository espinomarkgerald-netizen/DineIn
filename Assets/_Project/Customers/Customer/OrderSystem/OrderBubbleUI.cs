using UnityEngine;
using UnityEngine.UI;

public class OrderBubbleUI : MonoBehaviour
{
    [Header("Click")]
    [SerializeField] private Button openButton;

    [Header("Legacy UI Refs (optional)")]
    public Image foodImage;
    public Image drinkImage;

    [Header("Patience")]
    [SerializeField] private Slider patienceSlider;

    [Header("Colors")]
    [SerializeField] private Color greenColor = Color.green;
    [SerializeField] private Color yellowColor = Color.yellow;
    [SerializeField] private Color redColor = Color.red;

    private CustomerGroup group;
    private Image fillImage;

    private void Awake()
    {
        AutoResolveReferences();
        BindButton();
        ForceVisible();
    }

    private void OnEnable()
    {
        BindButton();
        ForceVisible();
    }

    private void BindButton()
    {
        if (openButton == null)
            openButton = GetComponentInChildren<Button>(true);

        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OnClickBubble);
            openButton.onClick.AddListener(OnClickBubble);
        }
    }

    private void AutoResolveReferences()
    {
        if (foodImage == null || drinkImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                string n = images[i].name.ToLower();

                if (foodImage == null && n.Contains("food"))
                    foodImage = images[i];

                if (drinkImage == null && n.Contains("drink"))
                    drinkImage = images[i];
            }
        }

        if (patienceSlider == null)
            patienceSlider = GetComponentInChildren<Slider>(true);

        if (patienceSlider != null && patienceSlider.fillRect != null)
            fillImage = patienceSlider.fillRect.GetComponent<Image>();
    }

    private void ForceVisible()
    {
        gameObject.SetActive(true);

        CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].alpha = 1f;
            groups[i].interactable = true;
            groups[i].blocksRaycasts = true;
        }

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
            images[i].enabled = true;

        if (patienceSlider != null)
            patienceSlider.gameObject.SetActive(true);
    }

    public void Init(CustomerGroup g)
    {
        group = g;
        AutoResolveReferences();
        BindButton();
        ForceVisible();
        SetAlert();
        SetPatience(1f);
    }

    public void SetAlert()
    {
        AutoResolveReferences();
        ForceVisible();

        if (foodImage != null)
            foodImage.enabled = false;

        if (drinkImage != null)
            drinkImage.enabled = false;

        if (openButton != null && group != null)
            PlayerTaskBubbleFocus.Bind(openButton.gameObject, group);
    }

    public void SetPatience(float normalized)
    {
        if (patienceSlider == null) return;

        normalized = Mathf.Clamp01(normalized);
        patienceSlider.value = normalized;

        if (fillImage == null && patienceSlider.fillRect != null)
            fillImage = patienceSlider.fillRect.GetComponent<Image>();

        if (fillImage == null) return;

        if (normalized > 0.6f)
            fillImage.color = greenColor;
        else if (normalized > 0.3f)
            fillImage.color = yellowColor;
        else
            fillImage.color = redColor;
    }

    public void OnClickBubble()
    {
        if (group == null)
        {
            Debug.LogWarning("[OrderBubbleUI] Group is null.");
            return;
        }

        if (RoleManager.Instance == null)
        {
            Debug.LogWarning("[OrderBubbleUI] RoleManager missing.");
            return;
        }

        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
        {
            Debug.Log("[OrderBubbleUI] Only waiter can open the notepad.");
            return;
        }

        if (GameplayUIBlocker.IsBlockedExcept(gameObject))
        {
            Debug.Log("[OrderBubbleUI] Blocked by other gameplay UI.");
            return;
        }

        if (!IsReadyForPlayerOrder(out string stateWarning))
        {
            WarningSlideUI.Instance?.Show(stateWarning);
            return;
        }

        if (!RestaurantTaskClaim.TryClaimPlayer(group))
        {
            WarningSlideUI.Instance?.Show(RestaurantTaskClaim.PlayerHasActiveTask
                ? "Finish your current task first."
                : "The waiter is already taking this order.");
            return;
        }

        // Lock the order immediately, including while the player walks to the
        // table. Otherwise an autonomous waiter already polling this group can
        // finish its take-order coroutine before the notepad appears.
        if (!group.BeginPlayerOrderReview())
        {
            RestaurantTaskClaim.ReleasePlayer(group);
            WarningSlideUI.Instance?.Show("This customer is no longer waiting to order.");
            return;
        }

        // The offer is now exclusively owned by the Manager. Hide it at once so
        // neither side can click or process the same order twice.
        group.SetOrderTaskClaimedByStaff(true);

        OrderChecklistUI checklist = OrderChecklistUI.Instance;
        if (checklist == null)
            checklist = FindFirstObjectByType<OrderChecklistUI>(FindObjectsInactive.Include);

        if (checklist == null)
        {
            Debug.LogError("[OrderBubbleUI] No OrderChecklistUI found in scene.");
            group.EndPlayerOrderReview();
            group.SetOrderTaskClaimedByStaff(false);
            RestaurantTaskClaim.ReleasePlayer(group);
            return;
        }

        PlayerMovement movement = RoleManager.Instance.GetActivePlayerMovement();
        Transform approach = ResolveApproachPoint();

        if (movement == null || approach == null)
        {
            WarningSlideUI.Instance?.Show(group.IsTakeout
                ? "This takeout customer is not ready at the counter."
                : "This table has no reachable service point.");
            ReleasePlayerClaim();
            return;
        }

        movement.UI_MoveToAction(
            approach,
            group.IsTakeout ? 2.25f : 2.75f,
            () =>
            {
                if (group != null && IsReadyForPlayerOrder(out _))
                    checklist.Open(group);
                else
                    ReleasePlayerClaim();
            },
            ReleasePlayerClaim);
    }

    private bool IsReadyForPlayerOrder(out string warning)
    {
        warning = "This customer is no longer waiting to order.";

        if (group == null || group.state != CustomerGroup.GroupState.ReadyToOrder)
            return false;

        if (!group.IsTakeout)
            return true;

        TakeoutFlowManager flow = TakeoutFlowManager.Instance;
        if (flow != null && flow.ActiveGroup != group)
        {
            warning = "Another takeout customer is currently at the counter.";
            return false;
        }

        if (group.CurrentTakeoutQueueState != CustomerGroup.TakeoutQueueState.AtOrderPoint)
        {
            warning = "This takeout customer has not reached the counter yet.";
            return false;
        }

        if (flow != null && flow.CurrentPhase != TakeoutFlowManager.TakeoutPhase.WaitingForOrder)
        {
            warning = "This takeout order has already moved to the next step.";
            return false;
        }

        return true;
    }

    private Transform ResolveApproachPoint()
    {
        if (group == null)
            return null;

        if (group.IsTakeout)
        {
            TakeoutCustomerInteractable takeoutTarget =
                group.GetComponent<TakeoutCustomerInteractable>();
            return takeoutTarget != null ? takeoutTarget.StandPoint : group.UIAnchor;
        }

        Booth booth = group.assignedBooth;
        return booth != null && booth.approachPoint != null
            ? booth.approachPoint
            : booth != null ? booth.transform : null;
    }

    private void ReleasePlayerClaim()
    {
        if (group == null)
            return;

        group.EndPlayerOrderReview();
        group.SetOrderTaskClaimedByStaff(false);
        RestaurantTaskClaim.ReleasePlayer(group);
    }
}
