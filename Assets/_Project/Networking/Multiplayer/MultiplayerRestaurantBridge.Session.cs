using UnityEngine;

// The Casual Dining binding delegates to its existing systems. Future restaurant scenes
// provide this same interface, without copying chat, moderation or reconnect transport.
public partial class MultiplayerRestaurantBridge : IMultiplayerSessionGameplay
{
    public virtual bool ReadyForInput => MultiplayerProgressionContext.Ready && GetComponent<MultiplayerTaskClaims>()?.HasSnapshot == true;
    public virtual bool RestorationComplete => ReadyForInput && HasState
        && GetComponent<MultiplayerDayBridge>()?.HasRestoredState == true
        && GetComponent<MultiplayerServiceActions>()?.HasRestoredState == true
        && GetComponent<MultiplayerObjectBridge>()?.HasRestoredState == true;
    public virtual void InitializeSession()
    {
        session = GetComponent<MultiplayerSessionManager>();
        if (GetComponent<MultiplayerProgressionContext>() == null) gameObject.AddComponent<MultiplayerProgressionContext>();
        if (GetComponent<MultiplayerHUDBridge>() == null) gameObject.AddComponent<MultiplayerHUDBridge>();
        if (GetComponent<MultiplayerDayBridge>() == null) gameObject.AddComponent<MultiplayerDayBridge>();
        if (GetComponent<MultiplayerRestockBridge>() == null) gameObject.AddComponent<MultiplayerRestockBridge>();
        if (GetComponent<MultiplayerServiceActions>() == null) gameObject.AddComponent<MultiplayerServiceActions>();
        if (GetComponent<MultiplayerObjectBridge>() == null) gameObject.AddComponent<MultiplayerObjectBridge>();
        if (Debug.isDebugBuild && GetComponent<MultiplayerDiagnostics>() == null) gameObject.AddComponent<MultiplayerDiagnostics>();
    }
    public virtual void RequestRestoration()
    {
        GetComponent<MultiplayerServiceActions>()?.ResetDay();
        GetComponent<MultiplayerObjectBridge>()?.ResetObservedObjects();
        RequestSnapshot();
        GetComponent<MultiplayerDayBridge>()?.RefreshAfterRejoin();
    }
    public virtual void ParticipantReturned()
    {
        Publish(true);
        GetComponent<MultiplayerDayBridge>()?.PublishNow();
    }
    public virtual void ConnectionLost() => GameDayManager.Instance?.PrepareNextMultiplayerDay();
    public virtual void SuspendSession(bool ended)
    {
        if (GameDayManager.Instance != null) GameDayManager.Instance.ObserveDayOnly = true;
        FindFirstObjectByType<ManagementComputerController>()?.CloseComputer();
        CashierRegisterUI.Instance?.Hide();
        CardPaymentUI.Instance?.CloseNetworkPayment();
        ManagerComplaintSystem.Instance?.SuspendNetworkPresentation();
        if (!ended) return;
        foreach (var group in FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None)) group.StopAllCoroutines();
        foreach (var bot in FindObjectsByType<AutonomousStaffBot>(FindObjectsSortMode.None)) bot.enabled = false;
        foreach (var kitchen in FindObjectsByType<KitchenManager>(FindObjectsSortMode.None)) kitchen.StopAllCoroutines();
        foreach (var worker in FindObjectsByType<KitchenWorkerBot>(FindObjectsSortMode.None)) worker.StopAllCoroutines();
        foreach (var agent in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))
            if (agent.gameObject.scene == gameObject.scene && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }
}
