using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the Manager complaint loop. Each day rolls a saved 0/1/2/3 encounter
/// allowance shared by real incidents and paced automatic encounters, while
/// safe timing, coached answers and the debug path remain authoritative.
/// </summary>
public sealed class ManagerComplaintSystem : MonoBehaviour
{
    public const string SystemResourcePath = "ManagerComplaints/ManagerComplaintSystem";
    public const string MarkerResourcePath = "ManagerComplaints/CustomerComplaintMarker";

    public static ManagerComplaintSystem Instance { get; private set; }

    [Header("Editable Assets")]
    [SerializeField] private ManagerComplaintSettings settings;
    [SerializeField] private ManagerComplaintMarker markerPrefab;

    [Header("Dialogue")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private RectTransform dialoguePanel;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text headlineText;
    [SerializeField] private TMP_Text customerLineText;
    [SerializeField] private TMP_Text managerResponseText;
    [SerializeField] private TMP_Text coachingText;

    [Header("Responses")]
    [SerializeField] private Button professionalButton;
    [SerializeField] private TMP_Text professionalButtonText;
    [SerializeField] private Button acceptableButton;
    [SerializeField] private TMP_Text acceptableButtonText;
    [SerializeField] private Button poorButton;
    [SerializeField] private TMP_Text poorButtonText;

    [Header("Off-screen Indicator")]
    [SerializeField] private RectTransform overlayCanvasRect;
    [SerializeField] private RectTransform offscreenMarker;
    [SerializeField] private Button offscreenButton;

    private CustomerGroup activeGroup;
    private ManagerComplaintType activeType;
    private ManagerComplaintDefinition activeDefinition;
    private ManagerComplaintMarker worldMarker;
    private bool dialogueOpen;
    private bool resolving;
    private float unansweredDeadline;
    private int savedWeekIndex = -1;
    private int encountersThisWeek;
    private int lastEncounterDay;
    private int complaintScheduleDay;
    private int dailyComplaintAllowance;
    private int complaintsToday;
    private float lastComplaintShiftElapsedSeconds = -10000f;
    private float nextAutomaticSearchUnscaledTime;
    private MainCameraController focusedCamera;
    private Vector3 savedCameraTarget;
    private float savedCameraZoom;
    private bool cameraStateCaptured;
    private Coroutine finishRoutine;

    public bool HasActiveComplaint => activeGroup != null;

    public static ManagerComplaintSystem EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        ManagerComplaintSystem existing = FindFirstObjectByType<ManagerComplaintSystem>(
            FindObjectsInactive.Include);
        if (existing != null)
        {
            if (!existing.gameObject.activeSelf)
                existing.gameObject.SetActive(true);
            return existing;
        }

        GameObject prefabObject = Resources.Load<GameObject>(SystemResourcePath);
        ManagerComplaintSystem prefab = prefabObject != null
            ? prefabObject.GetComponent<ManagerComplaintSystem>()
            : null;
        if (prefab == null)
        {
            Debug.LogError(
                "[ManagerComplaint] Missing or invalid Resources/" +
                SystemResourcePath + ".prefab.");
            return null;
        }

        return Instantiate(prefab);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (settings == null)
            settings = Resources.Load<ManagerComplaintSettings>(
                ManagerComplaintSettings.ResourcePath);
        if (markerPrefab == null)
        {
            GameObject markerObject = Resources.Load<GameObject>(MarkerResourcePath);
            markerPrefab = markerObject != null
                ? markerObject.GetComponent<ManagerComplaintMarker>()
                : null;
        }

        BindButtons();
        HidePresentationImmediate();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        RestoreCamera();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (activeGroup == null)
        {
            if (worldMarker != null)
                CancelActiveComplaint(false);
            TryStartAutomaticComplaint();
            return;
        }

        if (!activeGroup.gameObject.activeInHierarchy)
        {
            CancelActiveComplaint(false);
            return;
        }

        if (!dialogueOpen && !resolving &&
            Time.unscaledTime >= unansweredDeadline)
        {
            ResolveResponse(activeDefinition != null ? activeDefinition.poor : null, true);
        }
    }

    private void LateUpdate()
    {
        UpdateOffscreenIndicator();
    }

