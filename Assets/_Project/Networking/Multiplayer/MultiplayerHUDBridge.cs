using UnityEngine;
using UnityEngine.SceneManagement;

// One scene-owned binding to the existing persistent Lobby HUD, never one HUD per avatar.
public sealed class MultiplayerHUDBridge : MonoBehaviour
{
    private const string TaskSource = "multiplayer.local";
    private bool bound;
    private float nextGuidanceRefresh;
    public static bool IsActive => MultiplayerDayBridge.IsActive
        && (SceneManager.GetActiveScene().name == "Lobby1 Multiplayer"
            || SceneManager.GetActiveScene().name == FastFoodScene.Multiplayer
            || SceneManager.GetActiveScene().name == FineDiningScene.Multiplayer);
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
        if (Time.unscaledTime < nextGuidanceRefresh) return;
        nextGuidanceRefresh = Time.unscaledTime + 0.1f;
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
        var claims = session.GetComponent<MultiplayerTaskClaims>();
        string task = claims != null ? claims.GetTaskForActor(session.LocalActorNumber) : null;
        if (session.CanAct && task != null) detail = claims.Describe(task) + " — " + detail;
        string icon=null,shortAction=null,destination=null,table=null;
        var hands=session.LocalManager!=null?session.LocalManager.GetComponent<WaiterHands>():null;
        if(session.CanAct && hands!=null && activity.phase!=MultiplayerActorActivity.Phase.AwaitingAuthority && activity.phase!=MultiplayerActorActivity.Phase.Reviewing) {
            CustomerGroup group=null;
            if(hands.HasMoney){icon="IconCash";shortAction="Take payment";destination="To cashier";group=hands.HeldMoney!=null?hands.HeldMoney.TargetGroup:hands.holdingMoneyFor;}
            else if(hands.HasBill){icon="IconReceipt";shortAction="Give bill";destination="To customer";group=hands.holdingBillFor;}
            else if(hands.HasTray){icon="IconTable";shortAction="Serve food";destination="To customer";group=hands.holdingTray!=null?hands.holdingTray.TargetGroup:null;}
            if(group!=null)table=group.CaptureInspection().TableLabel;
        }
        PlayerTaskGuidance.SetTask(TaskSource, task ?? action, action, detail, 10000,
            activity.target, PlayerTaskCategory.Service,icon,shortAction,destination,table);
    }

    private void OnDestroy()
    {
        PlayerTaskGuidance.ClearTask(TaskSource);
        CasualDiningProgressHUD.Instance?.RefreshSceneVisibility();
        PlayerTaskHUD.Instance?.RefreshSceneVisibility();
    }
}
