using UnityEngine;

// Claim/popup only. Actual greet and seating commands belong to a later pass.
public class MultiplayerCustomerInteractionBridge : MonoBehaviour
{
    private MultiplayerSessionManager session;
    private MultiplayerTaskClaims claims;
    private MultiplayerCustomerSpawn target;
    private Camera cameraForPopup;
    private string taskId;
    private bool pending, cancelled;

    public static bool TryHandle(RoleBasedAssignController controller, CustomerGroup group, Camera camera)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession) return false;
        if (session.LocalManager != controller.gameObject) return true;
        var bridge = session.GetComponent<MultiplayerCustomerInteractionBridge>();
        if (bridge == null) bridge = session.gameObject.AddComponent<MultiplayerCustomerInteractionBridge>();
        bridge.Begin(group.GetComponentInParent<MultiplayerCustomerSpawn>(), camera);
        return true;
    }

    public static bool BlockUnnetworkedAction(CustomerGroup group)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession) return false;
        WarningSlideUI.Instance?.Show("Greet/seating actions are not networked yet. Escape or right-click cancels.");
        return true;
    }

    private void Awake()
    {
        session = GetComponent<MultiplayerSessionManager>();
        claims = GetComponent<MultiplayerTaskClaims>();
        claims.ClaimResult += OnResult;
    }

    private void Begin(MultiplayerCustomerSpawn customer, Camera camera)
    {
        if (pending) return;
        Cancel();
        if (customer == null || !customer.ReadyForInteraction) return;
        target = customer;
        taskId = $"Customer:{customer.photonView.ViewID}:GreetSeat";
        cameraForPopup = camera;
        cancelled = false;
        pending = true;
        if (!claims.RequestClaim(taskId)) { pending = false; Cancel(); }
    }

    private void OnResult(string id, bool accepted)
    {
        if (!pending || id != taskId) return;
        pending = false;
        if (!accepted) { taskId = null; target = null; return; }
        if (cancelled || target == null || !target.ReadyForInteraction || session.LocalManager == null)
        { Cancel(); return; }
        if (claims.IsClaimedBy(id, session.LocalActorNumber))
            CustomerGreetBubbleSpawner.Instance?.Show(target.Group, cameraForPopup);
    }

    private void Update()
    {
        if (taskId == null) return;
        if (!session.IsMultiplayerSession || session.LocalManager == null || target == null
            || !target.ReadyForInteraction || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)
            || (!pending && !claims.IsClaimedBy(taskId, session.LocalActorNumber))) Cancel();
    }

    public void Cancel()
    {
        cancelled = true;
        if (taskId == null) return;
        CustomerGreetBubbleSpawner.Instance?.Hide();
        if (claims.IsClaimedBy(taskId, session.LocalActorNumber)) claims.Release(taskId);
        // Retain a cancelled request until its response so a late grant is released.
        if (!pending) { taskId = null; target = null; }
    }

    private void OnDestroy()
    {
        Cancel();
        if (claims != null) claims.ClaimResult -= OnResult;
    }
}
