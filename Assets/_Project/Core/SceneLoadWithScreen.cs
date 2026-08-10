using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Triggers a scene load with a loading screen overlay.
/// Routes through SceneManagerUI when available so there is never a
/// competing parallel async load operation.
/// Falls back to a standalone coroutine-based load only when
/// SceneManagerUI is absent (e.g. direct scene playback in the Editor).
/// </summary>
public class SceneLoadWithScreen : MonoBehaviour
{
    [SerializeField] private GameObject loadingScreenRoot;
    [SerializeField] private string sceneToLoad;
    [SerializeField] private float fakeDelayBeforeLoad = 1.5f;
    [SerializeField] private float minimumLoadingScreenTime = 2.5f;

    // Used only in standalone (no SceneManagerUI) mode.
    private bool isLoading;

    private void Awake()
    {
        if (loadingScreenRoot != null)
            loadingScreenRoot.SetActive(false);
    }

    private void OnEnable()
    {
        // Only subscribe in standalone mode — SceneManagerUI manages its own callback.
        if (SceneManagerUI.Instance == null)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Standalone fallback path only.
        isLoading = false;

        if (loadingScreenRoot != null)
            loadingScreenRoot.SetActive(false);
    }

    /// <summary>Loads the scene assigned in the Inspector field.</summary>
    public void LoadAssignedScene()
    {
        if (string.IsNullOrWhiteSpace(sceneToLoad)) return;
        TriggerLoad(sceneToLoad);
    }

    /// <summary>Loads any scene by name.</summary>
    public void LoadSceneByName(string targetSceneName)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName)) return;
        TriggerLoad(targetSceneName);
    }

    private void TriggerLoad(string targetSceneName)
    {
        // Preferred path: delegate to SceneManagerUI which owns the load guard.
        // This prevents two concurrent async operations racing each other.
        if (SceneManagerUI.Instance != null)
        {
            // Show our loading screen overlay for the visual effect, then let
            // SceneManagerUI do the actual load.
            if (loadingScreenRoot != null)
                loadingScreenRoot.SetActive(true);

            SceneManagerUI.Instance.LoadSceneWithScreen(targetSceneName, loadingScreenRoot);
            return;
        }

        // Standalone fallback (no Bootstrap — direct scene testing in Editor).
        if (isLoading) return;
        StartCoroutine(StandaloneLoadRoutine(targetSceneName));
    }

    private IEnumerator StandaloneLoadRoutine(string targetSceneName)
    {
        isLoading = true;

        if (loadingScreenRoot != null)
            loadingScreenRoot.SetActive(true);

        float visibleTime = 0f;

        while (visibleTime < fakeDelayBeforeLoad)
        {
            visibleTime += Time.unscaledDeltaTime;
            yield return null;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f || visibleTime < minimumLoadingScreenTime)
        {
            visibleTime += Time.unscaledDeltaTime;
            yield return null;
        }

        operation.allowSceneActivation = true;

        while (!operation.isDone)
            yield return null;
    }
}