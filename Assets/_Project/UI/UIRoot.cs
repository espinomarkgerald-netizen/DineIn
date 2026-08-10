using UnityEngine;

/// <summary>
/// Scene-local singleton. Each scene owns its own UIRoot — never persisted across loads.
/// When a new scene's UIRoot awakens, it always takes ownership so stale references from
/// unloaded scenes never block input or canvas access.
/// </summary>
public class UIRoot : MonoBehaviour
{
    public static UIRoot Instance { get; private set; }

    [Header("Assign in Scene")]
    public Canvas gameplayCanvas;
    public Camera gameplayCamera;

    private void Awake()
    {
        // Always let the newest UIRoot take over. UIRoot is purely scene-local,
        // so a pre-existing Instance means an old scene left a stale reference.
        // There is no DontDestroyOnLoad on UIRoot, so this only happens if the
        // same scene has two UIRoot components — which is a setup error.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[UIRoot] Duplicate detected. Previous owner: '{Instance.gameObject.scene.name}'. " +
                             $"New owner: '{gameObject.scene.name}'. Replacing instance.");
        }

        Instance = this;

        // Fallbacks if not assigned
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (gameplayCanvas == null) gameplayCanvas = FindFirstObjectByType<Canvas>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Returns the Canvas owned by the currently loaded scene's UIRoot.</summary>
    public static Canvas GameplayCanvasOrNull()
        => Instance != null ? Instance.gameplayCanvas : null;

    /// <summary>Returns the Camera owned by the currently loaded scene's UIRoot.</summary>
    public static Camera GameplayCameraOrNull()
        => Instance != null ? Instance.gameplayCamera : null;
}
