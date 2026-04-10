using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadWithScreen : MonoBehaviour
{
    [SerializeField] private GameObject loadingScreenRoot;
    [SerializeField] private string sceneToLoad;
    [SerializeField] private float fakeDelayBeforeLoad = 1.5f;
    [SerializeField] private float minimumLoadingScreenTime = 2.5f;

    private bool isLoading;

    private void Awake()
    {
        if (loadingScreenRoot != null)
            loadingScreenRoot.SetActive(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isLoading = false;

        if (loadingScreenRoot != null)
            loadingScreenRoot.SetActive(false);
    }

    public void LoadAssignedScene()
    {
        if (isLoading || string.IsNullOrWhiteSpace(sceneToLoad))
            return;

        StartCoroutine(LoadRoutine(sceneToLoad));
    }

    public void LoadSceneByName(string targetSceneName)
    {
        if (isLoading || string.IsNullOrWhiteSpace(targetSceneName))
            return;

        StartCoroutine(LoadRoutine(targetSceneName));
    }

    private IEnumerator LoadRoutine(string targetSceneName)
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