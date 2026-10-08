using System;
using UnityEngine;

public class BusserHands : MonoBehaviour
{
    public static BusserHands Instance { get; private set; }

    public static BusserHands ActivePlayerHands
    {
        get
        {
            var session = MultiplayerSessionManager.Instance;
            if (session != null && session.IsMultiplayerSession)
                return session.LocalManager?.GetComponent<BusserHands>();

            if (ManagerPlayer.Active != null)
            {
                BusserHands managerHands = ManagerPlayer.Active.GetComponent<BusserHands>();
                if (managerHands != null)
                    return managerHands;
            }

            return Instance;
        }
    }

    public static event Action OnHandsStateChanged;

    [Header("Holding")]
    public FoodTray holdingTray;

    [Header("Hold Points")]
    [SerializeField] private Transform trayHoldPoint;

    [Header("Trolley")]
    [Tooltip("Editable point the trolley handle aligns to while this character pushes it. Falls back to Tray Hold Point.")]
    [SerializeField] private Transform trolleyGripPoint;

    [Header("Tray hand alignment")]
    [SerializeField] private bool alignHandsToTray;
    [Tooltip("Hand contact offset from the tray's authored grips, in tray-local units.")]
    [SerializeField] private Vector3 trayGripOffset = new Vector3(0f, -.05f, -.5f);
    private Animator carryAnimator;
    private Avatar carryAvatar;
    private Transform leftUpper, leftLower, leftHand, rightUpper, rightLower, rightHand;
    private FoodTray posedTray;
    private FoodTrayCarryPose trayPose;

    private void LateUpdate()
    {
        if (!alignHandsToTray || !HasTray) return;
        if (carryAnimator == null) carryAnimator = GetComponentInChildren<Animator>();
        if (carryAnimator == null || !carryAnimator.isHuman) return;
        if (posedTray != holdingTray)
        {
            posedTray = holdingTray;
            trayPose = holdingTray.GetComponent<FoodTrayCarryPose>();
        }
        if (trayPose == null || !trayPose.IsValid) return;
        if (carryAvatar != carryAnimator.avatar || leftHand == null)
        {
            carryAvatar = carryAnimator.avatar;
            leftUpper = carryAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLower = carryAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = carryAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpper = carryAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = carryAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = carryAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        if (leftHand == null || rightHand == null) return;
        Quaternion leftRotation = leftHand.rotation, rightRotation = rightHand.rotation;
        Vector3 offset = holdingTray.transform.TransformVector(trayGripOffset);
        AlienProceduralAnimation.ApplyCcdArm(leftUpper, leftLower, leftHand, trayPose.LeftGrip.position + offset, 1f, 12);
        AlienProceduralAnimation.ApplyCcdArm(rightUpper, rightLower, rightHand, trayPose.RightGrip.position + offset, 1f, 12);
        leftHand.rotation = leftRotation;
        rightHand.rotation = rightRotation;
    }
    public bool HasTray => holdingTray != null;
    public Transform TrayHoldPoint => trayHoldPoint != null ? trayHoldPoint : transform;
    public Transform TrolleyGripPoint => trolleyGripPoint != null ? trolleyGripPoint : TrayHoldPoint;
    public bool HasDedicatedTrolleyGrip => trolleyGripPoint != null;

    public bool TryGetTrolleyGripPoint(out Transform gripPoint)
    {
        gripPoint = trolleyGripPoint;
        return gripPoint != null;
    }

    private void Awake()
    {
        Debug.Log($"[BusserHands] Awake on {name} id={GetInstanceID()}");

        bool belongsToManager = GetComponent<ManagerPlayer>() != null;
        if (!belongsToManager && Instance != null && Instance != this)
        {
            Debug.LogWarning($"[BusserHands] Duplicate staff instance ignored on {name}");
            enabled = false;
            return;
        }

        if (!belongsToManager)
            Instance = this;
        holdingTray = null;

        NotifyHandsChanged();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static BusserHands For(PlayerMovement mover)
    {
        if (mover != null)
        {
            BusserHands ownedHands = mover.GetComponent<BusserHands>();
            var session = MultiplayerSessionManager.Instance;
            if (session != null && session.IsMultiplayerSession)
                return ownedHands; // Never substitute another actor's hands.
            if (ownedHands != null)
                return ownedHands;
        }

        return ActivePlayerHands;
    }

    private void NotifyHandsChanged()
    {
        OnHandsStateChanged?.Invoke();
    }

    public void ReconcileHeldTrayReference()
    {
        if (holdingTray == null || holdingTray.transform.IsChildOf(TrayHoldPoint)) return;
        holdingTray = null;
        NotifyHandsChanged();
    }

    public void ClearTray()
    {
        Debug.Log("[BusserHands] ClearTray");
        FoodTray completedTray = holdingTray;
        holdingTray = null;
        RestaurantTaskClaim.Complete(completedTray);
        NotifyHandsChanged();
    }

    public bool PickupTray(FoodTray tray)
    {
        if (MultiplayerRestaurantBridge.IsObserver) return false;
        if (tray == null)
        {
            Debug.LogWarning("[BusserHands] PickupTray failed: tray is null");
            return false;
        }

        if (HasTray)
        {
            Debug.LogWarning("[BusserHands] PickupTray failed: already holding a tray");
            return false;
        }

        Transform parent = TrayHoldPoint;
        if (parent == null)
        {
            Debug.LogError("[BusserHands] PickupTray failed: TrayHoldPoint is null");
            return false;
        }

        holdingTray = tray;

        WaiterHands.AttachKeepingWorldScale(
            tray.transform,
            parent,
            Vector3.zero,
            Quaternion.identity);
        WaiterHands.SetAllColliders(tray.gameObject, false);

        Debug.Log($"[BusserHands] PickupTray success: {tray.name}");

        NotifyHandsChanged();
        return true;
    }

    public void DisposeTray(bool destroyObject = true)
    {
        var tray = holdingTray;
        holdingTray = null;

        RestaurantTaskClaim.Complete(tray);

        if (destroyObject && tray != null)
        {
            tray.GetComponent<FoodTrayInteractable>()?.ReportHygieneWashed();
            Destroy(tray.gameObject);
        }

        Debug.Log("[BusserHands] DisposeTray");

        NotifyHandsChanged();
    }

    public bool ReleaseTrayForRetry(Vector3 worldPosition)
    {
        FoodTray tray = holdingTray;
        if (tray == null)
            return false;

        holdingTray = null;
        tray.transform.SetParent(null, true);
        tray.transform.position = worldPosition;
        WaiterHands.SetAllColliders(tray.gameObject, true);
        tray.GetComponent<FoodTrayInteractable>()?.RestoreAfterStaffPickup();
        NotifyHandsChanged();
        return true;
    }
}
