using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TableNumberUI : MonoBehaviour
{
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private Button button;

    [Header("Booth Targets")]
    [SerializeField] private Booth booth;
    [SerializeField] private Booth otherBooth;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Click);
        }
    }

    public void SetNumber(int number)
    {
        if (numberText != null)
            numberText.text = number.ToString();
    }

    public void SetBooth(Booth b)
    {
        booth = b;
    }

    public void SetOtherBooth(Booth b)
    {
        otherBooth = b;
    }

    private void Click()
    {
        Booth target = booth != null ? booth : otherBooth;
        if (target == null) return;

        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return;

        var player = RoleManager.Instance.GetActivePlayerMovement();
        if (player == null) return;

        var interactables = target.GetComponents<MonoBehaviour>();
        for (int i = 0; i < interactables.Length; i++)
        {
            if (interactables[i] is IInteractable interactable)
            {
                player.UI_MoveTo(interactable);
                return;
            }
        }

        interactables = target.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < interactables.Length; i++)
        {
            if (interactables[i] is IInteractable interactable)
            {
                player.UI_MoveTo(interactable);
                return;
            }
        }

        interactables = target.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < interactables.Length; i++)
        {
            if (interactables[i] is IInteractable interactable)
            {
                player.UI_MoveTo(interactable);
                return;
            }
        }
    }
}