using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Connects the authored RestockScene camera rigs, room button, and ShelfGrids
/// to the persistent delivery hotbar. It does not create replacement rooms or shelves.
/// </summary>
public sealed class RestockRoomController
{
    private readonly Scene scene;
    private readonly RestockFlowHUD hud;
    private readonly RestockFlowCoordinator coordinator;
    private readonly List<ShelfGrid> grids = new List<ShelfGrid>();

    private Camera roomCamera;
    private Transform dryRig;
    private Transform freezerRig;
    private GameObject authoredEmptyHotbar;
    private Button switchRoomButton;
    private RestockStorageType activeRoom;

    private ItemData dragItem;
    private GameObject dragPreview;
    private ShelfGrid previewGrid;
    private int previewColumn = -1;
    private int previewRow = -1;
    private bool previewValid;
    private Vector3 previewScale = Vector3.one;
    private Outline previewOutline;
    private string wrongStorageConfirmationItemID;
    private string wrongStorageConfirmationShelfID;
    private float wrongStorageConfirmationUntil;

    public RestockStorageType ActiveRoom => activeRoom;

    public RestockRoomController(
        Scene configuredScene,
        RestockFlowHUD configuredHud,
        RestockFlowCoordinator configuredCoordinator)
    {
        scene = configuredScene;
        hud = configuredHud;
        coordinator = configuredCoordinator;
        DiscoverSceneObjects();
    }

    public void Activate(RestockStorageType requestedRoom)
    {
        SetNonRoomRootsVisible(false);
        if (authoredEmptyHotbar != null)
            authoredEmptyHotbar.SetActive(false);
        WireButtons();
        RestoreSavedContainers();
        RefreshStorageContainers();
        SwitchToRoom(requestedRoom);
        hud?.SetRestockContext(this, activeRoom);
        hud?.SetRoomMessage(
            "DRAG A BOX TO AN OPEN SHELF",
            false);
    }

    public void Deactivate()
    {
        CancelHotbarWorldDrag();
        hud?.SetLobbyContext();
    }

    public void Tick()
    {
        // Pointer input is intentionally owned by RestockHotbarSlotUI so mouse
        // and touch use the same deterministic drag path.
    }

    public void PrepareHotbarDrag(ItemData item)
    {
        CancelHotbarWorldDrag();
        dragItem = item;
    }

    public bool BeginHotbarWorldDrag(ItemData item, Vector2 screenPosition)
    {
        CancelHotbarWorldDrag();
        dragItem = item;
        if (dragItem == null || dragItem.worldContainerPrefab == null || roomCamera == null)
        {
            string message = dragItem == null
                ? "ITEM UNAVAILABLE"
                : "BOX PREFAB MISSING: " + dragItem.displayName;
            hud?.SetRoomMessage(message, true);
            return false;
        }

        dragPreview = Object.Instantiate(dragItem.worldContainerPrefab);
        dragPreview.name = dragItem.displayName + " Drag Preview";
        SceneManager.MoveGameObjectToScene(dragPreview, scene);
        previewScale = dragPreview.transform.localScale;

        RestockStorageContainer identity = dragPreview.GetComponent<RestockStorageContainer>();
        if (identity == null)
            identity = dragPreview.AddComponent<RestockStorageContainer>();
        identity.Bind(dragItem);

        Collider[] colliders = dragPreview.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
        Rigidbody[] rigidbodies = dragPreview.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }
        Canvas[] canvases = dragPreview.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
            canvases[i].gameObject.SetActive(false);
        MonoBehaviour[] behaviours = dragPreview.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
            behaviours[i].enabled = false;

        previewOutline = dragPreview.GetComponent<Outline>();
        if (previewOutline == null)
            previewOutline = dragPreview.AddComponent<Outline>();
        previewOutline.OutlineMode = Outline.Mode.OutlineAll;
        previewOutline.OutlineWidth = 5f;
        previewOutline.enabled = true;

