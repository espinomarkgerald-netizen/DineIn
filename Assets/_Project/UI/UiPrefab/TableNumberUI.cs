using TMPro;
using UnityEngine;

public class TableNumberUI : MonoBehaviour
{
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private Booth booth;
    private CustomerGroup group;

    public void SetGroup(CustomerGroup value)
    {
        group = value;
    }

    private void LateUpdate()
    {
        // A table number only represents food that is still pending. This
        // self-check also removes an orphaned UI instance if a delivery event
        // changed the group state but missed the normal visual cleanup call.
        if (group == null || group.state != CustomerGroup.GroupState.OrderTaken)
            Destroy(gameObject);
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

    public void OnClickMoveToTable()
    {
        if (booth == null) return;

        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return;

        var player = RoleManager.Instance.GetActivePlayerMovement();
        if (player == null) return;

        BoothDeliverInteractable boothDeliver = booth.GetComponent<BoothDeliverInteractable>();
        if (boothDeliver == null)
            boothDeliver = booth.GetComponentInChildren<BoothDeliverInteractable>(true);

        if (boothDeliver != null && boothDeliver.CanInteract())
        {
            player.UI_MoveTo(boothDeliver);
            return;
        }

        CustomerDeliverInteractable customerDeliver = booth.GetComponent<CustomerDeliverInteractable>();
        if (customerDeliver == null)
            customerDeliver = booth.GetComponentInChildren<CustomerDeliverInteractable>(true);

        if (customerDeliver != null && customerDeliver.CanInteract())
        {
            player.UI_MoveTo(customerDeliver);
            return;
        }

        Transform target = booth.approachPoint != null ? booth.approachPoint : booth.transform;
        if (player.Agent != null)
            player.Agent.SetDestination(target.position);
    }
}
