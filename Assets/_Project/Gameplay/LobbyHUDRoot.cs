using UnityEngine;

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
            PlayerTaskHUD.EnsureCombinedBinding(controls);
        }

        // The scene-owned LobbyPauseMenu controller activates and wires this
        // branch when Lobby1 is loaded.  Keeping it inactive before then stops
        // the pause button leaking into loading/menu scenes.
        if (pauseMenuView != null)
            pauseMenuView.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