        UpdateHotbarWorldDrag(screenPosition);
        return true;
    }

    public void UpdateHotbarWorldDrag(Vector2 screenPosition)
    {
        if (dragPreview == null || roomCamera == null)
            return;

        previewGrid = null;
        previewColumn = -1;
        previewRow = -1;
        previewValid = false;

        Ray ray = roomCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            ShelfGrid grid = hits[i].collider.GetComponentInParent<ShelfGrid>();
            if (grid == null || !grid.TryGetClosestCell(hits[i].point, out int column, out int row))
                continue;

            previewGrid = grid;
            previewColumn = column;
            previewRow = row;
            previewValid = dragItem != null && grid.IsCellFree(column, row);
            dragPreview.transform.position = grid.GetCellWorldPosition(column, row);
            break;
        }

        if (previewGrid == null)
            dragPreview.transform.position = ray.GetPoint(3.2f);

        dragPreview.transform.localScale = previewScale * (previewValid ? 1.04f : 0.92f);
        if (previewOutline != null)
        {
            bool wrongStorage = previewValid && dragItem != null &&
                                previewGrid != null &&
                                previewGrid.StorageType != dragItem.requiredStorage;
            previewOutline.OutlineColor = previewValid
                ? wrongStorage
                    ? new Color(1f, 0.63f, 0.10f, 1f)
                    : new Color(0.28f, 1f, 0.40f, 1f)
                : new Color(1f, 0.22f, 0.18f, 1f);
        }
    }

    public bool EndHotbarWorldDrag(Vector2 screenPosition)
    {
        if (dragPreview == null || dragItem == null)
            return false;

        UpdateHotbarWorldDrag(screenPosition);
        ItemData item = dragItem;
        ShelfGrid grid = previewGrid;
        int column = previewColumn;
        int row = previewRow;
        bool valid = previewValid;
        bool wrongStorage = valid && grid != null && grid.StorageType != item.requiredStorage;
        if (wrongStorage && !ConsumeWrongStorageConfirmation(item, grid))
        {
            valid = false;
            ArmWrongStorageConfirmation(item, grid);
        }
        if (valid)
            Object.Destroy(dragPreview);
        else
            coordinator?.AnimateInvalidDropReturn(dragPreview);
        ClearPreviewState();

        if (!valid || grid == null)
        {
            string message;
            if (wrongStorage)
                message = "! WRONG STORAGE  |  " + StorageLabel(item.requiredStorage).ToUpperInvariant() +
                          "  |  DROP AGAIN TO CONFIRM";
            else if (grid != null)
                message = "! SLOT OCCUPIED  |  BOX RETURNED";
            else
                message = "! OPEN SHELF REQUIRED  |  BOX RETURNED";
            hud?.SetRoomMessage(message, true);
            return false;
        }

        GameObject box = Object.Instantiate(
            item.worldContainerPrefab,
            grid.GetCellWorldPosition(column, row),
            item.worldContainerPrefab.transform.rotation);
        SceneManager.MoveGameObjectToScene(box, scene);

        RestockStorageContainer identity = box.GetComponent<RestockStorageContainer>();
        if (identity == null)
            identity = box.AddComponent<RestockStorageContainer>();
        identity.Bind(item);

        DraggableStorageBox draggable = box.GetComponent<DraggableStorageBox>();
        if (draggable == null)
            draggable = box.AddComponent<DraggableStorageBox>();
        if (!draggable.TryPlaceInitially(grid, column, row))
        {
            Object.Destroy(box);
            string occupied = "! SLOT OCCUPIED  |  BOX RETURNED";
            hud?.SetRoomMessage(occupied, true);
            return false;
        }

        if (!RestockOrderManager.Instance.TryStoreOneContainer(
                item,
                grid.StorageType,
                out string result,
                out string batchID,
                out int expiresDay))
        {
            grid.RemoveObject(box, column, row);
            Object.Destroy(box);
            hud?.SetRoomMessage(result, true);
            return false;
        }


        identity.Bind(
            item,
            batchID,
            expiresDay,
            identity.ContainerID,
            grid.StorageType,
            grid.StorageType != item.requiredStorage);
        RestockOrderManager.Instance.RegisterPhysicalContainer(
            identity,
            grid,
            column,
            row,
            box.transform.eulerAngles.y);

        hud?.SetRoomMessage(result, false);
        return true;
    }

    public void CancelHotbarWorldDrag()
    {
        if (dragPreview != null)
            Object.Destroy(dragPreview);
        ClearPreviewState();
    }

    private void DiscoverSceneObjects()
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            GameObject root = roots[r];
            Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].CompareTag("MainCamera") || roomCamera == null)
                    roomCamera = cameras[i];
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current.name == "DryStockRoomRIg")
                    dryRig = current;
                else if (current.name == "WalkInFreezerRoomRIg")
                    freezerRig = current;
                else if (current.name == "Hopbar")
                    authoredEmptyHotbar = current.gameObject;
            }

            ShelfGrid[] found = root.GetComponentsInChildren<ShelfGrid>(true);
            for (int i = 0; i < found.Length; i++)
            {
                ShelfGrid grid = found[i];
                grid.ConfigureStorageType(IsUnderNamedRoot(grid.transform, "Walk-inFreezer")
                    ? RestockStorageType.Frozen
                    : RestockStorageType.Dry);
                grids.Add(grid);
            }
        }
    }

    private void RefreshStorageContainers()
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            RestockStorageContainer[] containers =
                roots[r].GetComponentsInChildren<RestockStorageContainer>(true);
            for (int i = 0; i < containers.Length; i++)
            {
                RestockStorageContainer container = containers[i];
                if (container == null)
                    continue;
                container.TryResolveLegacyItem();
                container.RefreshExpiryState();
            }
        }
    }

    private void RestoreSavedContainers()
    {
        RestockOrderManager manager = RestockOrderManager.Instance;
        if (manager == null)
            return;
        int recoveryCount = manager.RestorePhysicalContainers(
            scene,
            grids,
            out int relocatedCount);
        if (recoveryCount > 0)
        {
            coordinator?.ShowMessage(
                recoveryCount + " stored box" + (recoveryCount == 1 ? string.Empty : "es") +
                " are safe in recovery because no shelf cell is available.");
        }
        else if (relocatedCount > 0)
        {
            coordinator?.ShowMessage(
                relocatedCount + " stored box" + (relocatedCount == 1 ? " was" : "es were") +
                " moved to the nearest free shelf cell.");
        }
    }

    private void ArmWrongStorageConfirmation(ItemData item, ShelfGrid grid)
    {
        wrongStorageConfirmationItemID = item != null ? item.StableItemId : string.Empty;
        wrongStorageConfirmationShelfID = grid != null ? grid.StableShelfId : string.Empty;
        wrongStorageConfirmationUntil = Time.unscaledTime + 4f;
    }

    private bool ConsumeWrongStorageConfirmation(ItemData item, ShelfGrid grid)
    {
        bool confirmed = item != null && grid != null &&
            Time.unscaledTime <= wrongStorageConfirmationUntil &&
            string.Equals(
                wrongStorageConfirmationItemID,
                item.StableItemId,
                System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                wrongStorageConfirmationShelfID,
                grid.StableShelfId,
                System.StringComparison.Ordinal);
        if (confirmed)
        {
            wrongStorageConfirmationUntil = 0f;
            wrongStorageConfirmationItemID = string.Empty;
            wrongStorageConfirmationShelfID = string.Empty;
        }
        return confirmed;
    }

    private void WireButtons()
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            Button[] buttons = roots[r].GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button.name == "ExitButton")
                {
                    button.onClick.RemoveListener(coordinator.ExitRestockRoom);
                    button.onClick.AddListener(coordinator.ExitRestockRoom);
                }
                else if (button.name == "SwitchRoomToFreezer")
                {
                    switchRoomButton = button;
                    button.onClick.RemoveListener(ToggleRoom);
                    button.onClick.AddListener(ToggleRoom);
                }
            }
        }
    }

    private void ToggleRoom()
    {
        SwitchToRoom(activeRoom == RestockStorageType.Frozen
            ? RestockStorageType.Dry
            : RestockStorageType.Frozen);
    }

    private void SwitchToRoom(RestockStorageType room)
    {
        CancelHotbarWorldDrag();
        activeRoom = room;
        Transform target = room == RestockStorageType.Frozen ? freezerRig : dryRig;
        if (roomCamera != null && target != null)
            roomCamera.transform.SetPositionAndRotation(target.position, target.rotation);
        UpdateSwitchLabel();
        hud?.SetActiveRoom(room);
        hud?.SetRoomMessage(
            room == RestockStorageType.Frozen ? "WALK-IN FREEZER" : "DRY STORAGE",
            false);
    }

    private void UpdateSwitchLabel()
    {
        TMP_Text label = switchRoomButton != null
            ? switchRoomButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (label != null)
            label.text = activeRoom == RestockStorageType.Frozen ? "DRY ROOM" : "FREEZER";
    }

    private void SetNonRoomRootsVisible(bool visible)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            string rootName = roots[i].name;
            // City contains the authored storage-room shell and environment.
            // Only hide the scene's unused character/pedestrian roots.
            if (rootName == "Roles" || rootName == "Pedestrian Route")
                roots[i].SetActive(visible);
        }
    }

    private void ClearPreviewState()
    {
        dragPreview = null;
        dragItem = null;
        previewGrid = null;
        previewColumn = -1;
        previewRow = -1;
        previewValid = false;
        previewOutline = null;
    }

    private static bool IsUnderNamedRoot(Transform current, string rootName)
    {
        while (current != null)
        {
            if (current.name == rootName)
                return true;
            current = current.parent;
        }
        return false;
    }

    private static string StorageLabel(RestockStorageType storage)
    {
        return storage == RestockStorageType.Frozen ? "Freezer" : "Dry Storage";
    }
}
