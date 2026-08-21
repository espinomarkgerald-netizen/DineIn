using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Preserves the exact Lobby1 session while RestockScene is shown, and owns
/// the truck -> compact hotbar -> physical shelf handoff.
/// </summary>
[DefaultExecutionOrder(-480)]
public sealed class RestockFlowCoordinator : MonoBehaviour
{
    private struct BehaviourState
    {
        public Behaviour behaviour;
        public bool enabled;
    }

    private struct RendererState
    {
        public Renderer renderer;
        public bool enabled;
    }

    private struct AudioState
    {
        public AudioSource source;
        public bool wasPlaying;
        public int timeSamples;
    }

    public static RestockFlowCoordinator Instance { get; private set; }

    private const string LobbySceneName = "Lobby1";
    private const string RestockSceneName = "RestockScene";
    private const float TransitionReleaseSafetySeconds = 1.25f;

    [Header("Lobby Placement")]
    [SerializeField] private Vector3 truckOffsetFromPlayer = new Vector3(5f, 0f, 3f);
    [SerializeField] private Vector3 dryEntranceOffsetFromPlayer = new Vector3(-4f, 0f, 2f);
    [SerializeField] private Vector3 freezerEntranceOffsetFromPlayer = new Vector3(-1.8f, 0f, 2f);

    [Header("Messages")]
    [SerializeField, TextArea] private string dayForecastTemplate =
        "Today's forecast: at least {0} customer groups. Review supplies and prepare the restaurant using the management computer before opening.";
    [SerializeField, TextArea] private string deliveryArrivedMessage =
        "Your order has arrived! Go to the delivery truck and hold to collect it.";
    [SerializeField, TextArea] private string expiredStockWarningMessage =
        "Some of your stocks are expired. Throw them away in the stock room.";

    private RestockFlowHUD hud;
    private GameObject truck;
    private RestockTruckOffscreenIndicator truckIndicator;
    private RestockStockRoomEntrance dryEntrance;
    private RestockStockRoomEntrance freezerEntrance;
    private Scene lobbyScene;
    private Scene restockScene;
    private readonly List<BehaviourState> lobbyBehaviourStates = new List<BehaviourState>();
    private readonly List<BehaviourState> lobbyInputModuleStates = new List<BehaviourState>();
    private readonly List<BehaviourState> lobbyEventSystemStates = new List<BehaviourState>();
    private readonly List<RendererState> lobbyRendererStates = new List<RendererState>();
    private readonly List<AudioState> lobbyAudioStates = new List<AudioState>();
    private readonly List<GameObject> restockRoots = new List<GameObject>();
    private readonly List<bool> restockRootAuthoredStates = new List<bool>();
    private RestockRoomController roomController;
    private RestockStorageType requestedRoom;
    private bool loading;
    private bool roomOpen;
    private float previousTimeScale = 1f;
    private Coroutine transitionReleaseRoutine;
    private int transitionGeneration;
    private static int lastForecastDayShown = int.MinValue;
    private static int lastExpiredStockWarningDay = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        lastForecastDayShown = int.MinValue;
        lastExpiredStockWarningDay = int.MinValue;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static RestockFlowCoordinator EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        RestockFlowCoordinator existing = FindFirstObjectByType<RestockFlowCoordinator>();
        if (existing != null)
            return existing;

