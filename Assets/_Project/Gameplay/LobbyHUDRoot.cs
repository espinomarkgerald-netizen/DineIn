using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Single persistent owner for every editable Lobby HUD branch.  The combined
/// Resources prefab contains flattened copies of the progress, controls, task,
/// and pause visual hierarchies so designers can edit the entire HUD at once.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class LobbyHUDRoot : MonoBehaviour
{
    private const string ResourcePath = "UI/LobbyHUD";

    public static LobbyHUDRoot Instance { get; private set; }

    [Header("Combined Editable HUD")]
    [SerializeField] private LobbyPauseMenuView pauseMenuView;

    [Header("Mobile Presentation")]
    [Tooltip("Size multiplier for the existing gameplay HUD canvases on mobile.")]
    [SerializeField, Min(0.1f)] private float mobileHudScale = 1.10f;
    [Tooltip("Additional mobile size multiplier for the CURRENT TASK panel and its text.")]
    [SerializeField, Min(0.1f)] private float mobileTaskPanelScale = 1.50f;
    [Tooltip("Visual and clickable size of the RestockScene Dry Room / Freezer button.")]
    [SerializeField, Min(0.1f)] private float mobileStorageButtonScale = 1.50f;

    public float MobileStorageButtonScale => mobileStorageButtonScale;

    public LobbyPauseMenuView PauseMenuView => pauseMenuView;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static LobbyHUDRoot EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        LobbyHUDRoot existing = FindFirstObjectByType<LobbyHUDRoot>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        LobbyHUDRoot prefab = Resources.Load<LobbyHUDRoot>(ResourcePath);
        return prefab != null ? Instantiate(prefab) : null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (pauseMenuView == null)
            pauseMenuView = GetComponentInChildren<LobbyPauseMenuView>(true);

        // The Task visuals are authored directly in this combined prefab.
        // Bind to those exact objects so the legacy standalone task prefab is
        // never instantiated and Inspector edits remain authoritative.
        LobbyHUDRedesign controls = GetComponentInChildren<LobbyHUDRedesign>(true);
        if (controls != null)
        {
            controls.UseCombinedAuthoredLayout();
            // Scale before binding so panel animations retain the mobile size.
            // Keep the actual tutorial target and all child text in the same rect.
            if (Application.isMobilePlatform &&
                controls.transform.Find("SafeArea/TaskMessage") is RectTransform taskPanel)
                taskPanel.localScale *= Mathf.Max(0.1f, mobileTaskPanelScale);
            PlayerTaskHUD.EnsureCombinedBinding(controls);
        }

        // The persistent pause controller activates this branch only in a lobby.
        // Keep it hidden until the presenter has bound its settings controls.
        if (pauseMenuView != null)
            pauseMenuView.gameObject.SetActive(false);
    }

    private void Start()
    {
        EnsurePauseController();
        RefreshScenePresentation();
        if (!Application.isMobilePlatform) return;

        // Let each presenter finish binding in Awake, then enlarge through its
        // existing canvas coordinate system. Edge anchors and safe areas survive.
        foreach (CanvasScaler scaler in GetComponentsInChildren<CanvasScaler>(true))
        {
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                scaler.referenceResolution /= Mathf.Max(0.1f, mobileHudScale);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void EnsurePauseController()
    {
        if (GetComponent<LobbyPauseMenu>() == null)
            gameObject.AddComponent<LobbyPauseMenu>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshScenePresentation();
    private void HandleActiveSceneChanged(Scene previous, Scene current) => RefreshScenePresentation();

    public void RefreshScenePresentation()
    {
        var controls = GetComponentInChildren<LobbyHUDRedesign>(true);
        if (controls != null)
        {
            controls.gameObject.SetActive(true);
            controls.RefreshVisibility();
        }
        GetComponentInChildren<PlayerTaskHUD>(true)?.RefreshSceneVisibility();
        GetComponent<LobbyPauseMenu>()?.RefreshSceneContext();
    }

    public LobbyPauseMenuView AcquirePauseMenuView()
    {
        if (pauseMenuView == null)
            pauseMenuView = GetComponentInChildren<LobbyPauseMenuView>(true);
        if (pauseMenuView != null)
            pauseMenuView.gameObject.SetActive(true);
        return pauseMenuView;
    }

    public void ReleasePauseMenuView(LobbyPauseMenuView view)
    {
        if (view != null && view == pauseMenuView)
            view.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(LobbyPauseMenuView configuredPauseMenuView)
    {
        pauseMenuView = configuredPauseMenuView;
    }
#endif
}
