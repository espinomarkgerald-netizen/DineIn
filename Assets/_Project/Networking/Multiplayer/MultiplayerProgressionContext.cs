using System;
using System.Collections.Generic;
using UnityEngine;

// A disposable runtime using the restaurant's authored catalog and existing save-data schema.
[DefaultExecutionOrder(-10999)]
public sealed class MultiplayerProgressionContext : MonoBehaviour
{
    private static MultiplayerProgressionContext instance;
    private GameSaveManager saves;
    private GameSaveData careerRuntime;
    private List<ItemData> careerItems;
    private Func<bool> previousPersistenceGuard;
    private bool isolated;
    private bool restoring;
    public static bool RestorationInProgress => instance != null && instance.restoring;
    private DailyObjectiveManager.NetworkState careerObjectives;
    private int[] careerFinance;
    private bool initialLoadWasComplete;
    private GameFlowManager.RestaurantSessionState careerPhase;
    public static bool LocallyPrepared => IsActive && instance.prepared;
    public static bool Ready => LocallyPrepared && (MultiplayerSessionManager.Instance.IsHostConnection
        || MultiplayerRestaurantBridge.Active != null && MultiplayerRestaurantBridge.Active.HasState);
    private bool prepared;
    public static bool IsActive => instance != null && !instance.restoring
        && MultiplayerSessionManager.Instance != null && MultiplayerSessionManager.Instance.IsMultiplayerSession;
    public static int CurrentDay => IsActive && GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
    public static MenuCatalog Catalog => IsActive ? MenuCatalog.Default : null;

    private void Awake()
    {
        if (MultiplayerSessionManager.Instance == null || !MultiplayerSessionManager.Instance.IsMultiplayerSession) return;
        instance = this;
        previousPersistenceGuard = GameSaveManager.PersistenceSuspended;
        isolated = true;
        GameSaveManager.PersistenceSuspended = SuspendPersistence;
        saves = GameSaveManager.Instance;
        // This context runs before the authored save manager's Awake (-500).
        // Reuse it: a new singleton would make it destroy the shared ManagementSystems root.
        if (saves == null) saves = FindFirstObjectByType<GameSaveManager>();
        if (saves == null) saves = new GameObject("GameSaveManager").AddComponent<GameSaveManager>();
        if (!Enum.TryParse(MultiplayerSessionManager.Instance.RestaurantType, out RestaurantType type))
        {
            Debug.LogError("[Multiplayer] Room restaurant type is invalid; refusing a fallback catalog.");
            enabled = false;
            return;
        }
        MenuCatalog.SetActiveRestaurantType(type);
        // Capture loaded career runtime only for restoration; never use it to seed the session.
        initialLoadWasComplete = saves.InitialLoadCompletedWithoutOverride;
        careerPhase = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentRestaurantSessionState
            : GameFlowManager.RestaurantSessionState.PreOpen;
        careerRuntime = saves.CaptureRuntimeState();
        careerObjectives = DailyObjectiveManager.Instance?.CaptureNetworkState();
        careerFinance = DailyFinanceBridge.Instance?.CaptureNetworkState();
        if (InventoryManager.Instance != null && InventoryManager.Instance.Items != null)
            careerItems = new List<ItemData>(InventoryManager.Instance.Items);
        if (MenuCatalog.Default == null || MenuCatalog.Default.RestaurantType != type)
        {
            Debug.LogError("[Multiplayer] Room restaurant catalog is unavailable.");
            enabled = false;
        }
    }

    private bool SuspendPersistence() => isolated || previousPersistenceGuard?.Invoke() == true;

    private void Start()
    {
        if (prepared || !IsActive || saves == null || Catalog == null) return;
        // GameSaveData carries the same new-restaurant defaults: Day 1, money and approval.
        saves.ApplyTemporaryRuntimeState(new GameSaveData());
        // Fresh temporary HR needs applicants; persisted empty Campaign pools remain valid.
        var employees = EmployeeManager.Instance;
        if (MultiplayerSessionManager.Instance.IsAuthority && employees != null && employees.allEmployees.Count == 0)
            employees.GenerateEmployees();
        // Only fresh context initialization clears inventory. Subsequent days
        // and a guest's authoritative rejoin snapshot retain their actual stock.
        InventoryManager.Instance?.ConfigureItems(new List<ItemData>(Catalog.Ingredients), grantStarterStock: false);
        RecipeManager.Instance?.UnlockByDay(CurrentDay);
        prepared = true;
        if (MultiplayerSessionManager.Instance.IsAuthority)
            GameFlowManager.Instance?.PrepareFreshMultiplayerRun();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        restoring = true;
        // Keep all writes and wallet callbacks suspended throughout restoration.
        if (careerItems != null) InventoryManager.Instance?.ConfigureItems(careerItems);
        if (saves != null && careerRuntime != null) saves.ApplyTemporaryRuntimeState(careerRuntime);
        DailyObjectiveManager.Instance?.ApplyNetworkState(careerObjectives);
        DailyFinanceBridge.Instance?.RestoreTemporaryState(careerFinance);
        GameFlowManager.Instance?.RestoreTemporaryRestaurantPhase(careerPhase);
        MenuCatalog.ClearActiveRestaurantOverride();
        isolated = false;
        GameSaveManager.PersistenceSuspended = previousPersistenceGuard;
        instance = null;
        // An initial campaign load deferred by direct multiplayer entry runs only after leaving.
        if (saves != null && !initialLoadWasComplete) saves.CompleteDeferredInitialLoad();
    }
}
