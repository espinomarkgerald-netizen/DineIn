using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
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
    private Button observedButton;
    private string observedButtonKey;
    private bool observedButtonClicked;
    private UnityAction observedButtonListener;
    private TutorialServicePointerGuard releaseGuard;
    private Coroutine releaseGuardRoutine;
    private PlayerMovement releaseGuardMovement;
    private bool restoreReleaseGuardControl;
    private bool spawnRequested, cleanupArmed;
    private FoodTrayInteractable cleanupTray;
    private readonly List<AutoInteractRadius> suppressedBillAutoPickup = new();
    private readonly List<(LobbyAutonomousService service, bool enabled)> autonomous = new();
    private readonly Dictionary<CustomerGroup, bool> practiceGroups = new();
    private readonly List<CustomerGroup> departedPracticeGroups = new();
    private string practiceRound;
    private int practiceCompleted;
    private int practiceSpawned;
    private int pendingPracticeSpawns;
    private readonly HashSet<CustomerGroup> eatingPermits = new();
    private bool staffDemonstration;
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
        ClearObservedButton();
        ClearReleaseGuard();
        foreach (AutoInteractRadius radius in suppressedBillAutoPickup)
            if (radius != null) radius.enabled = true;
        suppressedBillAutoPickup.Clear();
    }
    private void OnSpawnPermissionsChanged(bool customers, bool staff)
    {
        if (!customers || tutorial == null || tutorial.IsComplete) return;
        GroupSpawner.Instance?.SetAutoSpawn(false); spawnRequested = true;
    }

    private static readonly List<RaycastResult> tutorialPressHits = new();
    public static bool IsTutorialUIPress(Vector2 position, int pointerId)
    {
        if (EventSystem.current == null) return false;
        tutorialPressHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            { position = position, pointerId = pointerId }, tutorialPressHits);
        foreach (RaycastResult hit in tutorialPressHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    private void Update()
    {
        // ManagerPlayer.LateUpdate restores its input flag every frame. Reassert
        // before PlayerMovement.Update, including the release frame, without
        // StopForRoleSwitch/SetExternalInputSuppressed cancelling the UI task.
        if (releaseGuardRoutine != null && releaseGuardMovement != null)
            releaseGuardMovement.SetPlayerControlled(false);
        if (tutorial == null || tutorial.IsComplete) return;
        CaptureAndSuppressNormalShiftSpawning();
        // The guided lessons and both rounds share one shift. Customer spawning
        // is controlled separately; its cap does not stop the ordinary day clock.
        if (gameDay != null && gameDay.ShiftRunning && gameDay.TimeRemaining < 60f)
            typeof(GameDayManager).GetField("timeRemaining", PrivateInstance)?.SetValue(gameDay, 60f);
        SuppressAutonomousService();
        if (tutorial.CurrentStep?.Id == "practice_staff_booths")
            GetComponent<TutorialBoothAvailability>()?.OpenPracticeBooths();
        if (tutorial.CurrentPhase == TutorialSystem.TutorialPhase.NormalGameplay)
        {
            TickPractice();
            return;
        }
        if (day == null) day = GetComponent<TutorialDayContext>();
        if (tutorial.AllowCustomerSpawning && spawnRequested && group == null && GameDayManager.Instance != null &&
            GameDayManager.Instance.ShiftRunning && GroupSpawner.Instance != null)
        {
            if (day != null && !day.PrepareCustomerMenu()) return;
            GroupSpawner.Instance.SetAutoSpawn(false); group = GroupSpawner.Instance.SpawnGroup();
            spawnRequested = group == null;
            if (group != null)
            {
                group.SetPatienceSeconds(3600f);
                group.minOrderPatience = 3600f;
                group.maxOrderPatience = 3600f;
            }
        }
        if (!tutorial.IsWaitingForGameplayAction || tutorial.CurrentStep == null) return;
        string key = tutorial.CurrentStep.ActionKey;
        SuppressPrintedBillAutoPickup();
        ObserveRequiredButton(key);
        BindRequiredUIReleaseGuard();
        RefreshLiveUIActionTarget();
        FocusCurrentWorldAction(key);
        if (key == "Customer.TrayCleaned" && !cleanupArmed)
        {
            cleanupTray = FindTray(FoodTrayInteractable.TrayMode.None);
            if (cleanupTray != null) { cleanupTray.SetCleanupPickable(true); cleanupArmed = true; }
        }
        if (IsComplete(key)) tutorial.NotifyAction(key, group);
    }

    private IEnumerator StartPracticeStaff()
    {
        // Let Unity deliver any first Start before restarting the real service
        // coroutine stopped for solo practice. Avoid two concurrent service loops.
        yield return null;
        foreach (var state in autonomous)
        {
            if (state.service == null) continue;
            state.service.StopAllCoroutines();
            // Tutorial isolation intentionally keeps IsApplyingSave true. The
            // normal Start coroutine waits on that career-load flag forever.
            // Tutorial setup is already complete: bind and run the same real loop.
            typeof(LobbyAutonomousService).GetMethod("ResolveSceneReferences", PrivateInstance)?.Invoke(state.service, null);
            if (typeof(LobbyAutonomousService).GetMethod("ServiceLoop", PrivateInstance)?.Invoke(state.service, null) is IEnumerator loop)
                state.service.StartCoroutine(loop);
        }
    }

    private void TickPractice()
    {
        if (!tutorial.IsWaitingForGameplayAction) return;
        string key = tutorial.CurrentStep.ActionKey;
        if (key != "Practice.Player" && key != "Practice.Staff") return;
        if (practiceRound != key)
        {
            practiceRound = key;
            practiceCompleted = 0;
            practiceGroups.Clear();
            staffDemonstration = key == "Practice.Staff";
            if (staffDemonstration) StartCoroutine(StartPracticeStaff());
            foreach (AutoInteractRadius radius in suppressedBillAutoPickup)
                if (radius != null) radius.enabled = true;
            suppressedBillAutoPickup.Clear();
            ClearReleaseGuard();
            practiceSpawned = 0;
            pendingPracticeSpawns = staffDemonstration ? 2 : 1;
            eatingPermits.Clear();
        }
        departedPracticeGroups.Clear();
        foreach (var entry in practiceGroups)
        {
            CustomerGroup customer = entry.Key;
            if (customer != null && customer.state == CustomerGroup.GroupState.Eating && eatingPermits.Add(customer))
                pendingPracticeSpawns++;
            if (customer == null)
            {
                if (entry.Value) practiceCompleted++;
                departedPracticeGroups.Add(customer);
            }
            else if (customer.state == CustomerGroup.GroupState.Leaving &&
                     typeof(CustomerGroup).GetField("finalResult", PrivateInstance)?.GetValue(customer) is CustomerGroup.FinalResult result &&
                     result == CustomerGroup.FinalResult.Happy)
                departedPracticeGroups.Add(customer);
        }
        foreach (CustomerGroup customer in departedPracticeGroups)
            if (customer == null) practiceGroups.Remove(customer);
            else practiceGroups[customer] = true;
        int required = staffDemonstration ? 5 : 3;
        if (practiceCompleted >= required && practiceGroups.Count == 0)
        {
            tutorial.NotifyAction(key);
            return;
        }
        // Eating grants one admission, even while both slots are occupied.
        // Keep that permit until capacity opens; never turn an Eating customer
        // into a departed customer just to satisfy the cap.
        while (practiceGroups.Count < 2 && practiceSpawned < required && pendingPracticeSpawns > 0)
        {
            if (GroupSpawner.Instance == null) return;
            if (day == null) day = GetComponent<TutorialDayContext>();
            if (day == null || !day.PrepareCustomerMenu()) return;
            GroupSpawner.Instance.SetAutoSpawn(false);
            CustomerGroup spawned = GroupSpawner.Instance.SpawnGroup();
            if (spawned == null) return;
            practiceGroups.Add(spawned, false);
            practiceSpawned++;
            pendingPracticeSpawns--;
        }
    }

    private bool IsComplete(string key)
    {
        WaiterHands hands = WaiterHands.ActivePlayerHands;
        switch (key)
        {
            case "Customer.FrontOfLine":
            {
                LobbyLineManager line = FindFirstObjectByType<LobbyLineManager>(FindObjectsInactive.Exclude);
                return group != null && line != null && line.IsFrontOfLine(group) &&
                       group.state == CustomerGroup.GroupState.Waiting;
            }
            case "Customer.Selected": return group != null && FindGreetButton() != null;
            // MarkGreeted happens before the world-space action bubble is rebuilt.
            // Keep this step alive until the real Seat Table button is available.
            case "Customer.Greeted": return group != null && group.hasBeenGreeted &&
                                              FindCustomerActionButton("Seat Table") != null;
            case "Customer.SeatModeStarted":
                return group != null && BoothAssignArrowManager.Instance != null &&
                       BoothAssignArrowManager.Instance.ActiveSuggestedBooth != null;
            case "Customer.Seated": return group != null && group.assignedBooth != null && group.state >= CustomerGroup.GroupState.Seated && group.state < CustomerGroup.GroupState.Leaving;
            case "Customer.ReadyToOrder":
                if (group == null || group.state != CustomerGroup.GroupState.ReadyToOrder) return false;
                NormalizeTutorialOrder();
                return true;
            case "Customer.NotepadOpened": return group != null && group.IsPlayerReviewingOrder && OrderChecklistUI.Instance != null && OrderChecklistUI.Instance.gameObject.activeInHierarchy;
            case "Customer.NotepadFoodTab": return observedButtonClicked && IsNotepadTabVisible(false);
            case "Customer.NotepadDrinkTab": return observedButtonClicked && IsNotepadTabVisible(true);
            case "Customer.NotepadFoodQuantity": return IsOrderLineSelected(false);
            case "Customer.NotepadDrinkQuantity": return IsOrderLineSelected(true);
            case "Customer.NotepadSelectionCorrect": return IsNotepadSelectionCorrect();
            case "Customer.NotepadChecked": return IsCorrectOrderReviewOpen();
            case "Customer.OrderConfirmed": return group != null && group.HasConfirmedOrder && group.state == CustomerGroup.GroupState.OrderTaken;
            case "Customer.FoodReady":
            {
                FoodTrayInteractable tray = FindTray(FoodTrayInteractable.TrayMode.Delivery);
                return tray != null && ResolveTrayPickupButton(tray) != null;
            }
            case "Customer.TrayPickedUp": return hands != null && hands.HasTray && hands.holdingTray != null && hands.holdingTray.TargetGroup == group;
            case "Customer.FoodDelivered": return group != null && (group.state == CustomerGroup.GroupState.Eating || group.state == CustomerGroup.GroupState.NeedsBill);
            case "Customer.ReadyForCleanup":
            {
                if (group == null || group.state != CustomerGroup.GroupState.Leaving) return false;
                if (!cleanupArmed)
                {
                    cleanupTray = FindTray(FoodTrayInteractable.TrayMode.None) ??
                                  FindTray(FoodTrayInteractable.TrayMode.Cleanup);
                    if (cleanupTray == null) return false;
                    if (cleanupTray.CurrentMode == FoodTrayInteractable.TrayMode.None)
                        cleanupTray.SetCleanupPickable(true);
                    cleanupArmed = true;
                }
                return ResolveTrayPickupButton(cleanupTray) != null;
            }
            case "Customer.TrayCleaned": return cleanupArmed && cleanupTray == null && (BusserHands.ActivePlayerHands == null || !BusserHands.ActivePlayerHands.HasTray);
            case "Customer.NeedsBill": return group != null && group.state == CustomerGroup.GroupState.NeedsBill;
            case "Customer.BillPrinted":
            {
                BillPaper bill = FindBill();
                return bill != null && ResolveBillPickupButton(bill) != null;
            }
            case "Customer.BillPickedUp": return hands != null && hands.HasBill && hands.holdingBillFor == group;
            case "Customer.BillDelivered":
            {
                MoneyPickup money = FindMoney();
                return group != null && money != null && ResolvePaymentPickupButton(money) != null;
            }
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

    private void NormalizeTutorialOrder()
    {
        if (group.currentOrder == null) return;
        List<CustomerGroup.OrderLine> lines = new();
        foreach (CustomerGroup.OrderLine source in group.GetCurrentOrderLines())
        {
            if (source == null) continue;
            CustomerGroup.OrderLine line = source.Clone();
            line.quantity = 1;
            lines.Add(line);
        }
        if (lines.Count > 0) group.currentOrder.SetLines(lines, MenuCatalog.Default);
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

    private TutorialSystem.TutorialStep popupStep;
    private string popupKey;
    private RectTransform popupTarget;

    public RectTransform ResolveUI(string key)
    {
        bool stablePopup = key == "OrderBubble" || key == "DeliveryPopup" || key == "BillRequestPopup" ||
                           key == "TrayPickupButton" || key == "BillPickupButton" || key == "PaymentPickupButton";
        if (!stablePopup) return ResolveUIUncached(key);
        if (popupStep == tutorial?.CurrentStep && popupKey == key && popupTarget != null &&
            popupTarget.gameObject.activeInHierarchy) return popupTarget;
        popupStep = tutorial?.CurrentStep;
        popupKey = key;
        popupTarget = ResolveUIUncached(key);
        return popupTarget;
    }

    private RectTransform ResolveUIUncached(string key)
    {
        if (key == "CustomerGreetButton") return FindGreetButton()?.transform as RectTransform;
        if (key == "CustomerSeatButton") return FindCustomerActionButton("Seat Table")?.transform as RectTransform;
        if (key == "OrderBubble")
        {
            foreach (OrderBubbleUI bubble in FindObjectsByType<OrderBubbleUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (bubble == null || !bubble.gameObject.activeInHierarchy || Read<CustomerGroup>(bubble, "group") != group) continue;
                Button open = Read<Button>(bubble, "openButton") ?? bubble.GetComponentInChildren<Button>(true);
                if (VisibleButtonRect(open) != null) return VisibleButtonRect(open);
            }
            return null;
        }
        if (key == "DeliveryPopup")
        {
            foreach (TableNumberUI popup in FindObjectsByType<TableNumberUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (Read<CustomerGroup>(popup, "group") == group)
                {
                    RectTransform target = VisibleButtonRect(popup.GetComponent<Button>() ?? popup.GetComponentInChildren<Button>(true));
                    if (target != null) return target;
                }
            return null;
        }
        if (key == "BillRequestPopup") return ResolveBillRequestPopup();
        if (key == "TrayPickupButton")
            return ResolveTrayPickupButton(FindTray(FoodTrayInteractable.TrayMode.Delivery));
        if (key == "CleanupPickupButton")
            return ResolveTrayPickupButton(cleanupTray != null
                ? cleanupTray
                : FindTray(FoodTrayInteractable.TrayMode.Cleanup));
        if (key == "BillPickupButton") return ResolveBillPickupButton(FindBill());
        if (key == "PaymentPickupButton") return ResolvePaymentPickupButton(FindMoney());
        OrderChecklistUI note = OrderChecklistUI.Instance;
        if (key == "NotepadRoot") return note != null ? note.transform as RectTransform : null;
        if (key == "NotepadRequested") return Read<RectTransform>(note, "requestedIconsRoot");
        if (key == "NotepadFoodTab") return Read<Button>(note, "foodTabButton")?.transform as RectTransform;
        if (key == "NotepadDrinkTab") return Read<Button>(note, "drinkTabButton")?.transform as RectTransform;
        if (key == "NotepadFoodAdjust") return ResolveQuantityButton(false);
        if (key == "NotepadDrinkAdjust") return ResolveQuantityButton(true);
        if (key == "NotepadReviewSubmit") return Read<Button>(note, "reviewSubmitButton")?.transform as RectTransform;
        if ((key == "NotepadCorrectItem" || key == "NotepadFoodItem" || key == "NotepadDrinkItem") && note != null && group != null)
            foreach (NotepadMenuEntryUI entry in note.GetComponentsInChildren<NotepadMenuEntryUI>(true))
                foreach (CustomerGroup.OrderLine line in group.GetCurrentOrderLines())
                    if (string.Equals(entry.ItemId, line.itemId, StringComparison.OrdinalIgnoreCase) &&
                        (key == "NotepadCorrectItem" || line.IsDrink(MenuCatalog.Default) == (key == "NotepadDrinkItem")))
                        return entry.transform as RectTransform;
        if (key == "NotepadConfirm") return Read<Button>(note, "confirmButton")?.transform as RectTransform;
        CashierRegisterUI cash = CashierRegisterUI.Instance;
        if (key == "CashierRoot") return cash != null && cash.IsOpen ? Read<GameObject>(cash, "root")?.transform as RectTransform : null;
        if (key == "CashierDisplay") return cash != null && cash.IsOpen ? Read<TMPro.TMP_Text>(cash, "cashierChangeText")?.rectTransform : null;
        if (key == "CashierReceived") return cash != null && cash.IsOpen ? Read<TMPro.TMP_Text>(cash, "receivedText")?.rectTransform : null;
        if (key == "CashierRequiredChange") return cash != null && cash.IsOpen ? Read<TMPro.TMP_Text>(cash, "changeText")?.rectTransform : null;
        if (key == "CashierChangeControls" || key == "CashierNextMoneyButton")
            return ResolveNextCashierMoneyButton(cash);
        if (key == "CashierConfirm")
        {
            if (cash == null || !cash.IsOpen ||
                ReadInt(cash, "inputChangeAmount") != ReadInt(cash, "expectedChange")) return null;
            return VisibleButtonRect(Read<Button>(cash, "confirmButton"));
        }
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
        if (key == "TutorialBooth") return BoothAssignArrowManager.Instance != null ? BoothAssignArrowManager.Instance.GetSuggestionTarget(group) : null;
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
    private static Button FindGreetButton() => FindCustomerActionButton("Greet Customer");
    private static Button FindCustomerActionButton(string label)
    {
        foreach (CustomerGreetBubbleUI bubble in FindObjectsByType<CustomerGreetBubbleUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            foreach (Button button in bubble.GetComponentsInChildren<Button>(false))
            {
                if (!button.interactable) continue;
                foreach (TMPro.TMP_Text text in button.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (string.Equals(text.text?.Trim(), label, StringComparison.OrdinalIgnoreCase)) return button;
            }
        return null;
    }
    private static T Read<T>(object owner, string field) where T : class => owner?.GetType().GetField(field, PrivateInstance)?.GetValue(owner) as T;
    private static int ReadInt(object owner, string field) => owner?.GetType().GetField(field, PrivateInstance)?.GetValue(owner) is int value ? value : int.MinValue;

    private RectTransform ResolveBillRequestPopup()
    {
        if (group == null) return null;
        foreach (BillBubbleUI bubble in FindObjectsByType<BillBubbleUI>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (bubble == null || Read<CustomerGroup>(bubble, "group") != group) continue;
            RectTransform rect = VisibleButtonRect(bubble.GetComponent<Button>() ??
                                                   bubble.GetComponentInChildren<Button>(true));
            if (rect != null) return rect;
        }
        return null;
    }

    private RectTransform ResolveTrayPickupButton(FoodTrayInteractable wanted)
    {
        if (wanted == null) return null;
        foreach (TrayPickupUIButton pickup in FindObjectsByType<TrayPickupUIButton>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pickup == null || Read<FoodTrayInteractable>(pickup, "tray") != wanted) continue;
            RectTransform rect = VisibleButtonRect(Read<Button>(pickup, "button") ?? pickup.GetComponentInChildren<Button>(true));
            if (rect != null) return rect;
        }
        return null;
    }

    private RectTransform ResolveBillPickupButton(BillPaper wanted)
    {
        if (wanted == null) return null;
        foreach (BillPaperPickupButton pickup in FindObjectsByType<BillPaperPickupButton>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pickup == null || Read<BillPaper>(pickup, "bill") != wanted) continue;
            RectTransform rect = VisibleButtonRect(Read<Button>(pickup, "button") ?? pickup.GetComponentInChildren<Button>(true));
            if (rect != null) return rect;
        }
        return null;
    }

    private RectTransform ResolvePaymentPickupButton(MoneyPickup wanted)
    {
        if (wanted == null) return null;
        foreach (MoneyBubbleUI bubble in FindObjectsByType<MoneyBubbleUI>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (bubble == null || Read<MoneyPickup>(bubble, "money") != wanted) continue;
            RectTransform rect = VisibleButtonRect(Read<Button>(bubble, "button") ?? bubble.GetComponentInChildren<Button>(true));
            if (rect != null) return rect;
        }
        return null;
    }

    private static RectTransform ResolveNextCashierMoneyButton(CashierRegisterUI cash)
    {
        if (cash == null || !cash.IsOpen) return null;
        int expected = ReadInt(cash, "expectedChange");
        int entered = ReadInt(cash, "inputChangeAmount");
        if (expected == int.MinValue || entered == int.MinValue) return null;
        if (entered > expected) return VisibleButtonRect(Read<Button>(cash, "undoButton"));

        int remaining = expected - entered;
        if (remaining <= 0) return null;
        (int value, string field)[] denominations =
        {
            (1000, "bill1000Button"), (500, "bill500Button"),
            (200, "bill200Button"), (100, "bill100Button"), (50, "bill50Button"),
            (20, "coin20Button"), (10, "coin10Button"), (5, "coin5Button"), (1, "coin1Button")
        };
        foreach ((int value, string field) in denominations)
            if (value <= remaining)
            {
                RectTransform rect = VisibleButtonRect(Read<Button>(cash, field));
                if (rect != null) return rect;
            }
        return null;
    }

    private static RectTransform VisibleButtonRect(Button button)
    {
        if (button == null || !button.interactable || !button.gameObject.activeInHierarchy) return null;
        RectTransform rect = button.transform as RectTransform;
        if (rect == null || rect.rect.width <= .5f || rect.rect.height <= .5f) return null;
        for (Transform current = rect; current != null; current = current.parent)
        {
            CanvasGroup canvasGroup = current.GetComponent<CanvasGroup>();
            if (canvasGroup != null && (canvasGroup.alpha <= .01f || !canvasGroup.interactable)) return null;
        }
        return rect;
    }

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
            service.enabled = staffDemonstration;
            if (tutorial != null && tutorial.CurrentPhase == TutorialSystem.TutorialPhase.NormalGameplay && !staffDemonstration)
                service.StopAllCoroutines();
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

    private RectTransform ResolveQuantityButton(bool drink)
    {
        if (!TryGetRequestedEntry(drink, out CustomerGroup.OrderLine line, out NotepadMenuEntryUI entry)) return null;
        string field = entry.SelectedQuantity < line.quantity ? "increaseButton" :
            entry.SelectedQuantity > line.quantity ? "decreaseButton" : null;
        return field != null ? Read<Button>(entry, field)?.transform as RectTransform : null;
    }

    private bool TryGetRequestedEntry(bool drink, out CustomerGroup.OrderLine requested, out NotepadMenuEntryUI entry)
    {
        requested = null;
        entry = null;
        if (group == null || OrderChecklistUI.Instance == null) return false;
        foreach (CustomerGroup.OrderLine line in group.GetCurrentOrderLines())
            if (line != null && line.IsDrink(MenuCatalog.Default) == drink) { requested = line; break; }
        if (requested == null) return false;
        foreach (NotepadMenuEntryUI candidate in OrderChecklistUI.Instance.GetComponentsInChildren<NotepadMenuEntryUI>(true))
            if (string.Equals(candidate.ItemId, requested.itemId, StringComparison.OrdinalIgnoreCase))
            { entry = candidate; return true; }
        return false;
    }

    private bool IsNotepadTabVisible(bool drink)
    {
        OrderChecklistUI note = OrderChecklistUI.Instance;
        RectTransform content = Read<RectTransform>(note, drink ? "drinkContentRoot" : "foodContentRoot");
        return note != null && note.gameObject.activeInHierarchy && content != null && content.gameObject.activeInHierarchy;
    }

    private bool IsCorrectOrderReviewOpen()
    {
        OrderChecklistUI note = OrderChecklistUI.Instance;
        GameObject overlay = Read<GameObject>(note, "reviewOverlay");
        Button submit = Read<Button>(note, "reviewSubmitButton");
        return note != null && overlay != null && overlay.activeInHierarchy && submit != null && submit.interactable;
    }

    private void ObserveRequiredButton(string key)
    {
        bool needsClick = key == "Customer.NotepadFoodTab" || key == "Customer.NotepadDrinkTab";
        if (!needsClick) { ClearObservedButton(); return; }
        if (observedButtonKey == key && observedButton != null) return;
        ClearObservedButton();
        RectTransform target = ResolveUI(key == "Customer.NotepadFoodTab" ? "NotepadFoodTab" : "NotepadDrinkTab");
        observedButton = target != null ? target.GetComponent<Button>() : null;
        if (observedButton == null) return;
        observedButtonKey = key;
        observedButtonClicked = false;
        observedButtonListener = () => observedButtonClicked = true;
        observedButton.onClick.AddListener(observedButtonListener);
    }

    private void ClearObservedButton()
    {
        if (observedButton != null && observedButtonListener != null)
            observedButton.onClick.RemoveListener(observedButtonListener);
        observedButton = null;
        observedButtonKey = null;
        observedButtonClicked = false;
        observedButtonListener = null;
    }

    private void SuppressPrintedBillAutoPickup()
    {
        if (group == null) return;
        foreach (BillPaper bill in FindObjectsByType<BillPaper>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (bill == null || bill.TargetGroup != group) continue;
            AutoInteractRadius radius = Read<AutoInteractRadius>(bill, "autoRadius") ??
                                        bill.GetComponent<AutoInteractRadius>();
            if (radius == null || suppressedBillAutoPickup.Contains(radius)) continue;
            suppressedBillAutoPickup.Add(radius);
            radius.enabled = false;
        }
    }

    private void RefreshLiveUIActionTarget()
    {
        string key = tutorial.CurrentStep?.UITargetKey;
        if (key != "OrderBubble" && key != "DeliveryPopup" && key != "BillRequestPopup" && key != "TrayPickupButton" && key != "CleanupPickupButton" &&
            key != "BillPickupButton" && key != "PaymentPickupButton" &&
            key != "CashierNextMoneyButton") return;

        RectTransform live = ResolveUI(key);
        if (live != null) tutorial.RefreshLiveActionTarget(live);
    }

    private void BindRequiredUIReleaseGuard()
    {
        string key = tutorial.CurrentStep?.UITargetKey;
        bool transientServiceButton = key == "CustomerGreetButton" || key == "CustomerSeatButton" ||
                                      key == "OrderBubble" || key == "DeliveryPopup" || key == "BillRequestPopup" || key == "TrayPickupButton" ||
                                      key == "CleanupPickupButton" || key == "BillPickupButton" ||
                                      key == "PaymentPickupButton" || key == "CashierConfirm";
        Button live = transientServiceButton ? ResolveUI(key)?.GetComponent<Button>() : null;
        if (live != null && releaseGuard != null && releaseGuard.gameObject == live.gameObject) return;

        if (releaseGuard != null) releaseGuard.End(this);
        releaseGuard = null;
        if (live == null) return;
        releaseGuard = live.GetComponent<TutorialServicePointerGuard>() ??
                       live.gameObject.AddComponent<TutorialServicePointerGuard>();
        releaseGuard.Begin(this);
    }

    internal void GuardDisappearingServiceButtonPress()
    {
        PlayerMovement movement = RoleManager.Instance != null
            ? RoleManager.Instance.GetActivePlayerMovement()
            : null;
        if (movement == null) return;

        bool wasControlled = movement.IsPlayerControlled();
        if (releaseGuardRoutine != null) StopCoroutine(releaseGuardRoutine);
        restoreReleaseGuardControl = restoreReleaseGuardControl || wasControlled;
        releaseGuardMovement = movement;
        movement.SetPlayerControlled(false);
        releaseGuardRoutine = StartCoroutine(RestoreInputAfterReleasedPointer());
    }

    private IEnumerator RestoreInputAfterReleasedPointer()
    {
        yield return new WaitForEndOfFrame();
        while (Input.GetMouseButton(0) || Input.touchCount > 0)
            yield return null;
        // Restore after release processing, before the next intentional press.
        yield return new WaitForEndOfFrame();

        if (releaseGuardMovement != null && restoreReleaseGuardControl)
            releaseGuardMovement.SetPlayerControlled(true);
        releaseGuardMovement = null;
        restoreReleaseGuardControl = false;
        releaseGuardRoutine = null;
    }

    private void ClearReleaseGuard()
    {
        if (releaseGuard != null) releaseGuard.End(this);
        releaseGuard = null;
        if (releaseGuardRoutine != null) StopCoroutine(releaseGuardRoutine);
        releaseGuardRoutine = null;
        if (releaseGuardMovement != null && restoreReleaseGuardControl)
            releaseGuardMovement.SetPlayerControlled(true);
        releaseGuardMovement = null;
        restoreReleaseGuardControl = false;
    }

    private static void WriteInt(object owner, string field, int value) =>
        owner?.GetType().GetField(field, PrivateInstance)?.SetValue(owner, value);
}

/// <summary>Consumes only the pointer gesture that begins on a transient tutorial service button.</summary>
internal sealed class TutorialServicePointerGuard : MonoBehaviour, IPointerDownHandler
{
    private TutorialCustomerFlowBridge owner;

    public void Begin(TutorialCustomerFlowBridge bridge) => owner = bridge;
    public void End(TutorialCustomerFlowBridge bridge)
    {
        if (owner == bridge) owner = null;
    }

    public void OnPointerDown(PointerEventData eventData) => owner?.GuardDisappearingServiceButtonPress();
}
