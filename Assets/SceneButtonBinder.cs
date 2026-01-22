using UnityEngine;
using UnityEngine.UI;

public class SceneButtonBinder : MonoBehaviour
{
    [System.Serializable]
    public class ButtonBinding
    {
        public Button button;
        public string sceneName;
        public SceneManagerUI.SceneAction action;
    }

    [SerializeField] private ButtonBinding[] buttons;

    private void Start()
    {
        // Use the Singleton Instance instead of searching
        if (SceneManagerUI.Instance == null)
        {
            Debug.LogError("SceneManagerUI not found! Make sure you started from the Bootstrap scene.");
            return;
        }

        foreach (var binding in buttons)
        {
            SceneManagerUI.Instance.RegisterButton(
                binding.button,
                binding.sceneName,
                binding.action
            );
        }
    }
}