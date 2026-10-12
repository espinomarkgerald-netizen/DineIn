using System;
using UnityEngine;

public partial class CustomerGroup
{
    // Fine Dining augments the real customer lifecycle. Observers only project this state.
    [Serializable] public sealed class FineDiningNetworkState
    {
        public string reservationId, guestName, productId, servedDrinkProductId, primaryOrderJson, primarySubmittedJson;
        public int arrivalMinute, generation, kind, primaryOrderNumber, primaryTrayOrder, tip;
        public bool receptionSettled, seatingAvailable, eventUsed, tipAwarded, pourActive;
        public float requestRemaining, serviceRemaining;
    }

    private int fineDiningServiceGeneration, fineDiningLastPourTip;
    private bool fineDiningMultiplayerPourActive, observedFineReceptionSettled, observedFineSeatingAvailable;
    private bool fineDiningNetworkCommitInProgress;
    private float fineDiningRequestRemaining, fineDiningServiceRemaining;
    private Recipe fineDiningServedDrink;
    private FoodTray fineDiningProjectedDrinkTray;
    private Recipe fineDiningProjectedDrink;
    public int FineDiningServiceGeneration => fineDiningServiceGeneration;
    private bool IsFineDiningMultiplayer => MultiplayerSessionManager.Instance != null &&
        MultiplayerSessionManager.Instance.IsMultiplayerSession &&
        MultiplayerSessionManager.Instance.RestaurantType == "FineDining";
    public bool FineDiningReceptionSettled => IsNetworkObserver ? observedFineReceptionSettled :
        FineDiningReservations.For(this)?.waitingArea?.IsSettled(this) == true;
    public bool FineDiningSeatingAvailable => IsNetworkObserver ? observedFineSeatingAvailable :
        FineDiningReservations.For(this)?.FindTable(this) != null;

    private bool HasMultiplayerFineDiningClaim()
    {
        if (!IsFineDiningMultiplayer) return false;
        var customer = GetComponentInParent<MultiplayerCustomerSpawn>();
        var claims = MultiplayerSessionManager.Instance.GetComponent<MultiplayerTaskClaims>();
        if (customer == null || claims == null) return false;
        return claims.IsClaimed($"Customer:{customer.photonView.ViewID}:{(HasWineRefillRequest ? "Pour" : "Order")}") ||
            IsFineDiningAdditionalDrinkOrder && claims.IsClaimed($"Order:{currentOrderNumber}:Pickup");
    }

    public FineDiningNetworkState CaptureFineDiningState()
    {
        if (FineDiningReservations.For(this) == null) return null;
        var booking = FineDiningReservations.For(this).Booking(FineDiningReservationId);
        var primary = IsFineDiningAdditionalDrinkOrder ? fineDiningMeal : currentOrder;
        var submitted = IsFineDiningAdditionalDrinkOrder ? fineDiningSubmittedMeal : submittedOrder;
        var tray = IsFineDiningAdditionalDrinkOrder ? fineDiningMealTray : activeFoodTray;
        return new FineDiningNetworkState
        {
            reservationId = FineDiningReservationId, guestName = booking != null ? booking.guestName : reservationGuestName,
            arrivalMinute = booking != null ? booking.arrivalMinute : reservationArrivalMinute,
            receptionSettled = FineDiningReceptionSettled, seatingAvailable = FineDiningSeatingAvailable,
            generation = fineDiningServiceGeneration, kind = IsFineDiningAdditionalDrinkOrder ? 1 : HasWineRefillRequest ? IsWaterPourRequest ? 3 : 2 : 0,
            productId = fineDiningDrink != null ? fineDiningDrink.ProductId : string.Empty,
            servedDrinkProductId = fineDiningServedDrink != null ? fineDiningServedDrink.ProductId : string.Empty,
            primaryOrderNumber = IsFineDiningAdditionalDrinkOrder ? fineDiningMealNumber : currentOrderNumber,
            primaryOrderJson = primary != null ? JsonUtility.ToJson(primary) : string.Empty,
            primarySubmittedJson = submitted != null ? JsonUtility.ToJson(submitted) : string.Empty,
            primaryTrayOrder = tray != null ? tray.orderNumber : -1,
            eventUsed = fineDiningDrinkEventUsed, tipAwarded = fineDiningPourTipAwarded, tip = fineDiningLastPourTip,
            pourActive = fineDiningMultiplayerPourActive,
            requestRemaining = Mathf.Ceil(fineDiningRequestRemaining), serviceRemaining = Mathf.Ceil(fineDiningServiceRemaining)
        };
    }