    public bool TryRequestComplaint(
        CustomerGroup group,
        ManagerComplaintType type,
        bool bypassScheduleForDebug = false)
    {
        if (settings == null || group == null || activeGroup != null ||
            !group.CanReceiveManagerComplaint)
            return false;

        int day = CurrentDay();
        if (!bypassScheduleForDebug && !CanUseAuthenticEncounter(day))
            return false;

        activeDefinition = settings.GetDefinition(type);
        if (activeDefinition == null)
            return false;

        if (!bypassScheduleForDebug)
            RecordAuthenticEncounter(day);

        activeGroup = group;
        activeType = type;
        dialogueOpen = false;
        resolving = false;
        unansweredDeadline = Time.unscaledTime + settings.unansweredTimeoutSeconds;

        activeGroup.BeginManagerComplaint(type);
        SpawnWorldMarker();
        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public bool DebugForceComplaint(ManagerComplaintType type)
    {
        if (activeGroup != null)
            return false;

        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        CustomerGroup candidate = null;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null || !groups[i].CanReceiveManagerComplaint)
                continue;

            candidate = groups[i];
            if (groups[i].assignedBooth != null)
                break;
        }

        if (candidate == null || !TryRequestComplaint(candidate, type, true))
            return false;

        CasualDiningPolishManager polish = CasualDiningPolishManager.EnsureInstance();
        polish?.RegisterIncident(type == ManagerComplaintType.WrongOrder
            ? DailyIncidentType.WrongOrder
            : DailyIncidentType.OrderFailed);
        return true;
    }

    private void TryStartAutomaticComplaint()
    {
        if (settings == null || !settings.automaticallyCreateAllowedComplaints ||
            Time.timeScale <= 0f || GameDayManager.Instance == null ||
            !GameDayManager.Instance.ServiceActive)
            return;

        if (Time.unscaledTime < nextAutomaticSearchUnscaledTime)
            return;

        nextAutomaticSearchUnscaledTime = Time.unscaledTime +
            Mathf.Max(0.05f, settings.automaticSearchIntervalSeconds);

        if (!CanUseAuthenticEncounter(CurrentDay()))
            return;

        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        CustomerGroup candidate = null;
        int eligibleCount = 0;

        for (int i = 0; i < groups.Length; i++)
        {
            CustomerGroup group = groups[i];
            if (group == null || !group.CanReceiveManagerComplaint ||
                group.state != CustomerGroup.GroupState.Eating)
                continue;

            eligibleCount++;
            if (Random.Range(0, eligibleCount) == 0)
                candidate = group;
        }

        if (candidate == null)
            return;

        ManagerComplaintType type = Random.value <
            Mathf.Clamp01(settings.automaticBurntFoodChance)
                ? ManagerComplaintType.BurntFood
                : ManagerComplaintType.WrongOrder;
        candidate.TryBeginScheduledManagerComplaint(type);
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        NormalizeWeek(CurrentDay());
        data.managerComplaintWeekIndex = savedWeekIndex;
        data.managerComplaintsThisWeek = encountersThisWeek;
        data.managerComplaintLastDay = lastEncounterDay;
        data.managerComplaintScheduleDay = complaintScheduleDay;
        data.managerComplaintDailyAllowance = dailyComplaintAllowance;
        data.managerComplaintsToday = complaintsToday;
        data.managerComplaintLastShiftElapsedSeconds = lastComplaintShiftElapsedSeconds;
    }

    public void ApplySaveData(GameSaveData data)
    {
        CancelActiveComplaint(false);
        if (data == null)
            return;

        savedWeekIndex = data.managerComplaintWeekIndex;
        encountersThisWeek = Mathf.Max(0, data.managerComplaintsThisWeek);
        lastEncounterDay = Mathf.Max(0, data.managerComplaintLastDay);
        complaintScheduleDay = Mathf.Max(0, data.managerComplaintScheduleDay);
        dailyComplaintAllowance = Mathf.Clamp(data.managerComplaintDailyAllowance, 0, 3);
        complaintsToday = Mathf.Clamp(data.managerComplaintsToday, 0, 3);
        lastComplaintShiftElapsedSeconds = data.managerComplaintLastShiftElapsedSeconds;
        NormalizeWeek(Mathf.Max(1, data.currentDay));
    }

    public void ResetRun()
    {
        CancelActiveComplaint(false);
        savedWeekIndex = -1;
        encountersThisWeek = 0;
        lastEncounterDay = 0;
        complaintScheduleDay = 0;
        dailyComplaintAllowance = 0;
        complaintsToday = 0;
        lastComplaintShiftElapsedSeconds = -10000f;
        nextAutomaticSearchUnscaledTime = 0f;
    }

    private bool CanUseAuthenticEncounter(int day)
    {
        if (GameDayManager.Instance == null || !GameDayManager.Instance.ServiceActive)
            return false;

        EnsureDailySchedule(day);
        if (complaintsToday >= dailyComplaintAllowance)
            return false;

        float elapsed = Mathf.Max(0f,
            GameDayManager.Instance.ShiftLengthSeconds - GameDayManager.Instance.TimeRemaining);
        if (elapsed < Mathf.Max(0f, settings.firstComplaintDelaySeconds))
            return false;

        if (GameDayManager.Instance.TimeRemaining <=
            Mathf.Max(0f, settings.stopNewComplaintsBeforeCloseSeconds))
            return false;

        if (elapsed - lastComplaintShiftElapsedSeconds <
            Mathf.Max(0f, settings.minimumSecondsBetweenComplaints))
            return false;

        return true;
    }

    private void RecordAuthenticEncounter(int day)
    {
        NormalizeWeek(day);
        encountersThisWeek++;
        lastEncounterDay = day;
        complaintsToday++;
        if (GameDayManager.Instance != null)
        {
            lastComplaintShiftElapsedSeconds = Mathf.Max(0f,
                GameDayManager.Instance.ShiftLengthSeconds - GameDayManager.Instance.TimeRemaining);
        }
    }

    private void EnsureDailySchedule(int day)
    {
        day = Mathf.Max(1, day);
        if (complaintScheduleDay == day)
            return;

        complaintScheduleDay = day;
        complaintsToday = 0;
        lastComplaintShiftElapsedSeconds = -10000f;

        dailyComplaintAllowance = settings.RollDailyComplaintAllowance(Random.value);
        nextAutomaticSearchUnscaledTime = 0f;

        GameSaveManager.Instance?.RequestSave();
    }

    private void NormalizeWeek(int day)
    {
        int week = Mathf.Max(0, (Mathf.Max(1, day) - 1) / 7);
        if (savedWeekIndex == week)
            return;

        savedWeekIndex = week;
        encountersThisWeek = 0;
    }

    private static int CurrentDay() => GameFlowManager.Instance != null
        ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
        : 1;

    private void SpawnWorldMarker()
    {
        if (markerPrefab == null || activeGroup == null)
            return;

        worldMarker = Instantiate(markerPrefab);
        worldMarker.name = "Manager Complaint Marker";
        worldMarker.Bind(
            activeGroup.ManagerComplaintAnchor,
            settings.markerWorldOffset,
            settings.markerPulseSpeed,
            settings.markerPulseScale,
            settings.visibleMarkerScale,
            OpenActiveComplaint);
    }

    public void OpenActiveComplaint()
    {
        if (activeGroup == null || activeDefinition == null || resolving)
            return;

        dialogueOpen = true;
        activeGroup.SetManagerCallAnimation(false);
        if (worldMarker != null)
            worldMarker.SetWorldMarkerVisible(false);
        if (offscreenMarker != null)
            offscreenMarker.gameObject.SetActive(false);

        PopulateDialogue();
        if (dialogueRoot != null)
            dialogueRoot.SetActive(true);
        if (dialoguePanel != null)
        {
            if (LevelOneUIAccessibility.ReducedMotion)
                dialoguePanel.localScale = Vector3.one;
            else
                StartCoroutine(PopPanel(dialoguePanel));
        }

        FocusCameraOnGroup();
    }

    private void PopulateDialogue()
    {
        if (headlineText != null)
            headlineText.text = activeDefinition.headline;
        if (customerLineText != null)
            customerLineText.text = activeDefinition.PickCustomerLine();
        if (portraitImage != null)
        {
            Sprite portrait = activeGroup != null ? activeGroup.GetCustomerTypeImage() : null;
            portraitImage.sprite = portrait != null ? portrait : activeDefinition.fallbackPortrait;
            portraitImage.enabled = portraitImage.sprite != null;
            portraitImage.preserveAspect = true;
        }

        if (managerResponseText != null)
            managerResponseText.text = "CHOOSE THE MANAGER'S RESPONSE";
        if (coachingText != null)
            coachingText.text = "LISTEN  >  ACKNOWLEDGE  >  APOLOGIZE  >  SOLVE";

        SetResponseButton(
            professionalButton,
            professionalButtonText,
            activeDefinition.professional);
        SetResponseButton(
            acceptableButton,
            acceptableButtonText,
            activeDefinition.acceptable);
        SetResponseButton(poorButton, poorButtonText, activeDefinition.poor);
        SetResponseButtonsInteractable(true);
    }

    private static void SetResponseButton(
        Button button,
        TMP_Text label,
        ManagerComplaintResponseDefinition response)
    {
        if (button != null)
            button.gameObject.SetActive(response != null);
        if (label != null && response != null)
            label.text = response.buttonHeading + "\n\"" + response.managerLine + "\"";
    }

    private void BindButtons()
    {
        BindResponseButton(
            professionalButton,
            () => ResolveResponse(activeDefinition?.professional, false));
        BindResponseButton(
            acceptableButton,
            () => ResolveResponse(activeDefinition?.acceptable, false));
        BindResponseButton(
            poorButton,
            () => ResolveResponse(activeDefinition?.poor, false));

        if (offscreenButton != null)
        {
            offscreenButton.onClick.RemoveAllListeners();
            offscreenButton.onClick.AddListener(OpenActiveComplaint);
        }
    }

    private static void BindResponseButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void ResolveResponse(
        ManagerComplaintResponseDefinition response,
        bool unanswered)
    {
        if (activeGroup == null || resolving)
            return;

        resolving = true;
        dialogueOpen = true;
        if (dialogueRoot != null)
            dialogueRoot.SetActive(true);
        if (worldMarker != null)
            worldMarker.SetWorldMarkerVisible(false);
        if (offscreenMarker != null)
            offscreenMarker.gameObject.SetActive(false);

        response ??= activeDefinition != null ? activeDefinition.poor : null;
        ManagerComplaintResponseQuality quality = response != null
            ? response.quality
            : ManagerComplaintResponseQuality.Poor;

        int orderTotal = Mathf.Max(1, activeGroup.GetCurrentOrderTotal());
        int cost = response != null
            ? Mathf.CeilToInt(orderTotal * Mathf.Max(0f, response.orderCostMultiplier))
            : 0;
        if (cost > 0)
        {
            if (quality == ManagerComplaintResponseQuality.Acceptable)
            {
                if (DailyFinanceBridge.Instance != null)
                    DailyFinanceBridge.Instance.ApplyRefund(cost, "Manager complaint refund");
                else
                    MoneyManager.Instance?.ForceSpend(cost, "Manager complaint refund");

                activeGroup.ShowRefundPopup(cost);
                GameDayManager.Instance?.RefreshRevenueUI();
            }
            else
            {
                MoneyManager.Instance?.ForceSpend(cost, "Manager complaint remake");
            }
        }

        activeGroup.ResolveManagerComplaint(quality, activeType);
        SetResponseButtonsInteractable(false);

        if (managerResponseText != null)
            managerResponseText.text = unanswered
                ? "MANAGER RESPONSE\nThe customer was ignored."
                : "MANAGER RESPONSE\n\"" + (response?.managerLine ?? string.Empty) + "\"";

        if (coachingText != null)
        {
            string prefix = quality switch
            {
                ManagerComplaintResponseQuality.Professional => "GOOD APPROACH",
                ManagerComplaintResponseQuality.Acceptable => "ACCEPTABLE APPROACH",
                _ => "POOR APPROACH"
            };
            string detail = unanswered
                ? "Respond before the customer gives up."
                : response?.coachingFeedback ?? string.Empty;
            coachingText.text = prefix + "\n" + detail +
                                (cost > 0 ? "  COST: P" + cost : string.Empty);
            if (response != null)
                coachingText.color = response.feedbackColor;
        }

        GameSaveManager.Instance?.RequestSave();
        if (finishRoutine != null)
            StopCoroutine(finishRoutine);
        finishRoutine = StartCoroutine(FinishAfterFeedback());
    }

    private IEnumerator FinishAfterFeedback()
    {
        yield return new WaitForSecondsRealtime(
            settings != null ? settings.responseFeedbackSeconds : 2f);
        CancelActiveComplaint(true);
        finishRoutine = null;
    }

    private void SetResponseButtonsInteractable(bool interactable)
    {
        if (professionalButton != null) professionalButton.interactable = interactable;
        if (acceptableButton != null) acceptableButton.interactable = interactable;
        if (poorButton != null) poorButton.interactable = interactable;
    }

    private void FocusCameraOnGroup()
    {
        if (activeGroup == null)
            return;

        focusedCamera = FindFirstObjectByType<MainCameraController>();
        if (focusedCamera == null)
            return;

        savedCameraTarget = focusedCamera.GetRigTargetPosition();
        savedCameraZoom = focusedCamera.GetTargetOrthographicSize();
        cameraStateCaptured = true;

        Vector3 target = activeGroup.ManagerComplaintAnchor.position +
                         settings.cameraFramingOffset;
        focusedCamera.SetRigTargetPosition(target);
        focusedCamera.SetTargetOrthographicSize(settings.focusedOrthographicSize);
    }

    private void RestoreCamera()
    {
        if (!cameraStateCaptured || focusedCamera == null)
            return;

        focusedCamera.SetRigTargetPosition(savedCameraTarget);
        focusedCamera.SetTargetOrthographicSize(savedCameraZoom);
        focusedCamera = null;
        cameraStateCaptured = false;
    }

    private void UpdateOffscreenIndicator()
    {
        if (offscreenMarker == null || overlayCanvasRect == null ||
            activeGroup == null || worldMarker == null || dialogueOpen || resolving)
        {
            if (offscreenMarker != null && offscreenMarker.gameObject.activeSelf)
                offscreenMarker.gameObject.SetActive(false);
            return;
        }

        Camera camera = Camera.main;
        bool worldVisible = worldMarker.IsVisibleFrom(camera);
        worldMarker.SetWorldMarkerVisible(worldVisible);
        if (camera == null || worldVisible)
        {
            offscreenMarker.gameObject.SetActive(false);
            return;
        }

        Vector3 screen = camera.WorldToScreenPoint(worldMarker.WorldPosition);
        if (screen.z < 0f)
        {
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
        }

        float padding = settings != null ? settings.screenEdgePadding : 58f;
        Vector2 clamped = new Vector2(
            Mathf.Clamp(screen.x, padding, Screen.width - padding),
            Mathf.Clamp(screen.y, padding, Screen.height - padding));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            overlayCanvasRect,
            clamped,
            null,
            out Vector2 localPoint);
        offscreenMarker.anchoredPosition = localPoint;
        offscreenMarker.localScale = LevelOneUIAccessibility.ReducedMotion
            ? Vector3.one
            : Vector3.one * Mathf.Lerp(
                1f,
                settings != null ? settings.markerPulseScale : 1.12f,
                (Mathf.Sin(Time.unscaledTime *
                    (settings != null ? settings.markerPulseSpeed : 3.6f)) + 1f) * 0.5f);
        if (!offscreenMarker.gameObject.activeSelf)
            offscreenMarker.gameObject.SetActive(true);
    }

    private IEnumerator PopPanel(RectTransform panel)
    {
        panel.localScale = Vector3.one * 0.88f;
        float elapsed = 0f;
        const float duration = 0.2f;
        while (elapsed < duration && panel != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, t);
            yield return null;
        }
        if (panel != null)
            panel.localScale = Vector3.one;
    }

    private void CancelActiveComplaint(bool responseCompleted)
    {
        if (finishRoutine != null && !responseCompleted)
        {
            StopCoroutine(finishRoutine);
            finishRoutine = null;
        }

        if (activeGroup != null)
        {
            activeGroup.SetManagerCallAnimation(false);
            if (!responseCompleted)
                activeGroup.CancelManagerComplaint();
        }

        if (worldMarker != null)
            Destroy(worldMarker.gameObject);
        worldMarker = null;
        activeGroup = null;
        activeDefinition = null;
        dialogueOpen = false;
        resolving = false;
        HidePresentationImmediate();
        RestoreCamera();
    }

    private void HidePresentationImmediate()
    {
        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);
        if (offscreenMarker != null)
            offscreenMarker.gameObject.SetActive(false);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (activeGroup != null && scene.name != "Lobby1")
            CancelActiveComplaint(false);
    }
}
