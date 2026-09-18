using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

public class Booth : MonoBehaviour, Photon.Realtime.IOnEventCallback
{
    private const byte CleanupRequestEvent = 170;
    private MultiplayerTaskClaims cleanupClaims;
    private PlayerMovement cleanupMover;
    private IInteractable cleanupMove;
    private bool cleanupRequested, cleanupCancelled, authorityCleaning;
    private long authorityCleanupLease;
    public bool CleaningPromptOpen { get; private set; }
    public bool NeedsSurfaceCleaning => isDirty || HygieneManager.Instance?.BoothDirt(this) >= .25f;
    public void OpenCleaningPrompt() { if (CanRequestHumanCleanup) { CleaningPromptOpen = true; RefreshCleanUIVisibility(); } }
    public void CloseCleaningPrompt() { CleaningPromptOpen = false; RefreshCleanUIVisibility(); }
    public void CancelHeldCleanup() => CancelHumanCleanup();
    public int CleanupWorkActor { get; private set; }
    public readonly MultiplayerWorkTiming CleanupWorkTiming = new();
    private float cleanupStartedAt = -1f;
    public string CleanupTaskId => "Booth:" + MultiplayerCustomerInteractionBridge.BoothIdentity(this) + ":Cleanup";
    public float HumanCleanupProgress => cleanupStartedAt < 0f ? 0f :
        Mathf.Clamp01((Time.time - cleanupStartedAt) / Mathf.Max(0.05f, MessHoldSeconds));
    public bool HumanCleanupActive => cleanupRequested || cleanupMover != null || cleanupStartedAt >= 0f;
    public bool CanRequestHumanCleanup => NeedsSurfaceCleaning && currentGroup == null && !HasTrayOnTable() && gameObject.activeInHierarchy;
    internal bool CanBusserCleanMultiplayer
    {
        get
        {
            if (!MultiplayerProgressionContext.Ready || !MultiplayerSessionManager.Instance.IsAuthority
                || !NeedsSurfaceCleaning || currentGroup != null || !gameObject.activeInHierarchy || cleanupCommitPending) return false;
            var claims = MultiplayerSessionManager.Instance.GetComponent<MultiplayerTaskClaims>();
            var room = Photon.Pun.PhotonNetwork.CurrentRoom;
            string key = "restaurant.booth.dirty:" + MultiplayerCustomerInteractionBridge.BoothIdentity(this);
            return claims != null && !claims.IsClaimed(CleanupTaskId) && room != null;
        }
    }
    internal bool MultiplayerCleanupCommitPending => cleanupCommitPending;
    private Transform CleanupStand => approachPoint != null ? approachPoint : transform;
    public Transform NetworkTrayPoint => FindTableFoodSpawn();
    private float CleanupRadius
    {
        get
        {
            var drop = FindTableFoodSpawn();
            var tray = drop != null ? drop.GetComponentInChildren<FoodTrayInteractable>(true) : null;
            return tray != null ? tray.GetInteractRadius() : 2.75f;
        }
    }

    public void RequestHumanCleanup()
    {
        if (!MultiplayerProgressionContext.Ready || !CanRequestHumanCleanup) return;
        var session = MultiplayerSessionManager.Instance;
        if (session.LocalManager == null) return;
        if (HumanCleanupActive)
        {
            if (!cleanupRequested && cleanupClaims != null && cleanupClaims.IsClaimedBy(CleanupTaskId, session.LocalActorNumber))
                cleanupClaims.RequestClaim(CleanupTaskId);
            return;
        }
        cleanupClaims = session.GetComponent<MultiplayerTaskClaims>();
        if (cleanupClaims == null) return;
        cleanupClaims.ClaimResult -= OnCleanupClaim;
        cleanupClaims.ClaimResult += OnCleanupClaim;
        cleanupClaims.OwnerChanged -= OnCleanupOwnerChanged;
        cleanupClaims.OwnerChanged += OnCleanupOwnerChanged;
        cleanupCancelled = false;
        cleanupRequested = true;
        if (!cleanupClaims.RequestClaim(CleanupTaskId)) { cleanupRequested = false; CancelHumanCleanup(); }
    }

