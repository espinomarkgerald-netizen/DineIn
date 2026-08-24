using UnityEngine;
using UnityEngine.SceneManagement;
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
        foreach (var binding in buttons)
        {
            if (binding.button == null || string.IsNullOrWhiteSpace(binding.sceneName))
                continue;

            var actionToUse = forceLoadSingleToMakeActive
                ? SceneManagerUI.SceneAction.LoadSingle
                : binding.action;

            if (SceneManagerUI.Instance != null)
            {
                SceneManagerUI.Instance.RegisterButton(
                    binding.button,
                    binding.sceneName,
                    actionToUse);
                continue;
            }

            // Direct scene play and standalone builds may not have the Bootstrap
            // manager. Keep the button live with the same scene semantics instead
            // of raising a gameplay-blocking error.
            ButtonBinding capturedBinding = binding;
            binding.button.onClick.RemoveAllListeners();
            binding.button.onClick.AddListener(() => LoadWithoutBootstrap(
                capturedBinding.sceneName,
                actionToUse));
        }
    }

    private static void LoadWithoutBootstrap(
        string sceneName,
        SceneManagerUI.SceneAction action)
    {
        RestockFlowCoordinator restock = RestockFlowCoordinator.Instance;
        if (sceneName == "Lobby1" && restock != null && restock.IsRestockRoomOpen)
        {
            restock.ExitRestockRoom();
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
            return;

        switch (action)
        {
            case SceneManagerUI.SceneAction.LoadAdditive:
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                break;
            case SceneManagerUI.SceneAction.Unload:
                Scene loaded = SceneManager.GetSceneByName(sceneName);
                if (loaded.isLoaded)
                    SceneManager.UnloadSceneAsync(loaded);
                break;
            default:
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                break;
        }
    }
}
