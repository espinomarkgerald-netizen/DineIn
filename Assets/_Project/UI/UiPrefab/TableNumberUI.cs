using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TableNumberUI : MonoBehaviour
{
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private Booth booth;
    private CustomerGroup group;
    private MultiplayerCustomerSpawn networkCustomer;
    private bool multiplayerGuidance;
    private bool tableBound, serveAvailable, presentationInitialized;
    private int displayedNumber;
    private Button actionButton;
    private BoothDeliverInteractable boothDelivery;
    private CustomerDeliverInteractable customerDelivery;

    private void OnEnable()
    {
        WaiterHands.OnHandsStateChanged += OnHandsChanged;
        presentationInitialized = false;
        RefreshPresentation();
    }
    private void OnDisable()
    {
        WaiterHands.OnHandsStateChanged -= OnHandsChanged;
        GetComponentInParent<PlayerTaskBubbleFocus>()?.SetLocalDeliveryAction(false);
    }
    private void OnHandsChanged(WaiterHands _) => RefreshPresentation();

    public void SetGroup(CustomerGroup value)
    {
        group = value;
        tableBound = true;
        networkCustomer = value != null ? value.GetComponentInParent<MultiplayerCustomerSpawn>() : null;
        var session = MultiplayerSessionManager.Instance;
        multiplayerGuidance = networkCustomer != null || (session != null && session.IsMultiplayerSession);
        if (FineDiningReservations.For(value) != null)
            PlayerTaskBubbleFocus.Bind(gameObject, value, true);
        RefreshPresentation();
    }

    private void LateUpdate()
    {
        // FoodTray also uses this component for a passive identifier, without SetGroup.
        if (!tableBound) return;
        // A table number only represents food that is still pending. This
        // self-check also removes an orphaned UI instance if a delivery event
        // changed the group state but missed the normal visual cleanup call.
        bool pendingDelivery = group != null && group.state == CustomerGroup.GroupState.OrderTaken;
        if (group != null && group.IsTakeout && !multiplayerGuidance)
        {
            var bag = TakeoutBagInteractable.LocalHeldBag;
            pendingDelivery = TakeoutBagInteractable.PlayerHasHeldBag && bag != null && bag.TargetGroup == group
                && (group.state == CustomerGroup.GroupState.OrderTaken || group.state == CustomerGroup.GroupState.Waiting
                    || group.state == CustomerGroup.GroupState.WaitingToOrder);
        }
        if (group == null || !pendingDelivery
            || (multiplayerGuidance && (networkCustomer == null || !networkCustomer.HasLocalDeliveryGuidance)))
        {
            Destroy(gameObject);
            return;
        }
        RefreshPresentation();
    }

    public void SetNumber(int number)
    {
        displayedNumber = number;
        if (numberText != null)
            numberText.text = number.ToString();
        RefreshPresentation();
    }

    public void SetBooth(Booth b)
    {
        booth = b;
        boothDelivery = b != null ? b.GetComponentInChildren<BoothDeliverInteractable>(true) : null;
        customerDelivery = b != null ? b.GetComponentInChildren<CustomerDeliverInteractable>(true) : null;
        RefreshPresentation();
    }

    private bool CanShowServe()
    {
        return tableBound && CanShowServe(group, booth, displayedNumber, networkCustomer, multiplayerGuidance,
            boothDelivery, customerDelivery);
    }

    internal static bool CanShowServe(CustomerGroup group, Booth booth, int displayedNumber,
        MultiplayerCustomerSpawn networkCustomer, bool multiplayerGuidance,
        BoothDeliverInteractable boothDelivery, CustomerDeliverInteractable customerDelivery)
    {
        if (group == null || !group.isActiveAndEnabled || group.IsTakeout || booth == null ||
            group.state != CustomerGroup.GroupState.OrderTaken || group.assignedBooth != booth ||
            !group.AllowsStaffFoodDelivery || !group.HasConfirmedOrder || group.IsPlayerReviewingOrder ||
            booth.CurrentGroup != group || group.currentOrderNumber != displayedNumber ||
            !TutorialCustomerFlowBridge.AllowsServiceUI("DeliveryPopup")) return false;
        var session = MultiplayerSessionManager.Instance;
        bool network = multiplayerGuidance || (session != null && session.IsMultiplayerSession);
        if (network)
        {
            if (networkCustomer == null || !networkCustomer.HasLocalDeliveryGuidance) return false;
        }
        else if (RoleManager.Instance == null || !RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return false;
        var hands = WaiterHands.ActivePlayerHands;
        var tray = hands != null ? hands.holdingTray : null;
        if (tray == null || !tray.gameObject.activeInHierarchy || tray.TargetGroup != group ||
            tray.orderNumber != group.currentOrderNumber || !tray.transform.IsChildOf(hands.TrayHoldPoint)) return false;
        // Retain the delivery owners' current eligibility/claim and network rules as well.
        return (!group.HasWineRefillRequest && boothDelivery != null && boothDelivery.CanInteract()) ||
            (customerDelivery != null && customerDelivery.CanInteract());
    }

    private void RefreshPresentation()
    {
        if (!tableBound || group == null || booth == null) return;
        bool available = CanShowServe();
        if (presentationInitialized && available == serveAvailable)
        {
            // Claim changes can alter input without changing the displayed caption.
            var existingFocus = GetComponent<PlayerTaskBubbleFocus>();
            if (existingFocus == null) existingFocus = GetComponentInParent<PlayerTaskBubbleFocus>();
            existingFocus?.SetLocalDeliveryAction(available);
            existingFocus?.Refresh();
            return;
        }
        if (actionButton == null) actionButton = GetComponentInChildren<Button>(true);
        if (actionButton == null) return;
        var skin = available
            ? GameplayActionButtonSkin.Apply(actionButton, "IconServeFood", "Serve food", "Green", numberText)
            : GameplayActionButtonSkin.TableNumber(actionButton, numberText);
        if (skin == null) return;
        serveAvailable = available;
        presentationInitialized = true;
        var focus = PlayerTaskBubbleFocus.Bind(gameObject, group, !available);
        focus?.SetLocalDeliveryAction(available);
        focus?.RefreshPresentation();
    }

    public void OnClickMoveToTable()
    {
        if(!serveAvailable && CustomerDrawerView.TryInspectGroupFromUI(group))return;
        // A click on an expired Serve prompt must not fall through into ordinary navigation.
        if (serveAvailable && !CanShowServe()) { RefreshPresentation(); return; }
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

        if (serveAvailable)
        {
            IInteractable delivery = !group.HasWineRefillRequest && boothDelivery != null && boothDelivery.CanInteract()
                ? boothDelivery : customerDelivery;
            if (delivery == null || !delivery.CanInteract()) return;
            // Use the existing deferred movement owner. Revalidate the same customer,
            // order and held tray on arrival, before invoking its real delivery handler.
            var pendingFollow = GetComponentInParent<UIFollowWorldPoint>();
            if (pendingFollow == null) pendingFollow = GetComponentInChildren<UIFollowWorldPoint>(true);
            bool accepted = player.UI_MoveToAction(delivery.StandPoint, delivery.GetInteractRadius(), () =>
            {
                if (this != null && CanShowServe() && delivery.CanInteract()) delivery.Interact(player);
                if (pendingFollow != null) pendingFollow.SetActionPending(false);
            }, () => { if (pendingFollow != null) pendingFollow.SetActionPending(false); if (this != null) RefreshPresentation(); });
            if (accepted && pendingFollow != null) pendingFollow.SetActionPending(true);
            return;
        }

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
