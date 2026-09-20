using System.Collections;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-500)]
public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance { get; private set; }

    [Header("Save")]
    [SerializeField] private string saveFileName = "dinein_save.json";
    [SerializeField] private bool autoLoadOnStart = true;
    [SerializeField] private bool autoSaveOnPause = true;
    [SerializeField] private bool autoSaveOnQuit = true;

    public bool IsApplyingSave { get; private set; }
    // Optional disposable-session boundary. Unset for campaign and tutorial flows.
    public static System.Func<bool> PersistenceSuspended { get; set; }
    public static bool IsPersistenceSuspended => PersistenceSuspended?.Invoke() == true;
    public GameSaveData CaptureRuntimeState() => CaptureCurrentData();
    public void CompleteDeferredInitialLoad()
    {
        if (IsPersistenceSuspended || !CampaignSaveStore.RuntimeCampaign || !autoLoadOnStart || HasCompletedInitialLoad) return;
        hasAutoLoaded = true;
        LoadGame();
    }
    public void ApplyTemporaryRuntimeState(GameSaveData data)
    {
        if (IsPersistenceSuspended) ApplySaveData(data, false, false);
    }

#if UNITY_EDITOR
    public bool SuppressWritesForTests { get; set; }
#endif

    private string SavePath => Path.Combine(Application.persistentDataPath, CampaignSaveStore.ResolveFileName(saveFileName));
    public string CampaignSavePath => SavePath;
    // Preserve the existing clean post-tutorial start; never overwrite an existing career.
    public void CreateInitialCampaign(GameSaveData cleanStart)
    {
        // Tutorial graduation always belongs to Casual Dining, even after visiting Fast Food.
        string initialPath = Path.Combine(Application.persistentDataPath, saveFileName);
        string initialCheckpoint = Path.Combine(Application.persistentDataPath,
            Path.GetFileNameWithoutExtension(saveFileName) + "_day_start.json");
        if (IsPersistenceSuspended || MultiplayerRestockBridge.IsActive || File.Exists(initialPath) ||
            File.Exists(initialCheckpoint) || cleanStart == null) return;
#if UNITY_EDITOR
        if (SuppressWritesForTests) return;
#endif
        CampaignSaveStore.AtomicWrite(initialPath, JsonUtility.ToJson(cleanStart, true));
    }
    public void ReloadCampaignFromDisk()
    {
        if (!CampaignSaveStore.RuntimeCampaign) return;
        hasAutoLoaded = true;
        LoadGame();
    }
    private string DayCheckpointPath => Path.Combine(
        Application.persistentDataPath,
        Path.GetFileNameWithoutExtension(CampaignSaveStore.ResolveFileName(saveFileName)) + "_day_start.json");

    private bool hasAutoLoaded;
    private string loadedPath;

    public bool InitialLoadCompletedWithoutOverride => !autoLoadOnStart || (hasAutoLoaded && loadedPath == SavePath);
    public bool HasCompletedInitialLoad => IsPersistenceSuspended || !autoLoadOnStart || (hasAutoLoaded && loadedPath == SavePath);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        // OnApplicationQuit is unreliable when stopping Play mode in the Editor.
        // This hook fires reliably when the user presses Stop.
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
    }

#if UNITY_EDITOR
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // ExitingPlayMode fires before Unity destroys scene objects — safe to read all managers.
        if (state == PlayModeStateChange.ExitingPlayMode && autoSaveOnQuit)
            SaveGame();
    }
