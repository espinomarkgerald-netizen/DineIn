using UnityEngine;
using UnityEngine.UI;

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
        if (SceneManagerUI.Instance == null)
        {
            Debug.LogError("SceneManagerUI not found! Make sure you started from the Bootstrap scene.");
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
