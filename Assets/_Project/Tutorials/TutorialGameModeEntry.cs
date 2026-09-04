using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Routes a new Casual Dining career to Tutorial Day and adds its revisit entry.</summary>
public static class TutorialGameModeEntry
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "NewGameMenu") return;
        GameModePopupController controller = UnityEngine.Object.FindFirstObjectByType<GameModePopupController>(FindObjectsInactive.Include);
        if (controller == null) return;
        bool hasSave = GameSaveManager.Instance != null && GameSaveManager.Instance.HasSave();
        string[] routes = controller.GetType().GetField("campaignRestaurantScenes", PrivateInstance)?.GetValue(controller) as string[];
        if (!hasSave && routes != null && routes.Length > 0) routes[0] = "Lobby1Tutorial";
        if (PlayerPrefs.GetInt(TutorialSystem.TutorialCompletedSaveKey, 0) == 1) AddRevisitButton(controller);
    }

    private static void AddRevisitButton(GameModePopupController controller)
    {
        if (GameObject.Find("RevisitTutorialButton") != null) return;
        Button source = null;
        foreach (Button button in controller.GetComponentsInChildren<Button>(true))
            foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                if (label.text.IndexOf("CAMPAIGN", StringComparison.OrdinalIgnoreCase) >= 0) { source = button; break; }
        if (source == null) return;
        Button revisit = UnityEngine.Object.Instantiate(source, source.transform.parent);
        revisit.name = "RevisitTutorialButton";
        revisit.onClick = new Button.ButtonClickedEvent();
        revisit.onClick.AddListener(() => Load("Lobby1Tutorial"));
        TMP_Text text = revisit.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = "RE-VISIT TUTORIAL";
        revisit.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
    }

    private static void Load(string scene)
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(scene);
        else SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
    }
}
