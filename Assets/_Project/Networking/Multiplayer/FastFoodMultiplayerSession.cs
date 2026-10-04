using Photon.Realtime;
using UnityEngine;

// Fast Food's lifecycle; inherits only the existing shared management request/snapshot transport.
// The scene contains this binding instead of a Casual Dining MultiplayerRestaurantBridge component.
public sealed class FastFoodMultiplayerSession : MultiplayerRestaurantBridge
{
    [Header("Authored shared session components")]
    [SerializeField] private MultiplayerProgressionContext progression;
    [SerializeField] private MultiplayerHUDBridge hudBridge;
    [SerializeField] private MultiplayerDayBridge dayBridge;
    [SerializeField] private MultiplayerRestockBridge restockBridge;
    [SerializeField] private MultiplayerServiceActions serviceBridge;
    [SerializeField] private MultiplayerObjectBridge objectBridge;
    [SerializeField] private MultiplayerTaskClaims taskClaims;
    [SerializeField] private MultiplayerCustomerInteractionBridge interactionBridge;
    [SerializeField] private MultiplayerStaffRosterController staffRoster;
    [SerializeField] private MultiplayerFastFoodCooking cookingBridge;
    [SerializeField] private MultiplayerServiceStaffBridge serviceStaffBridge;
    [SerializeField] private FastFoodRestaurant restaurant;
    private bool configured;

    public new static bool IsActive => !MultiplayerProgressionContext.RestorationInProgress
        && MultiplayerSessionManager.Instance != null
        && MultiplayerSessionManager.Instance.IsMultiplayerSession
        && MultiplayerSessionManager.Instance.RestaurantType == "FastFood";
    public static bool IsAuthority => IsActive && MultiplayerSessionManager.Instance.IsAuthority;
    public override bool ReadyForInput => configured && MultiplayerProgressionContext.Ready
        && taskClaims.HasSnapshot && cookingBridge.HasRestoredState;
    public override bool RestorationComplete => ReadyForInput && HasState
        && dayBridge.HasRestoredState && restockBridge.HasRestoredState
        && serviceBridge.HasRestoredState && objectBridge.HasRestoredState && serviceStaffBridge.HasRestoredState
        && HygieneManager.Instance != null && HygieneManager.Instance.HasRestoredMultiplayerState;

    public override void InitializeSession()
    {
        session = GetComponent<MultiplayerSessionManager>();
        configured = session != null && session.RestaurantType == "FastFood"
            && gameObject.scene.name == FastFoodScene.Multiplayer
            && progression != null && hudBridge != null && dayBridge != null && restockBridge != null
            && serviceBridge != null && objectBridge != null && taskClaims != null
            && interactionBridge != null && staffRoster != null && cookingBridge != null && serviceStaffBridge != null && restaurant != null;
        if (!configured) Debug.LogError("[FastFood Multiplayer] The scene's session bindings are incomplete.", this);
    }
    protected override void Update()
    {
        if (!configured) return;
        base.Update();
    }
    public override void RequestRestoration()
    {
        if (!configured) return;
        base.RequestRestoration();
        taskClaims.RefreshAfterRejoin();
        restockBridge.RefreshAfterRejoin();
        serviceStaffBridge.ResetObservedState();
        cookingBridge.RequestRestoration();
        HygieneManager.Instance?.RefreshMultiplayerState();
    }
    public override void ParticipantReturned()
    {
        if (!configured) return;
        base.ParticipantReturned();
        restockBridge.PublishNow();
        cookingBridge.PublishNow();
        HygieneManager.Instance?.RefreshMultiplayerState();
    }
    public void ParticipantUnavailable(int actor)
    {
        if (!configured || !session.IsAuthority) return;
        taskClaims.ReleaseActorOnAuthority(actor);
        restockBridge.ParticipantUnavailable(actor);
        cookingBridge.ParticipantUnavailable(actor);
    }
    public override void OnPlayerLeftRoom(Player otherPlayer) => ParticipantUnavailable(otherPlayer.ActorNumber);
    public override void ConnectionLost()
    {
        if (!configured) return;
        cookingBridge.SuspendSession();
        base.ConnectionLost();
    }
    public override void SuspendSession(bool ended)
    {
        if (configured)
        {
            cookingBridge.SuspendSession();
            restockBridge.SuspendSession();
            if (ended)
            {
                restaurant.StopAdmissions();
                restaurant.StopAllCoroutines();
            }
        }
        base.SuspendSession(ended);
    }
}
