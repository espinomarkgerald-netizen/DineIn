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
            label.text = "Assign to Table";
            icon.sprite = assignSprite;
        }
    }

    private void OnClick()
    {
        if (group == null) return;

        if (!group.hasBeenGreeted)
        {
            group.MarkGreeted();
            ShowHostGreetingBubble();
            Refresh();

            Debug.Log($"Greeted group: {group.name}");
            return;
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
    }

    private void ShowHostGreetingBubble()
    {
        RoleBasedAssignController[] controllers = FindObjectsByType<RoleBasedAssignController>(FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            var controller = controllers[i];
            if (controller == null) continue;

            var staffRole = controller.GetComponent<StaffRole>();
            if (staffRole == null) continue;
            if (staffRole.role != StaffRole.Role.Host) continue;
            if (RoleManager.Instance != null && !RoleManager.Instance.IsActiveRole(controller.gameObject)) continue;

            Transform anchor = controller.transform;

            HostSpeechBubbleAnchor speechAnchor = controller.GetComponentInChildren<HostSpeechBubbleAnchor>(true);
            if (speechAnchor != null)
                anchor = speechAnchor.transform;

            HostSpeechBubbleSpawner.Instance?.ShowForHost(anchor, Camera.main);
            return;
        }

        Debug.LogWarning("No active host found for greeting bubble.");
    }
}