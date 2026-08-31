using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent singleton that manages all scene transitions.
/// - Survives scene loads via DontDestroyOnLoad.
/// - Spawns its own loading screen from a prefab reference (a Project asset,
///   never a scene object) so nothing can go "Missing" after an unload.
/// - Guards against overlapping loads and guarantees its lock is always released,
///   even if something fails mid-transition.
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

    [Tooltip("Hard ceiling in seconds. If a load exceeds this, we abort instead of hanging forever.")]
    [SerializeField] private float safetyTimeoutSeconds = 30f;

    private GameObject spawnedLoadingCanvas;
    private Slider progressBar;
    private TMPro.TextMeshProUGUI progressText;
    private bool isLoading = false;

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
        RequestLoad(() => SceneManager.LoadSceneAsync(sceneName), sceneName);
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
        RequestLoad(() => SceneManager.LoadSceneAsync(buildIndex), $"buildIndex {buildIndex}");
    }

    // ---------- Internal ----------

    private void RequestLoad(System.Func<AsyncOperation> beginLoad, string label)
    {
        if (isLoading)
        {
            // This is the mutex guard: rapid double-clicks or spammed triggers are ignored,
            // not queued and not crashed on.
            Debug.LogWarning($"[SceneLoader] Ignored request to load '{label}' — a transition is already in progress.");
            return;
        }

        StartCoroutine(LoadSceneRoutine(beginLoad, label));
    }

    private IEnumerator LoadSceneRoutine(System.Func<AsyncOperation> beginLoad, string label)
    {
        isLoading = true;

        // Defensive: if the canvas was ever destroyed unexpectedly, rebuild it before use.
        if (spawnedLoadingCanvas == null)
        {
            InitializeUI();
        }

        if (spawnedLoadingCanvas != null)
        {
            spawnedLoadingCanvas.SetActive(true);
        }

        UpdateProgress(0f);
        float elapsedTime = 0f;

        AsyncOperation asyncOperation = beginLoad.Invoke();

        // Guaranteed reset path #1: the load never started (bad name/index, not in Build Settings).
        if (asyncOperation == null)
        {
            Debug.LogError($"[SceneLoader] Failed to begin loading '{label}'. " +
                            "Confirm it is added under File > Build Settings > Scenes In Build.");
            if (spawnedLoadingCanvas != null) spawnedLoadingCanvas.SetActive(false);
            isLoading = false;
            yield break;
        }

        asyncOperation.allowSceneActivation = false;

        while (!asyncOperation.isDone)
        {
            elapsedTime += Time.unscaledDeltaTime;

            // Guaranteed reset path #2: safety timeout, in case progress ever stalls
            // for a reason unrelated to allowSceneActivation.
            if (elapsedTime > safetyTimeoutSeconds)
            {
                Debug.LogError($"[SceneLoader] Loading '{label}' exceeded the {safetyTimeoutSeconds}s safety " +
                                "timeout. Aborting to avoid a permanent lock. Check for scene load hangs.");
                break;
            }

            float normalizedProgress = Mathf.Clamp01(asyncOperation.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(elapsedTime / minimumLoadingTime);
            float finalDisplayProgress = Mathf.Min(normalizedProgress, timeProgress);

            UpdateProgress(finalDisplayProgress);

            if (asyncOperation.progress >= 0.9f && elapsedTime >= minimumLoadingTime)
            {
                asyncOperation.allowSceneActivation = true;
            }

            yield return null;
        }

        // Guaranteed reset path #3: normal completion.
        if (spawnedLoadingCanvas != null)
        {
            spawnedLoadingCanvas.SetActive(false);
        }

        isLoading = false;
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
