using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal sealed class DeveloperPracticeSession : MonoBehaviour
{
    private const string GameMenu = "NewGameMenu";
    private static DeveloperPracticeSession instance;
    private Func<bool> previousGuard;
    private GameSaveManager saves;
    private GameSaveData career;
    private DailyObjectiveManager.NetworkState objectives;
    private int[] finance;
    private GameFlowManager.RestaurantSessionState phase;
    private string restaurant;
    private string account;
    private bool exiting;

    internal static bool IsActive => instance != null;
    internal static bool IsWindows => Application.platform == RuntimePlatform.WindowsPlayer;
    internal static bool CanStart => IsWindows && !IsActive && CampaignSaveStore.RuntimeCampaign &&
        !CampaignSaveStore.NeedsReload && GameSaveManager.Instance != null &&
        GameSaveManager.Instance.HasCompletedInitialLoad && !GameSaveManager.Instance.IsApplyingSave &&
        GameFlowManager.Instance != null && GameFlowManager.Instance.UsesSingleRestaurantFlow &&
        GameDayManager.Instance != null && !MultiplayerRestaurantBridge.IsActive &&
        !Photon.Pun.PhotonNetwork.InRoom && !SceneLoader.HasPendingLoad &&
        (SceneLoader.Instance == null || !SceneLoader.Instance.IsLoading) &&
        SceneManager.GetActiveScene().name == CampaignSaveStore.RestaurantScene;

    internal static bool CanRunCommands => IsActive && !instance.exiting &&
        instance.account == CurrentAccount && !MultiplayerRestaurantBridge.IsActive &&
        !Photon.Pun.PhotonNetwork.InRoom && SceneManager.GetActiveScene().name == instance.restaurant;

    private static string CurrentAccount => PlayFabAuthManager.Instance?.IsLoggedIn == true
        ? PlayFabAuthManager.Instance.PlayFabId : null;

    internal static bool TryStart()
    {
        if (!CanStart)
            return false;

        var saves = GameSaveManager.Instance;
        var career = saves.CaptureRuntimeState();
        var session = new GameObject("Developer Practice").AddComponent<DeveloperPracticeSession>();
        session.restaurant = SceneManager.GetActiveScene().name;
        session.account = CurrentAccount;
        session.saves = saves;
        session.career = career;
        session.objectives = DailyObjectiveManager.Instance?.CaptureNetworkState();
        session.finance = DailyFinanceBridge.Instance?.CaptureNetworkState();
        session.phase = GameFlowManager.Instance.CurrentRestaurantSessionState;
        session.previousGuard = GameSaveManager.PersistenceSuspended;
        DontDestroyOnLoad(session.gameObject);
        instance = session;
        GameSaveManager.PersistenceSuspended = session.SuspendPersistence;
        SceneManager.sceneLoaded += session.OnSceneLoaded;
        try
        {
            session.CreateIndicator();
            GameFlowManager.Instance.ResetRun();
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            session.exiting = true;
            return false;
        }
        return true;
    }

    internal static bool TryExit()
    {
        if (!IsActive)
            return false;
        instance.exiting = true;
        return true;
    }

    private bool SuspendPersistence() => true;

    internal static bool AllowsScene(string sceneName, LoadSceneMode mode)
    {
        if (!IsActive)
            return true;
        return mode == LoadSceneMode.Additive ? sceneName == "RestockScene" :
            sceneName == instance.restaurant || sceneName == GameMenu || sceneName == "NewMainMenu";
    }

    private void Update()
    {
        if (account != CurrentAccount)
            exiting = true;
        if (!exiting || SceneLoader.HasPendingLoad || SceneLoader.Instance?.IsLoading == true)
            return;

        Time.timeScale = 1f;
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(GameMenu);
        else
            SceneLoader.TryLoadScene(GameMenu, LoadSceneMode.Single, out _);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || scene.name == restaurant)
            return;

        // The menu is active before the disposable runtime can release its save guard.
        try
        {
            if (saves != null) saves.ApplyTemporaryRuntimeState(career);
            DailyObjectiveManager.Instance?.ApplyNetworkState(objectives);
            DailyFinanceBridge.Instance?.RestoreTemporaryState(finance);
            GameFlowManager.Instance?.RestoreTemporaryRestaurantPhase(phase);
        }
        finally
        {
            CampaignSaveStore.NeedsReload = true;
            GameSaveManager.PersistenceSuspended = previousGuard;
            instance = null;
            Destroy(gameObject);
        }
    }

    private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void CreateIndicator()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        var label = new GameObject("Practice Status", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(transform, false);
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0, 12);
        rect.sizeDelta = new Vector2(420, 30);
        label.text = "Practice · Progress won’t save";
        label.fontSize = 20;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }
}
