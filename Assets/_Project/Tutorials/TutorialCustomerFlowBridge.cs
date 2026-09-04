using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Observes one real, isolated tutorial customer's complete lobby lifecycle.</summary>
[DefaultExecutionOrder(-8995), DisallowMultipleComponent]
public sealed class TutorialCustomerFlowBridge : MonoBehaviour
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private TutorialSystem tutorial;
    private TutorialDayContext day;
    private CustomerGroup group;
    private GameDayManager gameDay;
    private MainCameraController cameraController;
    private int originalMaxCustomers = -1;
    private string focusedActionKey;
    private bool spawnRequested, cleanupArmed;
    private FoodTrayInteractable cleanupTray;
    private readonly List<(LobbyAutonomousService service, bool enabled)> autonomous = new();
    public CustomerGroup ActiveGroup => group;

    private void Awake() { tutorial = GetComponent<TutorialSystem>(); day = GetComponent<TutorialDayContext>(); }
    private void OnEnable()
    {
        if (tutorial != null) tutorial.SpawnPermissionsChanged += OnSpawnPermissionsChanged;
        CaptureAndSuppressNormalShiftSpawning();
        SuppressAutonomousService();
    }
    private void OnDisable()
    {
        if (tutorial != null) tutorial.SpawnPermissionsChanged -= OnSpawnPermissionsChanged;
        foreach (var state in autonomous) if (state.service != null) state.service.enabled = state.enabled;
        autonomous.Clear();
        if (gameDay != null && originalMaxCustomers >= 0)
            WriteInt(gameDay, "maxCustomersToSpawn", originalMaxCustomers);
    }
    private void OnSpawnPermissionsChanged(bool customers, bool staff)
    {
        if (!customers || tutorial == null || tutorial.IsComplete) return;
        GroupSpawner.Instance?.SetAutoSpawn(false); spawnRequested = true;
    }

    private void Update()
    {
        if (tutorial == null || tutorial.IsComplete) return;
        CaptureAndSuppressNormalShiftSpawning();
        SuppressAutonomousService();
        if (day == null) day = GetComponent<TutorialDayContext>();
        if (tutorial.AllowCustomerSpawning && spawnRequested && group == null && GameDayManager.Instance != null &&
            GameDayManager.Instance.ShiftRunning && GroupSpawner.Instance != null)
        {
            if (day != null && !day.PrepareCustomerMenu()) return;
            GroupSpawner.Instance.SetAutoSpawn(false); group = GroupSpawner.Instance.SpawnGroup();
            spawnRequested = group == null; if (group != null) group.SetPatienceSeconds(3600f);
        }
        if (!tutorial.IsWaitingForGameplayAction || tutorial.CurrentStep == null) return;
        string key = tutorial.CurrentStep.ActionKey;
        FocusCurrentWorldAction(key);
        if (key == "Customer.TrayCleaned" && !cleanupArmed)
        {
            cleanupTray = FindTray(FoodTrayInteractable.TrayMode.None);
            if (cleanupTray != null) { cleanupTray.SetCleanupPickable(true); cleanupArmed = true; }
        }
        if (IsComplete(key)) tutorial.NotifyAction(key, group);
    }

    private bool IsComplete(string key)
    {
        WaiterHands hands = WaiterHands.ActivePlayerHands;
        switch (key)
        {
            case "Customer.Arrived": return group != null && (group.state == CustomerGroup.GroupState.WalkingToLobby || group.state == CustomerGroup.GroupState.Waiting);
            case "Customer.Selected": return group != null && FindGreetButton() != null;
            case "Customer.Greeted": return group != null && group.hasBeenGreeted;
            case "Customer.Seated": return group != null && group.assignedBooth != null && group.state >= CustomerGroup.GroupState.Seated && group.state < CustomerGroup.GroupState.Leaving;
            case "Customer.ReadyToOrder": return group != null && group.state == CustomerGroup.GroupState.ReadyToOrder;
            case "Customer.NotepadOpened": return group != null && group.IsPlayerReviewingOrder && OrderChecklistUI.Instance != null && OrderChecklistUI.Instance.gameObject.activeInHierarchy;
            case "Customer.NotepadFoodSelected": return IsOrderLineSelected(false);
            case "Customer.NotepadDrinkSelected": return IsOrderLineSelected(true);
            case "Customer.NotepadSelectionCorrect": return IsNotepadSelectionCorrect();
            case "Customer.OrderConfirmed": return group != null && group.HasConfirmedOrder && group.state == CustomerGroup.GroupState.OrderTaken;
            case "Customer.FoodReady": return FindTray(FoodTrayInteractable.TrayMode.Delivery) != null;
            case "Customer.TrayPickedUp": return hands != null && hands.HasTray && hands.holdingTray != null && hands.holdingTray.TargetGroup == group;
            case "Customer.FoodDelivered": return group != null && (group.state == CustomerGroup.GroupState.Eating || group.state == CustomerGroup.GroupState.NeedsBill);
            case "Customer.ReadyForCleanup": return group != null && group.state == CustomerGroup.GroupState.NeedsBill && FindTray(FoodTrayInteractable.TrayMode.None) != null;
            case "Customer.TrayCleaned": return cleanupArmed && cleanupTray == null && (BusserHands.ActivePlayerHands == null || !BusserHands.ActivePlayerHands.HasTray);
            case "Customer.NeedsBill": return group != null && group.state == CustomerGroup.GroupState.NeedsBill;
            case "Customer.BillPrinted": return FindBill() != null;
            case "Customer.BillPickedUp": return hands != null && hands.HasBill && hands.holdingBillFor == group;
            case "Customer.BillDelivered": return group != null && FindMoney() != null;
            case "Customer.PaymentPickedUp": return hands != null && hands.HasMoney && hands.HeldMoney != null && hands.HeldMoney.TargetGroup == group;
            case "Customer.CashierOpened": return CashierRegisterUI.Instance != null && CashierRegisterUI.Instance.IsOpen;
            case "Customer.ChangeCorrect": return CashierRegisterUI.Instance != null && ReadInt(CashierRegisterUI.Instance, "expectedChange") == ReadInt(CashierRegisterUI.Instance, "inputChangeAmount");
            case "Customer.PaymentCompleted": return group == null || group.state == CustomerGroup.GroupState.Leaving;
            default: return false;
        }
    }

    private bool IsNotepadSelectionCorrect()
    {
        if (group == null || OrderChecklistUI.Instance == null || !group.IsPlayerReviewingOrder) return false;
        Dictionary<string, int> selected = new(StringComparer.OrdinalIgnoreCase);
        foreach (NotepadMenuEntryUI entry in OrderChecklistUI.Instance.GetComponentsInChildren<NotepadMenuEntryUI>(true))
            if (entry.SelectedQuantity > 0) selected[entry.ItemId] = entry.SelectedQuantity;
        foreach (CustomerGroup.OrderLine line in group.GetCurrentOrderLines())
        { if (!selected.TryGetValue(line.itemId, out int amount) || amount != line.quantity) return false; selected.Remove(line.itemId); }
        return selected.Count == 0;
    }

    private bool IsOrderLineSelected(bool drink)
    {
        if (group == null || OrderChecklistUI.Instance == null || !group.IsPlayerReviewingOrder) return false;
        foreach (CustomerGroup.OrderLine line in group.GetCurrentOrderLines())
        {
            if (line == null || line.IsDrink(MenuCatalog.Default) != drink) continue;
            foreach (NotepadMenuEntryUI entry in OrderChecklistUI.Instance.GetComponentsInChildren<NotepadMenuEntryUI>(true))
                if (string.Equals(entry.ItemId, line.itemId, StringComparison.OrdinalIgnoreCase))
                    return entry.SelectedQuantity == line.quantity;
            return false;
        }
        return false;
    }

    public RectTransform ResolveUI(string key)
    {
        if (key == "CustomerGreetButton") return FindGreetButton()?.transform as RectTransform;
        if (key == "OrderBubble")
        {
            OrderBubbleUI bubble = FindFirstObjectByType<OrderBubbleUI>(FindObjectsInactive.Exclude);
            return bubble != null ? bubble.GetComponentInChildren<Button>(false)?.transform as RectTransform : null;
        }
        OrderChecklistUI note = OrderChecklistUI.Instance;
        if (key == "NotepadRoot") return note != null ? note.transform as RectTransform : null;
        if (key == "NotepadRequested") return Read<RectTransform>(note, "requestedIconsRoot");
        if ((key == "NotepadCorrectItem" || key == "NotepadFoodItem" || key == "NotepadDrinkItem") && note != null && group != null)
            foreach (NotepadMenuEntryUI entry in note.GetComponentsInChildren<NotepadMenuEntryUI>(true))
                foreach (CustomerGroup.OrderLine line in group.GetCurrentOrderLines())
                    if (string.Equals(entry.ItemId, line.itemId, StringComparison.OrdinalIgnoreCase) &&
                        (key == "NotepadCorrectItem" || line.IsDrink(MenuCatalog.Default) == (key == "NotepadDrinkItem")))
                        return entry.transform as RectTransform;
        if (key == "NotepadConfirm") return Read<Button>(note, "confirmButton")?.transform as RectTransform;
        CashierRegisterUI cash = CashierRegisterUI.Instance;
        if (key == "CashierRoot") return cash != null ? cash.transform as RectTransform : null;
        if (key == "CashierChangeControls") return Read<Button>(cash, "bill100Button")?.transform.parent as RectTransform;
        if (key == "CashierConfirm") return Read<Button>(cash, "confirmButton")?.transform as RectTransform;
        return null;
    }

    public Transform ResolveWorld(string key)
    {
        if (key == "TutorialCustomer")
        {
            if (group == null || group.members == null) return null;
            foreach (CustomerAgent member in group.members)
                if (member != null && member.gameObject.activeInHierarchy) return member.transform;
            return null;
        }
        if (key == "TutorialBooth") return BoothAssignArrowManager.Instance != null ? BoothAssignArrowManager.Instance.GetSuggestedBooth(group)?.transform : null;
        if (key == "TutorialFoodTray") return FindTray(FoodTrayInteractable.TrayMode.Delivery)?.transform;
        if (key == "TutorialCleanupTray") return (cleanupTray != null ? cleanupTray : FindTray(FoodTrayInteractable.TrayMode.None))?.transform;
        if (key == "TutorialCashier") return FindFirstObjectByType<CashierBoothInteractable>(FindObjectsInactive.Exclude)?.transform;
        if (key == "TutorialBill") return FindBill()?.transform;
        if (key == "TutorialPayment") return FindMoney()?.transform;
        return null;
    }

    private FoodTrayInteractable FindTray(FoodTrayInteractable.TrayMode mode)
    {
        foreach (FoodTrayInteractable item in FindObjectsByType<FoodTrayInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        { FoodTray tray = item.GetComponent<FoodTray>(); if (tray != null && tray.TargetGroup == group && item.CurrentMode == mode) return item; }
        return null;
    }
    private BillPaper FindBill() { foreach (BillPaper item in FindObjectsByType<BillPaper>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (item.TargetGroup == group) return item; return null; }
    private MoneyPickup FindMoney() { foreach (MoneyPickup item in FindObjectsByType<MoneyPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (item.TargetGroup == group) return item; return null; }
    private static Button FindGreetButton() { foreach (CustomerGreetBubbleUI bubble in FindObjectsByType<CustomerGreetBubbleUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) foreach (Button button in bubble.GetComponentsInChildren<Button>(false)) if (button.interactable) return button; return null; }
    private static T Read<T>(object owner, string field) where T : class => owner?.GetType().GetField(field, PrivateInstance)?.GetValue(owner) as T;
    private static int ReadInt(object owner, string field) => owner?.GetType().GetField(field, PrivateInstance)?.GetValue(owner) is int value ? value : int.MinValue;

    private void CaptureAndSuppressNormalShiftSpawning()
    {
        if (gameDay == null) gameDay = GameDayManager.Instance;
        if (gameDay == null) return;
        FieldInfo field = gameDay.GetType().GetField("maxCustomersToSpawn", PrivateInstance);
        if (field == null) return;
        if (originalMaxCustomers < 0 && field.GetValue(gameDay) is int current)
            originalMaxCustomers = current;
        // GameDayManager owns a separate timed spawn loop from GroupSpawner.
        // The tutorial manually creates exactly one controlled customer below.
        field.SetValue(gameDay, 0);
    }

    private void SuppressAutonomousService()
    {
        foreach (LobbyAutonomousService service in FindObjectsByType<LobbyAutonomousService>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool known = false;
            for (int i = 0; i < autonomous.Count; i++)
                if (autonomous[i].service == service) { known = true; break; }
            if (!known) autonomous.Add((service, service.enabled));
            service.enabled = false;
        }
    }

    private void FocusCurrentWorldAction(string actionKey)
    {
        TutorialSystem.TutorialStep step = tutorial.CurrentStep;
        if (step == null || string.IsNullOrWhiteSpace(step.WorldTargetKey) ||
            string.Equals(focusedActionKey, actionKey, StringComparison.Ordinal)) return;
        Transform target = ResolveWorld(step.WorldTargetKey);
        if (target == null) return;
        if (cameraController == null)
            cameraController = FindFirstObjectByType<MainCameraController>(FindObjectsInactive.Exclude);
        if (cameraController == null) return;
        cameraController.SetRigTargetPosition(target.position);
        focusedActionKey = actionKey;
    }

    private static void WriteInt(object owner, string field, int value) =>
        owner?.GetType().GetField(field, PrivateInstance)?.SetValue(owner, value);
}
