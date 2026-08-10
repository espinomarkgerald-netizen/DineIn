using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds UI buttons to scene-load actions via SceneManagerUI.
/// Registers on Start and re-registers on OnEnable so buttons stay
/// live after every scene activation, including returns from other scenes.
/// </summary>
public class SceneButtonBinder : MonoBehaviour
{
    [System.Serializable]
    public class ButtonBinding
    {
        public Button button;
        public string sceneName;
        public SceneManagerUI.SceneAction action; // kept for inspector compatibility
    }

    [SerializeField] private ButtonBinding[] buttons;

    [Tooltip("If ON: any scene loaded will be loaded as LoadSingle (and becomes active automatically).")]
    [SerializeField] private bool forceLoadSingleToMakeActive = true;

    private void Start()
    {
        RegisterAll();
    }

    private void OnEnable()
    {
        // Re-register after every activation in case SceneManagerUI cleared its
        // listeners or the object was disabled/enabled during scene transitions.
        RegisterAll();
    }

    private void RegisterAll()
    {
        if (SceneManagerUI.Instance == null)
        {
            Debug.LogError("[SceneButtonBinder] SceneManagerUI not found! Make sure you started from the Bootstrap scene.");
            return;
        }

        foreach (var binding in buttons)
        {
            if (binding.button == null) continue;

            var actionToUse = forceLoadSingleToMakeActive
                ? SceneManagerUI.SceneAction.LoadSingle
                : binding.action;

            SceneManagerUI.Instance.RegisterButton(
                binding.button,
                binding.sceneName,
                actionToUse
            );
        }
    }
}
