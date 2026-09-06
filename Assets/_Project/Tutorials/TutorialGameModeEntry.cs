using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Routes a new Casual Dining career to Tutorial Day and adds its revisit entry.</summary>
public static class TutorialGameModeEntry
{
    public static bool HasCompletedTutorial => PlayerPrefs.GetInt(TutorialSystem.TutorialCompletedSaveKey, 0) == 1;
    public static bool IsMenuLaunch { get; private set; }
    public static bool IsRevisitLaunch { get; private set; }

    public static string RouteCampaign(string careerScene)
    {
        IsMenuLaunch = !HasCompletedTutorial;
        IsRevisitLaunch = false;
        return IsMenuLaunch ? "Lobby1Tutorial" : careerScene;
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
        if (HasCompletedTutorial) AddRevisitButton(controller);
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
        revisit.onClick = new Button.ButtonClickedEvent();
        revisit.onClick.AddListener(() =>
        {
            if (!HasCompletedTutorial) return;
            IsMenuLaunch = IsRevisitLaunch = true;
            Load("Lobby1Tutorial");
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
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(scene);
        else SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
    }
}
