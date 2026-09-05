using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Supplies Lobby1Tutorial as the lobby context expected by the authoritative
/// RestockFlowCoordinator. Stock accounting, truck collection, room loading,
/// shelf placement, and inventory changes remain owned by the shared systems.
/// </summary>
[DefaultExecutionOrder(-8996)]
[DisallowMultipleComponent]
public sealed class TutorialRestockFlowBridge : MonoBehaviour
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private TutorialSystem tutorial;
    private RestockFlowCoordinator coordinator;
    private TutorialSystem.TutorialStep observedStep;
    private int startingHotbar;
    private int startingDry;
    private int startingFrozen;
    private bool roomWasOpen;
    private bool bootstrapped;
    private Button guardedExitButton;
    private Coroutine exitInputGuardRoutine;
    private ManagerPlayer guardedManager;
    private RestockStorageContainer placedLessonContainer;
    private RestockStorageType? lessonStorage;
    private bool placedBoxDragObserved;
    private readonly Dictionary<PlayerMovement, bool> suppressedStaffInputs = new();
    private bool loggedStaffInputSuppression;
    private RestockStorageType? firstRoom;
    private bool deliveryCollected, firstPlaced, switchedRoom, secondPlaced, switchClicked;
    private ItemData placementItem;
    private int placementHotbar;
    private Button liveSwitch;
    private readonly Dictionary<Button, bool> roomButtonStates = new();
    private bool uiGesture;
    private int uiReleaseFrame = -1;
    private readonly List<RaycastResult> uiHits = new();
    private RectTransform boxFocus;
    private bool boxActionsWereHidden;
    private RectTransform truckFocus;
    private RectTransform shelfFocus;
    private RectTransform slotFocus;
    private ShelfGrid lessonGrid;
    private int lessonColumn, lessonRow;
    private readonly HashSet<string> existingBatches = new();
    private PlayerMovement roomMovement;
    private bool roomMovementControlled;
    private int roomClosedFrame;

    private RestockRoomController LiveRoom => coordinator == null ? null :
        typeof(RestockFlowCoordinator).GetField("roomController", PrivateInstance)?.GetValue(coordinator) as RestockRoomController;

    private void LateUpdate()
    {
        if (tutorial == null || tutorial.CurrentPhase != TutorialSystem.TutorialPhase.PhysicalRestocking) return;
        if (coordinator == null || !coordinator.IsRestockRoomOpen)
        {
            tutorial.RefreshRestockPresentation();
            return;
        }
        if (lessonGrid != null && slotFocus != null)
        {
            Camera camera = typeof(RestockRoomController).GetField("roomCamera", PrivateInstance)?.GetValue(LiveRoom) as Camera;
            if (camera != null)
            {
                Vector3 center = lessonGrid.GetCellWorldPosition(lessonColumn, lessonRow);
                Vector3 point = camera.WorldToScreenPoint(center);
                Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector2 corner = camera.WorldToScreenPoint(center + lessonGrid.transform.TransformVector(
                            new Vector3(x * lessonGrid.cellWidth * .5f, 0f, z * lessonGrid.cellDepth * .5f)));
                        min = Vector2.Min(min, corner);
                        max = Vector2.Max(max, corner);
                    }
                slotFocus.position = (min + max) * .5f;
                slotFocus.sizeDelta = max - min;
                slotFocus.gameObject.SetActive(point.z > 0);
                Vector2 shelfMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 shelfMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (int c = 0; c < lessonGrid.columns; c++)
                    for (int r = 0; r < lessonGrid.rows; r++)
                        for (int x = -1; x <= 1; x += 2)
                            for (int z = -1; z <= 1; z += 2)
                            {
                                Vector2 corner = camera.WorldToScreenPoint(lessonGrid.GetCellWorldPosition(c, r) +
                                    lessonGrid.transform.TransformVector(new Vector3(x * lessonGrid.cellWidth * .5f, 0, z * lessonGrid.cellDepth * .5f)));
                                shelfMin = Vector2.Min(shelfMin, corner);
                                shelfMax = Vector2.Max(shelfMax, corner);
                            }
                shelfFocus.position = (shelfMin + shelfMax) * .5f;
                shelfFocus.sizeDelta = shelfMax - shelfMin;
                shelfFocus.gameObject.SetActive(point.z > 0);
            }
        }
        tutorial?.RefreshRestockPresentation();
    }

    private void RefreshRoomTargets()
    {
        if (coordinator == null) return;
        TrackUIRelease();
        bool suppress = coordinator.IsRestockRoomOpen || uiGesture || Time.frameCount <= uiReleaseFrame + 1;
        if (!suppress)
        {
            if (roomMovement != null && Time.frameCount > roomClosedFrame + 1)
            {
                roomMovement.SetPlayerControlled(roomMovementControlled);
                roomMovement = null;
            }
            return;
        }
        roomClosedFrame = Time.frameCount;
        if (roomMovement == null && ManagerPlayer.Active != null)
        {
            roomMovement = ManagerPlayer.Active.Movement;
            if (roomMovement != null) roomMovementControlled = roomMovement.IsPlayerControlled();
        }
        // Room input belongs to the real Restock UI. Leave movement tasks running,
        // but exclude hidden lobby click handlers through the closing release frame.
        if (roomMovement != null) roomMovement.SetPlayerControlled(false);
        if (!coordinator.IsRestockRoomOpen) return;
        GateRoomButtons();
        lessonStorage = CurrentRoom;
        if (lessonGrid == null || !lessonGrid.gameObject.activeInHierarchy || lessonGrid.StorageType != lessonStorage ||
            !lessonGrid.IsCellFree(lessonColumn, lessonRow))
        {
            lessonGrid = null;
            foreach (ShelfGrid grid in FindObjectsByType<ShelfGrid>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (grid.gameObject.scene.name == "RestockScene" && grid.StorageType == lessonStorage &&
                    TryChooseVisibleCell(grid, out lessonColumn, out lessonRow)) { lessonGrid = grid; break; }
        }
        if (slotFocus == null)
        {
            var root = new GameObject("TutorialRestockSlotFocus", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var focus = new GameObject("LiveShelfCell", typeof(RectTransform));
            focus.transform.SetParent(root.transform, false);
            slotFocus = (RectTransform)focus.transform;
            var shelf = new GameObject("LiveShelfGrid", typeof(RectTransform));
            shelf.transform.SetParent(root.transform, false);
            shelfFocus = (RectTransform)shelf.transform;
        }
    }

    private bool TryChooseVisibleCell(ShelfGrid grid, out int column, out int row)
    {
        column = row = 0;
        Camera camera = LiveRoom == null ? null :
            typeof(RestockRoomController).GetField("roomCamera", PrivateInstance)?.GetValue(LiveRoom) as Camera;
        if (camera == null) return false;
        for (int c = 0; c < grid.columns; c++)
            for (int r = 0; r < grid.rows; r++)
            {
                Vector3 point = camera.WorldToViewportPoint(grid.GetCellWorldPosition(c, r));
                if (!grid.IsCellFree(c, r) || point.z <= 0 || point.x <= 0 || point.x >= 1 || point.y <= 0 || point.y >= 1) continue;
                column = c;
                row = r;
                return true;
            }
        return false;
    }

    private void Awake() => tutorial = GetComponent<TutorialSystem>();

    private void TrackUIRelease()
    {
        if (tutorial == null || tutorial.CurrentPhase != TutorialSystem.TutorialPhase.PhysicalRestocking) return;
        bool down = Input.GetMouseButtonDown(0);
        bool held = Input.GetMouseButton(0);
        Vector2 position = Input.mousePosition;
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            down = touch.phase == TouchPhase.Began;
            held = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            position = touch.position;
        }
        if (down && !coordinator.IsRestockRoomOpen && roomMovement != null)
        {
            roomMovement.SetPlayerControlled(roomMovementControlled);
            roomMovement = null;
            roomClosedFrame = uiReleaseFrame = -10;
        }
        if (down && EventSystem.current != null)
        {
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            foreach (RaycastResult hit in uiHits)
                if (hit.module is GraphicRaycaster) { uiGesture = true; break; }
        }
        // Remember the press before transient UI disappears. PlayerMovement only
        // checks UI on release, so its same-release world fallback must stay gated.
        if (uiGesture && !held) { uiGesture = false; uiReleaseFrame = Time.frameCount; }
    }

    private Button RoomSwitch => LiveRoom == null ? null :
        typeof(RestockRoomController).GetField("switchRoomButton", PrivateInstance)?.GetValue(LiveRoom) as Button;

    private void GateRoomButtons()
    {
        if (tutorial == null || tutorial.CurrentPhase != TutorialSystem.TutorialPhase.PhysicalRestocking) return;
        Button current = RoomSwitch;
        if (liveSwitch != current)
        {
            if (liveSwitch != null) liveSwitch.onClick.RemoveListener(OnRoomSwitchClicked);
            liveSwitch = current;
            if (liveSwitch != null) liveSwitch.onClick.AddListener(OnRoomSwitchClicked);
        }
        SetRoomButton(current, firstPlaced && !secondPlaced && tutorial.CurrentStep?.ActionKey == "Restock.SwitchOther");
        RectTransform exit = FindActiveButton("ExitButton");
        SetRoomButton(exit != null ? exit.GetComponent<Button>() : null,
            deliveryCollected && firstPlaced && switchedRoom && secondPlaced);
    }

    private void SetRoomButton(Button button, bool allowed)
    {
        if (button == null) return;
        if (!roomButtonStates.ContainsKey(button)) roomButtonStates.Add(button, button.interactable);
        button.interactable = roomButtonStates[button] && allowed;
    }

    private void OnRoomSwitchClicked()
    {
        if (tutorial.IsWaitingForGameplayAction && tutorial.CurrentStep?.ActionKey == "Restock.SwitchOther")
            switchClicked = true;
    }


    public void Bootstrap()
    {
        if (bootstrapped || gameObject.scene.name != "Lobby1Tutorial") return;
        coordinator = RestockFlowCoordinator.EnsureInstance();
        if (coordinator == null) return;

        FieldInfo lobbyScene = typeof(RestockFlowCoordinator).GetField("lobbyScene", PrivateInstance);
        MethodInfo ensureHud = typeof(RestockFlowCoordinator).GetMethod("EnsureHud", PrivateInstance);
        MethodInfo createLobby = typeof(RestockFlowCoordinator).GetMethod("CreateLobbyInteractables", PrivateInstance);
        if (lobbyScene == null || ensureHud == null || createLobby == null)
        {
            Debug.LogError("[TutorialRestockFlowBridge] Shared Restock bootstrap contract changed.", this);
            return;
        }

        // The shared coordinator intentionally accepts only Lobby1. The tutorial
        // bridge supplies the current scene to that existing orchestration without
        // changing or duplicating its order/inventory logic.
        lobbyScene.SetValue(coordinator, gameObject.scene);
        ensureHud.Invoke(coordinator, null);
        createLobby.Invoke(coordinator, null);
        bootstrapped = true;
    }

    private void Update()
    {
        UpdatePhysicalRestockInputGate();
        RefreshRoomTargets();
        if (!bootstrapped && tutorial != null &&
            tutorial.CurrentPhase >= TutorialSystem.TutorialPhase.PhysicalRestocking)
            Bootstrap();
        if (coordinator != null && coordinator.IsRestockRoomOpen)
            KeepTutorialOverlayVisible();
        if (tutorial == null || !tutorial.IsWaitingForGameplayAction || tutorial.CurrentStep == null)
        {
            observedStep = null;
            return;
        }

        TutorialSystem.TutorialStep step = tutorial.CurrentStep;
        if (observedStep != step)
        {
            observedStep = step;
            RestockOrderManager manager = RestockOrderManager.Instance;
            startingHotbar = manager != null ? manager.HotbarContainerCount : 0;
            startingDry = manager != null ? manager.GetHotbarContainerCount(RestockStorageType.Dry) : 0;
            startingFrozen = manager != null ? manager.GetHotbarContainerCount(RestockStorageType.Frozen) : 0;
            roomWasOpen = coordinator != null && coordinator.IsRestockRoomOpen;
            placedBoxDragObserved = false;
            boxActionsWereHidden = !IsBoxActionPanelVisible(ResolvePlacedLessonBox());
            placementItem = ResolveUI("RestockActiveSlot")?.GetComponent<RestockHotbarSlotUI>()?.Item;
            placementHotbar = manager != null && placementItem != null ? manager.GetHotbarContainers(placementItem) : 0;
            existingBatches.Clear();
            foreach (RestockStorageContainer box in FindObjectsByType<RestockStorageContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(box.StockBatchID)) existingBatches.Add(box.StockBatchID);
        }

        if (step.ActionKey == "Management.CloseAfterRestock")
            EnsureExitInputGuard();
        else
            RemoveExitInputGuard();

        if (!IsComplete(step.ActionKey)) return;
        string key = step.ActionKey;
        if (key == "Restock.StoreDry" || key == "Restock.StoreFrozen")
            placedLessonContainer = FindPlacedLessonContainer();
        observedStep = null;
        tutorial.NotifyAction(key);
    }

    private void UpdatePhysicalRestockInputGate()
    {
        bool shouldSuppress = tutorial != null &&
                              (tutorial.CurrentPhase == TutorialSystem.TutorialPhase.PhysicalRestocking ||
                               tutorial.CurrentPhase == TutorialSystem.TutorialPhase.ReturnToComputer);
        if (!shouldSuppress)
        {
            RestoreStaffInputHandlers();
            return;
        }

        PlayerMovement managerMovement = ManagerPlayer.Active != null
            ? ManagerPlayer.Active.Movement
            : null;
        FieldInfo controlledField = typeof(PlayerMovement).GetField("isPlayerControlled", PrivateInstance);
        int newlySuppressed = 0;
        foreach (PlayerMovement movement in FindObjectsByType<PlayerMovement>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (movement == null || movement == managerMovement || suppressedStaffInputs.ContainsKey(movement))
                continue;

            bool wasControlled = controlledField != null &&
                                 controlledField.GetValue(movement) is bool value && value;
            suppressedStaffInputs.Add(movement, wasControlled);
            movement.SetPlayerControlled(false);
            newlySuppressed++;
        }

        if (newlySuppressed > 0 && !loggedStaffInputSuppression)
        {
            loggedStaffInputSuppression = true;
            Debug.Log("[TutorialRestock] Disabled duplicate click handlers on tutorial staff while the manager performs the physical Restock lesson.", this);
        }
    }

    private void RestoreStaffInputHandlers()
    {
        if (suppressedStaffInputs.Count == 0) return;
        foreach (KeyValuePair<PlayerMovement, bool> entry in suppressedStaffInputs)
            if (entry.Key != null)
                entry.Key.SetPlayerControlled(entry.Value);
        suppressedStaffInputs.Clear();
        loggedStaffInputSuppression = false;
    }

    private static void KeepTutorialOverlayVisible()
    {
        TutorialDialogueUI dialogue = FindFirstObjectByType<TutorialDialogueUI>(FindObjectsInactive.Include);
        TutorialHandIndicator hand = FindFirstObjectByType<TutorialHandIndicator>(FindObjectsInactive.Include);
        TutorialUIFocusMask mask = FindFirstObjectByType<TutorialUIFocusMask>(FindObjectsInactive.Include);
        EnableLocalCanvas(dialogue);
        EnableLocalCanvas(hand);
        EnableLocalCanvas(mask);
    }

    private static void EnableLocalCanvas(Component component)
    {
        if (component == null) return;
        Canvas canvas = component.GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = true;
    }

    private bool IsComplete(string key)
    {
        RestockOrderManager manager = RestockOrderManager.Instance;
        switch (key)
        {
            case "Management.CloseAfterRestock":
                return FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include)?.IsOpen == false;
            case "Restock.WaitForDelivery":
                return manager != null && manager.HasDeliveredOrders &&
                       ResolveWorld("RestockTruck")?.GetComponent<RestockTruckInteractable>()?.IsParked == true;
            case "Restock.TruckOpened":
                return ResolveUI("RestockGetOrders")?.gameObject.activeInHierarchy == true;
            case "Restock.GetOrders":
                deliveryCollected = manager != null && manager.HotbarContainerCount > startingHotbar &&
                                    manager.DeliveredContainerCount == 0;
                return deliveryCollected;
            case "Restock.EnterDry":
                return coordinator != null && coordinator.IsRestockRoomOpen &&
                       CurrentRoom == RestockStorageType.Dry;
            case "Restock.EnterAny":
                if (coordinator == null || !coordinator.IsRestockRoomOpen) return false;
                lessonStorage = CurrentRoom;
                if (!deliveryCollected || ResolveUI("RestockActiveSlot") == null || lessonGrid == null) return false;
                if (!firstRoom.HasValue) firstRoom = CurrentRoom;
                return true;
            case "Restock.StoreDry":
                return manager != null && startingDry > 0 &&
                       manager.GetHotbarContainerCount(RestockStorageType.Dry) < startingDry;
            case "Restock.StoreActive":
            case "Restock.StoreSecond":
            {
                if (manager == null || placementItem == null || !firstRoom.HasValue ||
                    !coordinator.IsRestockRoomOpen || placementHotbar <= 0 ||
                    manager.GetHotbarContainers(placementItem) >= placementHotbar) return false;
                bool second = key == "Restock.StoreSecond";
                RestockStorageType expected = second ? OtherRoom : firstRoom.Value;
                if (CurrentRoom != expected || placementItem.requiredStorage != expected ||
                    (second && (!firstPlaced || !switchedRoom))) return false;
                foreach (RestockStorageContainer box in FindObjectsByType<RestockStorageContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (box.Item != placementItem || box.CurrentStorage != expected || box.WrongStorage ||
                        string.IsNullOrEmpty(box.StockBatchID) || existingBatches.Contains(box.StockBatchID)) continue;
                    DraggableStorageBox draggable = box.GetComponent<DraggableStorageBox>();
                    ShelfGrid grid = draggable != null ? typeof(DraggableStorageBox).GetField("currentGrid", PrivateInstance)?.GetValue(draggable) as ShelfGrid : null;
                    if (grid == null || grid.StorageType != expected || InventoryManager.Instance == null ||
                        !InventoryManager.Instance.TryGetBatch(box.StockBatchID, out var batch) || batch == null || batch.unitsRemaining <= 0 || batch.currentStorage != expected || batch.wrongStorage) continue;
                    placedLessonContainer = box;
                    if (second) secondPlaced = true; else firstPlaced = true;
                    return true;
                }
                return false;
            }
            case "Restock.SwitchOther":
                switchedRoom = firstPlaced && switchClicked && coordinator.IsRestockRoomOpen &&
                               CurrentRoom == OtherRoom && lessonGrid != null && lessonGrid.StorageType == OtherRoom;
                return switchedRoom;
            case "Restock.BoxActionsShown":
                return firstPlaced && boxActionsWereHidden && IsBoxActionPanelVisible(ResolvePlacedLessonBox());
            case "Restock.BoxActionsHidden":
            {
                DraggableStorageBox box = ResolvePlacedLessonBox();
                if (box == null) return false;
                if (IsBoxBeingDragged(box)) placedBoxDragObserved = true;
                return placedBoxDragObserved && !IsBoxBeingDragged(box) &&
                       !Input.GetMouseButton(0) && Input.touchCount == 0 && !IsBoxActionPanelVisible(box);
            }
            case "Restock.SwitchFreezer":
                return coordinator != null && coordinator.IsRestockRoomOpen &&
                       CurrentRoom == RestockStorageType.Frozen;
            case "Restock.StoreFrozen":
                return manager != null && startingFrozen > 0 &&
                       manager.GetHotbarContainerCount(RestockStorageType.Frozen) < startingFrozen;
            case "Restock.ExitRoom":
                return deliveryCollected && firstPlaced && switchedRoom && secondPlaced && roomWasOpen && coordinator != null && !coordinator.IsRestockRoomOpen && !coordinator.IsTransitioning;
            case "Shift.Start":
                return GameDayManager.Instance != null && GameDayManager.Instance.ShiftRunning;
            default:
                return false;
        }
    }

    private RestockStorageType OtherRoom => firstRoom == RestockStorageType.Dry ? RestockStorageType.Frozen : RestockStorageType.Dry;

    private RestockStorageType CurrentRoom
    {
        get
        {
            if (coordinator == null) return RestockStorageType.Dry;
            FieldInfo roomField = typeof(RestockFlowCoordinator).GetField("roomController", PrivateInstance);
            RestockRoomController room = roomField?.GetValue(coordinator) as RestockRoomController;
            return room != null ? room.ActiveRoom : coordinator.ActiveStorageRoom;
        }
    }

    public Transform ResolveWorld(string key)
    {
        if (!bootstrapped) Bootstrap();
        if (key == "RestockEntrance")
            return ResolveWorld("RestockDryEntrance"); // Suggested entrance; both remain usable.
        if (key == "RestockTruck")
            return FindFirstObjectByType<RestockTruckInteractable>(FindObjectsInactive.Include)?.transform;
        if (key == "RestockDryEntrance" || key == "RestockFrozenEntrance")
        {
            RestockStorageType wanted = key == "RestockFrozenEntrance"
                ? RestockStorageType.Frozen : RestockStorageType.Dry;
            foreach (RestockStockRoomEntrance entrance in FindObjectsByType<RestockStockRoomEntrance>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (entrance.StorageType == wanted && entrance.gameObject.scene == gameObject.scene)
                    return entrance.transform;
        }
        if (key == "RestockDryShelf" || key == "RestockFrozenShelf")
        {
            RestockStorageType wanted = key == "RestockFrozenShelf"
                ? RestockStorageType.Frozen : RestockStorageType.Dry;
            foreach (ShelfGrid grid in FindObjectsByType<ShelfGrid>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (grid.StorageType == wanted) return grid.transform;
        }
        if (key == "RestockActiveShelf" && lessonStorage.HasValue)
            return lessonGrid != null ? slotFocus : null;
        if (key == "RestockPlacedDryBox" || key == "RestockPlacedBox")
            return ResolvePlacedLessonBox()?.transform;
        return null;
    }

    public RectTransform ResolveUI(string key)
    {
        if (key == "RestockBoxKeep" || key == "RestockBoxThrowAway")
        {
            Button button = FindBoxButton(ResolvePlacedLessonBox(), key == "RestockBoxKeep" ? "KeepButton" : "ThrowAwayButton");
            return button != null && button.gameObject.activeInHierarchy ? button.transform as RectTransform : null;
        }
        if (key == "RestockPlacedBoxFocus")
        {
            DraggableStorageBox box = ResolvePlacedLessonBox();
            Camera camera = LiveRoom == null ? null :
                typeof(RestockRoomController).GetField("roomCamera", PrivateInstance)?.GetValue(LiveRoom) as Camera;
            if (box == null || camera == null ||
                !TutorialWorldTargetGeometry.TryGetScreenRect(box.transform, camera, out Rect bounds)) return null;
            if (boxFocus == null)
            {
                var root = new GameObject("TutorialPlacedBoxFocus", typeof(RectTransform), typeof(Canvas));
                root.transform.SetParent(transform, false);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var focus = new GameObject("LivePlacedBoxBounds", typeof(RectTransform));
                focus.transform.SetParent(root.transform, false);
                boxFocus = (RectTransform)focus.transform;
            }
            boxFocus.position = bounds.center;
            boxFocus.sizeDelta = bounds.size;
            return boxFocus;
        }
        if (key == "RestockTruckFocus")
        {
            Transform truck = ResolveWorld("RestockTruck");
            Camera camera = Camera.main;
            if (truck == null || camera == null || !truck.gameObject.activeInHierarchy ||
                !TutorialWorldTargetGeometry.TryGetScreenRect(truck, camera, out Rect bounds)) return null;
            if (truckFocus == null)
            {
                var root = new GameObject("TutorialTruckFocus", typeof(RectTransform), typeof(Canvas));
                root.transform.SetParent(transform, false);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var focus = new GameObject("LiveTruckBounds", typeof(RectTransform));
                focus.transform.SetParent(root.transform, false);
                truckFocus = (RectTransform)focus.transform;
            }
            truckFocus.position = bounds.center;
            truckFocus.sizeDelta = bounds.size;
            return truckFocus;
        }
        if (key == "RestockShelfFocus") return lessonGrid != null ? shelfFocus : null;
        if (key == "RestockSlotFocus") return lessonGrid != null ? slotFocus : null;
        if (key == "RestockGetOrders")
        {
            RestockHoldButton hold = FindFirstObjectByType<RestockHoldButton>(FindObjectsInactive.Include);
            return hold != null && hold.gameObject.activeInHierarchy ? hold.transform as RectTransform : null;
        }

        RestockFlowHUD hud = FindFirstObjectByType<RestockFlowHUD>(FindObjectsInactive.Include);
        if (key == "RestockHotbar") return hud != null ? hud.HotbarRect : null;
        if (key == "RestockDrySlot" || key == "RestockFrozenSlot")
        {
            RestockStorageType wanted = key == "RestockFrozenSlot"
                ? RestockStorageType.Frozen : RestockStorageType.Dry;
            foreach (RestockHotbarSlotUI slot in FindObjectsByType<RestockHotbarSlotUI>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (slot.Item != null && slot.Item.requiredStorage == wanted)
                    return slot.transform as RectTransform;
        }
        if (key == "RestockActiveSlot" && lessonStorage.HasValue)
            foreach (RestockHotbarSlotUI slot in FindObjectsByType<RestockHotbarSlotUI>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (slot.Item != null && slot.Item.worldContainerPrefab != null && slot.Item.requiredStorage == lessonStorage.Value &&
                    RestockOrderManager.Instance != null && RestockOrderManager.Instance.GetHotbarContainers(slot.Item) > 0)
                    return slot.transform as RectTransform;
        if (key == "RestockSwitchRoom") return RoomSwitch != null && RoomSwitch.gameObject.activeInHierarchy ? RoomSwitch.transform as RectTransform : null;
        if (key == "RestockExit") return FindActiveButton("ExitButton");
        if (key == "RestockBoxActions")
        {
            DraggableStorageBox box = ResolvePlacedLessonBox();
            if (box == null) return null;
            Button keep = FindBoxButton(box, "KeepButton");
            Button discard = FindBoxButton(box, "ThrowAwayButton");
            Transform root = keep != null && discard != null && keep.transform.parent == discard.transform.parent
                ? keep.transform.parent
                : keep != null ? keep.transform : discard != null ? discard.transform : null;
            return root as RectTransform;
        }
        return null;
    }

    private void EnsureExitInputGuard()
    {
        if (guardedExitButton != null) return;
        ManagementComputerController computer = FindFirstObjectByType<ManagementComputerController>(
            FindObjectsInactive.Include);
        if (computer == null) return;
        foreach (Button candidate in computer.GetComponentsInChildren<Button>(true))
        {
            if (candidate == null || candidate.name != "ExitButton") continue;
            guardedExitButton = candidate;
            guardedExitButton.onClick.AddListener(BeginExitInputGuard);
            break;
        }
    }

    private void RemoveExitInputGuard()
    {
        if (guardedExitButton != null)
            guardedExitButton.onClick.RemoveListener(BeginExitInputGuard);
        guardedExitButton = null;
    }

    private void BeginExitInputGuard()
    {
        if (tutorial == null || tutorial.CurrentStep?.ActionKey != "Management.CloseAfterRestock")
            return;

        guardedManager = ManagerPlayer.Active;
        if (guardedManager == null) return;
        guardedManager.SetExternalInputSuppressed(true);
        if (exitInputGuardRoutine != null) StopCoroutine(exitInputGuardRoutine);
        exitInputGuardRoutine = StartCoroutine(ReleaseExitInputAfterPointer());
        Debug.Log("[TutorialRestock] Management EXIT release guarded so it cannot click through into a world task.", this);
    }

    private IEnumerator ReleaseExitInputAfterPointer()
    {
        yield return new WaitForEndOfFrame();
        while (Input.GetMouseButton(0) || Input.touchCount > 0)
            yield return null;
        yield return null;
        if (guardedManager != null)
            guardedManager.SetExternalInputSuppressed(false);
        guardedManager = null;
        exitInputGuardRoutine = null;
    }

    private DraggableStorageBox ResolvePlacedLessonBox()
    {
        if (placedLessonContainer != null && placedLessonContainer.gameObject.activeInHierarchy)
            return placedLessonContainer.GetComponent<DraggableStorageBox>();
        if (firstPlaced) return null; // Never substitute another crate for the verified placement.
        placedLessonContainer = FindPlacedLessonContainer();
        return placedLessonContainer != null ? placedLessonContainer.GetComponent<DraggableStorageBox>() : null;
    }

    private RestockStorageContainer FindPlacedLessonContainer()
    {
        if (!lessonStorage.HasValue) return null;
        foreach (RestockStorageContainer container in FindObjectsByType<RestockStorageContainer>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (container != null && container.Item != null &&
                container.CurrentStorage == lessonStorage.Value &&
                container.Item.requiredStorage == lessonStorage.Value &&
                container.GetComponent<DraggableStorageBox>() != null)
                return container;
        return null;
    }

    private static bool IsBoxActionPanelVisible(DraggableStorageBox box)
    {
        Button keep = FindBoxButton(box, "KeepButton");
        Button discard = FindBoxButton(box, "ThrowAwayButton");
        return (keep != null && keep.gameObject.activeInHierarchy) ||
               (discard != null && discard.gameObject.activeInHierarchy);
    }

    private static Button FindBoxButton(DraggableStorageBox box, string buttonName)
    {
        if (box == null) return null;
        foreach (Button button in box.GetComponentsInChildren<Button>(true))
            if (button != null && button.name == buttonName)
                return button;
        return null;
    }

    private static bool IsBoxBeingDragged(DraggableStorageBox box)
    {
        FieldInfo field = typeof(DraggableStorageBox).GetField("isDragging", PrivateInstance);
        return field != null && box != null && field.GetValue(box) is bool value && value;
    }

    private static RectTransform FindActiveButton(string objectName)
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (button.name == objectName && button.gameObject.scene.name == "RestockScene") return button.transform as RectTransform;
        return null;
    }

    private void OnDisable()
    {
        if (liveSwitch != null) liveSwitch.onClick.RemoveListener(OnRoomSwitchClicked);
        foreach (var entry in roomButtonStates) if (entry.Key != null) entry.Key.interactable = entry.Value;
        roomButtonStates.Clear();
        if (roomMovement != null) roomMovement.SetPlayerControlled(roomMovementControlled);
        roomMovement = null;
        RestoreStaffInputHandlers();
        RemoveExitInputGuard();
        if (exitInputGuardRoutine != null) StopCoroutine(exitInputGuardRoutine);
        exitInputGuardRoutine = null;
        if (guardedManager != null)
            guardedManager.SetExternalInputSuppressed(false);
        guardedManager = null;
    }
}
