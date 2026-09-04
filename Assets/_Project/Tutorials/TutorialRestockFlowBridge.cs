using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    private RestockStorageContainer placedDryContainer;
    private bool placedBoxDragObserved;
    private readonly Dictionary<PlayerMovement, bool> suppressedStaffInputs = new();
    private bool loggedStaffInputSuppression;

    private void Awake() => tutorial = GetComponent<TutorialSystem>();

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
        }

        if (step.ActionKey == "Management.CloseAfterRestock")
            EnsureExitInputGuard();
        else
            RemoveExitInputGuard();

        if (!IsComplete(step.ActionKey)) return;
        string key = step.ActionKey;
        if (key == "Restock.StoreDry")
            placedDryContainer = FindPlacedDryContainer();
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
                return manager != null && manager.HotbarContainerCount > startingHotbar &&
                       manager.DeliveredContainerCount == 0;
            case "Restock.EnterDry":
                return coordinator != null && coordinator.IsRestockRoomOpen &&
                       CurrentRoom == RestockStorageType.Dry;
            case "Restock.StoreDry":
                return manager != null && startingDry > 0 &&
                       manager.GetHotbarContainerCount(RestockStorageType.Dry) < startingDry;
            case "Restock.BoxActionsShown":
                return IsBoxActionPanelVisible(ResolvePlacedDryBox());
            case "Restock.BoxActionsHidden":
            {
                DraggableStorageBox box = ResolvePlacedDryBox();
                if (box == null) return false;
                if (IsBoxBeingDragged(box)) placedBoxDragObserved = true;
                return placedBoxDragObserved && !IsBoxActionPanelVisible(box);
            }
            case "Restock.SwitchFreezer":
                return coordinator != null && coordinator.IsRestockRoomOpen &&
                       CurrentRoom == RestockStorageType.Frozen;
            case "Restock.StoreFrozen":
                return manager != null && startingFrozen > 0 &&
                       manager.GetHotbarContainerCount(RestockStorageType.Frozen) < startingFrozen;
            case "Restock.ExitRoom":
                return roomWasOpen && coordinator != null && !coordinator.IsRestockRoomOpen && !coordinator.IsTransitioning;
            case "Shift.Start":
                return GameDayManager.Instance != null && GameDayManager.Instance.ShiftRunning;
            default:
                return false;
        }
    }

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
        if (key == "RestockPlacedDryBox")
            return ResolvePlacedDryBox()?.transform;
        return null;
    }

    public RectTransform ResolveUI(string key)
    {
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
        if (key == "RestockSwitchRoom") return FindActiveButton("SwitchRoomToFreezer");
        if (key == "RestockExit") return FindActiveButton("ExitButton");
        if (key == "RestockBoxActions")
        {
            DraggableStorageBox box = ResolvePlacedDryBox();
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

    private DraggableStorageBox ResolvePlacedDryBox()
    {
        if (placedDryContainer != null && placedDryContainer.gameObject.activeInHierarchy)
            return placedDryContainer.GetComponent<DraggableStorageBox>();
        placedDryContainer = FindPlacedDryContainer();
        return placedDryContainer != null ? placedDryContainer.GetComponent<DraggableStorageBox>() : null;
    }

    private static RestockStorageContainer FindPlacedDryContainer()
    {
        foreach (RestockStorageContainer container in FindObjectsByType<RestockStorageContainer>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (container != null && container.Item != null &&
                container.CurrentStorage == RestockStorageType.Dry &&
                container.Item.requiredStorage == RestockStorageType.Dry &&
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
            if (button.name == objectName) return button.transform as RectTransform;
        return null;
    }

    private void OnDisable()
    {
        RestoreStaffInputHandlers();
        RemoveExitInputGuard();
        if (exitInputGuardRoutine != null) StopCoroutine(exitInputGuardRoutine);
        exitInputGuardRoutine = null;
        if (guardedManager != null)
            guardedManager.SetExternalInputSuppressed(false);
        guardedManager = null;
    }
}
