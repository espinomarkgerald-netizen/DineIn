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

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

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
        if (IsApplyingSave)
            return;

        SaveGame();
    }

    public void SaveGame()
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

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);

        Debug.Log("[GameSaveManager] Game saved to: " + SavePath);
        Debug.Log("[GameSaveManager] Saved money: " + data.money);
        Debug.Log("[GameSaveManager] Saved day: " + data.currentDay);
        Debug.Log("[GameSaveManager] Saved approval: " + data.approval);
    }

    public void LoadGame()
    {
        if (!HasSave())
        {
            Debug.Log("[GameSaveManager] No save file found — using defaults.");
            return;
        }

        string json = File.ReadAllText(SavePath);
        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

        if (data == null)
        {
            Debug.LogWarning("[GameSaveManager] Save file could not be parsed.");
            return;
        }

        IsApplyingSave = true;

        try
        {
            if (UnlockManager.Instance != null)
                UnlockManager.Instance.ApplySaveData(data);

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.ApplySaveData(data);

            if (MoneyManager.Instance != null)
                MoneyManager.Instance.ApplySaveData(data);

            if (AlienApprovalManager.Instance != null)
                AlienApprovalManager.Instance.ApplySaveData(data);

            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.ApplySaveData(data);
        }
        finally
        {
            IsApplyingSave = false;
        }

        Debug.Log("[GameSaveManager] Game loaded from: " + SavePath);
        Debug.Log("[GameSaveManager] Loaded money: " + data.money);
        Debug.Log("[GameSaveManager] Loaded day: " + data.currentDay);
        Debug.Log("[GameSaveManager] Loaded approval: " + data.approval);
    }

    public void DeleteSave()
    {
        if (!HasSave())
            return;

        File.Delete(SavePath);
        Debug.Log("[GameSaveManager] Save deleted.");
    }
}