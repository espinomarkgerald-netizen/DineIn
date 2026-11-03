using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Routes each restaurant's first entry to its own tutorial and provides revisits.</summary>
public static class TutorialGameModeEntry
{
    public static bool HasCompletedTutorial => PlayerPrefs.GetInt(TutorialSystem.TutorialCompletedSaveKey, 0) == 1;
    public static bool IsMenuLaunch { get; private set; }
    public static bool IsRevisitLaunch { get; private set; }

    public static string ResolveCampaignDestination(string careerScene, out bool isMenuLaunch)
    {
        if (HasStartedRegularShift(careerScene))
        {
            isMenuLaunch = false;
            return careerScene;
        }
        if (careerScene == FineDiningScene.Career)
        {
            isMenuLaunch = PlayerPrefs.GetInt(FineDiningTutorialBridge.CompletedKey, 0) == 0 &&
                PlayerPrefs.GetInt(FineDiningTutorialBridge.SkippedKey, 0) == 0;
            return isMenuLaunch ? FineDiningScene.Tutorial : careerScene;
        }
        if (careerScene == "Lobby2")
        {
            isMenuLaunch = PlayerPrefs.GetInt(FastFoodTutorialBridge.CompletedKey, 0) == 0 &&
                PlayerPrefs.GetInt(FastFoodTutorialBridge.SkippedKey, 0) == 0;
            return isMenuLaunch ? FastFoodScene.Tutorial : careerScene;
        }

        isMenuLaunch = careerScene == "Lobby1" && !HasCompletedTutorial;
        return isMenuLaunch ? "Lobby1Tutorial" : careerScene;
    }

    public static void BeginCampaignRoute(string careerScene, bool isMenuLaunch)
    {
        if (careerScene == "Lobby1" || careerScene == "Lobby2" || careerScene == "Lobby3")
            CampaignSaveStore.SelectRestaurant(careerScene);

        IsMenuLaunch = isMenuLaunch;
        IsRevisitLaunch = false;
    }

