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
    private bool initialLoadWasComplete;
    private GameFlowManager.RestaurantSessionState careerPhase;
    public static bool Ready => IsActive && instance.prepared;
    private bool prepared;
    public static bool IsActive => instance != null && !instance.restoring
        && MultiplayerSessionManager.Instance != null && MultiplayerSessionManager.Instance.IsMultiplayerSession;
    public static int CurrentDay => IsActive
        ? MultiplayerSessionManager.Instance.GetComponent<MultiplayerDayBridge>()?.CurrentDay ?? 1 : 1;
    public static MenuCatalog Catalog => IsActive ? MenuCatalog.Default : null;

    private void Awake()
    {
        if (MultiplayerSessionManager.Instance == null || !MultiplayerSessionManager.Instance.IsMultiplayerSession) return;
        instance = this;
        previousPersistenceGuard = GameSaveManager.PersistenceSuspended;
        isolated = true;
        GameSaveManager.PersistenceSuspended = SuspendPersistence;
        saves = GameSaveManager.Instance;
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
        if (!IsActive || saves == null || Catalog == null) return;
        // GameSaveData carries the same new-restaurant defaults: Day 1, money and approval.
        saves.ApplyTemporaryRuntimeState(new GameSaveData());
        // Reuse the normal one-box-per-ingredient initialization and exact authored ItemData assets.
        InventoryManager.Instance?.ConfigureItems(new List<ItemData>(Catalog.Ingredients));
        RecipeManager.Instance?.UnlockByDay(CurrentDay);
        prepared = true;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        restoring = true;
        // Keep all writes and wallet callbacks suspended throughout restoration.
        if (careerItems != null) InventoryManager.Instance?.ConfigureItems(careerItems);
        if (saves != null && careerRuntime != null) saves.ApplyTemporaryRuntimeState(careerRuntime);
        GameFlowManager.Instance?.RestoreTemporaryRestaurantPhase(careerPhase);
        MenuCatalog.ClearActiveRestaurantOverride();
        isolated = false;
        GameSaveManager.PersistenceSuspended = previousPersistenceGuard;
        instance = null;
        // An initial campaign load deferred by direct multiplayer entry runs only after leaving.
        if (saves != null && !initialLoadWasComplete) saves.CompleteDeferredInitialLoad();
    }
}