    // Explicit staff-help choice reuses the same human cleanup path and claim.
    public void RequestHygieneAssistance(PlayerMovement mover)
    {
        CleaningPromptOpen = true;
        if (MultiplayerProgressionContext.Ready) { RequestHumanCleanup(); return; }
        if (mover == null || !CanRequestHumanCleanup || !RestaurantTaskClaim.TryClaimPlayer(this)) return;
        if (!mover.UI_MoveToAction(CleanupStand, CleanupRadius, () =>
        {
            RefreshCleanUIVisibility();
            if (cleanUI == null || !CanRequestHumanCleanup) { RestaurantTaskClaim.ReleasePlayer(this); return; }
            cleanUI.OnPointerDown(null);
            StartCoroutine(WatchHygieneAssistance(mover, mover.CommandVersion));
        }, () => { cleanUI?.OnPointerUp(null); RestaurantTaskClaim.ReleasePlayer(this); }))
            RestaurantTaskClaim.ReleasePlayer(this);
    }

    private System.Collections.IEnumerator WatchHygieneAssistance(PlayerMovement mover, uint version)
    {
        while (NeedsSurfaceCleaning && mover != null && mover.CommandVersion == version && RestaurantTaskClaim.IsClaimedByPlayer(this))
            yield return null;
        if (NeedsSurfaceCleaning) CancelHygieneAssistance();
    }
    public void CancelHygieneAssistance()
    {
        if (!RestaurantTaskClaim.IsClaimedByPlayer(this)) return;
        cleanUI?.OnPointerUp(null);
        RestaurantTaskClaim.ReleasePlayer(this);
    }

    private void OnCleanupOwnerChanged(string id, int owner)
    {
        if (id == CleanupTaskId && owner != MultiplayerSessionManager.Instance.LocalActorNumber && HumanCleanupActive)
            CancelHumanCleanup();
    }

    private void OnCleanupClaim(string id, bool accepted)
    {
        if (id != CleanupTaskId || !cleanupRequested) return;
        cleanupRequested = false;
        if (!accepted) { CancelHumanCleanup(); return; }
        var session = MultiplayerSessionManager.Instance;
        if (cleanupCancelled || !CanRequestHumanCleanup || session.LocalManager == null)
        { CancelHumanCleanup(); return; }
        var mover = session.LocalManager.GetComponent<PlayerMovement>();
        if (mover == null || !mover.isActiveAndEnabled) { CancelHumanCleanup(); return; }
        cleanupMover = mover;
        bool started = mover.UI_MoveToAction(CleanupStand, CleanupRadius, () =>
        {
            cleanupMove = null;
            if (!CanRequestHumanCleanup || cleanupCancelled || !cleanupClaims.IsClaimedBy(id, session.LocalActorNumber))
            { CancelHumanCleanup(); return; }
            cleanupStartedAt = Time.time;
            if (session.IsAuthority) BeginAuthorityCleanup(session.LocalActorNumber, cleanupClaims.GetLease(CleanupTaskId));
            else if (!MultiplayerWire.Raise(CleanupRequestEvent,
                new object[] { MultiplayerCustomerInteractionBridge.BoothIdentity(this), cleanupClaims.GetLease(CleanupTaskId) },
                new Photon.Realtime.RaiseEventOptions { Receivers = Photon.Realtime.ReceiverGroup.MasterClient },
                ExitGames.Client.Photon.SendOptions.SendReliable)) CancelHumanCleanup();
        }, () => { cleanupMover = null; cleanupMove = null; CancelHumanCleanup(); });
        if (started) cleanupMove = mover.CurrentTarget;
        else CancelHumanCleanup();
    }

    private void CancelHumanCleanup()
    {
        cleanupCancelled = true;
        cleanupStartedAt = -1f;
        var mover = cleanupMover;
        var move = cleanupMove;
        cleanupMover = null;
        cleanupMove = null;
        if (mover != null && move != null && ReferenceEquals(mover.CurrentTarget, move)) mover.CancelLockedTask();
        var session = MultiplayerSessionManager.Instance;
        if (session != null && cleanupClaims != null && cleanupClaims.IsClaimedBy(CleanupTaskId, session.LocalActorNumber))
            cleanupClaims.Release(CleanupTaskId);
    }