    public static string RouteCampaign(string careerScene)
    {
        string destination = ResolveCampaignDestination(careerScene, out bool isMenuLaunch);
        BeginCampaignRoute(careerScene, isMenuLaunch);
        return destination;
    }

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        IsMenuLaunch = IsRevisitLaunch = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "NewGameMenu") return;
        GameModePopupController controller = UnityEngine.Object.FindFirstObjectByType<GameModePopupController>(FindObjectsInactive.Include);
        if (controller == null) return;
        IsMenuLaunch = IsRevisitLaunch = false;
        AddRevisitButton(controller);
    }

    public static bool HasStartedRegularShift(string restaurant)
    {
        if (PlayerPrefs.GetInt("DineIn_RegularShiftStarted_" + restaurant, 0) == 1) return true;
        try
        {
            var account = CampaignSaveStore.ReadAccount(false);
            var auth = PlayFabAuthManager.Instance;
            if (auth != null && auth.IsLoggedIn && account.account != auth.PlayFabId) return false;
            var career = restaurant == "Lobby3" ? account.fineDining : restaurant == "Lobby2" ? account.fastFood : account.casual;
            return HasCareerProgress(career);
        }
        catch (Exception e) { Debug.LogWarning("[Tutorial] Career lookup deferred: " + e.Message); return false; }
    }

    private static bool HasCareerProgress(CampaignSaveStore.Snapshot career)
    {
        if (career == null || !CampaignSaveStore.Valid(career)) return false;
        var data = JsonUtility.FromJson<GameSaveData>(career.save);
        return data != null && (data.currentDay > 1 || data.lobbyCompleted || data.kitchenCompleted ||
            data.campaignCompleted || data.fastFoodDayComplete || data.fineDiningDayComplete ||
            data.polishLastFinalizedDay > 0 || data.successfulRestaurantDays?.Count > 0);
    }

    public static void MarkRegularShiftStarted()
    {
        if (!CampaignSaveStore.RuntimeCampaign || CampaignSaveStore.ProtectedSession) return;
        string key = "DineIn_RegularShiftStarted_" + CampaignSaveStore.RestaurantScene;
        if (PlayerPrefs.GetInt(key, 0) == 1) return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        CampaignSaveStore.NotifyLocalWrite();
    }

    private static void AddRevisitButton(GameModePopupController controller)
    {
        GameObject popup = controller.GetType().GetField("gamemodePopupUI", PrivateInstance)?.GetValue(controller) as GameObject;
        if (popup == null) return;
        foreach (Button existing in popup.GetComponentsInChildren<Button>(true))
            if (existing.name == "TutorialButton") return;
        Button source = null;
        foreach (Button button in popup.GetComponentsInChildren<Button>(true))
            foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                if (label.text.IndexOf("CAMPAIGN", StringComparison.OrdinalIgnoreCase) >= 0) { source = button; break; }
        if (source == null) return;
        Button revisit = UnityEngine.Object.Instantiate(source, source.transform.parent);
        revisit.name = "TutorialButton";
        // Refresh on the popup, since the button itself may be hidden for a new career.
        var availability = popup.AddComponent<TutorialReplayAvailability>();
        availability.Bind(controller, revisit);
        revisit.onClick = new Button.ButtonClickedEvent();
        revisit.onClick.AddListener(() =>
        {
            if (!controller.EnsureRestaurantAccess()) return;
            IsMenuLaunch = IsRevisitLaunch = true;
            Load(controller.SelectedRestaurantIndex == 2 ? FineDiningScene.Tutorial :
                controller.SelectedRestaurantIndex == 1 ? FastFoodScene.Tutorial : "Lobby1Tutorial");
        });
        TMP_Text text = revisit.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = "TUTORIAL";
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize * .85f;
            text.fontSizeMin = text.fontSizeMax * .75f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }
        RectTransform rect = (RectTransform)revisit.transform;
        RectTransform sourceRect = (RectTransform)source.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(sourceRect.sizeDelta.x * .7f, sourceRect.sizeDelta.y * .75f);
        rect.anchoredPosition = new Vector2(0, sourceRect.anchoredPosition.y - sourceRect.sizeDelta.y * .5f - rect.sizeDelta.y * .5f - 16f);
        rect.SetAsLastSibling();
        // The popup is initially inactive, so its existing effect list is configured before Awake.
        var effects = source.GetComponentInParent<UI.Effects.UIButtonEffects>(true);
        var entries = effects != null ? typeof(UI.Effects.UIButtonEffects).GetField("buttonEntries", PrivateInstance)?.GetValue(effects)
            as System.Collections.Generic.List<UI.Effects.UIButtonEffects.ButtonConfig> : null;
        var original = entries?.Find(entry => entry.targetButton == source);
        if (original != null)
            entries.Add(new UI.Effects.UIButtonEffects.ButtonConfig {
                label = "TutorialButton", targetButton = revisit, preset = original.preset,
                speedMultiplier = original.speedMultiplier, hoverSFX = original.hoverSFX, clickSFX = original.clickSFX
            });
    }

    private static void Load(string scene)
    {
        if (!CampaignCloudSync.CanEnterGameplay()) return;
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(scene);
        else SceneLoader.TryLoadScene(scene, LoadSceneMode.Single, out _);
    }
}

public sealed class TutorialReplayAvailability : MonoBehaviour
{
    private GameModePopupController controller;
    private Button replay;
    public void Bind(GameModePopupController owner, Button button) { controller = owner; replay = button; Refresh(); }
    private void OnEnable() => Refresh();
    private void Refresh()
    {
        if (controller == null || replay == null) return;
        string restaurant = controller.SelectedRestaurantIndex == 2 ? "Lobby3" : controller.SelectedRestaurantIndex == 1 ? "Lobby2" : "Lobby1";
        replay.gameObject.SetActive(TutorialGameModeEntry.HasStartedRegularShift(restaurant));
    }
}
