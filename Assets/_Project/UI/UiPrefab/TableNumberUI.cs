using TMPro;
using UnityEngine;

public class TableNumberUI : MonoBehaviour
{
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private Booth booth;
    private CustomerGroup group;
    private MultiplayerCustomerSpawn networkCustomer;
    private bool multiplayerGuidance;

    public void SetGroup(CustomerGroup value)
    {
        group = value;
        networkCustomer = value != null ? value.GetComponentInParent<MultiplayerCustomerSpawn>() : null;
        var session = MultiplayerSessionManager.Instance;
        multiplayerGuidance = networkCustomer != null || (session != null && session.IsMultiplayerSession);
    }

    private void LateUpdate()
    {
        // A table number only represents food that is still pending. This
        // self-check also removes an orphaned UI instance if a delivery event
        // changed the group state but missed the normal visual cleanup call.
        if (group == null || group.state != CustomerGroup.GroupState.OrderTaken
            || (multiplayerGuidance && (networkCustomer == null || !networkCustomer.HasLocalDeliveryGuidance)))
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
        if (!TutorialCustomerFlowBridge.AllowsServiceUI("DeliveryPopup")) return;
        if (booth == null) return;

        PlayerMovement player;
        var session = MultiplayerSessionManager.Instance;
        if (multiplayerGuidance || (session != null && session.IsMultiplayerSession))
        {
            if (networkCustomer == null || !networkCustomer.HasLocalDeliveryGuidance) return;
            var manager = session != null ? session.LocalManager : null;
            player = manager != null ? manager.GetComponent<PlayerMovement>() : null;
        }
        else
        {
            if (RoleManager.Instance == null) return;
            if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return;
            player = RoleManager.Instance.GetActivePlayerMovement();
        }
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
