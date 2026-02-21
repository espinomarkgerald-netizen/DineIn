using UnityEngine;

public class UIRoot : MonoBehaviour
{
    public static UIRoot Instance { get; private set; }

    [Header("Assign in Scene")]
    public Canvas gameplayCanvas;
    public Camera gameplayCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Fallbacks if not assigned
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (gameplayCanvas == null) gameplayCanvas = FindFirstObjectByType<Canvas>();
    }

    // ✅ The exact methods your CustomerGroup expects
    public static Canvas GameplayCanvasOrNull()
        => Instance != null ? Instance.gameplayCanvas : null;

    public static Camera GameplayCameraOrNull()
        => Instance != null ? Instance.gameplayCamera : null;
}
