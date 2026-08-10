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
    [Tooltip("Optional overlay shown during the Bootstrap -> first scene transition. " +
             "Assign a Canvas root from the Bootstrap scene. It will be hidden automatically " +
             "once the first scene loads, or destroyed if it's scene-local.")]
    [SerializeField] private GameObject bootstrapLoadingScreen;

    public enum SceneAction { LoadSingle, LoadAdditive, Unload }

    [System.Serializable]
    public class SceneButtonBinding
    {
        public Button button;
        public string sceneName;
        public SceneAction action;
    }

    [Header("Menu <-> Gameplay")]
    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private string gameplaySceneName = "Multiplayer";

    [Tooltip("When a scene loads additively, disable root objects in all OTHER loaded scenes.")]
    [SerializeField] private bool deactivateOtherScenesOnLoad = true;

    [Header("Customization Sync")]
    [Tooltip("Push customization to Photon properties when Multiplayer loads (recommended).")]
    [SerializeField] private bool pushCustomizationOnGameplayLoad = true;

    // Guard against double-firing while a load/unload is already in progress.
    private bool isLoading;

    // When a LoadSingle is requested but the scene is still mid-unload, we defer
    // the actual load until OnSceneUnloaded fires.
    private string pendingLoadAfterUnload;

    // Loading screen overlay owned by SceneLoadWithScreen — hidden after the scene activates.
    private GameObject _pendingOverlay;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded   += OnSceneLoaded;
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
            SceneManager.sceneLoaded   -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }
    }

    private void Start()
    {
        // Bootstrap scene should be buildIndex 0.
        // Use the async guarded path so isLoading is set correctly and
        // OnSceneLoaded fires normally — which resets isLoading and clears
        // any _pendingOverlay. Never use synchronous LoadScene here:
        // in Unity 6 it is deprecated and, more importantly, it bypasses
        // isLoading entirely, which can leave the loading flag in an
        // inconsistent state if a callback fires during the same frame.
        if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            Debug.Log("[SceneManagerUI] Bootstrap -> Loading: " + firstSceneToLoad);

            // Show the Bootstrap loading overlay if one is assigned, then use the
            // guarded async path so OnSceneLoaded handles the hide step.
            if (bootstrapLoadingScreen != null)
            {
                bootstrapLoadingScreen.SetActive(true);
                _pendingOverlay = bootstrapLoadingScreen;
            }

            LoadSingleSafe(firstSceneToLoad);
        }
    }

    // -------------------------------------------------------------------------
    // Button registration
    // -------------------------------------------------------------------------

    /// <summary>Registers a button with a scene name and action. Safe to call multiple times.</summary>
    public void RegisterButton(Button button, string sceneName, SceneAction action)
    {
        if (button == null || string.IsNullOrEmpty(sceneName)) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => HandleScene(sceneName, action));
    }

    // -------------------------------------------------------------------------
    // Core scene dispatch
    // -------------------------------------------------------------------------

    private void HandleScene(string sceneName, SceneAction action)
    {
        if (isLoading)
        {
            Debug.LogWarning($"[SceneManagerUI] Load already in progress — ignoring request for '{sceneName}'.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneManagerUI] Scene '{sceneName}' is NOT in the Build Profile!");
            return;
        }

        // ------------------------------------------------------------------
        // Photon multiplayer path
        // ------------------------------------------------------------------
        if (sceneName == gameplaySceneName && PhotonNetwork.IsConnectedAndReady)
        {
            if (PhotonNetwork.InRoom)
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    Debug.LogWarning("[SceneManagerUI] Not MasterClient — Master should call PhotonNetwork.LoadLevel.");
                    return;
                }

                Debug.Log("[SceneManagerUI] Multiplayer load via PhotonNetwork.LoadLevel: " + gameplaySceneName);
                PhotonNetwork.LoadLevel(gameplaySceneName);
                return;
            }

            Debug.LogWarning("[SceneManagerUI] Connected to Photon but NOT in a room — join/create first.");
            return;
        }

        // ------------------------------------------------------------------
        // Offline / menu scene loads
        // ------------------------------------------------------------------
        switch (action)
        {
            case SceneAction.LoadSingle:
                LoadSingleSafe(sceneName);
                break;

            case SceneAction.LoadAdditive:
                LoadAdditiveSafe(sceneName);
                break;

            case SceneAction.Unload:
                UnloadSafe(sceneName);
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Public API used by SceneLoadWithScreen
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by SceneLoadWithScreen to trigger a load while ensuring there is
    /// only one active async operation at a time. The overlay root is hidden
    /// once the new scene has fully loaded.
    /// </summary>
    public void LoadSceneWithScreen(string sceneName, GameObject overlayRoot)
    {
        if (isLoading)
        {
            // Already loading — hide the overlay that was prematurely shown.
            if (overlayRoot != null) overlayRoot.SetActive(false);
            Debug.LogWarning($"[SceneManagerUI] LoadSceneWithScreen ignored — already loading.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            if (overlayRoot != null) overlayRoot.SetActive(false);
            Debug.LogError($"[SceneManagerUI] Scene '{sceneName}' is NOT in the Build Profile!");
            return;
        }

        _pendingOverlay = overlayRoot;
        LoadSingleSafe(sceneName);
    }

    // -------------------------------------------------------------------------
    // Load helpers — each sets isLoading so the button is guarded
    // -------------------------------------------------------------------------

    /// <summary>
    /// Triggers a single-mode load of the given scene through the normal
    /// isLoading guard. Safe to call from external components (e.g. TutorialResetOnLaunch).
    /// </summary>
    public void LoadSingle(string sceneName)
    {
        if (isLoading)
        {
            Debug.LogWarning($"[SceneManagerUI] LoadSingle ignored — already loading '{sceneName}'.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneManagerUI] Scene '{sceneName}' is NOT in the Build Profile!");
            return;
        }

        LoadSingleSafe(sceneName);
    }

    private void LoadSingleSafe(string sceneName)
    {
        isLoading = true;
        Debug.Log($"[SceneManagerUI] LoadSingle -> '{sceneName}'");
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        // isLoading is cleared in OnSceneLoaded.
    }

    private void LoadAdditiveSafe(string sceneName)
    {
        Scene existing = SceneManager.GetSceneByName(sceneName);

        if (existing.isLoaded)
        {
            // Scene is still loaded (or mid-unload). Unload it first, then reload.
            // Store the pending target so OnSceneUnloaded can re-fire the load.
            Debug.Log($"[SceneManagerUI] '{sceneName}' still loaded — unloading before re-adding.");
            pendingLoadAfterUnload = sceneName;
            isLoading = true;
            SceneManager.UnloadSceneAsync(sceneName);
            // Flow continues in OnSceneUnloaded.
            return;
        }

        isLoading = true;
        Debug.Log($"[SceneManagerUI] LoadAdditive -> '{sceneName}'");
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        // isLoading is cleared in OnSceneLoaded.
    }

    private void UnloadSafe(string sceneName)
    {
        Scene s = SceneManager.GetSceneByName(sceneName);
        if (!s.isLoaded)
        {
            Debug.LogWarning($"[SceneManagerUI] Tried to unload '{sceneName}' but it is not loaded.");
            return;
        }

        isLoading = true;
        SceneManager.UnloadSceneAsync(sceneName);
        // isLoading is cleared in OnSceneUnloaded.
    }

    // -------------------------------------------------------------------------
    // Scene events
    // -------------------------------------------------------------------------

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[SceneManagerUI] OnSceneLoaded: '{scene.name}' ({mode})");

        isLoading = false;
        pendingLoadAfterUnload = null;

        // Hide any loading screen overlay that was shown by SceneLoadWithScreen
        // or by the Bootstrap startup path.
        // NOTE: _pendingOverlay may reference a scene-local object that was already
        // destroyed by LoadSceneMode.Single. Unity's == operator returns true for
        // destroyed objects compared to null, so we must use ReferenceEquals(null)
        // to check for a truly unassigned reference, and catch the case where the
        // object exists in C# but is destroyed on the Unity side.
        if (_pendingOverlay != null)
        {
            try { _pendingOverlay.SetActive(false); }
            catch (System.Exception) { /* object was destroyed mid-load */ }
            _pendingOverlay = null;
        }

        // Make the newly loaded scene the active scene.
        SceneManager.SetActiveScene(scene);

        // When loading additively, hide roots in every other scene so only the
        // new scene is visible.
        if (mode == LoadSceneMode.Additive && deactivateOtherScenesOnLoad)
            DeactivateAllOtherScenesExcept(scene);

        // Gameplay-specific extras.
        if (scene.name == gameplaySceneName)
        {
            if (pushCustomizationOnGameplayLoad
                && PlayfabManager.Instance != null
                && PlayfabManager.Instance.IsLoggedIn)
            {
                PlayfabManager.Instance.PushCustomizationToPhoton();
                Debug.Log("[SceneManagerUI] Pushed customization to Photon.");
            }
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        Debug.Log($"[SceneManagerUI] OnSceneUnloaded: '{scene.name}'");

        // If we unloaded a scene in order to reload it (LoadAdditiveSafe race-fix),
        // now do the actual load.
        if (!string.IsNullOrEmpty(pendingLoadAfterUnload) && pendingLoadAfterUnload == scene.name)
        {
            string target = pendingLoadAfterUnload;
            pendingLoadAfterUnload = null;
            // isLoading stays true until OnSceneLoaded fires for the new load.
            Debug.Log($"[SceneManagerUI] Re-loading '{target}' after unload completed.");
            SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);
            return;
        }

        isLoading = false;

        // Ensure active scene is valid after unload.
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded)
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded)
                {
                    SceneManager.SetActiveScene(s);
                    if (deactivateOtherScenesOnLoad)
                        DeactivateAllOtherScenesExcept(s);
                    break;
                }
            }
        }

        // If gameplay unloaded, make sure menu roots are visible.
        if (scene.name == gameplaySceneName)
        {
            SetSceneRootObjectsActive(menuSceneName, true);
            Debug.Log("[SceneManagerUI] Gameplay unloaded — menu restored.");
        }
    }

    // -------------------------------------------------------------------------
    // Return to menu
    // -------------------------------------------------------------------------

    /// <summary>Call this from any Exit / Back button.</summary>
    public void ReturnToMenu()
    {
        if (isLoading)
        {
            Debug.LogWarning("[SceneManagerUI] ReturnToMenu ignored — load already in progress.");
            return;
        }

        // Multiplayer: leave room first; OnLeftRoom will handle the scene load.
        if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InRoom)
        {
            Debug.Log("[SceneManagerUI] ReturnToMenu -> Leaving Photon room first.");
            PhotonNetwork.LeaveRoom();
            return;
        }

        // Offline additive: unload gameplay scene; OnSceneUnloaded restores menu.
        if (SceneManager.GetSceneByName(gameplaySceneName).isLoaded)
        {
            UnloadSafe(gameplaySceneName);
            return;
        }

        // Fallback: just make sure menu roots are visible.
        SetSceneRootObjectsActive(menuSceneName, true);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[SceneManagerUI] OnLeftRoom -> Loading menu scene.");
        isLoading = false; // clear any leftover guard before triggering new load
        LoadSingleSafe(menuSceneName);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void DeactivateAllOtherScenesExcept(Scene keepScene)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.IsValid() || !s.isLoaded) continue;

            bool keep = (s == keepScene);
            GameObject[] roots = s.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r] == gameObject) continue; // never disable the persistent manager
                roots[r].SetActive(keep);
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
}