#endif

    private void Start()
    {
        if (IsPersistenceSuspended || !CampaignSaveStore.RuntimeCampaign) return;
        if (autoLoadOnStart && (!hasAutoLoaded || loadedPath != SavePath))
        {
            hasAutoLoaded = true;
            // Load immediately on Start — no yield needed now that LocalGameSaveManager
            // is removed and nothing else overwrites the managers before this runs.
            LoadGame();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && autoSaveOnPause)
            SaveGame();
    }

    private void OnApplicationQuit()
    {
        if (autoSaveOnQuit)
            SaveGame();
    }

    public bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public void RequestSave()
    {
        if (IsPersistenceSuspended || !CampaignSaveStore.RuntimeCampaign || CampaignSaveStore.NeedsReload) return;
#if UNITY_EDITOR
        if (SuppressWritesForTests)
            return;
#endif
        // Other managers can request a save from Awake while this manager is
        // waiting to auto-load in Start. Never overwrite the existing file with
        // scene defaults during that bootstrap window.
        if (autoLoadOnStart && (!hasAutoLoaded || loadedPath != SavePath))
            return;

        if (IsApplyingSave)
            return;

        SaveGame();
    }

    public void SaveGame()
    {
        if (IsPersistenceSuspended || !CampaignSaveStore.RuntimeCampaign || CampaignSaveStore.NeedsReload) return;
        if (IsApplyingSave) return;
#if UNITY_EDITOR
        if (SuppressWritesForTests)
            return;
#endif
        if (autoLoadOnStart && (!hasAutoLoaded || loadedPath != SavePath))
            return;

        if (File.Exists(DayCheckpointPath) && (CampaignSaveStore.IsFastFood ||
            (GameFlowManager.Instance != null && GameFlowManager.Instance.HasRunningRestaurantDay)))
        {
            Debug.Log("[GameSaveManager] Unfinished day active; preserving the day-start checkpoint.");
            return;
        }

        GameSaveData data = CaptureCurrentData();

        WriteSaveData(SavePath, data);
    }

    public void CaptureDayStartCheckpoint()
    {
        if (IsPersistenceSuspended || !CampaignSaveStore.RuntimeCampaign || CampaignSaveStore.NeedsReload) return;
#if UNITY_EDITOR
        if (SuppressWritesForTests)
            return;
#endif
        if ((autoLoadOnStart && (!hasAutoLoaded || loadedPath != SavePath)) || IsApplyingSave)
            return;

        GameSaveData data = CaptureCurrentData();
        string json = JsonUtility.ToJson(data, true);
        CampaignSaveStore.WritePair(new CampaignSaveStore.Snapshot { save = json, checkpoint = json });
        CampaignSaveStore.NeedsReload = false;
        Debug.Log($"[GameSaveManager] Captured Day {data.currentDay} start checkpoint.");
    }

    public bool RestoreDayStartCheckpoint()
    {
        if (IsPersistenceSuspended || CampaignSaveStore.ProtectedSession) return false;
        if (!File.Exists(DayCheckpointPath))
            return false;

        GameSaveData data = ReadSaveData(DayCheckpointPath);
        if (data == null)
            return false;

        ApplySaveData(data, true, false);
        if (CampaignSaveStore.IsFastFood)
        {
            CampaignSaveStore.WritePair(new CampaignSaveStore.Snapshot { save = JsonUtility.ToJson(data, true), checkpoint = null });
            CampaignSaveStore.NeedsReload = false;
        }
        else WriteSaveData(SavePath, data);
        Debug.Log($"[GameSaveManager] Restored Day {data.currentDay} start checkpoint.");
        return true;
    }

    public void CommitDayCheckpoint()
    {
        if (IsPersistenceSuspended || CampaignSaveStore.ProtectedSession) return;
        if (File.Exists(DayCheckpointPath))
            File.Delete(DayCheckpointPath);
    }

    public bool CommitRestaurantResults()
    {
        if (!CampaignSaveStore.RuntimeCampaign || IsApplyingSave || !HasCompletedInitialLoad) return false;
#if UNITY_EDITOR
        if (SuppressWritesForTests) return true;
#endif
        try
        {
            // Recoverable pair transaction: a crash cannot load a stale day-start snapshot over final results.
            CampaignSaveStore.WritePair(new CampaignSaveStore.Snapshot {
                save = JsonUtility.ToJson(CaptureCurrentData(), true), checkpoint = null });
            CampaignSaveStore.NeedsReload = false;
            return true;
        }
        catch (System.Exception error)
        {
            Debug.LogError("[GameSaveManager] Results could not be committed. Checkpoint preserved: " + error.Message);
            return false;
        }
    }

    private GameSaveData CaptureCurrentData()
    {
        GameSaveData data = new GameSaveData();
        GameSaveData previous = IsPersistenceSuspended ? null : ReadSaveData(SavePath);
        if (previous?.campaignCreditReceipts != null)
            data.campaignCreditReceipts = previous.campaignCreditReceipts;

        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.FillSaveData(data);

        if (MoneyManager.Instance != null)
            MoneyManager.Instance.FillSaveData(data);

        if (AlienApprovalManager.Instance != null)
            AlienApprovalManager.Instance.FillSaveData(data);

        if (UnlockManager.Instance != null)
            UnlockManager.Instance.FillSaveData(data);

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.FillSaveData(data);

        if (MenuAvailabilityManager.Instance != null)
            MenuAvailabilityManager.Instance.FillSaveData(data);

        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.FillSaveData(data);

        UnlockCelebrationManager.EnsureInstance()?.FillSaveData(data);

        if (EmployeeManager.Instance != null)
            EmployeeManager.Instance.FillSaveData(data);

        if (RestockOrderManager.Instance != null)
            RestockOrderManager.Instance.FillSaveData(data);

        CasualDiningPolishManager.EnsureInstance()?.FillSaveData(data);
        ManagerComplaintSystem.EnsureInstance()?.FillSaveData(data);
        if (CampaignSaveStore.IsFastFood)
        {
            DailyObjectiveManager.Instance?.FillSaveData(data);
            data.fastFoodFinance = DailyFinanceBridge.Instance?.CaptureNetworkState();
            if (data.fastFoodDayComplete) data.fastFoodDayStats = GameDayManager.Instance?.CaptureFastFoodReport();
        }

        return data;
    }

    private void WriteSaveData(string path, GameSaveData data)
    {
        if (CampaignSaveStore.ProtectedSession) return;
        string json = JsonUtility.ToJson(data, true);
        CampaignSaveStore.AtomicWrite(path, json);

        Debug.Log("[GameSaveManager] Game saved to: " + path);
        Debug.Log("[GameSaveManager] Saved money: " + data.money);
        Debug.Log("[GameSaveManager] Saved day: " + data.currentDay);
        Debug.Log("[GameSaveManager] Saved approval: " + data.approval);
    }

    private static GameSaveData ReadSaveData(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            return JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[GameSaveManager] Could not read save data from {path}: {exception.Message}");
            return null;
        }
    }

    public void LoadGame()
    {
        try { LoadGameCore(); }
        catch (System.Exception error)
        {
            hasAutoLoaded = false;
            CampaignSaveStore.NeedsReload = true;
            Debug.LogError("[GameSaveManager] Save could not be loaded; original files preserved. " + error.Message);
            WarningSlideUI.Instance?.Show("Restaurant progress could not be loaded. Reopen the restaurant after checking its save.");
        }
    }

    private void LoadGameCore()
    {
        if (IsPersistenceSuspended) return;
        bool profileChanged = loadedPath != null && loadedPath != SavePath;
        loadedPath = SavePath;
        if (profileChanged)
        {
            DailyObjectiveManager.Instance?.ResetForNewRun();
            DailyRevenueTracker.Instance?.ResetForNewDay();
            DailyFinanceBridge.Instance?.ResetDay();
            FinanceManager.Instance?.ResetDailyExpenses();
        }
        CampaignSaveStore.RecoverPendingWrite();
        if (!CampaignSaveStore.ProtectedSession) CampaignSaveStore.NeedsReload = false;
        if (!HasSave() && !File.Exists(DayCheckpointPath))
        {
            Debug.Log("[GameSaveManager] No save file found — using defaults.");
            if (CampaignSaveStore.IsFastFood || profileChanged)
            {
                ApplySaveData(new GameSaveData(), false, false);
                EmployeeManager.Instance?.PrepareFreshRestaurantRoster();
                RequestSave();
            }
            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.HasRunningRestaurantDay)
                CaptureDayStartCheckpoint();
            return;
        }

        string loadPath = File.Exists(DayCheckpointPath) ? DayCheckpointPath : SavePath;
        string json = File.ReadAllText(loadPath);
        bool requiresFiniteInventoryMigration =
            !json.Contains("\"inventorySystemVersion\"");
        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

        if (data == null)
        {
            throw new System.IO.InvalidDataException("Save file could not be parsed.");
        }

        if (data.currentDay < 1 || data.saveSchemaVersion > 3 || !json.Contains("\"money\"") || !json.Contains("\"currentDay\""))
            throw new System.IO.InvalidDataException("Save is incomplete or from a newer version.");
        bool hasCasualDiningSchema = json.Contains("\"saveSchemaVersion\"");
        if (!hasCasualDiningSchema)
            data.saveSchemaVersion = 0;
        bool requiresCasualDiningMigration = !hasCasualDiningSchema || data.saveSchemaVersion < 3;

        ApplySaveData(data, false, requiresFiniteInventoryMigration);

        if (CampaignSaveStore.IsFastFood && loadPath == DayCheckpointPath)
        {
            // Rollback is complete. Subsequent preparation edits must not be masked by an old checkpoint.
            CampaignSaveStore.WritePair(new CampaignSaveStore.Snapshot { save = JsonUtility.ToJson(data, true), checkpoint = null });
            CampaignSaveStore.NeedsReload = false;
        }

        Debug.Log("[GameSaveManager] Game loaded from: " + loadPath);
        Debug.Log("[GameSaveManager] Loaded money: " + data.money);
        Debug.Log("[GameSaveManager] Loaded day: " + data.currentDay);
        Debug.Log("[GameSaveManager] Loaded approval: " + data.approval);

        if (requiresFiniteInventoryMigration || requiresCasualDiningMigration)
        {
            if (requiresFiniteInventoryMigration)
                Debug.Log("[GameSaveManager] Migrated the save to finite restaurant stock.");
            if (requiresCasualDiningMigration)
                Debug.Log("[GameSaveManager] Migrated the save to Casual Dining schema 3.");
            SaveGame();
        }

        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.HasRunningRestaurantDay)
            CaptureDayStartCheckpoint();
    }

    private void ApplySaveData(
        GameSaveData data,
        bool reconcileMoney,
        bool migrateFiniteInventory)
    {
        IsApplyingSave = true;

        try
        {
            if (UnlockManager.Instance != null)
                UnlockManager.Instance.ApplySaveData(data);

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.ApplySaveData(data);
                if (migrateFiniteInventory && !MultiplayerProgressionContext.IsActive)
                    InventoryManager.Instance.EnsureStarterStockForFiniteInventory();
            }

            if (MenuAvailabilityManager.Instance != null)
                MenuAvailabilityManager.Instance.ApplySaveData(data);

            if (EquipmentManager.Instance != null)
                EquipmentManager.Instance.ApplySaveData(data);

            UnlockCelebrationManager.EnsureInstance()?.ApplySaveData(data);

            if (EmployeeManager.Instance != null)
                EmployeeManager.Instance.ApplySaveData(data);

            RestockOrderManager.EnsureInstance()?.ApplySaveData(data);

            CasualDiningPolishManager.EnsureInstance()?.ApplySaveData(data);
            ManagerComplaintSystem.EnsureInstance()?.ApplySaveData(data);
            if (CampaignSaveStore.IsFastFood)
            {
                DailyObjectiveManager.Instance?.ApplySaveData(data);
                FinanceManager.Instance?.ResetDailyExpenses();
                DailyRevenueTracker.Instance?.ResetForNewDay();
                DailyFinanceBridge.Instance?.ApplyRestaurantSave(data.fastFoodFinance);
                if (data.fastFoodDayComplete) GameDayManager.Instance?.RestoreFastFoodReport(data.fastFoodDayStats);
            }

            if (MoneyManager.Instance != null)
            {
                if (reconcileMoney)
                    MoneyManager.Instance.SetMoney(data.money, "Unfinished Day Rollback");
                else
                    MoneyManager.Instance.ApplySaveData(data);
            }

            if (AlienApprovalManager.Instance != null)
                AlienApprovalManager.Instance.ApplySaveData(data);

            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.ApplySaveData(data);
        }
        finally
        {
            IsApplyingSave = false;
        }

    }

    public void DeleteSave()
    {
        if (IsPersistenceSuspended || CampaignSaveStore.ProtectedSession) return;
        if (!HasSave() && !File.Exists(DayCheckpointPath))
            return;

        if (File.Exists(SavePath))
            File.Delete(SavePath);
        if (File.Exists(DayCheckpointPath))
            File.Delete(DayCheckpointPath);
        Debug.Log("[GameSaveManager] Save deleted.");
    }
}