    public void OnEvent(ExitGames.Client.Photon.EventData data)
    {
        if (data.Code == CleanupRequestEvent && MultiplayerWire.TryRead(data, out var payload) && payload is object[] args
            && args.Length == 2 && args[0] is string id && args[1] is long lease
            && id == MultiplayerCustomerInteractionBridge.BoothIdentity(this)) BeginAuthorityCleanup(data.Sender, lease);
    }

    private bool ValidateCleanupActor(int actor)
    {
        var session = MultiplayerSessionManager.Instance;
        var room = Photon.Pun.PhotonNetwork.CurrentRoom;
        if (session == null || !session.IsMultiplayerSession || !session.IsAuthority || !CanRequestHumanCleanup
            || room == null || !room.Players.TryGetValue(actor, out var player) || player.IsInactive
            || !session.GetComponent<MultiplayerTaskClaims>().IsClaimedBy(CleanupTaskId, actor)
            || !session.TryGetManager(actor, out var manager) || manager == null || !manager.activeInHierarchy) return false;
        string key = "restaurant.booth.dirty:" + MultiplayerCustomerInteractionBridge.BoothIdentity(this);
        if (!HygieneManager.HandsEmpty(manager.GetComponent<PlayerMovement>())) return false;
        Vector3 offset = manager.transform.position - CleanupStand.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= CleanupRadius * CleanupRadius;
    }

