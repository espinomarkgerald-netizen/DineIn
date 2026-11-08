using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Shared screen-edge locators for the delivery truck and the local tray destination.</summary>
public sealed class RestockTruckOffscreenIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform marker;
    [SerializeField] private RectTransform directionArrow;
    [SerializeField, Min(0f)] private float arrowGap = 3f;
    [SerializeField, Min(0f)] private float edgePadding = 48f;
    [SerializeField] private Vector3 targetWorldOffset = new Vector3(0f, 1.5f, 0f);
    [Header("Target visibility")]
    [SerializeField, Range(0f, 1f)] private float hideVisibility = .75f;
    [SerializeField, Range(0f, 1f)] private float showVisibility = .65f;
    [SerializeField, Min(0f)] private float visibilityInsetAt1080 = 24f;

    [SerializeField] private RectTransform serveMarker, serveDirectionArrow;
    [SerializeField] private RectTransform reservationMarker, reservationDirectionArrow;
    [SerializeField] private Sprite reservationCountPill;
    private TMP_Text reservationCount;
    private RectTransform countPill;
    private int displayedUnread;

    private RestockTruckInteractable truck;
    private Booth cachedBooth;
    private CustomerGroup cachedGroup;
    private MultiplayerCustomerSpawn networkCustomer;
    private BoothDeliverInteractable boothDelivery;
    private CustomerDeliverInteractable customerDelivery;
    private Renderer[] tableRenderers;
    private UnityEngine.Object truckVisibilityTarget, serveVisibilityTarget, receptionVisibilityTarget;
    private bool truckGuideVisible, serveGuideVisible, receptionGuideVisible;
    private Canvas canvas;
    private RectTransform canvasRect;
    private void FocusTruck()
    {
        if(truck==null || !truck.HasReadyDelivery || GameplayUIBlocker.IsBlocked())return;
        var camera=Camera.main;var controller=camera!=null?camera.GetComponentInParent<MainCameraController>():null;
        if(controller==null)return;
        var renderers=truck.GetComponentsInChildren<Renderer>();Bounds bounds=new Bounds(truck.transform.position,Vector3.zero);bool first=true;
        foreach(var renderer in renderers)if(renderer.enabled){if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
        controller.FrameWorldTarget(bounds.center);
    }

    public void Bind(RestockTruckInteractable target)
    {
        truck = target;
        Hide();
    }

    private void Awake()
    {
        canvasRect = transform as RectTransform;
        canvas = GetComponent<Canvas>();
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        SetupButton(marker, FocusTruck);
        SetupButton(serveMarker, FocusTable);
        SetupButton(reservationMarker, FocusReception);
        BuildReservationCount();
        Hide();
        SetVisible(serveMarker, false);
        SetVisible(reservationMarker, false);
    }

    private static void SetupButton(RectTransform badge, UnityEngine.Events.UnityAction click)
    {
        if (badge == null) return;
        var button = badge.GetComponent<Button>() ?? badge.gameObject.AddComponent<Button>();
        button.targetGraphic = badge.GetComponent<Image>();
        button.onClick.AddListener(click);
        UISubtlePressFeedback.EnsureButtons(badge);
    }

    private bool TryGetTable(out Bounds bounds)
    {
        bounds = default;
        if (RestockFlowCoordinator.Instance?.IsRestockRoomOpen == true) return false;
        var session = MultiplayerSessionManager.Instance;
        if (session != null && session.IsMultiplayerSession && !session.CanAct) return false;
        var hands = WaiterHands.ActivePlayerHands;
        var tray = hands != null ? hands.holdingTray : null;
        var group = tray != null ? tray.TargetGroup : null;
        var booth = group != null ? group.assignedBooth : null;
        if (booth == null || !booth.isActiveAndEnabled) return false;

        if (cachedGroup != group)
        {
            serveVisibilityTarget = null;
            cachedGroup = group;
            networkCustomer = group.GetComponentInParent<MultiplayerCustomerSpawn>();
        }
        if (cachedBooth != booth)
        {
            cachedBooth = booth;
            boothDelivery = booth.GetComponentInChildren<BoothDeliverInteractable>(true);
            customerDelivery = booth.GetComponentInChildren<CustomerDeliverInteractable>(true);
            tableRenderers = booth.GetComponentsInChildren<Renderer>();
        }
        if (!TableNumberUI.CanShowServe(group, booth, tray.orderNumber, networkCustomer,
                networkCustomer != null, boothDelivery, customerDelivery)) return false;

        bounds = new Bounds(booth.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (var renderer in tableRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (hasBounds) bounds.Encapsulate(renderer.bounds);
            else { bounds = renderer.bounds; hasBounds = true; }
        }
        return true;
    }

    private void FocusTable()
    {
        if (GameplayUIBlocker.IsBlocked() || !TryGetTable(out Bounds bounds)) return;
        var camera = Camera.main;
        if (camera != null)
            camera.GetComponentInParent<MainCameraController>()?.FrameWorldTarget(bounds.center);
    }

    private void BuildReservationCount()
    {
        if (reservationMarker == null) return;
        var background = new GameObject("Unread count pill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        countPill = background.rectTransform;
        countPill.SetParent(reservationMarker, false);
        countPill.anchorMin = countPill.anchorMax = new Vector2(.83f, .83f);
        background.sprite = reservationCountPill;
        background.type = Image.Type.Sliced;
        background.color = new Color32(214, 61, 69, 255);
        background.raycastTarget = false;
        reservationCount = new GameObject("Unread requests", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        reservationCount.rectTransform.SetParent(countPill, false);
        reservationCount.rectTransform.anchorMin = Vector2.zero;
        reservationCount.rectTransform.anchorMax = Vector2.one;
        reservationCount.rectTransform.offsetMin = reservationCount.rectTransform.offsetMax = Vector2.zero;
        if (CustomerDrawerArt.Shared != null) reservationCount.font = CustomerDrawerArt.Shared.headingFont;
        reservationCount.fontSize = 14f;
        reservationCount.color = Color.white;
        reservationCount.alignment = TextAlignmentOptions.Center;
        reservationCount.raycastTarget = false;
    }

    private static bool TryGetReception(out FineDiningReceptionNotification notice, out Bounds bounds)
    {
        notice = FineDiningReceptionNotification.Active;
        bounds = default;
        var session = MultiplayerSessionManager.Instance;
        return RestockFlowCoordinator.Instance?.IsRestockRoomOpen != true &&
            (session == null || !session.IsMultiplayerSession || session.CanAct) &&
            notice != null && notice.TryGetReceptionBounds(out bounds);
    }

    private static void FocusReception()
    {
        if (GameplayUIBlocker.IsBlocked() || !TryGetReception(out _, out Bounds bounds)) return;
        var camera = Camera.main;
        if (camera != null) camera.GetComponentInParent<MainCameraController>()?.FrameWorldTarget(bounds.center);
    }

    private void LateUpdate()
    {
        var camera = Camera.main;
        if (canvasRect == null || camera == null || camera.GetComponentInParent<MainCameraController>() == null ||
            GameplayUIBlocker.IsBlocked())
        {
            Hide();
            SetVisible(serveMarker, false);
            SetVisible(reservationMarker, false);
            truckVisibilityTarget = serveVisibilityTarget = receptionVisibilityTarget = null;
            return;
        }

        Rect? truckArea = null;
        if (marker != null && truck != null && truck.isActiveAndEnabled && truck.HasReadyDelivery &&
            TutorialSceneRuntimeMarker.AllowsHUDControl("TruckIndicator") && truck.TryGetVisibleBounds(out Bounds truckBounds))
        {
            if (NeedsGuide(camera, truckBounds, truck, ref truckVisibilityTarget, ref truckGuideVisible))
                truckArea = Place(marker, directionArrow, camera, truck.transform.position + targetWorldOffset, null);
        }
        else truckVisibilityTarget = null;
        SetVisible(marker, truckArea.HasValue);

        Rect? serveArea = null;
        if (serveMarker != null && TryGetTable(out Bounds tableBounds))
        {
            if (NeedsGuide(camera, tableBounds, cachedBooth, ref serveVisibilityTarget, ref serveGuideVisible))
                serveArea = Place(serveMarker, serveDirectionArrow, camera, tableBounds.center, truckArea);
        }
        else serveVisibilityTarget = null;
        SetVisible(serveMarker, serveArea.HasValue);

        bool showReservations = false;
        if (reservationMarker != null && TryGetReception(out var notice, out Bounds receptionBounds))
            showReservations = NeedsGuide(camera, receptionBounds, notice, ref receptionVisibilityTarget, ref receptionGuideVisible)
                && Place(reservationMarker, reservationDirectionArrow, camera, receptionBounds.center, truckArea, serveArea).HasValue;
        else receptionVisibilityTarget = null;
        if (showReservations && reservationCount != null)
        {
            int unread = FineDiningReceptionNotification.Active.UnreadCount;
            if (displayedUnread != unread)
            {
                displayedUnread = unread;
                reservationCount.text = unread.ToString();
                countPill.sizeDelta = new Vector2(Mathf.Max(19f, reservationCount.preferredWidth + 8f), 19f);
            }
        }
        SetVisible(reservationMarker, showReservations);
    }

    private bool NeedsGuide(Camera camera, Bounds bounds, UnityEngine.Object target,
        ref UnityEngine.Object previousTarget, ref bool visible)
    {
        float fraction = VisibleFraction(camera, bounds, visibilityInsetAt1080);
        float hideAt = Mathf.Clamp01(hideVisibility);
        float showBelow = Mathf.Min(Mathf.Clamp01(showVisibility), hideAt);
        if (previousTarget != target) visible = fraction < hideAt;
        else if (fraction >= hideAt) visible = false;
        else if (fraction < showBelow) visible = true;
        previousTarget = target;
        return visible;
    }

    // Screen-space AABB of all eight world bounds corners; no per-frame allocations.
    internal static float VisibleFraction(Camera camera, Bounds bounds, float insetAt1080)
    {
        Rect viewport = camera.pixelRect;
        Rect safe = Screen.safeArea;
        float inset = Mathf.Max(0f, insetAt1080) * viewport.height / 1080f;
        float left = Mathf.Max(viewport.xMin, safe.xMin) + inset;
        float right = Mathf.Min(viewport.xMax, safe.xMax) - inset;
        float bottom = Mathf.Max(viewport.yMin, safe.yMin) + inset;
        float top = Mathf.Min(viewport.yMax, safe.yMax) - inset;
        if (right <= left || top <= bottom) return 0f;

        Vector3 min = bounds.min, max = bounds.max;
        Vector2 screenMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 screenMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = camera.WorldToScreenPoint(new Vector3(
                (i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z));
            if (point.z <= camera.nearClipPlane || !float.IsFinite(point.x) || !float.IsFinite(point.y))
                return 0f;
            screenMin = Vector2.Min(screenMin, point);
            screenMax = Vector2.Max(screenMax, point);
        }
        float area = (screenMax.x - screenMin.x) * (screenMax.y - screenMin.y);
        if (area <= Mathf.Epsilon) return 0f;
        float width = Mathf.Max(0f, Mathf.Min(screenMax.x, right) - Mathf.Max(screenMin.x, left));
        float height = Mathf.Max(0f, Mathf.Min(screenMax.y, top) - Mathf.Max(screenMin.y, bottom));
        return Mathf.Clamp01(width * height / area);
    }

    private Rect? Place(RectTransform badge, RectTransform arrow, Camera camera, Vector3 target, Rect? occupied, Rect? second = null)
    {
        Vector3 cameraLocal = camera.transform.InverseTransformPoint(target);
        bool showRight = cameraLocal.x >= 0f;
        Vector2 screen = camera.WorldToScreenPoint(target);
        if (cameraLocal.z < 0f && !camera.orthographic)
            screen = 2f * camera.pixelRect.center - screen;
        if (!float.IsFinite(screen.x) || !float.IsFinite(screen.y))
            screen = camera.pixelRect.center + new Vector2(cameraLocal.x, cameraLocal.y).normalized * Screen.width;

        Rect safe = Screen.safeArea;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        float half = Mathf.Max(badge.rect.width, badge.rect.height) * .5f;
        float arrowRadius = arrow != null ? arrow.rect.size.magnitude * .5f : 0f;
        float extent = (half + arrowGap + arrowRadius * 2f) * scale;
        float pad = Mathf.Min(Mathf.Max(edgePadding, extent), Mathf.Min(safe.width, safe.height) * .5f);
        float lowY = Mathf.Max(safe.yMin + pad, Screen.height * .14f);
        float highY = Mathf.Min(safe.yMax - pad, Screen.height * .72f);
        if (lowY > highY) { lowY = safe.yMin + pad; highY = safe.yMax - pad; }
        var edge = new Vector2(showRight ? safe.xMax - pad : safe.xMin + pad, Mathf.Clamp(screen.y, lowY, highY));

        Rect Area(Vector2 center) => new Rect(center - Vector2.one * extent, Vector2.one * (2f * extent));
        bool IsClear(Vector2 center) => !(occupied?.Overlaps(Area(center)) ?? false)
            && !(second?.Overlaps(Area(center)) ?? false);
        if (!IsClear(edge))
        {
            float nearest = float.PositiveInfinity;
            float bestY = edge.y;
            void TryY(float y)
            {
                float distance = Mathf.Abs(y - edge.y);
                if (distance >= nearest || y < safe.yMin + pad || y > safe.yMax - pad ||
                    !IsClear(new Vector2(edge.x, y))) return;
                nearest = distance;
                bestY = y;
            }
            void TryBeside(Rect? obstacle)
            {
                if (!(obstacle is Rect blocker)) return;
                TryY(blocker.yMax + extent + 4f * scale);
                TryY(blocker.yMin - extent - 4f * scale);
            }
            TryBeside(occupied);
            TryBeside(second);
            if (float.IsPositiveInfinity(nearest)) return null;
            edge.y = bestY;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, edge, null, out Vector2 anchored);
        badge.anchoredPosition = anchored;
        if (arrow != null)
        {
            Vector2 direction = screen - edge;
            if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || direction.sqrMagnitude < .01f)
                direction = showRight ? Vector2.right : Vector2.left;
            direction.Normalize();
            float edgeDistance = 1f / Mathf.Max(
                Mathf.Abs(direction.x) / (badge.rect.width * .5f),
                Mathf.Abs(direction.y) / (badge.rect.height * .5f));
            arrow.anchoredPosition = direction * (edgeDistance + arrowGap + arrowRadius);
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
        return Area(edge);
    }

    private void Hide() => SetVisible(marker, false);

    private static void SetVisible(RectTransform badge, bool visible)
    {
        if (badge != null && badge.gameObject.activeSelf != visible)
            badge.gameObject.SetActive(visible);
    }
}