    public void PresentFineDiningState(FineDiningNetworkState value)
    {
        if (!IsNetworkObserver || value == null) return;
        FineDiningReservationId = value.reservationId;
        reservationGuestName = value.guestName;
        reservationArrivalMinute = value.arrivalMinute;
        observedFineReceptionSettled = value.receptionSettled;
        observedFineSeatingAvailable = value.seatingAvailable;
        fineDiningServiceGeneration = value.generation;
        fineDiningDrinkEventUsed = value.eventUsed;
        fineDiningPourTipAwarded = value.tipAwarded;
        fineDiningLastPourTip = value.tip;
        fineDiningMultiplayerPourActive = value.pourActive;
        IsFineDiningAdditionalDrinkOrder = value.kind == 1;
        HasWineRefillRequest = value.kind == 2 || value.kind == 3;
        fineDiningDrink = MenuCatalog.Default?.FindProduct(value.productId);
        fineDiningServedDrink = MenuCatalog.Default?.FindProduct(value.servedDrinkProductId);
        fineDiningMealNumber = value.primaryOrderNumber;
        fineDiningMeal = IsFineDiningAdditionalDrinkOrder && !string.IsNullOrEmpty(value.primaryOrderJson)
            ? JsonUtility.FromJson<SimpleOrder>(value.primaryOrderJson) : null;
        fineDiningSubmittedMeal = IsFineDiningAdditionalDrinkOrder && !string.IsNullOrEmpty(value.primarySubmittedJson)
            ? JsonUtility.FromJson<SimpleOrder>(value.primarySubmittedJson) : null;
        fineDiningMealTray = value.primaryTrayOrder >= 0 ? MultiplayerWorldRegistry.Kitchen?.GetPreparedResult(value.primaryTrayOrder) : null;
        fineDiningRequestRemaining = value.requestRemaining;
        fineDiningServiceRemaining = value.serviceRemaining;
        if (fineDiningMealTray != null) PresentFineDiningServedDrink(fineDiningMealTray);
        RefreshFineDiningReceptionIndicator();
    }

    private void PresentFineDiningServedDrink(FoodTray tray)
    {
        if (!IsNetworkObserver || fineDiningServedDrink == null || tray == null ||
            tray.orderNumber != fineDiningMealNumber ||
            fineDiningProjectedDrinkTray == tray && fineDiningProjectedDrink == fineDiningServedDrink) return;
        tray.ReplaceDrinkVisual(fineDiningServedDrink);
        fineDiningProjectedDrinkTray = tray;
        fineDiningProjectedDrink = fineDiningServedDrink;
    }

    public void SetMultiplayerPourActive(bool active)
    {
        if (IsFineDiningMultiplayer && CanDecideCustomerOutcome) fineDiningMultiplayerPourActive = active && HasWineRefillRequest;
    }

    public bool TryCompleteFineDiningPour(int expectedGeneration, out int tip)
    {
        tip = 0;
        if (!IsFineDiningMultiplayer || !CanDecideCustomerOutcome || IsNetworkObserver ||
            expectedGeneration != fineDiningServiceGeneration || !HasWineRefillRequest ||
            fineDiningDrink == null || state != GroupState.Eating || leavingRoutineStarted) return false;
        // The transport stores its terminal receipt before releasing the network lease.
        fineDiningNetworkCommitInProgress = true;
        try { return CompleteFineDiningPour(out tip); }
        finally { fineDiningNetworkCommitInProgress = false; }
    }
}
