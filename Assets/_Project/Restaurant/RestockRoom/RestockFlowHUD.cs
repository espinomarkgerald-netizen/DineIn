using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Persistent, prefab-backed delivery UI shared by Lobby1 and RestockScene.
/// Designer-authored styling is read from the prefab. The hotbar width adapts to
/// its cells so it stays compact without changing the established visual style.
/// </summary>
public sealed class RestockFlowHUD : MonoBehaviour
{
    [Header("Notification")]
    [SerializeField] private GameObject notificationRoot;
    [SerializeField] private TMP_Text notificationText;
    [SerializeField, Min(0.5f)] private float notificationSeconds = 6f;

    [Header("Truck Hold")]
    [SerializeField] private GameObject holdRoot;
    [SerializeField] private RestockHoldButton holdButton;
    [SerializeField] private Button holdCloseButton;

    [Header("Compact Restock Hotbar")]
    [SerializeField] private GameObject hotbarRoot;
    [SerializeField] private RectTransform hotbarContent;
    [SerializeField] private Button hotbarSlotPrefab;
    [SerializeField] private TMP_Text roomMessageText;
    [SerializeField] private GameObject roomMessageRoot;
    [SerializeField] private CanvasGroup roomMessageCanvasGroup;
    [SerializeField] private TMP_Text remainingText;
    [SerializeField] private GameObject tooltipRoot;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField, Min(40f)] private float hotbarSlotSize = 54f;
    [SerializeField, Min(0f)] private float hotbarSlotSpacing = 6f;
    [SerializeField, Min(100f)] private float maxHotbarWidth = 720f;
    [SerializeField, Min(0.05f)] private float hotbarGrowSeconds = 0.24f;
    [SerializeField, Min(0.01f)] private float pickupSlotDelay = 0.08f;
    [SerializeField, Min(0.05f)] private float pickupPopSeconds = 0.18f;
    [SerializeField, Min(1f)] private float returnPunchScale = 1.18f;
    [SerializeField, Min(0.5f)] private float roomMessageInfoSeconds = 1.5f;
    [SerializeField, Min(0.5f)] private float roomMessageErrorSeconds = 4.1f;

    [Header("Start-day Restock Reminder")]
    [SerializeField] private GameObject startReminderRoot;
    [SerializeField] private TMP_Text startReminderText;
    [SerializeField] private Button restockFirstButton;
    [SerializeField] private Button startAnywayButton;

    [Header("Transition")]
    [SerializeField] private RestockIrisGraphic iris;

    private readonly List<RestockHotbarSlotUI> slots = new List<RestockHotbarSlotUI>();
    private RestockRoomController roomController;
    private RestockHotbarSlotUI selectedSlot;
    private RestockHotbarSlotUI draggedSlot;
    private RestockStorageType activeRoom;
    private bool inRestockRoom;
    private bool worldDragStarted;
    private bool pickupAnimationRequested;
    private Coroutine pickupRoutine;
    private Coroutine returnRoutine;
    private Coroutine notificationRoutine;
    private Coroutine hotbarResizeRoutine;
    private Coroutine roomMessageRoutine;
    private Action startAnyway;
    private CanvasGroup hotbarInputGroup;
    private Vector2 roomMessageShownPosition;

    public RectTransform HotbarRect => hotbarRoot != null
        ? hotbarRoot.transform as RectTransform
        : null;

    public void ConfigureReferences(
        GameObject configuredNotificationRoot,
        TMP_Text configuredNotificationText,
        GameObject configuredHoldRoot,
        RestockHoldButton configuredHoldButton,
        Button configuredHoldClose,
        GameObject configuredHotbarRoot,
        RectTransform configuredHotbarContent,
        Button configuredSlotPrefab,
        TMP_Text configuredRoomMessage,
        RestockIrisGraphic configuredIris)
    {
        notificationRoot = configuredNotificationRoot;
        notificationText = configuredNotificationText;
        holdRoot = configuredHoldRoot;
        holdButton = configuredHoldButton;
        holdCloseButton = configuredHoldClose;
        hotbarRoot = configuredHotbarRoot;
        hotbarContent = configuredHotbarContent;
        hotbarSlotPrefab = configuredSlotPrefab;
        roomMessageText = configuredRoomMessage;
        iris = configuredIris;
    }

    public void ConfigureExtendedReferences(
        TMP_Text configuredRemainingText,
        GameObject configuredTooltipRoot,
        TMP_Text configuredTooltipText,
        GameObject configuredStartReminderRoot,
        TMP_Text configuredStartReminderText,
        Button configuredRestockFirst,
        Button configuredStartAnyway)
    {
        remainingText = configuredRemainingText;
        tooltipRoot = configuredTooltipRoot;
        tooltipText = configuredTooltipText;
        startReminderRoot = configuredStartReminderRoot;
        startReminderText = configuredStartReminderText;
        restockFirstButton = configuredRestockFirst;
        startAnywayButton = configuredStartAnyway;
    }

    private void Awake()
    {
        ResolveOptionalReferences();
        MakeTooltipInputTransparent();
        PrepareRoomMessage();
        SetHotbarInteraction(false);

        if (holdRoot != null)
            holdRoot.SetActive(false);
        if (notificationRoot != null)
            notificationRoot.SetActive(false);
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
        if (remainingText != null)
            remainingText.gameObject.SetActive(false);
        if (startReminderRoot != null)
            startReminderRoot.SetActive(false);
        if (iris != null)
            iris.gameObject.SetActive(false);

        if (holdCloseButton != null)
        {
            holdCloseButton.onClick.RemoveAllListeners();
            holdCloseButton.onClick.AddListener(HideHold);
        }

        if (restockFirstButton != null)
        {
            restockFirstButton.onClick.RemoveAllListeners();
            restockFirstButton.onClick.AddListener(HideStartReminder);
        }

        if (startAnywayButton != null)
        {
            startAnywayButton.onClick.RemoveAllListeners();
            startAnywayButton.onClick.AddListener(ContinueStartAnyway);
        }
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeAndRefresh());
    }

    private void OnDisable()
    {
        if (RestockOrderManager.Instance != null)
            RestockOrderManager.Instance.OrdersChanged -= HandleOrdersChanged;

        if (hotbarResizeRoutine != null)
        {
            StopCoroutine(hotbarResizeRoutine);
            hotbarResizeRoutine = null;
        }
        if (roomMessageRoutine != null)
        {
            StopCoroutine(roomMessageRoutine);
            roomMessageRoutine = null;
        }
    }

    private IEnumerator SubscribeAndRefresh()
    {
        yield return null;
        if (RestockOrderManager.Instance != null)
        {
            RestockOrderManager.Instance.OrdersChanged -= HandleOrdersChanged;
            RestockOrderManager.Instance.OrdersChanged += HandleOrdersChanged;
        }
        RebuildHotbar();
    }

    public void ShowNotification(string message)
    {
        if (notificationText != null)
            notificationText.text = message ?? string.Empty;
        if (notificationRoot != null)
        {
            notificationRoot.SetActive(true);
            if (notificationRoutine != null)
                StopCoroutine(notificationRoutine);
            notificationRoutine = StartCoroutine(HideNotificationAfterDelay());
        }
    }

    public void HideNotification()
    {
        if (notificationRoutine != null)
        {
            StopCoroutine(notificationRoutine);
            notificationRoutine = null;
        }
        if (notificationRoot != null)
            notificationRoot.SetActive(false);
    }

    public void ShowHold(Action completed)
    {
        if (holdRoot != null)
            holdRoot.SetActive(true);
        holdButton?.Begin(() =>
        {
            HideHold();
            completed?.Invoke();
        });
    }

    public void HideHold()
    {
        if (holdRoot != null)
            holdRoot.SetActive(false);
    }

    public void SetLobbyContext()
    {
        roomController = null;
        inRestockRoom = false;
        HideRoomMessageImmediate();
        SetHotbarInteraction(false);
        RebuildHotbar();
    }

    public void SetRestockContext(RestockRoomController controller, RestockStorageType room)
    {
        roomController = controller;
        inRestockRoom = true;
        activeRoom = room;
        SetHotbarInteraction(true);
        RebuildHotbar();
    }

    public void SetActiveRoom(RestockStorageType room)
    {
        activeRoom = room;
        RefreshRemainingText();
    }

    private void SetHotbarInteraction(bool enabled)
    {
        HideTooltip();
        selectedSlot = null;
        draggedSlot = null;
        worldDragStarted = false;

        if (hotbarRoot != null)
        {
            if (hotbarInputGroup == null)
                hotbarInputGroup = hotbarRoot.GetComponent<CanvasGroup>();
            if (hotbarInputGroup == null)
                hotbarInputGroup = hotbarRoot.AddComponent<CanvasGroup>();

            hotbarInputGroup.alpha = 1f;
            hotbarInputGroup.blocksRaycasts = enabled;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            RestockHotbarSlotUI slot = slots[i];
            if (slot == null)
                continue;
            slot.SetDragging(false);
            slot.SetSelected(false);
            slot.RestoreVisualState();
        }
    }

    public void RequestPickupAnimation()
    {
        pickupAnimationRequested = true;
    }

    public void CancelPickupAnimation()
    {
        pickupAnimationRequested = false;
    }

    public void RebuildHotbar()
    {
        if (draggedSlot != null && roomController != null)
            roomController.CancelHotbarWorldDrag();
        worldDragStarted = false;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                Destroy(slots[i].gameObject);
        }
        slots.Clear();
        selectedSlot = null;
        draggedSlot = null;

        RestockOrderManager manager = RestockOrderManager.Instance;
        bool hasBoxes = manager != null && manager.HotbarContainerCount > 0;
        RectTransform hotbarRect = HotbarRect;
        float previousWidth = hotbarRect != null ? hotbarRect.rect.width : 0f;
        if (hotbarRoot != null)
            hotbarRoot.SetActive(hasBoxes);
        if (!hasBoxes)
        {
            if (hotbarResizeRoutine != null)
            {
                StopCoroutine(hotbarResizeRoutine);
                hotbarResizeRoutine = null;
            }
            HideTooltip();
            if (inRestockRoom)
                SetRoomMessage("ALL BOXES STORED", false);
            return;
        }

        if (hotbarContent == null || hotbarSlotPrefab == null)
            return;

        List<ItemData> items = manager.GetHotbarItems();
        float targetWidth = Mathf.Min(
            maxHotbarWidth,
            items.Count * hotbarSlotSize + Mathf.Max(0, items.Count - 1) * hotbarSlotSpacing);
        if (hotbarRect != null)
        {
            hotbarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, hotbarSlotSize);
            if (hotbarResizeRoutine != null)
                StopCoroutine(hotbarResizeRoutine);
            float startWidth = pickupAnimationRequested ? 0f : previousWidth;
            hotbarResizeRoutine = StartCoroutine(
                AnimateHotbarWidth(hotbarRect, startWidth, targetWidth));
        }

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            Button button = Instantiate(hotbarSlotPrefab, hotbarContent);
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();

            RestockHotbarSlotUI slot = button.GetComponent<RestockHotbarSlotUI>();
            if (slot == null)
                slot = button.gameObject.AddComponent<RestockHotbarSlotUI>();
            slot.Bind(this, item, manager.GetHotbarContainers(item));
            slots.Add(slot);
        }

        RefreshRemainingText();
        if (pickupAnimationRequested)
        {
            pickupAnimationRequested = false;
            if (pickupRoutine != null)
                StopCoroutine(pickupRoutine);
            pickupRoutine = StartCoroutine(PlayPickupRoutine());
        }
    }

    public void HandleSlotClicked(RestockHotbarSlotUI slot)
    {
        if (!inRestockRoom || slot == null || slot.Item == null)
            return;

        SelectSlot(slot);
        ShowTooltip(slot.Item);
    }

    public void HandleSlotHover(RestockHotbarSlotUI slot, bool entered)
    {
        if (!inRestockRoom)
        {
            slot?.RestoreVisualState();
            HideTooltip();
            return;
        }

        if (entered && slot != null)
            ShowTooltip(slot.Item);
        else if (selectedSlot == null || selectedSlot != slot)
            HideTooltip();
    }

    public void HandleSlotDragBegin(RestockHotbarSlotUI slot, PointerEventData eventData)
    {
        if (!inRestockRoom || roomController == null || slot == null || slot.Item == null)
            return;

        SelectSlot(slot);
        ShowTooltip(slot.Item);
        draggedSlot = slot;
        draggedSlot.SetDragging(true);
        worldDragStarted = false;
        roomController.PrepareHotbarDrag(slot.Item);
    }

    public void HandleSlotDrag(RestockHotbarSlotUI slot, PointerEventData eventData)
    {
        if (slot == null || slot != draggedSlot || roomController == null)
            return;

        if (!worldDragStarted && IsInsideHotbar(eventData.position, eventData.pressEventCamera))
            return;

        if (!worldDragStarted)
        {
            worldDragStarted = roomController.BeginHotbarWorldDrag(slot.Item, eventData.position);
            if (!worldDragStarted)
                return;
        }

        roomController.UpdateHotbarWorldDrag(eventData.position);
    }

    public void HandleSlotDragEnd(RestockHotbarSlotUI slot, PointerEventData eventData)
    {
        if (slot == null || slot != draggedSlot)
            return;

        bool stored = worldDragStarted && roomController != null &&
                      roomController.EndHotbarWorldDrag(eventData.position);
        slot.SetDragging(false);
        draggedSlot = null;
        worldDragStarted = false;

        if (!stored)
            PlayInvalidReturn(slot);
        else
            RebuildHotbar();
    }

    public void SetRoomMessage(string message, bool error)
    {
        if (roomMessageText == null || roomMessageRoot == null)
            return;

        if (string.IsNullOrWhiteSpace(message))
        {
            HideRoomMessageImmediate();
            return;
        }

        roomMessageText.text = message.Trim();
        roomMessageText.color = error
            ? new Color(1f, 0.94f, 0.84f, 1f)
            : Color.white;
        roomMessageRoot.SetActive(true);

        if (roomMessageRoutine != null)
            StopCoroutine(roomMessageRoutine);
        roomMessageRoutine = StartCoroutine(
            ShowRoomMessageRoutine(error ? roomMessageErrorSeconds : roomMessageInfoSeconds));
    }

    public bool ShowStartReminder(int remainingBoxes, Action onStartAnyway)
    {
        if (remainingBoxes <= 0 || startReminderRoot == null)
            return false;

        startAnyway = onStartAnyway;
        if (startReminderText != null)
        {
            startReminderText.text = remainingBoxes + " delivered box" +
                (remainingBoxes == 1 ? " is" : "es are") +
                " still in your hotbar. Store them now, or open the restaurant anyway.";
        }
        startReminderRoot.SetActive(true);
        return true;
    }

    public void PlayClose(Action completed)
    {
        if (iris != null)
            iris.Close(completed);
        else
            completed?.Invoke();
    }

    public void PlayOpen(Action completed = null)
    {
        if (iris != null)
            iris.Open(completed);
        else
            completed?.Invoke();
    }

    public void ReleaseTransitionInputBlocker()
    {
        iris?.ForceOpen();
    }

    private void HandleOrdersChanged() => RebuildHotbar();

    private void SelectSlot(RestockHotbarSlotUI slot)
    {
        selectedSlot = slot;
        for (int i = 0; i < slots.Count; i++)
            slots[i]?.SetSelected(slots[i] == selectedSlot);
    }

    private void ShowTooltip(ItemData item)
    {
        if (tooltipRoot == null || tooltipText == null || item == null)
            return;

        tooltipText.text = item.displayName.ToUpperInvariant() + "  |  " +
                           StorageLabel(item.requiredStorage).ToUpperInvariant();
        tooltipRoot.SetActive(true);
    }

    private void HideTooltip()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    private void MakeTooltipInputTransparent()
    {
        if (tooltipRoot == null)
            return;

        Graphic[] graphics = tooltipRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }

        CanvasGroup group = tooltipRoot.GetComponent<CanvasGroup>();
        if (group == null)
            group = tooltipRoot.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void RefreshRemainingText()
    {
        if (remainingText == null)
            return;
        remainingText.text = string.Empty;
        remainingText.gameObject.SetActive(false);
    }

    private IEnumerator AnimateHotbarWidth(RectTransform rect, float from, float to)
    {
        if (rect == null)
            yield break;

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, hotbarGrowSeconds);
        while (elapsed < duration && rect != null)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.LerpUnclamped(Mathf.Max(0f, from), Mathf.Max(hotbarSlotSize, to), t));
            yield return null;
        }

        if (rect != null)
            rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Max(hotbarSlotSize, to));
        hotbarResizeRoutine = null;
    }

    private void PrepareRoomMessage()
    {
        if (roomMessageRoot == null && roomMessageText != null)
            roomMessageRoot = roomMessageText.transform.parent != null
                ? roomMessageText.transform.parent.gameObject
                : roomMessageText.gameObject;
        if (roomMessageRoot == null)
            return;

        if (roomMessageCanvasGroup == null)
            roomMessageCanvasGroup = roomMessageRoot.GetComponent<CanvasGroup>();
        if (roomMessageCanvasGroup == null)
            roomMessageCanvasGroup = roomMessageRoot.AddComponent<CanvasGroup>();
        roomMessageCanvasGroup.interactable = false;
        roomMessageCanvasGroup.blocksRaycasts = false;

        RectTransform rect = roomMessageRoot.transform as RectTransform;
        if (rect != null)
            roomMessageShownPosition = rect.anchoredPosition;
        HideRoomMessageImmediate();
    }

    private IEnumerator ShowRoomMessageRoutine(float staySeconds)
    {
        RectTransform rect = roomMessageRoot != null
            ? roomMessageRoot.transform as RectTransform
            : null;
        if (rect == null || roomMessageCanvasGroup == null)
            yield break;

        Vector2 hidden = roomMessageShownPosition +
                         Vector2.up * (Mathf.Max(54f, rect.rect.height) + 18f);
        rect.anchoredPosition = hidden;
        roomMessageCanvasGroup.alpha = 0f;

        float elapsed = 0f;
        const float slideSeconds = 0.18f;
        while (elapsed < slideSeconds)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideSeconds));
            rect.anchoredPosition = Vector2.LerpUnclamped(hidden, roomMessageShownPosition, t);
            roomMessageCanvasGroup.alpha = t;
            yield return null;
        }

        rect.anchoredPosition = roomMessageShownPosition;
        roomMessageCanvasGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, staySeconds));

        elapsed = 0f;
        const float hideSeconds = 0.16f;
        while (elapsed < hideSeconds)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / hideSeconds));
            rect.anchoredPosition = Vector2.LerpUnclamped(roomMessageShownPosition, hidden, t);
            roomMessageCanvasGroup.alpha = 1f - t;
            yield return null;
        }

        roomMessageRoutine = null;
        HideRoomMessageImmediate();
    }

    private void HideRoomMessageImmediate()
    {
        if (roomMessageCanvasGroup != null)
            roomMessageCanvasGroup.alpha = 0f;
        if (roomMessageRoot != null)
            roomMessageRoot.SetActive(false);
    }

    private bool IsInsideHotbar(Vector2 position, Camera eventCamera)
    {
        RectTransform rect = HotbarRect;
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, position, eventCamera);
    }

    private IEnumerator PlayPickupRoutine()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null)
                slots[i].transform.localScale = Vector3.zero;

        for (int i = 0; i < slots.Count; i++)
        {
            RestockHotbarSlotUI slot = slots[i];
            if (slot == null)
                continue;

            float elapsed = 0f;
            while (elapsed < pickupPopSeconds)
            {
                elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, pickupPopSeconds));
                float scale = Mathf.LerpUnclamped(0f, 1f, 1f - Mathf.Pow(1f - t, 3f));
                slot.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            slot.transform.localScale = Vector3.one;
            if (pickupSlotDelay > 0f)
                yield return new WaitForSecondsRealtime(pickupSlotDelay);
        }
        pickupRoutine = null;
    }

    private IEnumerator HideNotificationAfterDelay()
    {
        yield return new WaitForSecondsRealtime(notificationSeconds);
        HideNotification();
    }

    private void PlayInvalidReturn(RestockHotbarSlotUI slot)
    {
        if (slot == null)
            return;
        if (returnRoutine != null)
            StopCoroutine(returnRoutine);
        returnRoutine = StartCoroutine(ReturnRoutine(slot));
    }

    private IEnumerator ReturnRoutine(RestockHotbarSlotUI slot)
    {
        float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration && slot != null)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = t < 0.5f
                ? Mathf.Lerp(1f, returnPunchScale, t * 2f)
                : Mathf.Lerp(returnPunchScale, selectedSlot == slot ? 1.08f : 1f, (t - 0.5f) * 2f);
            slot.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        if (slot != null)
            slot.SetSelected(selectedSlot == slot);
        returnRoutine = null;
    }

    private void HideStartReminder()
    {
        startAnyway = null;
        if (startReminderRoot != null)
            startReminderRoot.SetActive(false);
        RestockFlowCoordinator.Instance?.ShowMessage(
            "Restock first: collect deliveries at the truck, then enter Dry Room or Freezer.");
    }

    private void ContinueStartAnyway()
    {
        Action callback = startAnyway;
        startAnyway = null;
        if (startReminderRoot != null)
            startReminderRoot.SetActive(false);
        callback?.Invoke();
    }

    private void ResolveOptionalReferences()
    {
        if (remainingText == null)
            remainingText = hotbarRoot != null
                ? hotbarRoot.transform.Find("Remaining")?.GetComponent<TMP_Text>()
                : null;
        if (tooltipRoot == null && hotbarRoot != null)
            tooltipRoot = hotbarRoot.transform.Find("Tooltip")?.gameObject;
        if (tooltipText == null && tooltipRoot != null)
            tooltipText = tooltipRoot.GetComponentInChildren<TMP_Text>(true);
        if (startReminderRoot == null)
            startReminderRoot = transform.Find("StartReminder")?.gameObject;
        if (startReminderRoot != null)
        {
            if (startReminderText == null)
                startReminderText = startReminderRoot.transform.Find("Panel/Message")?.GetComponent<TMP_Text>();
            if (restockFirstButton == null)
                restockFirstButton = startReminderRoot.transform.Find("Panel/RestockFirst")?.GetComponent<Button>();
            if (startAnywayButton == null)
                startAnywayButton = startReminderRoot.transform.Find("Panel/StartAnyway")?.GetComponent<Button>();
        }
    }

    private static string StorageLabel(RestockStorageType storage)
    {
        return storage == RestockStorageType.Frozen ? "Freezer" : "Dry Room";
    }
}
