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

#if UNITY_EDITOR
    public bool SuppressWritesForTests { get; set; }
#endif

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    private string DayCheckpointPath => Path.Combine(
        Application.persistentDataPath,
        Path.GetFileNameWithoutExtension(saveFileName) + "_day_start.json");

    private bool hasAutoLoaded;

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
        if (autoLoadOnStart && !hasAutoLoaded)
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
#if UNITY_EDITOR
        if (SuppressWritesForTests)
            return;
#endif
        // Other managers can request a save from Awake while this manager is
        // waiting to auto-load in Start. Never overwrite the existing file with
        // scene defaults during that bootstrap window.
        if (autoLoadOnStart && !hasAutoLoaded)
            return;

        if (IsApplyingSave)
            return;

        SaveGame();
    }

    public void SaveGame()
    {
#if UNITY_EDITOR
        if (SuppressWritesForTests)
            return;
#endif
        if (autoLoadOnStart && !hasAutoLoaded)
            return;

        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.HasRunningRestaurantDay &&
            File.Exists(DayCheckpointPath))
        {
            Debug.Log("[GameSaveManager] Unfinished day active; preserving the day-start checkpoint.");
            return;
        }

        GameSaveData data = CaptureCurrentData();

        WriteSaveData(SavePath, data);
    }

    public void CaptureDayStartCheckpoint()
    {
#if UNITY_EDITOR
        if (SuppressWritesForTests)
            return;
#endif
        if ((autoLoadOnStart && !hasAutoLoaded) || IsApplyingSave)
            return;

        GameSaveData data = CaptureCurrentData();
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        File.WriteAllText(DayCheckpointPath, json);
        Debug.Log($"[GameSaveManager] Captured Day {data.currentDay} start checkpoint.");
    }

    public bool RestoreDayStartCheckpoint()
    {
        if (!File.Exists(DayCheckpointPath))
            return false;

        GameSaveData data = ReadSaveData(DayCheckpointPath);
        if (data == null)
            return false;

        ApplySaveData(data, true, false);
        WriteSaveData(SavePath, data);
        Debug.Log($"[GameSaveManager] Restored Day {data.currentDay} start checkpoint.");
        return true;
    }

    public void CommitDayCheckpoint()
    {
        if (File.Exists(DayCheckpointPath))
            File.Delete(DayCheckpointPath);
    }

    private GameSaveData CaptureCurrentData()
    {
        GameSaveData data = new GameSaveData();

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

        if (EmployeeManager.Instance != null)
            EmployeeManager.Instance.FillSaveData(data);

        if (RestockOrderManager.Instance != null)
            RestockOrderManager.Instance.FillSaveData(data);

        CasualDiningPolishManager.EnsureInstance()?.FillSaveData(data);

        return data;
    }

    private void WriteSaveData(string path, GameSaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);

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
        if (!HasSave())
        {
            Debug.Log("[GameSaveManager] No save file found — using defaults.");
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
            Debug.LogWarning("[GameSaveManager] Save file could not be parsed.");
            return;
        }

        bool hasCasualDiningSchema = json.Contains("\"saveSchemaVersion\"");
        if (!hasCasualDiningSchema)
            data.saveSchemaVersion = 0;
        bool requiresCasualDiningMigration = !hasCasualDiningSchema || data.saveSchemaVersion < 3;

        ApplySaveData(data, false, requiresFiniteInventoryMigration);

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
                if (migrateFiniteInventory)
                    InventoryManager.Instance.EnsureStarterStockForFiniteInventory();
            }

            if (MenuAvailabilityManager.Instance != null)
                MenuAvailabilityManager.Instance.ApplySaveData(data);

            if (EquipmentManager.Instance != null)
                EquipmentManager.Instance.ApplySaveData(data);

            if (EmployeeManager.Instance != null)
                EmployeeManager.Instance.ApplySaveData(data);

            RestockOrderManager.EnsureInstance()?.ApplySaveData(data);

            CasualDiningPolishManager.EnsureInstance()?.ApplySaveData(data);

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
        if (!HasSave() && !File.Exists(DayCheckpointPath))
            return;

        if (File.Exists(SavePath))
            File.Delete(SavePath);
        if (File.Exists(DayCheckpointPath))
            File.Delete(DayCheckpointPath);
        Debug.Log("[GameSaveManager] Save deleted.");
    }
}
