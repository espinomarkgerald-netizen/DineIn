using UnityEngine;

public partial class CustomerGroup
{
    public bool WaitingForStock { get; private set; }
    public float StockWaitRemaining { get; private set; } = RestaurantStockout.WaitSeconds;
    private bool stockPatienceChosen, stockPatient, resumeStockOrder;
    private float nextStockRetry, ordinaryOrderPatience = -1f, ordinaryOrderRemaining;

    // Called only for orders which have not consumed ingredients or entered the kitchen.
    public bool HandleGlobalStockout()
    {
        if (!CanDecideCustomerOutcome || leavingRoutineStarted || hasConfirmedOrder || RestaurantStockout.Shortage == 0)
            return false;
        if (WaitingForStock) return true;
        MultiplayerDayBridge.ReportMissingStock(this);
        if (!stockPatienceChosen)
        { stockPatienceChosen = true; stockPatient = Random.value < RestaurantStockout.PatientChance; }
        StopReadyToOrderFlow();
        isPlayerReviewingOrder = false;
        isOrderPaused = false;
        RestaurantTaskClaim.Complete(this);
        var customer = GetComponentInParent<MultiplayerCustomerSpawn>();
        var session = MultiplayerSessionManager.Instance;
        if (customer != null && session != null && session.IsAuthority)
        {
            customer.ReviewActor = 0;
            var claims = session.GetComponent<MultiplayerTaskClaims>();
            string task = $"Customer:{customer.photonView.ViewID}:Order";
            if (claims != null) claims.CompleteOnAuthority(task, claims.GetOwner(task));
        }
        ClearOrderBubble();
        ClearTableNumber();
        OrderChecklistUI.Instance?.DismissUnavailableOrder(this);
        if (!stockPatient || StockWaitRemaining <= 0f)
        { LeaveForStockout(false); return true; }
        WaitingForStock = true;
        SetState(GroupState.WaitingToOrder);
        nextStockRetry = Time.time + 0.5f;
        ShowCustomThought(members.Count > 1 ? "It's okay, we can wait." : "It's okay, I can wait.", happyFaceSprite);
        return true;
    }

    private void TickStockWait()
    {
        if (!CanDecideCustomerOutcome || !WaitingForStock) return;
        if (leavingRoutineStarted || hasConfirmedOrder)
        { WaitingForStock = false; return; }
        StockWaitRemaining = Mathf.Max(0f, StockWaitRemaining - Time.deltaTime);
        if (StockWaitRemaining <= 0f || GameDayManager.Instance != null &&
            (GameDayManager.Instance.ClosingOut || GameDayManager.Instance.HasDayResults))
        { LeaveForStockout(true); return; }
        if (Time.time < nextStockRetry) return;
        nextStockRetry = Time.time + 0.5f;
        if (RestaurantStockout.Shortage != 0) return;
        GenerateRandomOrder();
        if (currentOrder == null || currentOrder.contents == null || currentOrder.contents.Count == 0
            || currentOrder.name == "No Food Available") return;
        WaitingForStock = false;
        ClearStockWaitThought();
        if (linePatienceInstance != null) linePatienceInstance.SetActive(false);
        // A regenerated selection needs a new identity; stale reviews and replicas
        // must not reuse the previous order's cached payload.
        currentOrderNumber = -1;
        resumeStockOrder = true;
        StartReadyToOrderFlow();
    }

    private void ClearStockWaitThought()
    {
        if (thoughtRoutine != null) { StopCoroutine(thoughtRoutine); thoughtRoutine = null; }
        ClearThoughtBubble();
        OutcomeThought = null;
    }

    private void LeaveForStockout(bool waited)
    {
        WaitingForStock = false;
        if (linePatienceInstance != null) linePatienceInstance.SetActive(false);
        CasualDiningPolishManager.EnsureInstance().RegisterIncident(DailyIncidentType.StockoutRefusal);
        ReportFinalResult(FinalResult.Neutral, false);
        string subject = members.Count > 1 ? "We" : "I";
        ShowCustomThought(waited ? (members.Count > 1 ? "Sorry, we can't wait any longer." : "Sorry, I can't wait any longer.")
            : subject + " don't have time for this.", unhappyFaceSprite);
        SetState(GroupState.UnhappyLeft);
        ClearOrderBubble(); ClearBillBubble(); ClearTableNumber(); ClearMoneyBubble(); ClearEatingBubble();
        StartLeaving(false);
    }

    public void PresentStockWait(bool waiting, float remaining)
    {
        if (!IsNetworkObserver) return;
        bool wasWaiting = WaitingForStock;
        if (WaitingForStock && !waiting) ClearStockWaitThought();
        WaitingForStock = waiting;
        StockWaitRemaining = Mathf.Clamp(remaining, 0f, RestaurantStockout.WaitSeconds);
        if (waiting)
        { ClearOrderBubble(); ClearTableNumber(); OrderChecklistUI.Instance?.DismissUnavailableOrder(this); }
        if (waiting || wasWaiting) ShowStockWaitPatience();
    }

    private void ShowStockWaitPatience()
    {
        if (!WaitingForStock)
        { if (linePatienceInstance != null) linePatienceInstance.SetActive(false); return; }
        if (linePatiencePrefab == null || groupUiAnchor == null) return;
        EnsureLinePatienceUI();
        if (linePatienceInstance != null) linePatienceInstance.SetActive(true);
        if (linePatienceUI != null) linePatienceUI.SetProgress(StockWaitRemaining / RestaurantStockout.WaitSeconds);
    }
}
