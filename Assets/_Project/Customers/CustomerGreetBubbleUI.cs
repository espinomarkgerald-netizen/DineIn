using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CustomerGreetBubbleUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image icon;

    [Header("Sprites")]
    [SerializeField] private Sprite greetSprite;
    [SerializeField] private Sprite assignSprite;

    private CustomerGroup group;

    public void Init(CustomerGroup g)
    {
        group = g;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
            PlayerTaskBubbleFocus.Bind(button.gameObject, group);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (group == null) return;
        if (label == null || icon == null) return;

        if (!group.hasBeenGreeted)
        {
            label.text = "Greet Customer";
            icon.sprite = greetSprite;
        }
        else
        {
            label.text = "Seat Table";
            icon.sprite = assignSprite;
        }
    }

    private void OnClick()
    {
        if (group == null) return;

        if (!group.hasBeenGreeted)
        {
            if (!RestaurantTaskClaim.TryClaimPlayer(group))
            {
                ShowClaimWarning();
                return;
            }

            if (!group.TryClaimReceptionForPlayer())
            {
                RestaurantTaskClaim.ReleasePlayer(group);
                ShowClaimWarning();
                return;
            }

            ManagerPlayer movementManager = ManagerPlayer.Active;
            if (movementManager != null && movementManager.Movement != null)
            {
                CustomerGroup targetGroup = group;
                Transform greetingStandPoint = FindClosestCustomer(
                    targetGroup,
                    movementManager.transform.position);

                if (greetingStandPoint == null)
                {
                    targetGroup.ReleasePlayerReceptionTask();
                    RestaurantTaskClaim.ReleasePlayer(targetGroup);
                    ShowClaimWarning();
                    return;
                }

                CustomerGreetBubbleSpawner.Instance?.SetVisibleAndRefresh(targetGroup, false);

                movementManager.Movement.UI_MoveToAction(
                    greetingStandPoint,
                    3.4f,
                    () =>
                    {
                        if (targetGroup == null) return;

                        // Preserve the original, proven greet flow: mark the
                        // group, then refresh this same action into Seat Table.
                        targetGroup.MarkGreeted();
                        HostSpeechBubbleSpawner.Instance?.HideImmediate();
                        CustomerGreetBubbleSpawner.Instance?.SetVisibleAndRefresh(targetGroup, true);
                        Debug.Log($"Greeted group: {targetGroup.name}");
                    },
                    () =>
                    {
                        if (targetGroup == null) return;
                        targetGroup.ReleasePlayerReceptionTask();
                        RestaurantTaskClaim.ReleasePlayer(targetGroup);
                        CustomerGreetBubbleSpawner.Instance?.SetVisibleAndRefresh(targetGroup, true);
                    });
                return;
            }

            group.MarkGreeted();
            HostSpeechBubbleSpawner.Instance?.HideImmediate();
            Refresh();

            Debug.Log($"Greeted group: {group.name}");
            return;
        }

        if (!group.IsReceptionClaimedByPlayer)
        {
            // Recover safely if scripts were reloaded between Greet and Assign:
            // the greeted state lives on the group, so the same player can
            // re-establish ownership as long as the receptionist did not take it.
            if (!RestaurantTaskClaim.TryClaimPlayer(group) ||
                !group.TryClaimReceptionForPlayer())
            {
                RestaurantTaskClaim.ReleasePlayer(group);
                ShowClaimWarning();
                return;
            }
        }
        else if (!RestaurantTaskClaim.TryClaimPlayer(group))
        {
            ShowClaimWarning();
            return;
        }

        ManagerPlayer manager = ManagerPlayer.Active;
        if (manager != null && manager.Can(ManagerPlayer.Capability.Host))
        {
            RoleBasedAssignController managerController = manager.GetComponent<RoleBasedAssignController>();
            if (managerController != null)
            {
                managerController.BeginAssignFromBubble(group);
                HostSpeechBubbleSpawner.Instance?.HideImmediate();
                return;
            }
        }

        RoleBasedAssignController[] controllers = FindObjectsByType<RoleBasedAssignController>(FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            var controller = controllers[i];
            if (controller == null) continue;

            var staffRole = controller.GetComponent<StaffRole>();
            if (staffRole == null) continue;
            if (staffRole.role != StaffRole.Role.Host) continue;
            if (RoleManager.Instance != null && !RoleManager.Instance.IsActiveRole(controller.gameObject)) continue;

            controller.BeginAssignFromBubble(group);
            HostSpeechBubbleSpawner.Instance?.HideImmediate();
            return;
        }

        Debug.LogWarning("No active host RoleBasedAssignController found for assign action.");
        group.ReleasePlayerReceptionTask();
        RestaurantTaskClaim.ReleasePlayer(group);
    }

    private void ShowClaimWarning()
    {
        WarningSlideUI.Instance?.Show(RestaurantTaskClaim.PlayerHasActiveTask
            ? "Finish your current task first."
            : "The receptionist is already helping this group.");
    }

    private static Transform FindClosestCustomer(CustomerGroup targetGroup, Vector3 from)
    {
        if (targetGroup == null || targetGroup.members == null)
            return null;

        Transform closest = null;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < targetGroup.members.Count; i++)
        {
            CustomerAgent member = targetGroup.members[i];
            if (member == null || !member.gameObject.activeInHierarchy)
                continue;

            float distance = (member.transform.position - from).sqrMagnitude;
            if (distance >= closestDistance)
                continue;

            closest = member.transform;
            closestDistance = distance;
        }

        return closest;
    }
}