    private void BeginAuthorityCleanup(int actor, long lease)
    {
        if (HygieneManager.Defer(() => { if (this != null) BeginAuthorityCleanup(actor, lease); })) return;
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsAuthority || lease <= 0 || authorityCleaning && authorityCleanupLease == lease
            || session.GetComponent<MultiplayerTaskClaims>().GetLease(CleanupTaskId) != lease) return;
        authorityCleaning = true; authorityCleanupLease = lease;
        StartCoroutine(CompleteHumanCleanup(actor, lease));
    }

    private System.Collections.IEnumerator CompleteHumanCleanup(int actor, long lease)
    {
        var session = MultiplayerSessionManager.Instance;
        var claims = session.GetComponent<MultiplayerTaskClaims>();
        bool OwnsLease() => this != null && authorityCleanupLease == lease && claims != null
            && claims.GetLease(CleanupTaskId) == lease && claims.IsClaimedBy(CleanupTaskId, actor);
        float deadline = HygieneManager.ServiceTime + .4f;
        while (OwnsLease() && !ValidateCleanupActor(actor) && HygieneManager.ServiceTime < deadline) yield return null;
        if (!OwnsLease()) { if (authorityCleanupLease == lease) authorityCleaning = false; yield break; }
        CleanupWorkActor = actor;
        CleanupWorkTiming.Begin(Mathf.Max(0.05f, MessHoldSeconds));
        float elapsed = 0f;
        while (elapsed < Mathf.Max(0.05f, MessHoldSeconds) && OwnsLease() && ValidateCleanupActor(actor))
        { elapsed += Time.deltaTime; yield return null; }
        if (elapsed >= Mathf.Max(0.05f, MessHoldSeconds) && OwnsLease() && ValidateCleanupActor(actor)
            && TryCommitMultiplayerCleanup())
            while (cleanupCommitPending && MultiplayerProgressionContext.Ready) yield return null;
        if (authorityCleanupLease == lease) { CleanupWorkTiming.Clear(); CleanupWorkActor = 0; authorityCleaning = false; }
        if (session != null && session.IsAuthority && claims.GetLease(CleanupTaskId) == lease)
            session.GetComponent<MultiplayerTaskClaims>().CompleteOnAuthority(CleanupTaskId, actor);
    }

    private void OnEnable() => Photon.Pun.PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable()
    {
        CleanupWorkTiming.Clear();
        CleanupWorkActor = 0;
        Photon.Pun.PhotonNetwork.RemoveCallbackTarget(this);
        CancelHumanCleanup();
        // A pending claim callback stays subscribed to release a late grant.
        if (!cleanupRequested && cleanupClaims != null) cleanupClaims.ClaimResult -= OnCleanupClaim;
        if (cleanupClaims != null) cleanupClaims.OwnerChanged -= OnCleanupOwnerChanged;
    }
    [Header("Approach / Seating")]
    public Transform approachPoint;
    public List<Transform> seats = new List<Transform>(4);

    [Header("Navigation")]
    [SerializeField] private bool carveBoothFromNavMesh = true;
    [SerializeField, Min(0.25f)] private float approachSampleRadius = 1.5f;

    [Header("Facing")]
    public Transform tableLookTarget;
    public float seatYawOffset = 0f;

    [Header("Table Props - Menu Book")]
    public GameObject menuBookPrefab;
    public Transform menuSpawnPoint;
    public bool parentMenuToSpawnPoint = true;

    [Header("Table Props - Other")]
    public Transform tableNumberAnchor;

    [Header("Messy Customer")]
    [SerializeField] private GameObject puddle;
    [SerializeField] private GameObject cleanUIRoot;
    [SerializeField] private BoothMessCleanUI cleanUI;
    [SerializeField] private float messHoldSeconds = 1.25f;
    [SerializeField] private float messAppearDelayAfterEating = 0.5f;
    [SerializeField] private bool isDirty;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    [Header("Runtime")]
    [SerializeField] private GameObject menuInstance;
    [SerializeField] private CustomerGroup currentGroup;

    private bool messSpawnedForCurrentGroup;
    private float eatingTimer = -1f;
    private float nextDirtySnapshot;
    private string dirtySnapshotKey;
    private bool cleanupCommitPending;

    // Completion boundary for future validated Player/Busser routines. Returning
    // true means queued; dirty state stays intact until the server confirms zero.
    internal bool TryCommitMultiplayerCleanup()
    {
        if (!MultiplayerProgressionContext.Ready || !MultiplayerSessionManager.Instance.IsAuthority
            || !NeedsSurfaceCleaning || currentGroup != null || cleanupCommitPending) return false;
        var room = Photon.Pun.PhotonNetwork.CurrentRoom;
        if (!isDirty) { HygieneManager.Instance?.CleanDining(this); RefreshCleanUIVisibility(); return true; }
        dirtySnapshotKey ??= "restaurant.booth.dirty:" + MultiplayerCustomerInteractionBridge.BoothIdentity(this);
        if (room == null || room.CustomProperties[dirtySnapshotKey] is not int flags || (flags & 1) == 0)
            return false;
        cleanupCommitPending = room.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable { [dirtySnapshotKey] = 0 },
            new ExitGames.Client.Photon.Hashtable { [dirtySnapshotKey] = flags });
        return cleanupCommitPending;
    }

    private void ApplyCompletedMultiplayerCleanup()
    {
        cleanupCommitPending = false;
        if (!isDirty) return;
        isDirty = false;
        HygieneManager.Instance?.CleanDining(this);
        var drop = FindTableFoodSpawn();
        var tray = drop != null ? drop.GetComponentInChildren<FoodTray>(true) : null;
        if (tray != null)
        {
            // Detach immediately so availability no longer sees a tray awaiting
            // Unity's end-of-frame Destroy. Repeated mirrors cannot find it again.
            tray.gameObject.SetActive(false);
            tray.transform.SetParent(null, true);
            Destroy(tray.gameObject);
        }
        ApplyDirtyVisuals();
        RefreshCleanUIVisibility();
    }

    public void ResetForMultiplayerDay()
    {
        if (!MultiplayerDayBridge.IsActive) return;
        CancelHumanCleanup();
        currentGroup = null;
        messSpawnedForCurrentGroup = false;
        eatingTimer = -1f;
        ClearBoothProps();
        ApplyCompletedMultiplayerCleanup();
        if (MultiplayerSessionManager.Instance.IsAuthority && Photon.Pun.PhotonNetwork.CurrentRoom != null)
        {
            dirtySnapshotKey ??= "restaurant.booth.dirty:" + MultiplayerCustomerInteractionBridge.BoothIdentity(this);
            Photon.Pun.PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [dirtySnapshotKey] = 0 });
        }
    }

    public bool MultiplayerCleaningBlocked => MultiplayerRestaurantBridge.IsObserver && NeedsSurfaceCleaning;

    public CustomerGroup CurrentGroup => currentGroup;
    public bool IsDirty => isDirty;
    public float MessHoldSeconds => messHoldSeconds;
    public bool CanCleanMessNow => !MultiplayerCleaningBlocked && CanRequestHumanCleanup;

    private void Awake()
    {
        HygieneManager.Instance?.RegisterBooth(this);
        MultiplayerWorldRegistry.Track(this);
        EnsureNavigationObstacle();

        if (cleanUI == null && cleanUIRoot != null)
            cleanUI = cleanUIRoot.GetComponentInChildren<BoothMessCleanUI>(true);

        if (cleanUI != null)
            cleanUI.Setup(this, FindSceneCamera());

        ApplyDirtyVisuals();
        RefreshCleanUIVisibility();
    }

    private void Update()
    {
        if (HumanCleanupActive && (!MultiplayerProgressionContext.Ready || !CanRequestHumanCleanup
            || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)
            || (!cleanupRequested && cleanupClaims != null && !cleanupClaims.IsClaimedBy(CleanupTaskId, MultiplayerSessionManager.Instance.LocalActorNumber))
            || (cleanupMover != null && (!cleanupMover.isActiveAndEnabled
                || cleanupMover.gameObject != MultiplayerSessionManager.Instance.LocalManager
                || (cleanupMove != null && !ReferenceEquals(cleanupMover.CurrentTarget, cleanupMove))
                || (cleanupStartedAt >= 0f && cleanupMover.CurrentTarget != null))))) CancelHumanCleanup();
        RefreshMultiplayerDirtyState();
        TrackMessyCustomerSpill();
        RefreshCleanUIVisibility();
    }

    public void SetCurrentGroup(CustomerGroup g)
    {
        if (g != null) { CleaningPromptOpen = false; CancelHumanCleanup(); }
        currentGroup = g;
        messSpawnedForCurrentGroup = false;
        eatingTimer = -1f;

        if (cleanUI != null)
            cleanUI.Setup(this, FindSceneCamera());

        if (debugLogs)
        {
            string typeName = g != null ? g.CurrentCustomerType.ToString() : "NULL";
            bool messy = g != null && g.IsMessy;
            string stateName = g != null ? g.state.ToString() : "NULL";

            Debug.Log($"[Booth] {name} SetCurrentGroup -> {(g != null ? g.name : "NULL")} | type={typeName} | messy={messy} | state={stateName}", this);
        }

        RefreshCleanUIVisibility();
    }

    public void ClearCurrentGroup()
    {
        if (debugLogs)
            Debug.Log($"[Booth] {name} ClearCurrentGroup", this);

        currentGroup = null;
        eatingTimer = -1f;

        RefreshCleanUIVisibility();
    }

    public bool IsAvailableFor(int groupSize)
    {
        if (HumanCleanupActive || IsAutomatedMessCleaning || RestaurantTaskClaim.IsClaimedByBot(this)
            || RestaurantTaskClaim.IsClaimedByPlayer(this) || HygieneManager.Instance?.IsBoothQueued(this) == true) return false;
        if (isDirty) return false;
        if (HasTrayOnTable()) return false;

        if (approachPoint == null) return false;
        if (seats == null || seats.Count < groupSize) return false;
        if (currentGroup != null) return false;

        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i] == null) continue;
            if (SeatAnchor.IsSeatOccupied(seats[i])) return false;
        }

        return true;
    }

    public Transform GetSeat(int index)
    {
        if (seats == null) return null;
        if (index < 0 || index >= seats.Count) return null;
        return seats[index];
    }

    public Vector3 GetNavigableApproachPosition()
    {
        Vector3 desired = approachPoint != null ? approachPoint.position : transform.position;

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, approachSampleRadius, NavMesh.AllAreas))
            return hit.position;

        return desired;
    }

    public Quaternion GetSeatedRotation(Vector3 seatPos)
    {
        Vector3 dir = tableLookTarget != null ? tableLookTarget.position - seatPos : transform.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

        return Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, seatYawOffset, 0f);
    }

    public void SpawnMenuBook()
    {
        if (menuSpawnPoint == null || menuBookPrefab == null) return;

        if (menuInstance == null)
            menuInstance = FindExistingMenu();

        if (menuInstance != null) return;

        menuInstance = parentMenuToSpawnPoint
            ? Instantiate(menuBookPrefab, menuSpawnPoint.position, menuSpawnPoint.rotation, menuSpawnPoint)
            : Instantiate(menuBookPrefab, menuSpawnPoint.position, menuSpawnPoint.rotation);
    }

    public void ClearMenuBook()
    {
        if (menuInstance == null)
            menuInstance = FindExistingMenu();

        if (menuInstance != null)
        {
            Destroy(menuInstance);
            menuInstance = null;
        }
    }

    public void ClearBoothProps()
    {
        ClearMenuBook();
    }

    public void SetDirty(bool value)
    {
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer &&
            !MultiplayerSessionManager.Instance.IsAuthority) return;
        if (MultiplayerRestaurantBridge.IsActive && !value)
        {
            dirtySnapshotKey ??= "restaurant.booth.dirty:" + MultiplayerCustomerInteractionBridge.BoothIdentity(this);
            Photon.Pun.PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [dirtySnapshotKey] = 0 });
        }
        if (isDirty == value)
            return;

        isDirty = value;
        if (value) HygieneManager.Instance?.RecordDiningUse(this, .15f);
        else HygieneManager.Instance?.CleanDining(this);

        if (debugLogs)
            Debug.Log($"[Booth] {name} SetDirty = {isDirty}", this);

        ApplyDirtyVisuals();
        RefreshCleanUIVisibility();
    }

    public void CleanMess()
    {
        SetDirty(false);
        HygieneManager.Instance?.CleanDining(this);
        CleaningPromptOpen = false;
    }

    public bool BeginAutomatedMessCleaning()
    {
        if (MultiplayerCleaningBlocked) return false;
        CleaningPromptOpen = true;
        RefreshCleanUIVisibility();
        return cleanUI != null && cleanUI.BeginAutomatedCleaning();
    }

    public bool IsAutomatedMessCleaning => cleanUI != null && cleanUI.IsAutomatedCleaning;

    public void CancelAutomatedMessCleaning()
    {
        cleanUI?.CancelAutomatedCleaning();
    }

    public void OnTableCleaned()
    {
        CleanMess();
    }

    public void ForceDirtyForTest()
    {
        SetDirty(true);
    }

    public void ArmTrayCleaningForCurrentGroup()
    {
        ArmTrayCleaningForGroup(currentGroup);
    }

    public void ArmTrayCleaningForGroup(CustomerGroup group)
    {
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer) return;
        var drop = FindTableFoodSpawn();
        if (drop == null) return;

        var tray = drop.GetComponentInChildren<FoodTray>(true);
        if (tray == null) return;

        if (TryInvokeComponentMethod(tray, "TrayHoldToClean", "Arm", this, group)) return;
        if (TryInvokeComponentMethod(tray, "TrayCleanable", "ArmForCleaning", this)) return;
    }

    private void TrackMessyCustomerSpill()
    {
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer && !MultiplayerSessionManager.Instance.IsAuthority) return;
        if (currentGroup == null)
            return;

        if (!currentGroup.IsMessy)
            return;

        if (messSpawnedForCurrentGroup)
            return;

        if (currentGroup.state != CustomerGroup.GroupState.Eating)
        {
            eatingTimer = -1f;
            return;
        }

        if (eatingTimer < 0f)
            eatingTimer = 0f;

        eatingTimer += Time.deltaTime;

        if (eatingTimer < messAppearDelayAfterEating)
            return;

        messSpawnedForCurrentGroup = true;
        SetDirty(true);

        if (debugLogs)
            Debug.Log($"[Booth] {name} spawned spill because messy group started eating.", this);
    }

    private void ApplyDirtyVisuals()
    {
        if (puddle != null)
            puddle.SetActive(isDirty);
    }

    private void RefreshCleanUIVisibility()
    {
        if (cleanUIRoot == null)
            return;

        bool show = ShouldShowCleanUI();
        if (show && MultiplayerDayBridge.IsActive) MultiplayerTaskPresentation.Bind(cleanUIRoot, CleanupTaskId);
        show &= !MultiplayerRestockView.Active;
        cleanUIRoot.SetActive(show);

        if (cleanUI != null && cleanUI.gameObject.activeSelf != show)
            cleanUI.gameObject.SetActive(show);
    }

    private bool ShouldShowCleanUI()
    {
        if (HygieneManager.Instance != null && !CleaningPromptOpen && !HumanCleanupActive && !IsAutomatedMessCleaning) return false;
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer) return CanRequestHumanCleanup;
        return CanCleanMessNow;
    }

    private void RefreshMultiplayerDirtyState()
    {
        if (!MultiplayerProgressionContext.Ready || Time.unscaledTime < nextDirtySnapshot) return;
        nextDirtySnapshot = Time.unscaledTime + 0.5f;
        var room = Photon.Pun.PhotonNetwork.CurrentRoom;
        if (room == null) return;
        dirtySnapshotKey ??= "restaurant.booth.dirty:" + MultiplayerCustomerInteractionBridge.BoothIdentity(this);
        int flags = room.CustomProperties[dirtySnapshotKey] is int saved ? saved : 0;
        bool authority = MultiplayerSessionManager.Instance.IsAuthority;
        if (room.CustomProperties[dirtySnapshotKey] is int confirmed && confirmed == 0 && currentGroup == null)
        {
            ApplyCompletedMultiplayerCleanup();
            return;
        }
        if (cleanupCommitPending) return;
        if (authority)
        {
            // Dirty state belongs to this booth, not to the departing customer.
            if (isDirty) flags |= 1;
            if (currentGroup != null && currentGroup.MultiplayerPaymentComplete) flags |= 3;
            if (flags != 0 && (!(room.CustomProperties[dirtySnapshotKey] is int previous) || previous != flags))
                room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [dirtySnapshotKey] = flags });
        }
        if ((flags & 1) == 0) return;
        if (!isDirty)
        {
            isDirty = true; // Observer mirror: visuals/availability only, no gameplay callbacks.
            ApplyDirtyVisuals();
            RefreshCleanUIVisibility();
        }
        if ((flags & 2) == 0) return;
        var drop = FindTableFoodSpawn();
        var tray = drop != null ? drop.GetComponentInChildren<FoodTray>(true) : null;
        var pickup = tray != null ? tray.GetComponent<FoodTrayInteractable>() : null;
        if (pickup == null && tray != null) pickup = tray.GetComponentInParent<FoodTrayInteractable>();
        if (pickup != null && !pickup.IsCleanupPickable) pickup.SetCleanupPickable(true);
        // The served tray already belongs to the table. Never instantiate dishes
        // or depend on its CustomerGroup reference to retain this state.
    }

    private Camera FindSceneCamera()
    {
        Camera main = Camera.main;
        if (main != null)
            return main;

        GameObject tagged = GameObject.FindGameObjectWithTag("RoomCamera");
        if (tagged != null)
            return tagged.GetComponent<Camera>();

        return FindFirstObjectByType<Camera>();
    }

    private void EnsureNavigationObstacle()
    {
        if (!carveBoothFromNavMesh)
            return;

        BoxCollider solidCollider = GetComponent<BoxCollider>();
        if (solidCollider == null || solidCollider.isTrigger)
        {
            Debug.LogWarning($"[Booth] {name} cannot carve navigation because its root BoxCollider is missing or is a trigger.", this);
            return;
        }

        NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle == null)
            obstacle = gameObject.AddComponent<NavMeshObstacle>();

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = solidCollider.center;
        obstacle.size = solidCollider.size;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.carvingMoveThreshold = 0.05f;
        obstacle.carvingTimeToStationary = 0.1f;
    }

    private GameObject FindExistingMenu()
    {
        if (menuSpawnPoint == null) return null;

        if (menuSpawnPoint.childCount > 0)
        {
            for (int i = 0; i < menuSpawnPoint.childCount; i++)
            {
                var child = menuSpawnPoint.GetChild(i);
                if (child == null) continue;

                if (menuBookPrefab != null && child.name.StartsWith(menuBookPrefab.name))
                    return child.gameObject;
            }

            return menuSpawnPoint.GetChild(0).gameObject;
        }

        return null;
    }

    private Transform FindTableFoodSpawn()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "TableFoodSpawn")
                return t;
        }
        return null;
    }

    private bool HasTrayOnTable()
    {
        var drop = FindTableFoodSpawn();
        if (drop == null) return false;

        return drop.GetComponentInChildren<FoodTray>(true) != null;
    }

    private static bool TryInvokeComponentMethod(Component host, string componentName, string methodName, params object[] args)
    {
        if (host == null) return false;

        var comp = host.GetComponent(componentName);
        if (comp == null) return false;

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var methods = comp.GetType().GetMethods(flags);

        for (int i = 0; i < methods.Length; i++)
        {
            var method = methods[i];
            if (method.Name != methodName) continue;

            var parameters = method.GetParameters();
            if (parameters.Length != args.Length) continue;

            method.Invoke(comp, args);
            return true;
        }

        return false;
    }
}
