using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstraps movement and camera binding for every spawned player instance.
///
/// Camera resolution order (scoped to the active scene only):
///   1. Camera injected directly by RoomManager via InjectCamera()
///   2. Enabled camera tagged "RoomCamera"
///   3. Enabled camera tagged "MainCamera"
///   4. Any other enabled camera in the active scene
///   5. Any camera in the active scene (inactive last resort)
///
/// Local player (singleplayer OR photonView.IsMine):
///   - Player-owned camera child is disabled.
///   - Movement is enabled and the scene camera is bound to PlayerMovement.
///
/// Remote multiplayer player (photonView.IsMine == false):
///   - Movement is disabled; position is driven by NetworkPlayerMovementSync.
/// </summary>
public class PlayerSetup : MonoBehaviourPun
{
    [Tooltip("Optional child camera on this player prefab. Always disabled — the scene camera is used instead.")]
    public GameObject cameraGameObject;

    [Tooltip("PlayerMovement component on this prefab. Auto-resolved if left empty.")]
    public PlayerMovement movement;

    // Camera injected directly by RoomManager immediately after Instantiate.
    // When set, the coroutine scan is skipped on the first pass.
    private Camera injectedCamera;

    // Flag so InjectCamera() can be called before Start() without the coroutine overwriting it.
    private bool cameraInjectedBySpawner;

    // Seconds to wait before the final camera-resolve fallback pass.
    private const float CameraRefreshDelay = 0.15f;

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        // Disable the prefab-owned camera for every instance, always.
        if (cameraGameObject != null)
        {
            cameraGameObject.SetActive(false);
            Debug.Log($"[PlayerSetup] Disabled prefab-owned camera child '{cameraGameObject.name}'.");
        }

        bool isMultiplayer = PhotonNetwork.IsConnected;

        if (isMultiplayer && !photonView.IsMine)
        {
            // Remote player — all local-input-driven components are disabled here.
            // Animation and particles are driven by NetworkPlayerMovementSync instead.
            if (movement != null)
                movement.enabled = false;

            // Explicitly disable the local-only animation driver.
            PlayerAnimationController animCtrl = GetComponent<PlayerAnimationController>();
            if (animCtrl != null)
                animCtrl.enabled = false;

            // Remote players also need a camera reference so NameTagBillboard can face it.
            // Start a coroutine to resolve it — the scene camera may not be ready yet at Start().
            StartCoroutine(BindNameTagCameraRoutine());

            Debug.Log("[PlayerSetup] Remote player — local input and animation controller disabled.");
            return;
        }

        Debug.Log($"[PlayerSetup] Local player starting setup | Multiplayer={isMultiplayer} | " +
                  $"InjectedCamera={injectedCamera?.name ?? "none"}");

