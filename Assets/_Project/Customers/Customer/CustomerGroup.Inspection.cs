using UnityEngine;

public readonly struct CustomerInspectionSnapshot
{
    public readonly CustomerGroup Group;
    public readonly Booth Table;
    public readonly Sprite Portrait;
    public readonly string Type, Status, TableLabel, Reaction;
    public readonly int Members, ReactionMood;
    public readonly bool Visible, PatienceKnown, PatienceActive, FactsKnown, OrderTaken, FoodServed, BillDelivered;
    public readonly float Patience;
    public CustomerInspectionSnapshot(CustomerGroup group, string status, bool known, bool active, float patience)
    {
        Group=group;Table=group.assignedBooth != null && group.assignedBooth.CurrentGroup==group ? group.assignedBooth : null;
        Portrait=group.GetCustomerTypeImage();Type=group.GetCustomerTypeName();Members=group.Size;
        TableLabel=group.InspectionTableLabel;
        Status=status;PatienceKnown=known;PatienceActive=active;Patience=Mathf.Clamp01(patience);
        FactsKnown=!group.IsNetworkObserver || group.GetComponentInParent<MultiplayerCustomerSpawn>()?.HasRestoredServiceState==true;
        ReactionMood=FactsKnown && !string.IsNullOrWhiteSpace(group.OutcomeThought) ? group.OutcomeThoughtMood : -1;
        Reaction=!FactsKnown?"Reaction: syncing":ReactionMood<0?"No reaction yet":"Last reaction: "+(ReactionMood==0?"Happy":ReactionMood==1?"Neutral":"Angry");
        OrderTaken=group.HasConfirmedOrder;FoodServed=group.InspectionFoodServed;BillDelivered=group.HasReceivedBill;
        Visible=group.isActiveAndEnabled && group.state!=CustomerGroup.GroupState.Spawning &&
            group.state!=CustomerGroup.GroupState.Leaving && group.state!=CustomerGroup.GroupState.AngryLeft && group.state!=CustomerGroup.GroupState.UnhappyLeft;
    }
}

public partial class CustomerGroup
{
    public string InspectionTableLabel => IsTakeout ? "Takeout" : assignedBooth == null || assignedBooth.CurrentGroup != this
        ? "Not seated" : currentOrderNumber > 0 ? "Table " + currentOrderNumber : "Awaiting number";

    public bool TryGetInspectionPosition(out Vector3 position, out Transform highlightTarget)
    {
        position = Vector3.zero; highlightTarget = null;
        if (assignedBooth != null && assignedBooth.CurrentGroup == this &&
            state != GroupState.WalkingToBooth && state != GroupState.WalkingToLobby &&
            state != GroupState.Waiting && state != GroupState.Spawning)
        {
            position = assignedBooth.transform.position; highlightTarget = assignedBooth.transform; return true;
        }
        int count = 0;
        foreach (var member in members)
        {
            if (member == null || !member.gameObject.activeInHierarchy) continue;
            position += member.transform.position; count++;
            if (highlightTarget == null) highlightTarget = member.transform;
        }
        if (count == 0) return false;
        position /= count; return true;
    }
    public double InspectionArrivalTime { get; private set; }
    private bool inspectionPatienceKnown, inspectionPatienceActive;
    private float inspectionPatience;
    internal bool InspectionFoodServed => firstDeliveryCompleted;
    // Presentation-only projection. Never starts, resets or advances a customer timer.
    public CustomerInspectionSnapshot CaptureInspection()
    {
        string status=state switch {
            GroupState.WalkingToLobby=>"Arriving", GroupState.Waiting=>"Waiting to seat",
            GroupState.WalkingToBooth=>"Walking to table", GroupState.WaitingToOrder=>"Browsing menu", GroupState.Seated=>"Seated",
            GroupState.ReadyToOrder=>"Ready to order", GroupState.OrderTaken=>"Waiting for food",
            GroupState.Eating=>"Eating", GroupState.NeedsBill=>hasReceivedBill?"Waiting for payment":"Waiting for bill",
            _=>"Unavailable" };
        if(IsTakeout) status=hasConfirmedOrder?"Waiting for takeaway":"Waiting to order";
        if(FastFood!=null) {
            if(!FastFoodPaid)status=HasConfirmedOrder?"Paying at counter":"Waiting to order";
            else if(FastFoodAwaitingSeat)status="Finding a seat";
            else if(!firstDeliveryCompleted)status=IsCollectingFastFoodTray?"Collecting food":FastFood.IsBagReady(this)?"Takeaway ready":FastFoodSelfPickup?"Waiting for pickup":"Waiting for food";
        }
        if(WaitingForStock)status="Waiting for stock";
        if(HasFineDiningDrinkEvent)status=HasWineRefillRequest?(IsWaterPourRequest?"Waiting for water":"Waiting for wine"):
            HasConfirmedOrder?"Waiting for drink":"Ready for drink order";
        if(IsNetworkObserver && !inspectionPatienceKnown)return new CustomerInspectionSnapshot(this,status,false,false,0);
        if(IsNetworkObserver)return new CustomerInspectionSnapshot(this,status,true,inspectionPatienceActive,inspectionPatience);
        bool active=false;float value=0;
        if(!TrainingGroupProtected) {
            if(WaitingForStock){active=true;value=StockWaitRemaining/Mathf.Max(1f,RestaurantStockout.WaitSeconds);}
            else if(QueuePatienceVisible){active=true;value=QueuePatience01;}
            else if(state==GroupState.ReadyToOrder && !HasFineDiningDrinkEvent && ordinaryOrderPatience>0 && !isOrderPaused && !ReceivingAssistedService){active=true;value=ordinaryOrderRemaining/ordinaryOrderPatience;}
            else if(FastFood!=null && FastFood.TryGetInspectionPatience(this,out float fastFoodPatience)){active=true;value=fastFoodPatience;}
            else if(HasFineDiningDrinkEvent && activeWineUI==null && !fineDiningMultiplayerPourActive && fineDiningServiceRemaining>0) {
                var settings=FineDiningReservations.For(this);
                if(settings!=null){
                    active=true;value=fineDiningServiceRemaining/Mathf.Max(15f,Mathf.Max(settings.additionalDrinkServiceSeconds,settings.additionalDrinkMaximumServiceSeconds));
                    if((!hasConfirmedOrder || HasWineRefillRequest) && !RestaurantTaskClaim.IsClaimedByPlayer(this) && !RestaurantTaskClaim.IsClaimedByBot(this) && !HasMultiplayerFineDiningClaim())
                        value=Mathf.Min(value,fineDiningRequestRemaining/Mathf.Max(15f,settings.additionalDrinkServiceSeconds));
                }
            }
        }
        return new CustomerInspectionSnapshot(this,status,true,active,value);
    }
    public void PresentInspectionPatience(bool known,bool active,float value)
    {
        if(!IsNetworkObserver)return;
        inspectionPatienceKnown=known;inspectionPatienceActive=active;inspectionPatience=Mathf.Clamp01(value);
    }
}
