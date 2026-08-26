using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerTaskRestockSource : MonoBehaviour
{
    private const string SourceId = "restock";

    [SerializeField, Min(0.05f)] private float refreshInterval = 0.15f;
    [SerializeField] private int taskPriority = 40;

    private float refreshTimer;

    private void OnEnable()
    {
        refreshTimer = 0f;
        RefreshTask();
    }

    private void OnDisable()
    {
        PlayerTaskGuidance.ClearTask(SourceId);
    }

    private void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval)
            return;

        refreshTimer = 0f;
        RefreshTask();
    }

    private void RefreshTask()
    {
        if (!IsCasualDiningSceneLoaded())
        {
            PlayerTaskGuidance.ClearTask(SourceId);
            return;
        }

        RestockOrderManager manager = RestockOrderManager.Instance;
        if (manager == null)
        {
            PlayerTaskGuidance.ClearTask(SourceId);
            return;
        }

        if (manager.DeliveredContainerCount > 0)
        {
            int count = manager.DeliveredContainerCount;
            SetTask(
                "collect_delivery",
                "COLLECT DELIVERY  >  TRUCK",
                BoxCount(count));
            return;
        }

        int dry = manager.GetHotbarContainerCount(RestockStorageType.Dry);
        int frozen = manager.GetHotbarContainerCount(RestockStorageType.Frozen);
        if (dry <= 0 && frozen <= 0)
        {
            PlayerTaskGuidance.ClearTask(SourceId);
            return;
        }

        RestockFlowCoordinator coordinator = RestockFlowCoordinator.Instance;
        if (coordinator != null && coordinator.IsRestockRoomOpen)
        {
            RestockStorageType room = coordinator.ActiveStorageRoom;
            if (room == RestockStorageType.Dry && dry > 0)
            {
                SetTask("store_dry", "STORE BOXES  >  DRY SHELVES", BoxCount(dry));
                return;
            }

            if (room == RestockStorageType.Frozen && frozen > 0)
            {
                SetTask("store_frozen", "STORE BOXES  >  FREEZER", BoxCount(frozen));
                return;
            }

            if (dry > 0)
            {
                SetTask("switch_dry", "GO TO DRY STORAGE", BoxCount(dry));
                return;
            }

            SetTask("switch_frozen", "GO TO FREEZER", BoxCount(frozen));
            return;
        }

        if (dry > 0)
            SetTask("enter_dry", "GO TO DRY STORAGE", BoxCount(dry));
        else
            SetTask("enter_frozen", "GO TO FREEZER", BoxCount(frozen));
    }

    private void SetTask(string key, string action, string detail)
    {
        PlayerTaskGuidance.SetTask(
            SourceId,
            key,
            action,
            detail,
            taskPriority,
            RestockOrderManager.Instance,
            PlayerTaskCategory.Restock);
    }

    private static string BoxCount(int count)
    {
        return count + (count == 1 ? " BOX LEFT" : " BOXES LEFT");
    }

    private static bool IsCasualDiningSceneLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            string sceneName = SceneManager.GetSceneAt(i).name;
            if (sceneName == "Lobby1" || sceneName == "RestockScene")
                return true;
        }

        return false;
    }
}
