using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modular, reusable trigger. Attach to any UI Button or any GameObject with a
/// Collider/Collider2D (set as trigger) in any scene. Passes its target scene
/// dynamically to the persistent SceneLoader — no hardcoded references between scripts.
/// </summary>
public class SceneTrigger : MonoBehaviour
{
    private enum TargetMode { SceneName, BuildIndex }

    [Header("Target Settings")]
    [Tooltip("Choose whether to target the scene by name or by its Build Settings index.")]
    [SerializeField] private TargetMode targetMode = TargetMode.SceneName;

    [Tooltip("Used when Target Mode = Scene Name.")]
    [SerializeField] private string targetSceneName;

    [Tooltip("Used when Target Mode = Build Index.")]
    [SerializeField] private int targetBuildIndex;

    [Header("Trigger Options")]
    [Tooltip("Automatically binds to a UI Button component on this GameObject, if one exists.")]
    [SerializeField] private bool autoBindButton = true;

    [Tooltip("If true, a 3D/2D Collider marked 'Is Trigger' on this object will also fire the transition " +
             "when the player enters it. Leave off for pure UI buttons.")]
    [SerializeField] private bool useAsWorldTrigger = false;

    [Tooltip("Only used if 'Use As World Trigger' is on — restricts which objects can activate it.")]
    [SerializeField] private string requiredTag = "Player";

    private Button uiButton;

    private void Awake()
    {
        if (autoBindButton)
        {
            uiButton = GetComponent<Button>();
            if (uiButton != null)
            {
                uiButton.onClick.AddListener(TriggerSceneLoad);
            }
        }
    }

    private void OnDestroy()
    {
        // Prevents a stale listener from firing into a destroyed object across scene reloads.
        if (uiButton != null)
        {
            uiButton.onClick.RemoveListener(TriggerSceneLoad);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (useAsWorldTrigger && other.CompareTag(requiredTag))
        {
            TriggerSceneLoad();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (useAsWorldTrigger && other.CompareTag(requiredTag))
        {
            TriggerSceneLoad();
        }
    }

    /// <summary>Public so it can also be wired manually to a Button's OnClick() in the Inspector.</summary>
    public void TriggerSceneLoad()
    {
        if (SceneLoader.Instance == null)
        {
            Debug.LogError($"[SceneTrigger] No SceneLoader instance found in the scene. " +
                            "Make sure a persistent SceneLoader exists (it should be placed in your " +
                            "very first/bootstrap scene so it exists before any trigger fires).");
            return;
        }

        if (targetMode == TargetMode.SceneName)
        {
            SceneLoader.Instance.LoadScene(targetSceneName);
        }
        else
        {
            SceneLoader.Instance.LoadScene(targetBuildIndex);
        }
    }
}