using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent singleton that manages all scene transitions.
/// - Survives scene loads via DontDestroyOnLoad.
/// - Spawns its own loading screen from a prefab reference (a Project asset,
///   never a scene object) so nothing can go "Missing" after an unload.
/// - Guards against overlapping loads until Unity actually completes the operation,
///   including when the loading screen reaches its safety timeout.
/// </summary>
[DefaultExecutionOrder(-100)]
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Loading Screen Prefab")]
    [Tooltip("A PROJECT ASSET prefab (from your Assets folder), not a scene object. " +
             "This reference is safe because prefab assets are never unloaded with a scene.")]
    [SerializeField] private GameObject loadingCanvasPrefab;

    [Header("Transition Settings")]
    [Tooltip("Minimum time the loading screen stays visible, even if the scene loads instantly.")]
    [SerializeField] private float minimumLoadingTime = 1f;

    [Tooltip("Safety deadline in seconds. Releases blocked activation and reports the stall; navigation stays guarded until Unity completes.")]
    [SerializeField] private float safetyTimeoutSeconds = 30f;

    private GameObject spawnedLoadingCanvas;
    private Slider progressBar;
    private TMPro.TextMeshProUGUI progressText;
    private bool isLoading = false;
    private AsyncOperation activeOperation;
    private int transitionGeneration;
    private static int loadGeneration;
    private static AsyncOperation pendingOperation;
    private static string pendingScene;
    public static bool HasPendingLoad => pendingOperation != null && !pendingOperation.isDone;

    public bool IsLoading => isLoading;

    private void Awake()
    {
        // --- Strict singleton guard ---
        if (Instance != null && Instance != this)
        {
            Debug.Log("[SceneLoader] Duplicate instance detected on scene load — destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeUI();
    }

    /// <summary>
    /// Spawns the loading canvas from a prefab asset. Safe to call multiple times —
    /// only builds once, and rebuilds automatically if the instance was ever destroyed.
    /// </summary>
    private void InitializeUI()
    {
        if (loadingCanvasPrefab == null)
        {
            Debug.LogError("[SceneLoader] loadingCanvasPrefab is not assigned in the Inspector. " +
                            "No loading screen will be shown.");
            return;
        }

        // Unity overloads == to also catch destroyed-but-still-referenced objects.
        if (spawnedLoadingCanvas == null)
        {
            spawnedLoadingCanvas = Instantiate(loadingCanvasPrefab);
            spawnedLoadingCanvas.name = loadingCanvasPrefab.name + "_Instance";
            DontDestroyOnLoad(spawnedLoadingCanvas);
            foreach (Canvas canvas in spawnedLoadingCanvas.GetComponentsInChildren<Canvas>(true))
                if (canvas.isRootCanvas) canvas.sortingOrder = 32700;

            progressBar = spawnedLoadingCanvas.GetComponentInChildren<Slider>(true);
            progressText = FindDedicatedProgressText(spawnedLoadingCanvas);

            if (progressBar == null)
                Debug.LogWarning("[SceneLoader] No Slider found under the loading canvas prefab — " +
                                  "progress bar will not animate, but transitions will still work.");
            spawnedLoadingCanvas.SetActive(false);
        }
    }

    private static TMPro.TextMeshProUGUI FindDedicatedProgressText(GameObject loadingRoot)
    {
        if (loadingRoot == null)
            return null;

        // Loading screens may contain other text, including randomized tips.
        // Never claim the first TMP component and overwrite that content.
        TMPro.TextMeshProUGUI[] texts =
            loadingRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMPro.TextMeshProUGUI candidate = texts[i];
            if (candidate == null)
                continue;

            string objectName = candidate.gameObject.name;
            if (objectName.IndexOf("progress", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("percent", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }

        // The original burger screen intentionally uses only its thin Slider.
        return null;
    }

    // ---------- Public API ----------

    /// <summary>Load a scene by name.</summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoader] LoadScene called with a null or empty scene name.");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not available in the active build profile.");
            return;
        }
        RequestLoad(sceneName);
    }

    /// <summary>Load a scene by Build Settings index. Use this if you prefer not to hardcode names.</summary>
    public void LoadScene(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"[SceneLoader] Build index {buildIndex} is out of range " +
                            $"(0–{SceneManager.sceneCountInBuildSettings - 1}). Check Build Settings.");
            return;
        }
        LoadScene(SceneUtility.GetScenePathByBuildIndex(buildIndex));
    }

    /// <summary>Intentional reload, distinct from normal idempotent navigation.</summary>
    public void ReloadScene(string sceneName) => RequestLoad(sceneName, true);

    // Shared physical-load gate for the existing Bootstrap and fallback loaders.
    // Unity operations cannot be cancelled: keep the gate until the real operation completes.
    public static bool TryLoadScene(string sceneName, LoadSceneMode mode, out AsyncOperation operation, bool reload = false)
    {
        operation = null;
        if (Instance != null && Instance.IsLoading) return false;
        return BeginNativeLoad(sceneName, mode, out operation, reload);
    }

    private static bool BeginNativeLoad(string sceneName, LoadSceneMode mode, out AsyncOperation operation, bool reload)
    {
        operation = null;
        string path = ScenePath(sceneName);
        if (HasPendingLoad || (!reload && IsSceneLoaded(path))) return false;
        if (string.IsNullOrEmpty(path) || !Application.CanStreamedLevelBeLoaded(path))
        { Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not available in the active build profile."); return false; }
        if (mode == LoadSceneMode.Single) TutorialSystem.Instance?.PrepareForSceneTransition();
        int generation = ++loadGeneration;
        operation = SceneManager.LoadSceneAsync(path, mode);
        if (operation == null) return false;
        pendingOperation = operation; pendingScene = path;
        var captured = operation;
        operation.completed += _ =>
        {
            if (generation != loadGeneration || pendingOperation != captured) return;
            pendingOperation = null; pendingScene = null;
        };
        if (Debug.isDebugBuild) Debug.Log($"[SceneLoader] physicalLoad={path} mode={mode} frame={Time.frameCount} loadGeneration={generation}");
        return true;
    }

    private static string ScenePath(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return null;
        string value = sceneName.Replace('\\', '/');
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.Equals(value, path, System.StringComparison.OrdinalIgnoreCase) ||
                (value.IndexOf('/') < 0 && string.Equals(System.IO.Path.GetFileNameWithoutExtension(value), System.IO.Path.GetFileNameWithoutExtension(path), System.StringComparison.OrdinalIgnoreCase)))
                return path;
        }
        return value;
    }

    public static bool IsSceneLoaded(string sceneName)
    {
        string path = ScenePath(sceneName);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isLoaded && string.Equals(scene.path, path, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void RequestLoad(string sceneName, bool reload = false)
    {
        if (Debug.isDebugBuild)
            Debug.Log($"[SceneLoader] requestedScene={sceneName} currentActiveScene={SceneManager.GetActiveScene().name} currentlyLoading={isLoading || HasPendingLoad} pendingScene={pendingScene} requestSource={new System.Diagnostics.StackTrace(1, false)} frame={Time.frameCount} loadGeneration={loadGeneration}");
        if (isLoading || HasPendingLoad || (!reload && IsSceneLoaded(sceneName))) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        { Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not available in the active build profile."); return; }
        // Reserve before starting the iris/coroutine, including synchronous re-entry.
        isLoading = true;
        int generation = ++transitionGeneration;
        TutorialSystem.Instance?.PrepareForSceneTransition();
        StartCoroutine(LoadSceneRoutine(sceneName, reload, generation));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, bool reload, int generation)
    {
        IrisScaleToggle iris = null;
        if (IrisScaleToggle.IsMenuScene(SceneManager.GetActiveScene().name))
        {
            iris = IrisScaleToggle.Ensure();
            bool closed = false;
            iris.PlayCloseThenInvoke(() => closed = true);
            while (!closed) yield return null;
        }
        if (generation != transitionGeneration) yield break;
        if (spawnedLoadingCanvas == null) InitializeUI();
        if (spawnedLoadingCanvas != null) spawnedLoadingCanvas.SetActive(true);
        UpdateProgress(0f);
        if (iris != null) iris.SuspendForLoading();

        bool started = false;
        try { started = BeginNativeLoad(sceneName, LoadSceneMode.Single, out activeOperation, reload); }
        catch (System.Exception error) { Debug.LogException(error); }
        if (!started) { FinishTransition(generation); yield break; }
        var operation = activeOperation;
        operation.allowSceneActivation = false;
        operation.completed += _ => { if (this != null) FinishTransition(generation); };
        float elapsed = 0f;
        while (!operation.isDone)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > safetyTimeoutSeconds)
            {
                Debug.LogError($"[SceneLoader] Loading '{sceneName}' exceeded the {safetyTimeoutSeconds}s safety timeout. " +
                    $"progress={operation.progress}. Releasing activation; duplicate loads remain blocked until Unity completes the pending operation.");
                // A timeout is NOT cancellation. Leaving activation false blocks Unity's
                // queue; unlocking navigation here used to enqueue more stalled copies.
                operation.allowSceneActivation = true;
                RevealCurrentScene();
                yield break; // Completion owns the lock, even after this coroutine exits.
            }
            UpdateProgress(Mathf.Min(Mathf.Clamp01(operation.progress / 0.9f), Mathf.Clamp01(elapsed / Mathf.Max(0.001f, minimumLoadingTime))));
            if (operation.progress >= 0.9f && elapsed >= minimumLoadingTime) operation.allowSceneActivation = true;
            yield return null;
        }
        FinishTransition(generation);
    }

    private void FinishTransition(int generation)
    {
        if (generation != transitionGeneration || !isLoading) return;
        activeOperation = null;
        isLoading = false;
        RevealCurrentScene();
    }

    private void RevealCurrentScene()
    {
        if (spawnedLoadingCanvas != null) spawnedLoadingCanvas.SetActive(false);
        if (IrisScaleToggle.IsMenuScene(SceneManager.GetActiveScene().name)) IrisScaleToggle.Ensure().PlayOpen();
        else if (IrisScaleToggle.Instance != null) IrisScaleToggle.Instance.RevealImmediately();
    }

    private void OnDisable()
    {
        if (Instance != this || !isLoading) return;
        StopAllCoroutines();
        if (activeOperation != null && !activeOperation.isDone) activeOperation.allowSceneActivation = true;
        else { transitionGeneration++; isLoading = false; }
        if (spawnedLoadingCanvas != null) spawnedLoadingCanvas.SetActive(false);
        if (IrisScaleToggle.Instance != null) IrisScaleToggle.Instance.RevealImmediately();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (activeOperation != null && !activeOperation.isDone) activeOperation.allowSceneActivation = true;
        if (spawnedLoadingCanvas != null) Destroy(spawnedLoadingCanvas);
        Instance = null;
    }

    private void UpdateProgress(float progressValue)
    {
        if (progressBar != null)
        {
            progressBar.value = progressValue;
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(progressValue * 100f)}%";
        }
    }
}
