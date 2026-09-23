using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BoothMessCleanUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Slider radialFill;

    [Header("Billboard")]
    [SerializeField] private Transform billboardRoot;
    [SerializeField] private Camera cam;
    [SerializeField] private bool faceCamera = true;
    [Tooltip("Cleaning prompt width at a 1080-pixel viewport height; stays readable while zooming.")]
    [SerializeField, Min(100f)] private float readableWidth = 240f;
    [Tooltip("World-space clearance between the bottom of the prompt and the table.")]
    [SerializeField, Min(0f)] private float tableClearance = .8f;
    private readonly Vector3[] controlCorners = new Vector3[4];

    [Header("Blocked Feedback")]
    [SerializeField] private float blockedMessageSeconds = 0.9f;

    private Booth booth;
    private bool isHolding;
    private bool automatedCleaning;
    private float holdTimer;
    private float blockedTimer;
    private bool pointerHeld, approaching;
    private PlayerMovement holdMover;
    private IInteractable holdMove;
    private uint holdVersion;
    private Button staffButton;
    private TMP_Text staffLabel;
    private Button selfButton;
    private bool selfSelected;
    private RectTransform authoredControl;
    private Image authoredFrame;

    public bool IsAutomatedCleaning => automatedCleaning && isHolding;

    public void Setup(Booth targetBooth, Camera sceneCamera)
    {
        booth = targetBooth;
        cam = sceneCamera;

        if (billboardRoot == null)
            billboardRoot = transform;

        AssignWorldCamera();
        ResetUI();
    }

    private void OnEnable()
    {
        AssignWorldCamera();
        EnsureStaffButton();
        selfSelected = false;
        ResetUI();
        PlayerTaskBubbleFocus.Bind(gameObject, booth);
    }

    private void OnDisable()
    {
        CancelApproach();
        pointerHeld = false;
        if (!automatedCleaning) booth?.CancelHeldCleanup();
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer) return;
        if (!automatedCleaning)
            RestaurantTaskClaim.ReleasePlayer(booth);
    }

    private void Update()
    {
        if (booth == null)
            return;
        if (staffButton == null)
            EnsureStaffButton();
        if (staffButton != null)
        {
            bool queued = HygieneManager.Instance?.IsBoothQueued(booth) == true;
            bool choices = !selfSelected && !automatedCleaning && !booth.HumanCleanupActive && !isHolding && !approaching;
            staffButton.gameObject.SetActive(choices);
            selfButton.gameObject.SetActive(choices);
            selfButton.interactable = !queued;
            if (label != null) label.gameObject.SetActive(!choices);
            if (radialFill != null) radialFill.gameObject.SetActive(!choices);
            if (authoredFrame != null) authoredFrame.enabled = !choices;
            staffButton.interactable = !queued;
            if (staffLabel != null) staffLabel.text = queued ? "Staff requested" : "Ask staff";
        }
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer && !automatedCleaning)
        {
            if (radialFill != null) radialFill.value = booth.HumanCleanupProgress;
            if (label != null) label.text = booth.HumanCleanupActive ? "Cleaning..." : "Hold to clean";
            return;
        }

        if (!booth.NeedsSurfaceCleaning)
        {
            ResetUI();
            return;
        }

        if (blockedTimer > 0f)
        {
            blockedTimer -= Time.deltaTime;

            if (blockedTimer <= 0f && !isHolding && label != null)
                label.text = "Hold to clean";
        }

        if (!isHolding)
            return;
        if (!automatedCleaning && holdMover != null && (holdMover.CommandVersion != holdVersion
            || Vector3.Distance(holdMover.transform.position, booth.GetNavigableApproachPosition()) > 2.9f))
        { StopHold(); return; }

        if (!automatedCleaning && !CanCurrentPlayerClean(out string blockedReason))
        {
            StopHold(blockedReason);
            return;
        }

        holdTimer += Time.deltaTime;

        float pct = Mathf.Clamp01(holdTimer / Mathf.Max(0.05f, booth.MessHoldSeconds));

        if (radialFill != null)
            radialFill.value = pct;

        if (pct < 1f)
            return;

        isHolding = false;
        automatedCleaning = false;
        holdTimer = 0f;
        booth.CleanMess();
        RestaurantTaskClaim.Complete(booth);
        ResetUI();
    }

    private void LateUpdate()
    {
        if (!faceCamera || billboardRoot == null)
            return;

        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            return;

        // Yaw-only billboarding compresses text vertically in the tilted game camera.
        billboardRoot.rotation = cam.transform.rotation;
        if (authoredControl == null) authoredControl = label != null ? label.transform.parent as RectTransform : null;
        if (authoredControl == null || booth == null) return;
        float depth = Mathf.Max(.1f, Vector3.Dot(billboardRoot.position - cam.transform.position, cam.transform.forward));
        float viewHeight = cam.orthographic ? 2f * cam.orthographicSize
            : 2f * depth * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * .5f);
        float width = selfButton != null ? ((RectTransform)selfButton.transform).rect.width : authoredControl.rect.width * 1.4f;
        float currentWidth = width * Mathf.Abs(authoredControl.lossyScale.x);
        if (currentWidth > .0001f)
            billboardRoot.localScale *= (readableWidth / 1080f * viewHeight) / currentWidth;

        // Keep the entire prompt above the tabletop, including the lower staff button.
        float bottom = selfButton != null && selfButton.gameObject.activeSelf
            ? Mathf.Min(ControlBottom((RectTransform)selfButton.transform), ControlBottom((RectTransform)staffButton.transform))
            : ControlBottom(authoredControl);
        float tableY = booth.tableLookTarget != null ? booth.tableLookTarget.position.y : booth.transform.position.y + 3f;
        var position = billboardRoot.position;
        position.y += tableY + tableClearance - bottom;
        billboardRoot.position = position;
    }

    private float ControlBottom(RectTransform control)
    {
        control.GetWorldCorners(controlCorners);
        float bottom = float.PositiveInfinity;
        foreach (var corner in controlCorners) bottom = Mathf.Min(bottom, corner.y);
        return bottom;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Only an explicit selection reveals the hold interaction. UI clicks
        // on the choice buttons must never start a cleaning claim.
        if (eventData != null && selfButton != null && !selfSelected) return;
        if (staffButton != null && eventData?.pointerPressRaycast.gameObject != null
            && eventData.pointerPressRaycast.gameObject.transform.IsChildOf(staffButton.transform)) return;
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
        pointerHeld = true;
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer)
        { booth?.RequestHumanCleanup(); return; }
        if (automatedCleaning || booth == null || !booth.CanRequestHumanCleanup || approaching)
            return;

        if (!CanCurrentPlayerClean(out string blockedReason))
        {
            ShowBlocked(blockedReason);
            return;
        }

        if (!RestaurantTaskClaim.TryClaimPlayer(booth))
        {
            ShowBlocked(RestaurantTaskClaim.PlayerHasActiveTask
                ? "Finish Current Task"
                : EmployeeRoleCatalog.DisplayName(EmployeeRole.Busser) + " Is Cleaning");
            return;
        }

        var mover = ManagerPlayer.Active != null ? ManagerPlayer.Active.Movement : RoleManager.Instance?.GetActivePlayerMovement();
        if (eventData != null && mover != null)
        {
            approaching = true; holdMover = mover;
            if (label != null) label.text = "Hold to clean";
            if (!mover.UI_MoveToAction(booth.approachPoint != null ? booth.approachPoint : booth.transform, 2.75f, () =>
            {
                approaching = false; holdMove = null;
                if (!pointerHeld || !booth.CanRequestHumanCleanup || !CanCurrentPlayerClean(out _)) { StopHold(); return; }
                BeginHold();
            }, () => { approaching = false; holdMove = null; StopHold(); }))
            { approaching = false; StopHold(); }
            else if (approaching) holdMove = mover.CurrentTarget;
            return;
        }
        BeginHold();
    }

    private void BeginHold()
    {
        if (holdMover != null) holdVersion = holdMover.CommandVersion;

        isHolding = true;
        automatedCleaning = false;
        holdTimer = 0f;
        blockedTimer = 0f;

        if (label != null)
            label.text = "Cleaning...";

        if (radialFill != null)
            radialFill.value = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerHeld = false;
        CancelApproach();
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer) { booth?.CancelHeldCleanup(); return; }
        if (automatedCleaning)
            return;

        StopHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerHeld = false;
        CancelApproach();
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer) { booth?.CancelHeldCleanup(); return; }
        if (automatedCleaning)
            return;

        StopHold();
    }

    public bool BeginAutomatedCleaning()
    {
        if (booth == null || !booth.CanCleanMessNow)
            return false;

        automatedCleaning = true;
        isHolding = true;
        holdTimer = 0f;
        blockedTimer = 0f;

        if (label != null)
            label.text = "Cleaning...";

        if (radialFill != null)
            radialFill.value = 0f;

        return true;
    }

    public void CancelAutomatedCleaning()
    {
        if (!automatedCleaning)
            return;

        ResetUI();
    }

    private void StopHold()
    {
        CancelApproach();
        if (!automatedCleaning)
            RestaurantTaskClaim.ReleasePlayer(booth);

        isHolding = false;
        automatedCleaning = false;
        holdTimer = 0f;

        if (label != null)
            label.text = "Hold to clean";

        if (radialFill != null)
            radialFill.value = 0f;
    }

    private void StopHold(string blockedReason)
    {
        CancelApproach();
        if (!automatedCleaning)
            RestaurantTaskClaim.ReleasePlayer(booth);

        isHolding = false;
        automatedCleaning = false;
        holdTimer = 0f;

        if (label != null)
            label.text = blockedReason;

        if (radialFill != null)
            radialFill.value = 0f;

        blockedTimer = blockedMessageSeconds;
    }

    private void ShowBlocked(string blockedReason)
    {
        isHolding = false;
        automatedCleaning = false;
        holdTimer = 0f;

        if (label != null)
            label.text = blockedReason;

        if (radialFill != null)
            radialFill.value = 0f;

        blockedTimer = blockedMessageSeconds;
    }

    private void ResetUI()
    {
        isHolding = false;
        automatedCleaning = false;
        holdTimer = 0f;
        blockedTimer = 0f;

        if (label != null)
            label.text = "Hold to clean";

        if (radialFill != null)
            radialFill.value = 0f;
    }

    private void AssignWorldCamera()
    {
        if (cam == null)
            cam = Camera.main;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && cam != null)
            canvas.worldCamera = cam;
    }

    private bool CanCurrentPlayerClean(out string blockedReason)
    {
        if (!booth.CanRequestHumanCleanup) { blockedReason = "Table unavailable"; return false; }
        var mover = ManagerPlayer.Active != null ? ManagerPlayer.Active.Movement : RoleManager.Instance?.GetActivePlayerMovement();
        if (mover != null && !HygieneManager.HandsEmpty(mover)) { blockedReason = "Hands full"; return false; }
        if (RestaurantTaskClaim.IsClaimedByBot(booth))
        {
            blockedReason = EmployeeRoleCatalog.DisplayName(EmployeeRole.Busser) + " Is Cleaning";
            return false;
        }

        if (!IsBusserRoleActive())
        {
            blockedReason = EmployeeRoleCatalog.DisplayName(EmployeeRole.Busser) + " Only";
            return false;
        }

        if (IsBusserHandsBusy())
        {
            blockedReason = "Hands Full";
            return false;
        }

        blockedReason = null;
        return true;
    }

    private bool IsBusserHandsBusy()
    {
        if (BusserHands.ActivePlayerHands == null)
            return false;

        return BusserHands.ActivePlayerHands.HasTray;
    }

    private bool IsBusserRoleActive()
    {
        if (ManagerPlayer.Active != null &&
            ManagerPlayer.Active.Can(ManagerPlayer.Capability.Busser))
            return true;

        if (RoleManager.Instance != null &&
            RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Busser))
            return true;

        MonoBehaviour roleManager = FindRoleManager();
        if (roleManager == null)
            return true;

        object roleValue =
            GetMemberValue(roleManager, "CurrentRole") ??
            GetMemberValue(roleManager, "currentRole") ??
            GetMemberValue(roleManager, "SelectedRole") ??
            GetMemberValue(roleManager, "selectedRole") ??
            GetMemberValue(roleManager, "ActiveRole") ??
            GetMemberValue(roleManager, "activeRole");

        if (roleValue == null)
            return true;

        string roleText = roleValue.ToString();
        return !string.IsNullOrWhiteSpace(roleText) &&
               roleText.IndexOf("Busser", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private MonoBehaviour FindRoleManager()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null)
                continue;

            if (mb.GetType().Name == "RoleManager")
                return mb;
        }

        return null;
    }

    private object GetMemberValue(object source, string memberName)
    {
        if (source == null)
            return null;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = source.GetType();

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
            return field.GetValue(source);

        PropertyInfo prop = type.GetProperty(memberName, flags);
        if (prop != null && prop.CanRead)
            return prop.GetValue(source);

        return null;
    }

    private void CancelApproach()
    {
        var mover = holdMover; var move = holdMove;
        holdMover = null; holdMove = null; approaching = false;
        if (mover != null && move != null && ReferenceEquals(mover.CurrentTarget, move)) mover.CancelLockedTask();
    }
    private void EnsureStaffButton()
    {
        if (staffButton != null || label == null || HygieneManager.Instance == null) return;
        var style = HygieneCleaningPresentation.Load();
        if (style == null || style.choiceButton == null) return;
        authoredControl = label.transform.parent as RectTransform;
        if (authoredControl == null) return;
        authoredFrame = authoredControl.GetComponent<Image>();
        selfButton = CreateChoice("Clean myself", .5f, style.choiceButton, out _);
        staffButton = CreateChoice("Ask staff", -.5f, style.choiceButton, out staffLabel);
        selfButton.onClick.AddListener(() => selfSelected = true);
        staffButton.onClick.AddListener(() =>
        {
            HygieneManager.Instance?.RequestBoothStaff(booth);
            booth?.CloseCleaningPrompt();
        });
    }

    private Button CreateChoice(string text, float row, Sprite sprite, out TMP_Text caption)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = label.gameObject.layer;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(authoredControl, false);
        float height = Mathf.Max(8f, authoredControl.rect.height);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(Mathf.Max(32f, authoredControl.rect.width * 1.4f), height);
        rect.anchoredPosition = new Vector2(0f, row * (height + 2f));
        var image = go.GetComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Sliced;
        var button = go.GetComponent<Button>(); button.targetGraphic = image;
        caption = Instantiate(label, rect, false); caption.gameObject.SetActive(true); caption.raycastTarget = false;
        caption.rectTransform.localScale = Vector3.one;
        caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = Vector2.one;
        caption.rectTransform.offsetMin = new Vector2(2f, 1f); caption.rectTransform.offsetMax = new Vector2(-2f, -1f);
        caption.margin = Vector4.zero; caption.alignment = TextAlignmentOptions.Center;
        caption.color = Color.white; caption.enableAutoSizing = true;
        caption.fontSizeMax = Mathf.Clamp(label.fontSize, 3f, 6f); caption.fontSizeMin = caption.fontSizeMax * .65f;
        caption.fontSize = caption.fontSizeMax; caption.overflowMode = TextOverflowModes.Ellipsis;
        caption.text = text;
        return button;
    }
}
