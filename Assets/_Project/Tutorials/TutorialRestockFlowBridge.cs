using System;
using System.Collections;
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

    private IEnumerator Start()
    {
        tutorial = GetComponent<TutorialSystem>();
        yield return null;
        Bootstrap();
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
        if (!bootstrapped) Bootstrap();
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
        }

        if (!IsComplete(step.ActionKey)) return;
        string key = step.ActionKey;
        observedStep = null;
        tutorial.NotifyAction(key);
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
        return null;
    }

    private static RectTransform FindActiveButton(string objectName)
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (button.name == objectName) return button.transform as RectTransform;
        return null;
    }
}
