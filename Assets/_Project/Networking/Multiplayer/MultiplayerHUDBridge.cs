using UnityEngine;
using UnityEngine.SceneManagement;

// One scene-owned binding to the existing persistent Lobby HUD, never one HUD per avatar.
public sealed class MultiplayerHUDBridge : MonoBehaviour
{
    private const string TaskSource = "multiplayer.local";
    private bool bound;
    public static bool IsActive => MultiplayerDayBridge.IsActive
        && SceneManager.GetActiveScene().name == "Lobby1 Multiplayer";
    public static ManagerPlayer LocalPlayer => IsActive
        ? MultiplayerSessionManager.Instance.LocalManager?.GetComponent<ManagerPlayer>() : null;

    private void Update()
    {
        if (!IsActive)
        {
            PlayerTaskGuidance.ClearTask(TaskSource);
            return;
        }
        if (!bound)
        {
            var root = LobbyHUDRoot.EnsureInstance();
            if (root == null) return;
            root.GetComponentInChildren<LobbyHUDRedesign>(true)?.RefreshVisibility();
            CasualDiningProgressHUD.Instance?.RefreshSceneVisibility();
            PlayerTaskHUD.Instance?.RefreshSceneVisibility();
            bound = true;
        }
        var local = MultiplayerSessionManager.Instance.LocalManager;
        var movement = local != null ? local.GetComponent<PlayerMovement>() : null;
        var hands = local != null ? local.GetComponent<WaiterHands>() : null;
        var target = movement != null ? movement.LockedTarget ?? movement.CurrentTarget : null;
        string action = "Choose a task";
        string detail = "Interact with a customer or the restaurant computer.";
        if (hands != null && hands.HasTray)
        {
            action = "Deliver order #" + hands.holdingTray.orderNumber;
            detail = "Take your tray to its customer's table.";
        }
        else if (target is Component component)
        {
            action = "Current interaction";
            detail = component.gameObject.name;
        }
        else if (GameDayManager.Instance != null && GameDayManager.Instance.HasDayResults)
        {
            action = "Shift ended";
            detail = "The restaurant is closed.";
        }
        else if (GameDayManager.Instance != null && !GameDayManager.Instance.ServiceActive)
        {
            action = "Prepare the restaurant";
            detail = "Any Player can request Start Day at the computer.";
        }
        PlayerTaskGuidance.SetTask(TaskSource, action, action, detail, 10000,
            target as UnityEngine.Object, PlayerTaskCategory.Service);
    }

    private void OnDestroy()
    {
        PlayerTaskGuidance.ClearTask(TaskSource);
        CasualDiningProgressHUD.Instance?.RefreshSceneVisibility();
        PlayerTaskHUD.Instance?.RefreshSceneVisibility();
    }
}
