using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Photon.Pun;
using Photon.Realtime;

public class SceneManagerUI : MonoBehaviourPunCallbacks
{
    public static SceneManagerUI Instance { get; private set; }

    [Header("Bootstrap Config")]
    [SerializeField] private string firstSceneToLoad = "MainMenu";

    public enum SceneAction { LoadSingle, LoadAdditive, Unload }

    [System.Serializable]
    public class SceneButtonBinding
    {
        public Button button;
        public string sceneName;
        public SceneAction action;
    }

    [Header("Safety")]
    [SerializeField] private bool protectMenuFromSingleLoad = true;

    [Header("Menu <-> Gameplay")]
    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private string gameplaySceneName = "CoreGameplay";

    [Tooltip("When gameplay loads additively (offline only), disable all root objects in the Menu scene (keeps scene loaded but hides UI).")]
    [SerializeField] private bool hideMenuWhenGameplayLoaded = true;

    [Tooltip("Also disable these extra objects even if they're not in the menu scene (optional).")]
    [SerializeField] private GameObject[] extraMenuObjectsToDisable;

    [Tooltip("When a scene loads, make it the Active Scene.")]
    [SerializeField] private bool setRecentlyLoadedAsActiveScene = true;

    [Tooltip("When a scene loads, disable root objects in all OTHER loaded scenes.")]
    [SerializeField] private bool deactivateOtherScenesOnLoad = true;

    [Header("Customization Sync")]
    [Tooltip("Push customization to Photon properties when CoreGameplay loads (recommended).")]
    [SerializeField] private bool pushCustomizationOnGameplayLoad = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }
    }

    private void Start()
    {
        // Bootstrap scene should be buildIndex 0
        if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            Debug.Log("Bootstrap successful. Loading: " + firstSceneToLoad);
            SceneManager.LoadScene(firstSceneToLoad, LoadSceneMode.Single);
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
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' is NOT in the Build Profile!");
            return;
        }

        // -----------------------------
        // IMPORTANT: Photon Multiplayer Gameplay Load
        // -----------------------------
        if (sceneName == gameplaySceneName && PhotonNetwork.IsConnectedAndReady)
        {
            // If we are in a room, we MUST use PhotonNetwork.LoadLevel (synced)
            if (PhotonNetwork.InRoom)
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    Debug.LogWarning("[SceneManagerUI] Tried to load gameplay but not MasterClient. Master should call PhotonNetwork.LoadLevel.");
                    return;
                }

                Debug.Log("[SceneManagerUI] Multiplayer gameplay load via PhotonNetwork.LoadLevel: " + gameplaySceneName);
                PhotonNetwork.LoadLevel(gameplaySceneName);
                return;
            }

            // If connected but not in a room, do NOT load gameplay here (join/create first)
            Debug.LogWarning("[SceneManagerUI] Connected to Photon but NOT in a room. Join/Create a room before loading CoreGameplay.");
            return;
        }

        // -----------------------------
        // Offline / Menu scene loads (Unity)
        // -----------------------------
        if (protectMenuFromSingleLoad &&
            action == SceneAction.LoadSingle &&
            SceneManager.GetSceneByName(menuSceneName).isLoaded &&
            sceneName != menuSceneName)
        {
            Debug.LogWarning($"Prevented LoadSingle of '{sceneName}' while '{menuSceneName}' is loaded. Loading Additive instead.");
            action = SceneAction.LoadAdditive;
        }

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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ✅ Make recently loaded scene active
        if (setRecentlyLoadedAsActiveScene)
            SceneManager.SetActiveScene(scene);

        // ✅ Deactivate all other scenes (roots) so only this one is "active/visible"
        if (deactivateOtherScenesOnLoad)
            DeactivateAllOtherScenesExcept(scene);

        // Gameplay-specific extras
        if (scene.name == gameplaySceneName)
        {
            if (pushCustomizationOnGameplayLoad && PlayfabManager.Instance != null && PlayfabManager.Instance.IsLoggedIn)
            {
                PlayfabManager.Instance.PushCustomizationToPhoton();
                Debug.Log("✅ Pushed customization to Photon on gameplay load.");
            }

            // (Optional) Keep your old menu hide behavior if you want.
            // Note: deactivateOtherScenesOnLoad already hides menu roots,
            // but this keeps your extraMenuObjectsToDisable behavior too.
            if (hideMenuWhenGameplayLoaded && mode == LoadSceneMode.Additive)
            {
                SetSceneRootObjectsActive(menuSceneName, false);

                if (extraMenuObjectsToDisable != null)
                {
                    for (int i = 0; i < extraMenuObjectsToDisable.Length; i++)
                    {
                        if (extraMenuObjectsToDisable[i] != null)
                            extraMenuObjectsToDisable[i].SetActive(false);
                    }
                }
            }

            Debug.Log("✅ Gameplay loaded.");
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        // If something unloads, ensure active scene is valid and visible
        // (Pick the last loaded currently active, else fallback to any loaded)
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded)
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded)
                {
                    SceneManager.SetActiveScene(s);
                    if (deactivateOtherScenesOnLoad)
                        DeactivateAllOtherScenesExcept(s);
                    break;
                }
            }
        }

        // Your existing gameplay-unload behavior (optional)
        if (scene.name == gameplaySceneName)
        {
            if (hideMenuWhenGameplayLoaded)
            {
                SetSceneRootObjectsActive(menuSceneName, true);

                if (extraMenuObjectsToDisable != null)
                {
                    for (int i = 0; i < extraMenuObjectsToDisable.Length; i++)
                    {
                        if (extraMenuObjectsToDisable[i] != null)
                            extraMenuObjectsToDisable[i].SetActive(true);
                    }
                }
            }

            Debug.Log("✅ Gameplay unloaded: menu shown.");
        }
    }

    private void DeactivateAllOtherScenesExcept(Scene keepScene)
    {
        // KeepScene roots ON, all others OFF
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.IsValid() || !s.isLoaded) continue;

            bool active = (s == keepScene);

            GameObject[] roots = s.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                // don't disable this manager object
                if (roots[r] == gameObject) continue;

                roots[r].SetActive(active);
            }
        }
    }

    private void SetSceneRootObjectsActive(string sceneName, bool active)
    {
        Scene s = SceneManager.GetSceneByName(sceneName);
        if (!s.IsValid() || !s.isLoaded) return;

        GameObject[] roots = s.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == gameObject) continue;
            roots[i].SetActive(active);
        }
    }

    // Call this from your "Back" button
    public void ReturnToMenu()
    {
        // Multiplayer: leave room first, then load menu normally
        if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InRoom)
        {
            Debug.Log("[SceneManagerUI] ReturnToMenu -> Leaving Photon room first...");
            PhotonNetwork.LeaveRoom();
            return; // OnLeftRoom callback will load menu
        }

        // Offline: unload gameplay if additive, else ensure menu visible
        if (SceneManager.GetSceneByName(gameplaySceneName).isLoaded)
            SceneManager.UnloadSceneAsync(gameplaySceneName);
        else
            SetSceneRootObjectsActive(menuSceneName, true);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[SceneManagerUI] OnLeftRoom -> Loading menu scene");
        SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }
}
