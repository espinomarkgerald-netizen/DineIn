using TMPro;
using UnityEngine;

public class TableNumberUI : MonoBehaviour
{
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private Booth booth;

    public void SetNumber(int number)
    {
        if (numberText != null)
            numberText.text = number.ToString();
    }

    public void SetBooth(Booth b)
    {
        booth = b;
    }

    // assign this in Button OnClick
    public void OnClickMoveToTable()
    {
        if (booth == null) return;

        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return;

        var player = RoleManager.Instance.GetActivePlayerMovement();
        if (player == null || player.Agent == null) return;

        Transform target = booth.approachPoint != null ? booth.approachPoint : booth.transform;

        player.Agent.SetDestination(target.position);
    }
}