        GameObject root = new GameObject("Restock Delivery Flow");
        return root.AddComponent<RestockFlowCoordinator>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RestockOrderManager.EnsureInstance();
        EnsureHud();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Update()
    {
        if (roomOpen)
            roomController?.Tick();
        else
            ShowPendingDeliveryNotices();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        CancelTransitionReleaseSafety();
        hud?.ReleaseTransitionInputBlocker();
        if (roomOpen)
        {
            RestoreLobby();
            RestoreLobbyInputOwnership();
            Time.timeScale = previousTimeScale;
        }
        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == LobbySceneName)
        {
            lobbyScene = scene;
            StartCoroutine(SetupLobbyAfterOneFrame());
        }
        else if (scene.name == RestockSceneName)
        {
            restockScene = scene;
            ConfigureRestockEventSystems();
        }
    }

    private IEnumerator SetupLobbyAfterOneFrame()
    {
        yield return null;
        EnsureHud();
        EnsureLobbyWarningUI();
        hud?.SetLobbyContext();
        CreateLobbyInteractables();
        ShowPendingDeliveryNotices();
        ShowDayForecastOnce();
        ShowExpiredStockWarningOnce();
    }

    public void OpenTruckCollection()
    {
        EnsureHud();
        RestockOrderManager manager = RestockOrderManager.Instance;
        if (manager == null || manager.DeliveredContainerCount <= 0)
        {
            string message = manager != null && manager.HotbarContainerCount > 0
                ? "The delivery is already in your hotbar. Choose Dry Room or Freezer to store it."
                : "No order is ready yet. The truck will notify you when it arrives.";
            ShowMessage(message);
            return;
        }

        hud?.ShowHold(() =>
        {
            if (!manager.CollectDeliveredOrders())
                return;

            int count = manager.HotbarContainerCount;
            hud?.RequestPickupAnimation();
            ShowMessage(count + " delivered box" + (count == 1 ? string.Empty : "es") +
                        " added to your hotbar. Choose a delivery slot to see its storage room.");
        });
    }

    public void EnterRestockRoom()
    {
        EnterRestockRoom(RestockStorageType.Dry);
    }

    public void EnterRestockRoom(RestockStorageType room)
    {
        if (loading || roomOpen)
            return;

        requestedRoom = room;
        EnsureHud();
        PlayCloseThen(() => StartCoroutine(OpenRestockRoomRoutine()));
    }

    public void ExitRestockRoom()
    {
        if (!roomOpen || loading)
            return;

        loading = true;
        EnsureHud();
        PlayCloseThen(CloseRestockRoomNow);
    }

    public bool TryShowStartReminder(Action startAnyway)
    {
        int remaining = RestockOrderManager.Instance != null
            ? RestockOrderManager.Instance.HotbarContainerCount
            : 0;
        return remaining > 0 && hud != null && hud.ShowStartReminder(remaining, startAnyway);
    }

    public void GuideToStorage(RestockStorageType storage)
    {
        RestockStockRoomEntrance target = storage == RestockStorageType.Frozen
            ? freezerEntrance
            : dryEntrance;
        target?.PulseGuidance();
        ShowMessage(storage == RestockStorageType.Frozen
            ? "Frozen delivery selected — go to the Walk-in Freezer entrance."
            : "Dry delivery selected — go to the Dry Storage entrance.");
    }

    public void ShowMessage(string message)
    {
        if (!roomOpen && WarningSlideUI.Instance != null && WarningSlideUI.Instance.isActiveAndEnabled)
            WarningSlideUI.Instance.Show(message);
        else
            hud?.ShowNotification(message);
    }

    public void EnsureLobbyUIInputReady()
    {
        hud?.ReleaseTransitionInputBlocker();
        ReactivateLobbyInputModules();
    }

    public void AnimateInvalidDropReturn(GameObject preview)
    {
        if (preview != null)
            StartCoroutine(InvalidDropReturnRoutine(preview));
    }

    private static IEnumerator InvalidDropReturnRoutine(GameObject preview)
    {
        Vector3 startScale = preview.transform.localScale;
        const float duration = 0.18f;
        float elapsed = 0f;
        while (preview != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            preview.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
            yield return null;
        }
        if (preview != null)
            Destroy(preview);
    }

    private IEnumerator OpenRestockRoomRoutine()
    {
        loading = true;
        previousTimeScale = Time.timeScale;
        CaptureAndPauseLobby();
        Time.timeScale = 0f;

        if (!restockScene.IsValid() || !restockScene.isLoaded)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(RestockSceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                RestoreLobby();
                Time.timeScale = previousTimeScale;
                loading = false;
                RevealCurrentScene();
                ShowMessage("RestockScene could not be loaded.");
                yield break;
            }

            while (!load.isDone)
                yield return null;

            restockScene = SceneManager.GetSceneByName(RestockSceneName);
            CacheRestockRoots();
        }
        else
        {
            if (restockRoots.Count == 0)
                CacheRestockRoots();
            RestoreRestockRoots();
        }

        if (!restockScene.IsValid() || !restockScene.isLoaded)
        {
            RestoreLobby();
            Time.timeScale = previousTimeScale;
            loading = false;
            RevealCurrentScene();
            yield break;
        }

        SceneManager.SetActiveScene(restockScene);
        TakeRestockInputOwnership();
        roomController = new RestockRoomController(restockScene, hud, this);
        roomController.Activate(requestedRoom);
        roomOpen = true;
        loading = false;
        RevealCurrentScene();
    }

    private void CloseRestockRoomNow()
    {
        RunExitStep(() => roomController?.Deactivate(), "deactivate the restock room");
        roomController = null;
        RunExitStep(HideRestockRoots, "hide the restock scene");

        if (lobbyScene.IsValid() && lobbyScene.isLoaded)
            RunExitStep(() => SceneManager.SetActiveScene(lobbyScene), "activate the lobby scene");

        RunExitStep(RestoreLobby, "restore the lobby presentation");
        RunExitStep(RestoreLobbyInputOwnership, "restore lobby input ownership");
        RunExitStep(ReactivateLobbyInputModules, "reactivate lobby UI input");
        Time.timeScale = previousTimeScale;
        roomOpen = false;
        loading = false;
        RunExitStep(() => hud?.SetLobbyContext(), "restore the lobby HUD");
        RevealCurrentScene();

        int remaining = RestockOrderManager.Instance != null
            ? RestockOrderManager.Instance.HotbarContainerCount
            : 0;
        if (remaining > 0)
            ShowMessage(remaining + " delivered box" + (remaining == 1 ? string.Empty : "es") +
                        " still need shelf space.");
    }

    private void PlayCloseThen(Action completed)
    {
        CancelTransitionReleaseSafety();
        transitionGeneration++;

        if (hud == null)
        {
            completed?.Invoke();
            return;
        }

        bool callbackInvoked = false;
        Action guardedCompletion = () =>
        {
            if (callbackInvoked)
                return;
            callbackInvoked = true;
            completed?.Invoke();
        };

        try
        {
            hud.PlayClose(guardedCompletion);
        }
        catch (Exception exception)
        {
            Debug.LogError("[RestockFlow] Iris close failed; continuing the scene transition.");
            Debug.LogException(exception);
            hud.ReleaseTransitionInputBlocker();
            guardedCompletion();
        }
    }

    private void RevealCurrentScene()
    {
        CancelTransitionReleaseSafety();
        int generation = ++transitionGeneration;

        if (hud == null)
            return;

        try
        {
            hud.PlayOpen();
            transitionReleaseRoutine = StartCoroutine(
                ReleaseTransitionBlockerAfterDelay(generation));
        }
        catch (Exception exception)
        {
            Debug.LogError("[RestockFlow] Iris open failed; releasing the screen blocker immediately.");
            Debug.LogException(exception);
            hud.ReleaseTransitionInputBlocker();
        }
    }

    private IEnumerator ReleaseTransitionBlockerAfterDelay(int generation)
    {
        yield return new WaitForSecondsRealtime(TransitionReleaseSafetySeconds);
        transitionReleaseRoutine = null;

        if (generation == transitionGeneration)
            hud?.ReleaseTransitionInputBlocker();
    }

    private void CancelTransitionReleaseSafety()
    {
        transitionGeneration++;
        if (transitionReleaseRoutine == null)
            return;

        StopCoroutine(transitionReleaseRoutine);
        transitionReleaseRoutine = null;
    }

    private static void RunExitStep(Action step, string description)
    {
        try
        {
            step?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogError("[RestockFlow] Failed to " + description + "; continuing lobby recovery.");
            Debug.LogException(exception);
        }
    }

    private void EnsureHud()
    {
        if (hud != null)
            return;

        RestockFlowHUD prefab = Resources.Load<RestockFlowHUD>("RestockFlow/RestockFlowHUD");
        if (prefab == null)
        {
            Debug.LogError("[RestockFlow] RestockFlowHUD prefab is missing. " +
                           "Use Tools > Dine In > Create Missing Restock Flow Prefabs.");
            return;
        }

        hud = Instantiate(prefab, transform);
        hud.gameObject.name = "Restock Flow HUD";
    }

    private void CreateLobbyInteractables()
    {
        if (!lobbyScene.IsValid() || !lobbyScene.isLoaded)
            return;

        Vector3 origin = ManagerPlayer.Active != null
            ? ManagerPlayer.Active.transform.position
            : Vector3.zero;

        if (truck == null)
        {
            RestockTruckInteractable authoredTruck = FindAuthoredTruckInteractable();
            GameObject authoredMarker = authoredTruck == null
                ? FindAuthoredTruckMarker()
                : authoredTruck.gameObject;
            Vector3 parkedPosition = authoredMarker != null
                ? authoredMarker.transform.position
                : FindNearbyNavMeshPoint(origin + truckOffsetFromPlayer, origin);
            Quaternion parkedRotation = authoredMarker != null
                ? authoredMarker.transform.rotation
                : Quaternion.identity;

            if (authoredTruck != null)
            {
                truck = authoredTruck.gameObject;
            }
            else
            {
                RestockTruckInteractable prefab = Resources.Load<RestockTruckInteractable>(
                    "RestockFlow/RestockDeliveryTruck");
                if (prefab != null)
                {
                    truck = Instantiate(prefab.gameObject, parkedPosition, parkedRotation);
                    truck.name = "Restock Delivery Truck";
                    SceneManager.MoveGameObjectToScene(truck, lobbyScene);
                    if (authoredMarker != null)
                        authoredMarker.SetActive(false);
                }
            }
        }

        RestockTruckInteractable truckInteractable = truck != null
            ? truck.GetComponent<RestockTruckInteractable>()
            : null;
        if (truckInteractable != null)
        {
            Vector3 parkedPosition = truck.transform.position;
            Quaternion parkedRotation = truck.transform.rotation;
            ConfigureInteractionPresentation(truck);
            SnapStandPointToNavMesh(truckInteractable);
            if (!truckInteractable.ParkingConfigured)
                truckInteractable.ConfigureParkingPose(parkedPosition, parkedRotation);
            EnsureTruckIndicator(truckInteractable);
        }

        if (dryEntrance == null)
            dryEntrance = CreateEntrance(
                "Dry Storage Entrance",
                RestockStorageType.Dry,
                origin + dryEntranceOffsetFromPlayer,
                origin);

        if (freezerEntrance == null)
            freezerEntrance = CreateEntrance(
                "Walk-in Freezer Entrance",
                RestockStorageType.Frozen,
                origin + freezerEntranceOffsetFromPlayer,
                origin);
    }

    private RestockTruckInteractable FindAuthoredTruckInteractable()
    {
        if (!lobbyScene.IsValid() || !lobbyScene.isLoaded)
            return null;

        GameObject[] roots = lobbyScene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            RestockTruckInteractable[] candidates =
                roots[r].GetComponentsInChildren<RestockTruckInteractable>(true);
            if (candidates.Length > 0)
                return candidates[0];
        }

        return null;
    }

    private GameObject FindAuthoredTruckMarker()
    {
        if (!lobbyScene.IsValid() || !lobbyScene.isLoaded)
            return null;

        GameObject[] roots = lobbyScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject candidate = roots[i];
            if (candidate != null && candidate.name == "DeliveryTruck")
                return candidate;
        }

        return null;
    }

    private void EnsureTruckIndicator(RestockTruckInteractable target)
    {
        if (target == null)
            return;

        if (truckIndicator == null)
        {
            RestockTruckOffscreenIndicator prefab =
                Resources.Load<RestockTruckOffscreenIndicator>(
                    "RestockFlow/RestockTruckOffscreenIndicator");
            if (prefab != null)
            {
                truckIndicator = Instantiate(prefab, transform);
                truckIndicator.name = "Restock Truck Offscreen Indicator";
            }
        }

        truckIndicator?.Bind(target);
    }

    private RestockStockRoomEntrance CreateEntrance(
        string objectName,
        RestockStorageType storage,
        Vector3 desiredPosition,
        Vector3 fallback)
    {
        RestockStockRoomEntrance prefab = Resources.Load<RestockStockRoomEntrance>(
            "RestockFlow/RestockStockRoomEntrance");
        if (prefab == null)
            return null;

        Vector3 position = FindNearbyNavMeshPoint(desiredPosition, fallback);
        RestockStockRoomEntrance result = Instantiate(prefab, position, Quaternion.identity);
        result.name = objectName;
        result.ConfigureRoom(storage);
        SceneManager.MoveGameObjectToScene(result.gameObject, lobbyScene);
        ConfigureInteractionPresentation(result.gameObject);
        SnapStandPointToNavMesh(result);
        return result;
    }

    private static void ConfigureInteractionPresentation(GameObject target)
    {
        if (target == null)
            return;

        int interactionLayer = LayerMask.NameToLayer("Interactable ");
        if (interactionLayer >= 0)
            target.layer = interactionLayer;

        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.AddComponent<Outline>();
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = Color.white;
        outline.OutlineWidth = 4f;
        outline.enabled = false;
    }

    private static void SnapStandPointToNavMesh(IInteractable interactable)
    {
        Transform standPoint = interactable?.StandPoint;
        if (standPoint == null)
            return;

        if (NavMesh.SamplePosition(standPoint.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            standPoint.position = hit.position;
        else if (standPoint.parent != null &&
                 NavMesh.SamplePosition(standPoint.parent.position, out hit, 3f, NavMesh.AllAreas))
            standPoint.position = hit.position;
    }

    private void ShowPendingDeliveryNotices()
    {
        RestockOrderManager manager = RestockOrderManager.Instance;
        if (manager == null)
            return;

        IReadOnlyList<RestockOrderSaveData> orders = manager.Orders;
        for (int i = 0; i < orders.Count; i++)
        {
            RestockOrderSaveData order = orders[i];
            if (manager.ConsumeDeliveryNotice(order))
                ShowMessage(deliveryArrivedMessage);
        }
    }

    private void ShowDayForecastOnce()
    {
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        if (lastForecastDayShown == day)
            return;

        lastForecastDayShown = day;
        int groups = GameDayManager.Instance != null
            ? Mathf.Max(1, GameDayManager.Instance.MaxCustomersThisShift)
            : ShiftScaler.Instance != null
                ? Mathf.Max(1, ShiftScaler.Instance.CurrentGroupCount)
                : 1;
        ShowMessage(string.Format(dayForecastTemplate, groups));
    }

    private void ShowExpiredStockWarningOnce()
    {
        int day = GameFlowManager.Instance != null
            ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
            : 1;
        if (lastExpiredStockWarningDay == day ||
            InventoryManager.Instance == null ||
            !InventoryManager.Instance.HasExpiredStock(day))
            return;

        lastExpiredStockWarningDay = day;
        StartCoroutine(ShowExpiredStockWarningAfterForecast());
    }

    private IEnumerator ShowExpiredStockWarningAfterForecast()
    {
        yield return new WaitForSecondsRealtime(2.4f);
        ShowMessage(expiredStockWarningMessage);
    }

    private static void EnsureLobbyWarningUI()
    {
        if (WarningSlideUI.Instance != null)
            return;

        WarningSlideUI warning = FindFirstObjectByType<WarningSlideUI>(FindObjectsInactive.Include);
        if (warning != null && !warning.gameObject.activeSelf)
            warning.gameObject.SetActive(true);
    }

    private void CaptureAndPauseLobby()
    {
        lobbyBehaviourStates.Clear();
        lobbyRendererStates.Clear();
        lobbyAudioStates.Clear();
        if (!lobbyScene.IsValid())
            return;

        AudioSource[] allSources = FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < allSources.Length; i++)
        {
            AudioSource source = allSources[i];
            if (source == null ||
                (restockScene.IsValid() && source.gameObject.scene == restockScene))
                continue;

            lobbyAudioStates.Add(new AudioState
            {
                source = source,
                wasPlaying = source.isPlaying,
                timeSamples = source.clip != null ? source.timeSamples : 0
            });
            if (source.isPlaying)
                source.Pause();
        }

        // Keep every gameplay object alive. Disabling Lobby roots stops customer
        // coroutines and invalidates active NavMesh paths, so they may never resume.
        // Only the Lobby presentation and input are hidden while timeScale pauses play.
        CaptureAndDisableLobbyBehaviours<Camera>();
        CaptureAndDisableLobbyBehaviours<Canvas>();
        CaptureAndDisableLobbyBehaviours<Light>();
        CaptureAndDisableLobbyBehaviours<AudioListener>();
        CaptureAndDisableLobbyBehaviours<Terrain>();

        GameObject[] roots = lobbyScene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            Renderer[] renderers = roots[r].GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                lobbyRendererStates.Add(new RendererState
                {
                    renderer = renderer,
                    enabled = renderer.enabled
                });
                renderer.enabled = false;
            }
        }
    }

    private void RestoreLobby()
    {
        for (int i = 0; i < lobbyRendererStates.Count; i++)
        {
            RendererState state = lobbyRendererStates[i];
            if (state.renderer != null)
                state.renderer.enabled = state.enabled;
        }

        for (int i = 0; i < lobbyBehaviourStates.Count; i++)
        {
            BehaviourState state = lobbyBehaviourStates[i];
            if (state.behaviour != null)
                state.behaviour.enabled = state.enabled;
        }

        for (int i = 0; i < lobbyAudioStates.Count; i++)
        {
            AudioState state = lobbyAudioStates[i];
            if (state.source == null || state.source.clip == null)
                continue;
            state.source.Stop();
            state.source.timeSamples = Mathf.Clamp(state.timeSamples, 0, state.source.clip.samples - 1);
            if (state.wasPlaying)
                state.source.Play();
        }

        lobbyRendererStates.Clear();
        lobbyBehaviourStates.Clear();
        lobbyAudioStates.Clear();
    }

    private void CaptureAndDisableLobbyBehaviours<T>() where T : Behaviour
    {
        GameObject[] roots = lobbyScene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            T[] behaviours = roots[r].GetComponentsInChildren<T>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                T behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                lobbyBehaviourStates.Add(new BehaviourState
                {
                    behaviour = behaviour,
                    enabled = behaviour.enabled
                });
                behaviour.enabled = false;
            }
        }
    }

    private void CacheRestockRoots()
    {
        restockRoots.Clear();
        restockRootAuthoredStates.Clear();
        if (!restockScene.IsValid())
            return;

        GameObject[] roots = restockScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            restockRoots.Add(roots[i]);
            restockRootAuthoredStates.Add(roots[i].activeSelf);
        }
    }

    private void HideRestockRoots()
    {
        for (int i = 0; i < restockRoots.Count; i++)
        {
            if (restockRoots[i] != null)
                restockRoots[i].SetActive(false);
        }
    }

    private void RestoreRestockRoots()
    {
        for (int i = 0; i < restockRoots.Count; i++)
        {
            if (restockRoots[i] != null)
                restockRoots[i].SetActive(restockRootAuthoredStates[i]);
        }

        ConfigureRestockEventSystems();
    }

    private void ConfigureRestockEventSystems()
    {
        if (!restockScene.IsValid() || !restockScene.isLoaded)
            return;

        // RestockScene is loaded additively during normal gameplay. Both its
        // EventSystem and input module start disabled. Leaving the input module
        // alive is unsafe because both scenes reference the same UI action asset;
        // hiding RestockScene would then disable Lobby's pointer actions too.
        // Enable the pair only when RestockScene is played by itself.
        bool useRestockEventSystem = !HasActiveEventSystemOutsideRestockScene();
        GameObject[] roots = restockScene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            BaseInputModule[] modules = roots[r].GetComponentsInChildren<BaseInputModule>(true);
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != null)
                    modules[i].enabled = useRestockEventSystem;
            }

            EventSystem[] systems = roots[r].GetComponentsInChildren<EventSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    systems[i].enabled = useRestockEventSystem;
            }
        }
    }

    private bool HasActiveEventSystemOutsideRestockScene()
    {
        EventSystem[] systems = FindObjectsByType<EventSystem>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem system = systems[i];
            if (system != null &&
                system.enabled &&
                system.gameObject.activeInHierarchy &&
                system.gameObject.scene != restockScene)
            {
                return true;
            }
        }

        return false;
    }

    private void TakeRestockInputOwnership()
    {
        lobbyInputModuleStates.Clear();
        lobbyEventSystemStates.Clear();

        if (lobbyScene.IsValid() && lobbyScene.isLoaded)
        {
            GameObject[] lobbyRoots = lobbyScene.GetRootGameObjects();
            for (int r = 0; r < lobbyRoots.Length; r++)
            {
                EventSystem[] systems = lobbyRoots[r].GetComponentsInChildren<EventSystem>(true);
                for (int i = 0; i < systems.Length; i++)
                {
                    EventSystem system = systems[i];
                    if (system == null)
                        continue;

                    lobbyEventSystemStates.Add(new BehaviourState
                    {
                        behaviour = system,
                        enabled = system.enabled
                    });

                    BaseInputModule[] modules = system.GetComponents<BaseInputModule>();
                    for (int m = 0; m < modules.Length; m++)
                    {
                        BaseInputModule module = modules[m];
                        if (module == null)
                            continue;

                        lobbyInputModuleStates.Add(new BehaviourState
                        {
                            behaviour = module,
                            enabled = module.enabled
                        });
                    }
                }
            }
        }

        // Disable the previous EventSystem first. Disable its input modules
        // before enabling Restock's modules because both scenes use the same
        // InputActionAsset and the last enabled module must own those actions.
        SetBehaviourStatesEnabled(lobbyEventSystemStates, false);
        SetBehaviourStatesEnabled(lobbyInputModuleStates, false);

        EventSystem restockSystem = null;
        GameObject[] restockSceneRoots = restockScene.GetRootGameObjects();
        for (int r = 0; r < restockSceneRoots.Length; r++)
        {
            BaseInputModule[] modules =
                restockSceneRoots[r].GetComponentsInChildren<BaseInputModule>(true);
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != null)
                    modules[i].enabled = true;
            }

            EventSystem[] systems =
                restockSceneRoots[r].GetComponentsInChildren<EventSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                EventSystem system = systems[i];
                if (system == null)
                    continue;

                system.enabled = true;
                if (system.gameObject.activeInHierarchy && restockSystem == null)
                    restockSystem = system;
            }
        }

        if (restockSystem != null)
        {
            EventSystem.current = restockSystem;
            restockSystem.UpdateModules();
            restockSystem.SetSelectedGameObject(null);
        }
        else
        {
            Debug.LogWarning(
                "[RestockFlow] RestockScene has no active EventSystem; keeping Lobby UI input as a fallback.");
            RestoreLobbyInputOwnership();
            ReactivateLobbyInputModules();
        }
    }

    private void RestoreLobbyInputOwnership()
    {
        // Restock roots are hidden before this method, which disables their
        // shared UI actions. Restore Lobby's modules last so pointer actions
        // are guaranteed to be enabled for the visible scene.
        RestoreBehaviourStates(lobbyInputModuleStates);
        RestoreBehaviourStates(lobbyEventSystemStates);
        lobbyInputModuleStates.Clear();
        lobbyEventSystemStates.Clear();
    }

    private static void SetBehaviourStatesEnabled(
        List<BehaviourState> states,
        bool enabled)
    {
        for (int i = 0; i < states.Count; i++)
        {
            Behaviour behaviour = states[i].behaviour;
            if (behaviour != null)
                behaviour.enabled = enabled;
        }
    }

    private static void RestoreBehaviourStates(List<BehaviourState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            BehaviourState state = states[i];
            if (state.behaviour != null)
                state.behaviour.enabled = state.enabled;
        }
    }

    private void ReactivateLobbyInputModules()
    {
        if (!lobbyScene.IsValid() || !lobbyScene.isLoaded)
            return;

        // A room transition ends any pointer/drag gesture. Re-enabling Lobby's
        // module restores its shared UI actions even after an older RestockScene
        // instance or another module disabled them.
        GameObject[] roots = lobbyScene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            EventSystem[] systems = roots[r].GetComponentsInChildren<EventSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                EventSystem system = systems[i];
                if (system == null || !system.enabled || !system.gameObject.activeInHierarchy)
                    continue;

                BaseInputModule[] modules = system.GetComponents<BaseInputModule>();
                for (int m = 0; m < modules.Length; m++)
                {
                    BaseInputModule module = modules[m];
                    if (module == null || !module.enabled)
                        continue;

                    module.enabled = false;
                    module.enabled = true;
                }

                EventSystem.current = system;
                system.UpdateModules();
                system.SetSelectedGameObject(null);
            }
        }
    }

    private static Vector3 FindNearbyNavMeshPoint(Vector3 desired, Vector3 fallback)
    {
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            return hit.position;
        if (NavMesh.SamplePosition(fallback, out hit, 8f, NavMesh.AllAreas))
            return hit.position;
        return fallback;
    }
}
