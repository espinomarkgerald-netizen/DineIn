using System;
using UnityEngine;

public partial class CustomerGroup
{
    private bool fineDiningTrainingProtected;
    public bool TrainingGroupProtected => fineDiningTrainingProtected && gameObject.scene.name == "Lobby3Tutorial";
    public bool TutorialSeated
    {
        get
        {
            if (!TrainingGroupProtected || assignedBooth == null || !hasBeenAssigned || members.Count == 0) return false;
            foreach (var member in members)
                if (member == null || !member.IsSeated || !seatedMembers.Contains(member)) return false;
            return true;
        }
    }

    // Success is reported by the normal delivery/pour completion path, never by closing its UI.
    public event Action<CustomerGroup, bool, bool> FineDiningDrinkServiceFinished;

    public void SetFineDiningTutorialProtection(bool protectedByLesson)
    {
        if (gameObject.scene.name != "Lobby3Tutorial") return;
        fineDiningTrainingProtected = protectedByLesson;
        tutorialDisableAutoOrderFlow = protectedByLesson;
        if (!protectedByLesson) return;
        StopReadyToOrderFlow();
        StopEatingRoutineForServiceFailure();
        ClearLinePatienceUI();
    }

    public void DisposeFineDiningTutorialGroup()
    {
        if (!TrainingGroupProtected) return;
        StopAllCoroutines();
        if (IsFineDiningAdditionalDrinkOrder) fineDiningDrinkKitchen?.CancelOptionalDrink(this, currentOrderNumber);
        CleanupFineDiningDrinks();
        RestaurantTaskClaim.Complete(this);
        NotifyLeftLineIfNeeded();
        ClearFineDiningReceptionIndicator();
        CleanupOnLeave();
        if (assignedBooth != null && assignedBooth.CurrentGroup == this) assignedBooth.ClearCurrentGroup();
        // OnDestroy runs later. It must not clear props from a new diner using this table in the same frame.
        assignedBooth = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    /// <summary>Stages a later lesson after seating; the familiar food lesson is intentionally omitted.</summary>
    public bool PrepareTutorialFineDiningMeal(Recipe meal, FoodTray tableTray = null)
    {
        if (!TutorialSeated || HasFineDiningDrinkEvent || leavingRoutineStarted || meal == null) return false;
        var kitchen = FindFirstObjectByType<KitchenManager>();
        var drop = assignedBooth.NetworkTrayPoint;
        if (tableTray == null && (kitchen == null || kitchen.foodTrayPrefab == null || drop == null)) return false;
        TutorialClearServiceUI();
        StopEatingRoutineForServiceFailure();
        var line = new OrderLine();
        line.SetProduct(meal);
        currentOrder = new SimpleOrder();
        currentOrder.SetLines(new[] { line });
        submittedOrder = new SimpleOrder();
        submittedOrder.SetLines(new[] { line.Clone() });
        hasConfirmedOrder = firstDeliveryCompleted = true;
        if (tableTray == null)
        {
            tableTray = Instantiate(kitchen.foodTrayPrefab, drop.position, drop.rotation);
            WaiterHands.AttachKeepingWorldScale(tableTray.transform, drop, Vector3.zero, Quaternion.identity);
            tableTray.Init(this);
            tableTray.GetComponent<FoodTrayInteractable>()?.NotifyDeliveredToTable();
            foreach (var label in tableTray.GetComponentsInChildren<TableNumberUI>(true))
                if (label.transform != tableTray.transform) label.gameObject.SetActive(false);
        }
        activeFoodTray = tableTray;
        assignedBooth.ClearMenuBook();
        SetState(GroupState.Eating);
        return true;
    }

    public bool BeginTutorialFineDiningRequest(Recipe drink, bool pour)
    {
        var settings = FineDiningReservations.For(this);
        if (!TutorialSeated || state != GroupState.Eating || HasFineDiningDrinkEvent || leavingRoutineStarted ||
            drink == null || drink.category != MenuProductCategory.Drink || settings == null ||
            LobbyStockBridge.Instance == null || !LobbyStockBridge.Instance.HasProductStock(drink)) return false;
        bool water = settings.waterPourProduct != null && settings.waterPourProduct.ProductId == drink.ProductId;
        if (pour && (settings.PourUI == null || (water ? !settings.PourUI.SupportsWaterService : settings.wineBottlePrefab == null))) return false;
        fineDiningDrinkKitchen = FindFirstObjectByType<KitchenManager>();
        if (!pour && (fineDiningDrinkKitchen == null || !fineDiningDrinkKitchen.HasDrinkServiceOutput)) return false;
        fineDiningDrinkEventUsed = true;
        fineDiningDrink = drink;
        if (pour)
        {
            HasWineRefillRequest = true;
            ClearEatingBubble();
            ClearTableNumber();
            RefreshMemberProceduralState();
        }
        else BeginAdditionalDrinkOrder();
        SpawnOrderBubble();
        return true;
    }
}