        SetupLocalPlayer();
    }

    // -------------------------------------------------------------------------
    // Public API — called by RoomManager right after PhotonNetwork.Instantiate
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by <see cref="RoomManager"/> immediately after spawning to push the
    /// exact scene camera reference before any coroutine timing can cause a miss.
    /// Safe to call before <see cref="Start"/> runs.
    /// </summary>
    public void InjectCamera(Camera cam)
    {
        if (cam == null)
        {
            Debug.LogWarning("[PlayerSetup] InjectCamera called with a null camera.");
            return;
        }

        injectedCamera = cam;
        cameraInjectedBySpawner = true;

        Debug.Log($"[PlayerSetup] InjectCamera() received '{cam.name}' | " +
                  $"Active={cam.isActiveAndEnabled} | " +
                  $"TargetDisplay={cam.targetDisplay} | " +
                  $"AudioListener={cam.GetComponent<AudioListener>() != null}");

        // If Start already ran, bind immediately.
        if (movement != null && movement.enabled)
            BindCamera(cam);
    }

    // -------------------------------------------------------------------------
    // Local player setup
    // -------------------------------------------------------------------------

    private void SetupLocalPlayer()
    {
        if (movement == null)
        {
            Debug.LogError("[PlayerSetup] PlayerMovement component is missing on this GameObject.");
            return;
        }

        movement.enabled = true;
        movement.SetPlayerControlled(true);

        // Use the injected camera immediately if the spawner already provided one.
        if (cameraInjectedBySpawner && injectedCamera != null)
        {
            BindCamera(injectedCamera);
        }

        // Run the coroutine scan regardless so we always recover if the injection was missed.
        StartCoroutine(BindSceneCameraRoutine());
    }

    /// <summary>
    /// Three-pass coroutine to resolve and assign the scene camera.
    /// If a camera was already injected by the spawner, the passes just confirm
    /// and log the current binding rather than overwriting with something worse.
    /// </summary>
    private IEnumerator BindSceneCameraRoutine()
    {
        // Pass 0 — this frame
        if (!cameraInjectedBySpawner)
            AssignBestSceneCamera();
        else
            LogCurrentBinding("Pass 0 (injected — no scan needed)");

        yield return null;

        // Pass 1 — next frame (catches same-frame camera state changes)
        AssignBestSceneCamera();

        yield return new WaitForSeconds(CameraRefreshDelay);

        // Pass 2 — after delay (catches deferred scene-manager camera swaps)
        AssignBestSceneCamera();
    }

    private void AssignBestSceneCamera()
    {
        if (movement == null) return;

        Camera cam = FindActiveSceneCamera();

        if (cam == null)
        {
            Debug.LogWarning("[PlayerSetup] No camera found in the active scene on this pass.");
            return;
        }

        BindCamera(cam);
    }

    private void BindCamera(Camera cam)
    {
        movement.SetCamera(cam);

        // Keep the nametag billboard in sync with the same camera.
        NameTagBillboard billboard = GetComponentInChildren<NameTagBillboard>(true);
        if (billboard != null)
            billboard.SetCamera(cam);

        Debug.Log($"[PlayerSetup] Bound camera '{cam.name}' to local player | " +
                  $"Tag={cam.tag} | " +
                  $"Active={cam.isActiveAndEnabled} | " +
                  $"TargetDisplay={cam.targetDisplay} | " +
                  $"AudioListener={cam.GetComponent<AudioListener>() != null} | " +
                  $"Scene={cam.gameObject.scene.name}");
    }

    /// <summary>
    /// Resolves the scene camera and pushes it to the nametag billboard for remote players.
    /// Retries across three passes to handle deferred scene-camera setup.
    /// </summary>
    private IEnumerator BindNameTagCameraRoutine()
    {
        NameTagBillboard billboard = GetComponentInChildren<NameTagBillboard>(true);
        if (billboard == null) yield break;

        // Pass 0 — this frame
        Camera cam = FindActiveSceneCamera();
        if (cam != null) billboard.SetCamera(cam);

        yield return null;

        // Pass 1 — next frame
        cam = FindActiveSceneCamera();
        if (cam != null) billboard.SetCamera(cam);

        yield return new WaitForSeconds(CameraRefreshDelay);

        // Pass 2 — after delay
        cam = FindActiveSceneCamera();
        if (cam != null) billboard.SetCamera(cam);
    }

    private void LogCurrentBinding(string passLabel)
    {
        if (movement == null) return;
        Debug.Log($"[PlayerSetup] {passLabel} — current binding is '{injectedCamera?.name ?? "none"}'.");
    }

    // -------------------------------------------------------------------------
    // Camera resolution — active-scene-scoped, priority ordered
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the best camera that belongs to <see cref="SceneManager.GetActiveScene()"/>.
    /// Cameras in prefab stages, DontDestroyOnLoad, or other additively loaded scenes
    /// are excluded by comparing <c>camera.gameObject.scene</c> to the active scene.
    ///
    /// Priority:
    ///   1. Enabled "RoomCamera"
    ///   2. Enabled "MainCamera"
    ///   3. Any other enabled camera in the active scene
    ///   4. Any camera in the active scene (inactive last resort)
    /// </summary>
    public static Camera FindActiveSceneCamera()
    {
        Scene active = SceneManager.GetActiveScene();

        Camera[] all = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        Camera roomCamActive = null;
        Camera mainCamActive = null;
        Camera anyActive     = null;
        Camera anyInScene    = null;

        foreach (Camera cam in all)
        {
            if (cam == null || cam.gameObject.scene != active)
                continue;

            bool isActive = cam.isActiveAndEnabled;

            if (isActive)
            {
                if (cam.CompareTag("RoomCamera") && roomCamActive == null)
                    roomCamActive = cam;

                if (cam.CompareTag("MainCamera") && mainCamActive == null)
                    mainCamActive = cam;

                if (anyActive == null)
                    anyActive = cam;
            }

            if (anyInScene == null)
                anyInScene = cam;
        }

        return roomCamActive   // 1. Active RoomCamera
            ?? mainCamActive   // 2. Active MainCamera
            ?? anyActive       // 3. Any other active camera in the scene
            ?? anyInScene;     // 4. Any camera (inactive fallback)
    }
}