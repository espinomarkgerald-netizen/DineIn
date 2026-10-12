using UnityEngine;

// Fine Dining adds its booking and optional-service baseline to the shared session lifecycle.
public sealed class FineDiningMultiplayerSession : MultiplayerRestaurantBridge
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
    [SerializeField] private MultiplayerServiceStaffBridge serviceStaffBridge;
    [SerializeField] private MultiplayerFineDiningService fineDiningService;
    [SerializeField] private FineDiningReservations reservations;
    private bool configured;

    public new static bool IsActive => !MultiplayerProgressionContext.RestorationInProgress
        && MultiplayerSessionManager.Instance != null
        && MultiplayerSessionManager.Instance.IsMultiplayerSession
        && MultiplayerSessionManager.Instance.RestaurantType == "FineDining";
    public static bool IsAuthority => IsActive && MultiplayerSessionManager.Instance.IsAuthority;
    public override bool ReadyForInput => configured && MultiplayerProgressionContext.Ready
        && taskClaims.HasSnapshot && fineDiningService.HasRestoredState;
    public override bool RestorationComplete => ReadyForInput && HasState
        && dayBridge.HasRestoredState && restockBridge.HasRestoredState
        && serviceBridge.HasRestoredState && objectBridge.HasRestoredState && serviceStaffBridge.HasRestoredState;

    public override void InitializeSession()
    {
        session = GetComponent<MultiplayerSessionManager>();
        configured = session != null && session.RestaurantType == "FineDining"
            && gameObject.scene.name == FineDiningScene.Multiplayer
            && progression != null && hudBridge != null && dayBridge != null && restockBridge != null
            && serviceBridge != null && objectBridge != null && taskClaims != null
            && interactionBridge != null && staffRoster != null && serviceStaffBridge != null
            && fineDiningService != null && reservations != null;
        if (!configured) Debug.LogError("[Fine Dining Multiplayer] The scene's session bindings are incomplete.", this);
    }
    protected override void Update()
    {
        if (configured) base.Update();
    }
    public override void RequestRestoration()
    {
        if (!configured) return;
        base.RequestRestoration();
        taskClaims.RefreshAfterRejoin();
        restockBridge.RefreshAfterRejoin();
        serviceStaffBridge.ResetObservedState();
        fineDiningService.RequestRestoration();
    }
    public override void ParticipantReturned()
    {
        if (!configured) return;
        base.ParticipantReturned();
        restockBridge.PublishNow();
        fineDiningService.PublishNow();
    }
    public override void ParticipantUnavailable(int actor)
    {
        if (!configured || !session.IsAuthority) return;
        fineDiningService.ParticipantUnavailable(actor);
        base.ParticipantUnavailable(actor);
    }
    public override void ConnectionLost()
    {
        if (configured) fineDiningService.SuspendSession();
        base.ConnectionLost();
    }
    public override void SuspendSession(bool ended)
    {
        if (configured)
        {
            fineDiningService.SuspendSession();
            restockBridge.SuspendSession();
        }
        base.SuspendSession(ended);
    }
}
