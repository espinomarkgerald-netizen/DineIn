using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Ends Tutorial Day through the real result panel, then exits isolation safely.</summary>
[DisallowMultipleComponent]
public sealed class TutorialCompletionFlow : MonoBehaviour
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private TutorialSystem tutorial;
    private TutorialDayContext day;
    private bool ending, rebound;

    private void Awake() { tutorial = GetComponent<TutorialSystem>(); day = GetComponent<TutorialDayContext>(); }
    private void OnEnable() { if (tutorial != null) tutorial.TutorialCompletedChanged += OnCompleted; }
    private void OnDisable() { if (tutorial != null) tutorial.TutorialCompletedChanged -= OnCompleted; }

    private void OnCompleted()
    {
        if (ending) return;
        if (day == null) day = GetComponent<TutorialDayContext>();
        ending = true;
        GameDayManager.Instance?.EndShift();
        StartCoroutine(BindResultAction());
    }

    private IEnumerator BindResultAction()
    {
        while (!rebound)
        {
            GameDayManager manager = GameDayManager.Instance;
            GameObject panel = Read<GameObject>(manager, "resultsPanel");
            Button action = Read<Button>(manager, "resultsActionButton");
            if (panel != null && panel.activeInHierarchy && action != null)
            {
                action.onClick = new Button.ButtonClickedEvent();
                action.onClick.AddListener(Finish);
                TMP_Text label = Read<TMP_Text>(manager, "resultsActionButtonText");
                if (label != null) label.text = day != null && day.CareerSaveExisted
                    ? "RETURN TO GAME MODE" : "START DAY 2";
                rebound = true;
                yield break;
            }
            yield return null;
        }
    }

    private void Finish()
    {
        bool revisit = day != null && day.CareerSaveExisted;
        if (revisit) day.RestoreExistingCareerNow(); else day?.CommitFirstCareerDayTwo();
        Load(revisit ? "NewGameMenu" : "Lobby1");
    }

    private static T Read<T>(object owner, string field) where T : class =>
        owner?.GetType().GetField(field, PrivateInstance)?.GetValue(owner) as T;
    private static void Load(string scene)
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(scene);
        else SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
    }
}
