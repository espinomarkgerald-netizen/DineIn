using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class SceneManagerUI : MonoBehaviour
{
    public static SceneManagerUI Instance { get; private set; }

    [Header("Bootstrap Config")]
    [SerializeField] private string firstSceneToLoad = "MainMenu"; // Type your real scene name here

    public enum SceneAction { LoadSingle, LoadAdditive, Unload }

    [System.Serializable]
    public class SceneButtonBinding
    {
        public Button button;
        public string sceneName;
        public SceneAction action;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Only the Bootstrap scene version of this script should trigger this
        // Check if we are currently in the Bootstrap scene to auto-load the next one
        if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            Debug.Log("Bootstrap successful. Loading: " + firstSceneToLoad);
            SceneManager.LoadScene(firstSceneToLoad);
        }
    }

    public void RegisterButton(Button button, string sceneName, SceneAction action)
    {
        if (button == null || string.IsNullOrEmpty(sceneName)) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => HandleScene(sceneName, action));
    }

    private void HandleScene(string sceneName, SceneAction action)
    {
        switch (action)
        {
            case SceneAction.LoadSingle:
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                break;
            case SceneAction.LoadAdditive:
                if (!SceneManager.GetSceneByName(sceneName).isLoaded)
                    SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                break;
            case SceneAction.Unload:
                if (SceneManager.GetSceneByName(sceneName).isLoaded)
                    SceneManager.UnloadSceneAsync(sceneName);
                break;
        }
    }
}