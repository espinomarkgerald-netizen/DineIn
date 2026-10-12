using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class CustomerGroup
{
    public bool IsFineDiningAdditionalDrinkOrder { get; private set; }
    public bool HasWineRefillRequest { get; private set; }
    public bool HasFineDiningDrinkEvent => IsFineDiningAdditionalDrinkOrder || HasWineRefillRequest;
    public Recipe RequestedFineDiningDrink => fineDiningDrink;
    public bool IsWaterPourRequest => HasWineRefillRequest && fineDiningDrink != null &&
        FineDiningReservations.For(this)?.waterPourProduct?.ProductId == fineDiningDrink.ProductId;
    public Sprite FineDiningDrinkRequestIcon => IsWaterPourRequest && FineDiningReservations.For(this)?.waterPitcherIcon != null
        ? FineDiningReservations.For(this).waterPitcherIcon : fineDiningDrink != null ? fineDiningDrink.sprite : null;
    private bool fineDiningDrinkEventUsed;
    private bool fineDiningPourTipAwarded;
    private Recipe fineDiningDrink;
    private SimpleOrder fineDiningMeal, fineDiningSubmittedMeal;
    private int fineDiningMealNumber;
    private FoodTray fineDiningMealTray;
    private GameObject refillBottle;
    private WinePouringUI activeWineUI;
    private KitchenManager fineDiningDrinkKitchen;
    private readonly Dictionary<Renderer, bool> hiddenCarriedRenderers = new();

    private IEnumerator FineDiningMeal(float ordinarySeconds)
    {
        // Guided diners wait for the bridge to introduce their request. No random roll or timeout.
        while (TrainingGroupProtected && !leavingRoutineStarted) yield return null;
        var settings = FineDiningReservations.For(this);
        if (settings == null || MultiplayerRestaurantBridge.IsActive && !IsFineDiningMultiplayer)
        { yield return new WaitForSeconds(ordinarySeconds); yield break; }
        float duration = ordinarySeconds * Mathf.Max(1f, settings.eatingDurationMultiplier);
        float firstPart = duration * Mathf.Clamp01(settings.additionalDrinkTriggerFraction);
        yield return new WaitForSeconds(firstPart);
        float remainingMeal = Mathf.Max(0f, duration - firstPart);
        // A temporary service/complaint owns the table. Defer the one probability
        // roll until that work finishes instead of losing it at one instant.
        while (!fineDiningDrinkEventUsed && state == GroupState.Eating && !leavingRoutineStarted &&
            remainingMeal > 0f && (waitingForRemake || isPlayerReviewingOrder || managerComplaintPending ||
            RestaurantTaskClaim.IsClaimedByPlayer(this) || RestaurantTaskClaim.IsClaimedByBot(this)))
        {
            remainingMeal -= Time.deltaTime;
            yield return null;
        }
        if (!fineDiningDrinkEventUsed && remainingMeal > 0f && state == GroupState.Eating && !leavingRoutineStarted
            && !waitingForRemake && !isPlayerReviewingOrder && !managerComplaintPending)
        {
            fineDiningDrinkEventUsed = true;
            if (Random.value < settings.additionalDrinkChance)
            {
                var available = MenuCatalog.Default != null
                    ? MenuCatalog.Default.GetProducts(MenuProductCategory.Drink, true) : new List<Recipe>();
                available.RemoveAll(product => product == null || product.restaurantType != RestaurantType.FineDining
                    || LobbyStockBridge.Instance == null || !LobbyStockBridge.Instance.HasProductStock(product));
                if (available.Count > 0)
                {
                    var refills = available.FindAll(product => System.Array.Exists(settings.wineRefillProducts, wine => wine != null && wine.ProductId == product.ProductId)
                        && currentOrder != null && currentOrder.ResolveProducts().Exists(served => served != null && served.ProductId == product.ProductId));
                    Recipe pourProduct = refills.Count > 0 ? refills[Random.Range(0, refills.Count)]
                        : available.Find(product => settings.waterPourProduct != null && product.ProductId == settings.waterPourProduct.ProductId);
                    bool waterPour = pourProduct != null && settings.waterPourProduct != null
                        && pourProduct.ProductId == settings.waterPourProduct.ProductId;
                    bool refill = pourProduct != null && (waterPour || settings.wineBottlePrefab != null)
                        && settings.PourUI != null && (!waterPour || settings.PourUI.SupportsWaterService)
                        && Random.value < settings.wineRefillChance;
                    fineDiningDrinkKitchen = FindFirstObjectByType<KitchenManager>();
                    if (!refill && (fineDiningDrinkKitchen == null || !fineDiningDrinkKitchen.HasDrinkServiceOutput))
                    { yield return new WaitForSeconds(Mathf.Max(0f, remainingMeal)); yield break; }
                    fineDiningDrink = refill ? pourProduct : available[Random.Range(0, available.Count)];
                    fineDiningServiceGeneration++;
                    if (refill) { HasWineRefillRequest = true; ClearEatingBubble(); ClearTableNumber(); RefreshMemberProceduralState(); }
                    else BeginAdditionalDrinkOrder();
                    SpawnOrderBubble();
                    float remaining = Mathf.Max(15f, settings.additionalDrinkServiceSeconds);
                    float serviceRemaining = Mathf.Max(remaining, settings.additionalDrinkMaximumServiceSeconds);
                    while (HasFineDiningDrinkEvent && !leavingRoutineStarted)
                    {
                        // An ignored optional request ends peacefully; it never causes an unhappy departure.
                        // Give accepted service a generous bound so a lost ticket cannot block payment forever.
                        fineDiningRequestRemaining = remaining;
                        fineDiningServiceRemaining = serviceRemaining;
                        if (activeWineUI == null && !fineDiningMultiplayerPourActive)
                        {
                            serviceRemaining -= Time.deltaTime;
                            if (!hasConfirmedOrder || HasWineRefillRequest)
                            {
                                if (!RestaurantTaskClaim.IsClaimedByPlayer(this) && !RestaurantTaskClaim.IsClaimedByBot(this) &&
                                    !HasMultiplayerFineDiningClaim())
                                    remaining -= Time.deltaTime;
                            }
                            if (remaining <= 0f || serviceRemaining <= 0f)
                            { FinishFineDiningDrinks(false); break; }
                        }
                        yield return null;
                    }
                }
            }
        }
        if (leavingRoutineStarted || state != GroupState.Eating) yield break;
        yield return new WaitForSeconds(Mathf.Max(0f, remainingMeal));
    }

    private void BeginAdditionalDrinkOrder()
    {
        StopReadyToOrderFlow();
        ClearEatingBubble();
        ClearTableNumber();
        fineDiningMeal = currentOrder;
        fineDiningSubmittedMeal = submittedOrder;
        fineDiningMealNumber = currentOrderNumber;
        fineDiningMealTray = activeFoodTray;
        var line = new OrderLine(); line.SetProduct(fineDiningDrink);
        currentOrder = new SimpleOrder(); currentOrder.SetLines(new[] { line });
        submittedOrder = new SimpleOrder();
        AssignFreshOrderNumberForRemake();
        hasConfirmedOrder = false;
        isPlayerReviewingOrder = isOrderPaused = false;
        IsFineDiningAdditionalDrinkOrder = true;
        if (fineDiningDrinkKitchen == null) fineDiningDrinkKitchen = FindFirstObjectByType<KitchenManager>();
        if (fineDiningDrinkKitchen != null) fineDiningDrinkKitchen.OrderFinished += OnFineDiningDrinkPrepared;
        SetState(GroupState.ReadyToOrder);
    }

    private void OnFineDiningDrinkPrepared(CustomerGroup group, int order, bool succeeded)
    {
        if (group == this && IsFineDiningAdditionalDrinkOrder && currentOrderNumber == order && !succeeded)
            FinishFineDiningDrinks(false);
    }

    private bool ReceiveFineDiningDrinks(List<string> contents, FoodTray tray)
    {
        if (!IsFineDiningAdditionalDrinkOrder) return false;
        if (!IsCorrectDeliveredOrder(contents))
        {
            // A mistaken optional drink must not discard the paid meal or start a food remake.
            WarningSlideUI.Instance?.Show("That is not the requested drink.");
            if (tray != null) Destroy(tray.gameObject);
            FinishFineDiningDrinks(false);
            return true;
        }
        if (fineDiningMealTray != null)
        {
            fineDiningMealTray.ReplaceDrinkVisual(fineDiningDrink);
            if (tray != null && tray != fineDiningMealTray) Destroy(tray.gameObject);
        }
        else fineDiningMealTray = tray;
        FinishFineDiningDrinks(true);
        return true;
    }

    private void FinishFineDiningDrinks(bool served)
    {
        bool order = IsFineDiningAdditionalDrinkOrder;
        if (fineDiningDrinkKitchen != null) fineDiningDrinkKitchen.OrderFinished -= OnFineDiningDrinkPrepared;
        if (!order && !HasWineRefillRequest) return;
        if (order && !served) fineDiningDrinkKitchen?.CancelOptionalDrink(this, currentOrderNumber);
        StopReadyToOrderFlow();
        ClearOrderBubble(); ClearTableNumber();
        OrderChecklistUI.Instance?.DismissUnavailableOrder(this);
        if (!served) CancelOutstandingGroupTask();
        if (!fineDiningNetworkCommitInProgress) RestaurantTaskClaim.Complete(this);
        RestoreRefillProp();
        if (activeWineUI != null) activeWineUI.CloseWithoutResult();
        activeWineUI = null;
        if (order)
        {
            currentOrder = fineDiningMeal;
            submittedOrder = fineDiningSubmittedMeal;
            currentOrderNumber = fineDiningMealNumber;
            activeFoodTray = fineDiningMealTray;
        }
        if (served && currentOrder != null && fineDiningDrink != null)
        {
            fineDiningServedDrink = fineDiningDrink;
            currentOrder.ResolveProducts();
            var lines = new List<OrderLine>();
            if (currentOrder.lines != null) foreach (var line in currentOrder.lines) if (line != null) lines.Add(line.Clone());
            var drinkLine = new OrderLine(); drinkLine.SetProduct(fineDiningDrink); lines.Add(drinkLine);
            currentOrder.SetLines(lines);
            if (!order && activeFoodTray != null) activeFoodTray.ReplaceDrinkVisual(fineDiningDrink);
        }
        IsFineDiningAdditionalDrinkOrder = HasWineRefillRequest = false;
        fineDiningMultiplayerPourActive = false;
        fineDiningRequestRemaining = fineDiningServiceRemaining = 0f;
        hasConfirmedOrder = true;
        isPlayerReviewingOrder = isOrderPaused = false;
        if (!leavingRoutineStarted && state != GroupState.Leaving && state != GroupState.AngryLeft && state != GroupState.UnhappyLeft)
        {
            SetState(GroupState.Eating);
            SetMembersEating(true);
            RefreshMemberProceduralState();
        }
        fineDiningMeal = fineDiningSubmittedMeal = null;
        fineDiningMealTray = null;
        fineDiningDrink = null;
        FineDiningDrinkServiceFinished?.Invoke(this, served, !order);
    }

    private bool HandleFineDiningDrinkStockout()
    {
        if (!IsFineDiningAdditionalDrinkOrder || hasConfirmedOrder) return false;
        if (fineDiningDrink != null && LobbyStockBridge.Instance != null
            && LobbyStockBridge.Instance.HasProductStock(fineDiningDrink)) return false;
        FinishFineDiningDrinks(false);
        return true;
    }

    public void BeginWineRefill()
    {
        if (IsFineDiningMultiplayer) { MultiplayerFineDiningService.RequestPour(this); return; }
        if (!HasWineRefillRequest || RoleManager.Instance == null
            || !RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)
            || GameplayUIBlocker.IsBlockedExcept(orderBubbleInstance)) return;
        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null || assignedBooth == null || assignedBooth.approachPoint == null)
        { WarningSlideUI.Instance?.Show("This table has no reachable drink service point."); return; }
        if (!RestaurantTaskClaim.TryClaimPlayer(this))
        { WarningSlideUI.Instance?.Show("Finish your current task before pouring a drink."); return; }
        SetOrderTaskClaimedByStaff(true);
        mover.UI_MoveToAction(assignedBooth.approachPoint, 2.75f,
            () => OpenWineRefill(mover), ReleaseWineRefill);
    }

    private void OpenWineRefill(PlayerMovement mover)
    {
        if (!HasWineRefillRequest || state != GroupState.Eating || leavingRoutineStarted)
        { ReleaseWineRefill(); return; }
        var settings = FineDiningReservations.For(this);
        var hands = WaiterHands.For(mover);
        var ui = settings != null ? settings.PourUI : null;
        if (settings == null || hands == null || ui == null || (!IsWaterPourRequest && settings.wineBottlePrefab == null) || ui.IsOpen)
        {
            WarningSlideUI.Instance?.Show(ui != null && ui.IsOpen
                ? "Finish the current pour first." : "Drink pouring is unavailable right now.");
            ReleaseWineRefill(); return;
        }
        if (fineDiningDrink == null || LobbyStockBridge.Instance == null || !LobbyStockBridge.Instance.HasProductStock(fineDiningDrink))
        { FinishFineDiningDrinks(false); return; }
        // Preserve each carried renderer exactly; inventory ownership/parents never change.
        HideCarriedRenderers(hands.TrayHoldPoint);
        HideCarriedRenderers(hands.BillHoldPoint);
        if (!IsWaterPourRequest)
        {
            refillBottle = Instantiate(settings.wineBottlePrefab);
            WaiterHands.SetAllColliders(refillBottle, false);
            foreach (var body in refillBottle.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
            WaiterHands.AttachKeepingWorldScale(refillBottle.transform, hands.BillHoldPoint,
                settings.wineBottleHandOffset, Quaternion.Euler(settings.wineBottleHandRotation));
            refillBottle.transform.localScale = Vector3.Scale(refillBottle.transform.localScale, settings.wineBottleHandScale);
            var pose = refillBottle.GetComponent<WineBottleCarryPose>();
            if (pose == null) pose = refillBottle.AddComponent<WineBottleCarryPose>();
            pose.Bind(mover.transform, hands.BillHoldPoint, settings.wineBottleHandOffset, settings.wineBottleHandRotation);
        }
        activeWineUI = ui;
        if (!ui.Open(IsWaterPourRequest, CompleteWineRefill, ReleaseWineRefill))
        {
            WarningSlideUI.Instance?.Show("The pouring panel could not open.");
            ReleaseWineRefill();
        }
    }

    private void CompleteWineRefill()
    {
        activeWineUI = null;
        if (!HasWineRefillRequest || fineDiningDrink == null || leavingRoutineStarted)
        { ReleaseWineRefill(); return; }
        CompleteFineDiningPour(out _);
    }

    private bool CompleteFineDiningPour(out int awardedTip)
    {
        awardedTip = 0;
        bool served = LobbyStockBridge.Instance != null && LobbyStockBridge.Instance.TryUseOrderStock(
            new[] { fineDiningDrink }, () => HasWineRefillRequest && state == GroupState.Eating && !leavingRoutineStarted);
        if (!served) WarningSlideUI.Instance?.Show("That drink is no longer in stock. No charge was added.");
        if (served && !fineDiningPourTipAwarded && GameDayManager.Instance != null && GameDayManager.Instance.ServiceActive)
        {
            var settings = FineDiningReservations.For(this);
            Vector2Int range = settings != null ? settings.pourServiceTipRange : Vector2Int.zero;
            int minimum = Mathf.Clamp(range.x, 0, int.MaxValue - 1);
            int maximum = Mathf.Clamp(range.y, minimum, int.MaxValue - 1);
            int tip = Random.Range(minimum, maximum + 1);
            fineDiningPourTipAwarded = true;
            fineDiningLastPourTip = awardedTip = tip;
            if (tip > 0)
            {
                GameDayManager.Instance.RegisterTip(tip);
                SpawnTipPopup(tip);
            }
            ShowCustomThought("Thank you for the lovely service!", happyFaceSprite);
        }
        FinishFineDiningDrinks(served);
        return served;
    }

    private void ReleaseWineRefill()
    {
        RestoreRefillProp();
        activeWineUI = null;
        RestaurantTaskClaim.ReleasePlayer(this);
        if (HasWineRefillRequest && !leavingRoutineStarted) SetOrderTaskClaimedByStaff(false);
    }

    private void HideCarriedRenderers(Transform point)
    {
        if (point == null) return;
        // Only attached items, never the character's own renderer.
        foreach (Transform child in point)
            foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                if (!hiddenCarriedRenderers.ContainsKey(renderer))
                { hiddenCarriedRenderers.Add(renderer, renderer.enabled); renderer.enabled = false; }
    }

    private void RestoreRefillProp()
    {
        if (refillBottle != null) Destroy(refillBottle);
        refillBottle = null;
        foreach (var entry in hiddenCarriedRenderers) if (entry.Key != null) entry.Key.enabled = entry.Value;
        hiddenCarriedRenderers.Clear();
    }

    private void CleanupFineDiningDrinks()
    {
        if (fineDiningDrinkKitchen != null) fineDiningDrinkKitchen.OrderFinished -= OnFineDiningDrinkPrepared;
        if (IsFineDiningAdditionalDrinkOrder && fineDiningMeal != null)
        {
            currentOrder = fineDiningMeal; submittedOrder = fineDiningSubmittedMeal;
            currentOrderNumber = fineDiningMealNumber; hasConfirmedOrder = true;
            fineDiningMeal = fineDiningSubmittedMeal = null;
        }
        if (activeWineUI != null) activeWineUI.CloseWithoutResult();
        activeWineUI = null;
        RestoreRefillProp();
        if (HasFineDiningDrinkEvent) RestaurantTaskClaim.Complete(this);
        IsFineDiningAdditionalDrinkOrder = HasWineRefillRequest = false;
        fineDiningMultiplayerPourActive = false;
    }
}

