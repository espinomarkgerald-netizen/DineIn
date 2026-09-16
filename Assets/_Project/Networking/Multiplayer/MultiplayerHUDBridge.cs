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
        var session = MultiplayerSessionManager.Instance;
        var activity = MultiplayerActorActivity.Read(session.LocalManager);
        string action = activity.action, detail = activity.detail;
        if (!session.CanAct)
        {
            action = session.Ended ? "Run ended" : "Synchronizing restaurant";
            detail = session.Status;
        }
        else if (activity.phase == MultiplayerActorActivity.Phase.Idle && GameDayManager.Instance != null)
        {
            if (GameDayManager.Instance.HasDayResults)
            { action = "Shift ended"; detail = "The restaurant is closed."; }
            else if (!GameDayManager.Instance.ServiceActive)
            { action = "Prepare the restaurant"; detail = "The host requests Start Day at the computer; guests confirm Ready in the popup."; }
        }
        PlayerTaskGuidance.SetTask(TaskSource, action, action, detail, 10000,
            activity.target, PlayerTaskCategory.Service);
    }

    private void OnDestroy()
    {
        PlayerTaskGuidance.ClearTask(TaskSource);
        CasualDiningProgressHUD.Instance?.RefreshSceneVisibility();
        PlayerTaskHUD.Instance?.RefreshSceneVisibility();
    }
}
