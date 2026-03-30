using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public enum TutorialDay
    {
        Day1Host = 0,
        Day2Waiter = 1,
        Day3Cashier = 2,
        Day4Busser = 3,
        Day5AllTogether = 4,
        Day5LobbyMastery = 4
    }

    public enum TutorialPhase
    {
        None,
        Intro,

        GreetCustomer,
        AssignTable,

        TakeOrder,
        ConfirmOrder,
        SubmitOrder,
        ServeFood,
        PickupBill,
        DeliverBill,
        PickupMoney,
        CollectPayment,

        CashierWaitForMoney,
        CashierProcessPayment,

        CleanTray,

        PracticeGameplay,

        AllTogetherGameplay,
        LobbyMasteryGameplay = AllTogetherGameplay,

        Complete
    }

    [Serializable]
    public class IntroStep
    {
        public string roleName;
        [TextArea(2, 5)] public string message;
        public Transform cameraAnchor;
        public GameObject highlightTarget;
    }

    [Serializable]
    public class DayConfig
    {
        public TutorialDay day;

        [Header("Info")]
        public string dayTitle = "Day";
        public string roleName = "Role";
        [TextArea(2, 5)] public string dayGoalMessage;
        [TextArea(2, 6)] public string introMessage;
        [TextArea(2, 6)] public string practiceStartMessage;
        [TextArea(2, 5)] public string completionMessage;

        [Header("Waiter Spawned Setup")]
        public List<Booth> waiterTutorialBooths = new List<Booth>();
        public float waiterOrderDelay = 0.25f;
        public bool waiterMarkGroupAsGreeted = true;

        [Header("Scene Setup")]
        public bool autoSpawnGroups;
        public int spawnGroupCount = 1;
        public float firstSpawnDelay = 0.35f;
        public List<CustomerGroup> preplacedGroups = new List<CustomerGroup>();
        public List<FoodTray> preplacedDirtyTrays = new List<FoodTray>();

        [Header("Practice")]
        public bool enablePractice = true;
        public float practiceDurationSeconds = 120f;
        public float practiceSpawnIntervalSeconds = 30f;
        public int practiceTargetCount = 1;
        public bool autoSpawnDuringPractice;
        public int practiceSpawnCountPerWave = 1;

        [Header("Visual Guidance")]
        public Transform cameraAnchor;
        public GameObject roleHighlightTarget;
    }

    private int waiterBoothCursor;

    [Header("Scene References")]
    [SerializeField] private GroupSpawner groupSpawner;
    [SerializeField] private LobbyLineManager lobbyLineManager;
    [SerializeField] private RoleManager roleManager;
    [SerializeField] private OrderFlowManager orderFlowManager;
    [SerializeField] private BillManager billManager;
    [SerializeField] private KitchenManager kitchenManager;
    [SerializeField] private RoleCameraController roleCameraController;

    [Header("Tutorial Helpers")]
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private TutorialRoleHighlight roleHighlight;
    [SerializeField] private TutorialArrowManager arrowManager;
    [SerializeField] private TutorialSceneWatcher sceneWatcher;

    [Header("Opening / Intro")]
    [SerializeField] private bool playOpeningSequenceBeforeDay1 = true;
    [SerializeField] private IntroStep[] introSteps;
    [SerializeField] private bool returnCameraAfterIntro = true;
    [SerializeField] private Transform gameplayCameraAnchorAfterIntro;
    [SerializeField] private string openingDoneMessage = "Now that you know the roles, let’s begin with the Host. Greet the first customer.";

    [Header("UI")]
    [SerializeField] private GameObject tutorialIntroPanel;
    [SerializeField] private Button startTutorialButton;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private Slider progressBar;

    [Header("Dialogue")]
    [SerializeField] private bool showDialoguePerPhase = true;
    [SerializeField] private float phaseDialogueDuration = 2.6f;

    [Header("Tutorial Days")]
    [SerializeField] private TutorialDay currentDay = TutorialDay.Day1Host;
    [SerializeField] private DayConfig[] dayConfigs = new DayConfig[5];

    [Header("Completion UI")]
    [SerializeField] private GameObject tutorialCompletePanel;
    [SerializeField] private TMP_Text tutorialCompleteText;
    [SerializeField] private Button finishButton;
    [SerializeField] private bool showCompletionPanel = false;

    [Header("Runtime")]
    [SerializeField] private TutorialPhase currentPhase = TutorialPhase.None;
    [SerializeField] private CustomerGroup activeTutorialGroup;
    [SerializeField] private FoodTray activeDirtyTray;
    [SerializeField] private bool tutorialStarted;

    private int currentIntroIndex = -1;
    private bool openingSequenceFinished;
    private bool completionShown;

    private bool notepadOpened;
    private bool orderConfirmed;
    private bool cashierOpened;
    private bool cashierConfirmed;

    private bool practiceRunning;
    private float practiceTimer;
    private float practiceSpawnTimer;
    private int practiceProgressCount;

    private readonly List<CustomerGroup> spawnedGroups = new List<CustomerGroup>();
    private readonly HashSet<CustomerGroup> watchedGroups = new HashSet<CustomerGroup>();
    private readonly HashSet<FoodTray> watchedDirtyTrays = new HashSet<FoodTray>();

    public TutorialDay CurrentDay => currentDay;
    public TutorialPhase CurrentPhase => currentPhase;
    public CustomerGroup ActiveTutorialGroup => activeTutorialGroup;
    public FoodTray ActiveDirtyTray => activeDirtyTray;
    public bool TutorialStarted => tutorialStarted;

    private bool IsLobbyMasteryDay => currentDay == TutorialDay.Day5AllTogether;

    [SerializeField] private TMP_Text practiceTimerText;

    private void Reset()
    {
        EnsureDayConfigs(true);
        showCompletionPanel = false;
    }

    private void OnValidate()
    {
        EnsureDayConfigs(false);

        if (phaseDialogueDuration <= 0f)
            phaseDialogueDuration = 2.6f;

        if (string.IsNullOrWhiteSpace(openingDoneMessage))
            openingDoneMessage = "Now that you know the roles, let’s begin with the Host. Greet the first customer.";
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResolveSceneReferences();
        EnsureDayConfigs(false);

        if (tutorialIntroPanel != null)
            tutorialIntroPanel.SetActive(true);

        if (tutorialCompletePanel != null)
            tutorialCompletePanel.SetActive(false);
    }

    private void Start()
    {
        if (startTutorialButton != null)
        {
            startTutorialButton.onClick.RemoveAllListeners();
            startTutorialButton.onClick.AddListener(StartTutorial);
        }

        if (finishButton != null)
        {
            finishButton.onClick.RemoveAllListeners();
            finishButton.onClick.AddListener(OnFinishTutorial);
        }

        RefreshUI();
    }

    private void Update()
    {
        RefreshRuntimeTargets();
        UpdatePracticeMode();
        RefreshUI();
    }

    [ContextMenu("Auto Fill Day Configs")]
    private void AutoFillDayConfigs()
    {
        EnsureDayConfigs(true);
    }

    private void EnsureDayConfigs(bool overwriteDefaults)
    {
        const int dayCount = 5;

        DayConfig[] rebuilt = new DayConfig[dayCount];

        if (dayConfigs != null)
        {
            for (int i = 0; i < dayConfigs.Length; i++)
            {
                DayConfig cfg = dayConfigs[i];
                if (cfg == null)
                    continue;

                int index = Mathf.Clamp((int)cfg.day, 0, dayCount - 1);
                if (rebuilt[index] == null)
                    rebuilt[index] = cfg;
            }
        }

        dayConfigs = rebuilt;

        EnsureDayConfig(0, TutorialDay.Day1Host, overwriteDefaults);
        EnsureDayConfig(1, TutorialDay.Day2Waiter, overwriteDefaults);
        EnsureDayConfig(2, TutorialDay.Day3Cashier, overwriteDefaults);
        EnsureDayConfig(3, TutorialDay.Day4Busser, overwriteDefaults);
        EnsureDayConfig(4, TutorialDay.Day5AllTogether, overwriteDefaults);
    }

    private void EnsureDayConfig(int index, TutorialDay day, bool overwriteDefaults)
    {
        bool created = false;

        if (dayConfigs[index] == null)
        {
            dayConfigs[index] = new DayConfig();
            created = true;
        }

        DayConfig config = dayConfigs[index];
        config.day = day;

        if (config.preplacedGroups == null)
            config.preplacedGroups = new List<CustomerGroup>();

        if (config.preplacedDirtyTrays == null)
            config.preplacedDirtyTrays = new List<FoodTray>();

        ApplyDefaultDayConfig(config, day, overwriteDefaults || created);
    }

    private void ApplyDefaultDayConfig(DayConfig config, TutorialDay day, bool force)
    {
        switch (day)
        {
            case TutorialDay.Day1Host:
                SetStringDefault(ref config.dayTitle, "Day 1 - Host", force);
                SetStringDefault(ref config.roleName, "Host", force);
                SetStringDefault(ref config.dayGoalMessage, "Goal: teach the player how to greet and assign customers to a table.", force);
                SetStringDefault(ref config.introMessage, "Today you are the Host. First I will teach you how to greet customers and assign them to a table. After that, you will do it yourself.", force);
                SetStringDefault(ref config.practiceStartMessage, "Now do it yourself. Assign 4 customer groups to tables within 2 minutes.", force);
                SetStringDefault(ref config.completionMessage, "Good job. You finished the Host tutorial day.", force);

                if (force)
                {
                    config.autoSpawnGroups = true;
                    config.autoSpawnDuringPractice = true;
                    config.enablePractice = true;
                }

                SetIntDefault(ref config.spawnGroupCount, 1, force);
                SetFloatDefault(ref config.firstSpawnDelay, 0.35f, force);
                SetFloatDefault(ref config.practiceDurationSeconds, 120f, force);
                SetFloatDefault(ref config.practiceSpawnIntervalSeconds, 30f, force);
                SetIntDefault(ref config.practiceTargetCount, 4, force);
                SetIntDefault(ref config.practiceSpawnCountPerWave, 1, force);
                break;

            case TutorialDay.Day2Waiter:
                SetStringDefault(ref config.dayTitle, "Day 2 - Waiter", force);
                SetStringDefault(ref config.roleName, "Waiter", force);
                SetStringDefault(ref config.dayGoalMessage, "Goal: teach the player how to take order, get bill, give bill, take money, and bring money to the cashier booth.", force);
                SetStringDefault(ref config.introMessage, "Today you are the Waiter. I will teach you the full waiter flow step by step. Then you will handle the flow yourself.", force);
                SetStringDefault(ref config.practiceStartMessage, "Now do it yourself. Handle 2 customer groups within 2 minutes.", force);
                SetStringDefault(ref config.completionMessage, "Good job. You finished the Waiter tutorial day.", force);

                if (force)
                {
                    config.autoSpawnGroups = true;
                    config.autoSpawnDuringPractice = true;
                    config.enablePractice = true;
                }

                SetIntDefault(ref config.spawnGroupCount, 1, force);
                SetFloatDefault(ref config.firstSpawnDelay, 0.35f, force);
                SetFloatDefault(ref config.practiceDurationSeconds, 120f, force);
                SetFloatDefault(ref config.practiceSpawnIntervalSeconds, 30f, force);
                SetIntDefault(ref config.practiceTargetCount, 2, force);
                SetIntDefault(ref config.practiceSpawnCountPerWave, 1, force);
                SetFloatDefault(ref config.waiterOrderDelay, 0.25f, force);
                break;

            case TutorialDay.Day3Cashier:
                SetStringDefault(ref config.dayTitle, "Day 3 - Cashier", force);
                SetStringDefault(ref config.roleName, "Cashier", force);
                SetStringDefault(ref config.dayGoalMessage, "Goal: teach the player how to use the POS.", force);
                SetStringDefault(ref config.introMessage, "Today you are the Cashier. I will teach you how to wait for payment and process it in the POS. Then you will do it yourself.", force);
                SetStringDefault(ref config.practiceStartMessage, "Now do it yourself. Process 4 payments within 2 minutes.", force);
                SetStringDefault(ref config.completionMessage, "Good job. You finished the Cashier tutorial day.", force);

                if (force)
                {
                    config.autoSpawnGroups = false;
                    config.autoSpawnDuringPractice = false;
                    config.enablePractice = true;
                }

                SetIntDefault(ref config.spawnGroupCount, 1, force);
                SetFloatDefault(ref config.firstSpawnDelay, 0.35f, force);
                SetFloatDefault(ref config.practiceDurationSeconds, 120f, force);
                SetFloatDefault(ref config.practiceSpawnIntervalSeconds, 30f, force);
                SetIntDefault(ref config.practiceTargetCount, 4, force);
                SetIntDefault(ref config.practiceSpawnCountPerWave, 1, force);
                break;

            case TutorialDay.Day4Busser:
                SetStringDefault(ref config.dayTitle, "Day 4 - Busser", force);
                SetStringDefault(ref config.roleName, "Busser", force);
                SetStringDefault(ref config.dayGoalMessage, "Goal: teach the player how to pick up and clean trays.", force);
                SetStringDefault(ref config.introMessage, "Today you are the Busser. I will teach you how to pick up trays and clean tables. Then you will clean on your own.", force);
                SetStringDefault(ref config.practiceStartMessage, "Now do it yourself. Clean as many trays as you can within 2 minutes.", force);
                SetStringDefault(ref config.completionMessage, "Good job. You finished the Busser tutorial day.", force);

                if (force)
                {
                    config.autoSpawnGroups = false;
                    config.autoSpawnDuringPractice = false;
                    config.enablePractice = true;
                }

                SetIntDefault(ref config.spawnGroupCount, 1, force);
                SetFloatDefault(ref config.firstSpawnDelay, 0.35f, force);
                SetFloatDefault(ref config.practiceDurationSeconds, 120f, force);
                SetFloatDefault(ref config.practiceSpawnIntervalSeconds, 30f, force);
                SetIntDefault(ref config.practiceTargetCount, 4, force);
                SetIntDefault(ref config.practiceSpawnCountPerWave, 1, force);
                break;

            case TutorialDay.Day5AllTogether:
                SetStringDefault(ref config.dayTitle, "Day 5 - All Together", force);
                SetStringDefault(ref config.roleName, "Host", force);
                SetStringDefault(ref config.dayGoalMessage, "Goal: explain each process while playing, including mistakes, customer mood, arrows, messages, and indicators.", force);
                SetStringDefault(ref config.introMessage, "Today everything comes together. Play the normal lobby flow while I explain what the game is tracking.", force);
                SetStringDefault(ref config.practiceStartMessage, "Play the lobby flow with all guidance active.", force);
                SetStringDefault(ref config.completionMessage, "Good job. You finished the All Together tutorial day.", force);

                if (force)
                {
                    config.autoSpawnGroups = true;
                    config.autoSpawnDuringPractice = true;
                    config.enablePractice = false;
                }

                SetIntDefault(ref config.spawnGroupCount, 1, force);
                SetFloatDefault(ref config.firstSpawnDelay, 0.35f, force);
                SetFloatDefault(ref config.practiceDurationSeconds, 120f, force);
                SetFloatDefault(ref config.practiceSpawnIntervalSeconds, 30f, force);
                SetIntDefault(ref config.practiceTargetCount, 1, force);
                SetIntDefault(ref config.practiceSpawnCountPerWave, 1, force);
                break;
        }
    }

    private void SetStringDefault(ref string value, string fallback, bool force)
    {
        if (force || string.IsNullOrWhiteSpace(value))
            value = fallback;
    }

    private void SetIntDefault(ref int value, int fallback, bool force)
    {
        if (force || value <= 0)
            value = fallback;
    }

    private void SetFloatDefault(ref float value, float fallback, bool force)
    {
        if (force || value <= 0f)
            value = fallback;
    }

    private void ResolveSceneReferences()
    {
        if (groupSpawner == null)
            groupSpawner = FindFirstObjectByType<GroupSpawner>();

        if (lobbyLineManager == null)
            lobbyLineManager = FindFirstObjectByType<LobbyLineManager>();

        if (roleManager == null)
            roleManager = FindFirstObjectByType<RoleManager>();

        if (orderFlowManager == null)
            orderFlowManager = FindFirstObjectByType<OrderFlowManager>();

        if (billManager == null)
            billManager = FindFirstObjectByType<BillManager>();

        if (kitchenManager == null)
            kitchenManager = FindFirstObjectByType<KitchenManager>();

        if (roleCameraController == null)
            roleCameraController = FindFirstObjectByType<RoleCameraController>();

        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<TutorialDialogueUI>(FindObjectsInactive.Include);

        if (roleHighlight == null)
            roleHighlight = FindFirstObjectByType<TutorialRoleHighlight>(FindObjectsInactive.Include);

        if (arrowManager == null)
            arrowManager = GetComponent<TutorialArrowManager>();

        if (sceneWatcher == null)
            sceneWatcher = GetComponent<TutorialSceneWatcher>();
    }

    public void SetCurrentDay(int dayIndex)
    {
        currentDay = (TutorialDay)Mathf.Clamp(dayIndex, 0, 4);
        RefreshUI();
    }

    public void SetCurrentDay(TutorialDay day)
    {
        currentDay = day;
        RefreshUI();
    }

    public void StartTutorial()
    {
        tutorialStarted = true;
        openingSequenceFinished = false;
        completionShown = false;
        currentIntroIndex = -1;

        currentPhase = TutorialPhase.None;

        activeTutorialGroup = null;
        activeDirtyTray = null;

        notepadOpened = false;
        orderConfirmed = false;
        cashierOpened = false;
        cashierConfirmed = false;

        practiceRunning = false;
        practiceTimer = 0f;
        practiceSpawnTimer = 0f;
        practiceProgressCount = 0;

        CancelInvoke(nameof(SpawnConfiguredGroups));

        spawnedGroups.Clear();
        watchedGroups.Clear();
        watchedDirtyTrays.Clear();

        if (tutorialIntroPanel != null)
            tutorialIntroPanel.SetActive(false);

        if (tutorialCompletePanel != null)
            tutorialCompletePanel.SetActive(false);

        ClearGuidance();

        if (sceneWatcher != null)
            sceneWatcher.ResetWatcher();

        bool shouldPlayOpening =
            currentDay == TutorialDay.Day1Host &&
            playOpeningSequenceBeforeDay1 &&
            introSteps != null &&
            introSteps.Length > 0;

        if (shouldPlayOpening)
        {
            SetPhase(TutorialPhase.Intro);
            PlayNextIntroStep();
            return;
        }

        openingSequenceFinished = true;
        StartCurrentDayFlow();
    }

    private void PlayNextIntroStep()
    {
        currentIntroIndex++;

        if (introSteps == null || introSteps.Length == 0 || currentIntroIndex >= introSteps.Length)
        {
            FinishOpeningSequence();
            return;
        }

        IntroStep step = introSteps[currentIntroIndex];
        if (step == null)
        {
            PlayNextIntroStep();
            return;
        }

        if (roleCameraController != null && step.cameraAnchor != null)
            roleCameraController.PanToTarget(step.cameraAnchor);

        if (roleHighlight != null)
            roleHighlight.Show(step.highlightTarget);

        if (dialogueUI != null)
        {
            dialogueUI.ShowManual(
                string.IsNullOrWhiteSpace(step.roleName) ? "Manager" : step.roleName,
                step.message,
                PlayNextIntroStep
            );
        }
        else
        {
            PlayNextIntroStep();
        }
    }

    private void FinishOpeningSequence()
    {
        if (roleHighlight != null)
            roleHighlight.Hide();

        if (returnCameraAfterIntro && roleCameraController != null && gameplayCameraAnchorAfterIntro != null)
            roleCameraController.PanToTarget(gameplayCameraAnchorAfterIntro);

        openingSequenceFinished = true;

        if (dialogueUI != null && !string.IsNullOrWhiteSpace(openingDoneMessage))
        {
            dialogueUI.ShowManual("Manager", openingDoneMessage, StartCurrentDayFlow);
        }
        else
        {
            StartCurrentDayFlow();
        }
    }

    private void StartCurrentDayFlow()
    {
        completionShown = false;
        activeTutorialGroup = null;
        activeDirtyTray = null;

        notepadOpened = false;
        orderConfirmed = false;
        cashierOpened = false;
        cashierConfirmed = false;

        practiceRunning = false;
        practiceTimer = 0f;
        practiceSpawnTimer = 0f;
        practiceProgressCount = 0;

        CancelInvoke(nameof(SpawnConfiguredGroups));

        if (tutorialCompletePanel != null)
            tutorialCompletePanel.SetActive(false);

        if (sceneWatcher != null)
            sceneWatcher.ResetWatcher();

        DayConfig config = GetCurrentDayConfig();
        if (config == null)
        {
            Debug.LogWarning("[TutorialManager] No DayConfig found for " + currentDay);
            return;
        }

        FocusDayPresentation(config);
        TrySelectRole(config.roleName);
        ConfigureCurrentDayScene();

        string introMessage = BuildDayIntroMessage(config);

        if (dialogueUI != null && !string.IsNullOrWhiteSpace(introMessage))
        {
            dialogueUI.ShowManual("Manager", introMessage, BeginCurrentDayGameplay);
        }
        else
        {
            BeginCurrentDayGameplay();
        }
    }

    private string BuildDayIntroMessage(DayConfig config)
    {
        if (config == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(config.dayGoalMessage) && !string.IsNullOrWhiteSpace(config.introMessage))
            return config.dayGoalMessage + "\n\n" + config.introMessage;

        if (!string.IsNullOrWhiteSpace(config.introMessage))
            return config.introMessage;

        return config.dayGoalMessage;
    }

    private void BeginCurrentDayGameplay()
    {
        switch (currentDay)
        {
            case TutorialDay.Day1Host:
                SetPhase(TutorialPhase.GreetCustomer);
                break;

            case TutorialDay.Day2Waiter:
                SetPhase(TutorialPhase.TakeOrder);
                break;

            case TutorialDay.Day3Cashier:
                SetPhase(TutorialPhase.CashierWaitForMoney);
                break;

            case TutorialDay.Day4Busser:
                SetPhase(TutorialPhase.CleanTray);
                break;

            case TutorialDay.Day5AllTogether:
                SetPhase(TutorialPhase.AllTogetherGameplay);
                break;
        }
    }

    private void ConfigureCurrentDayScene()
    {
        DayConfig config = GetCurrentDayConfig();
        if (config == null)
            return;

        RegisterPreplacedDirtyTrays(config.preplacedDirtyTrays);

        if (currentDay == TutorialDay.Day3Cashier || currentDay == TutorialDay.Day4Busser)
            RegisterPreplacedGroups(config.preplacedGroups);

        if (config.autoSpawnGroups && groupSpawner != null)
            Invoke(nameof(SpawnConfiguredGroups), Mathf.Max(0f, config.firstSpawnDelay));

        activeTutorialGroup = GetBestGroupForCurrentDay(null);
        activeDirtyTray = GetBestTrayForCurrentDay(null);
    }

    private void SpawnConfiguredGroups()
    {
        DayConfig config = GetCurrentDayConfig();
        if (config == null || groupSpawner == null || !tutorialStarted)
            return;

        int count = Mathf.Max(1, config.spawnGroupCount);

        for (int i = 0; i < count; i++)
        {
            CustomerGroup group = groupSpawner.SpawnGroup();
            if (group == null)
                continue;

            spawnedGroups.Add(group);
            RegisterGroupWatcher(group);
            ConfigureSpawnedGroupForCurrentDay(group, false);
        }

        activeTutorialGroup = GetBestGroupForCurrentDay(null);
    }

    private void RegisterPreplacedGroups(List<CustomerGroup> groups)
    {
        if (groups == null)
            return;

        for (int i = 0; i < groups.Count; i++)
            RegisterGroupWatcher(groups[i]);
    }

    private void RegisterPreplacedDirtyTrays(List<FoodTray> trays)
    {
        if (trays == null)
            return;

        for (int i = 0; i < trays.Count; i++)
        {
            FoodTray tray = trays[i];
            if (tray == null || watchedDirtyTrays.Contains(tray))
                continue;

            watchedDirtyTrays.Add(tray);

            if (activeDirtyTray == null)
                activeDirtyTray = tray;
        }
    }

    private void RegisterGroupWatcher(CustomerGroup group)
    {
        if (group == null || watchedGroups.Contains(group))
            return;

        watchedGroups.Add(group);

        TutorialGroupWatcher watcher = group.GetComponent<TutorialGroupWatcher>();
        if (watcher == null)
            watcher = group.gameObject.AddComponent<TutorialGroupWatcher>();

        watcher.Init(group);
    }

    private void FocusDayPresentation(DayConfig config)
    {
        if (config == null)
            return;

        if (roleCameraController != null && config.cameraAnchor != null)
            roleCameraController.PanToTarget(config.cameraAnchor);

        if (roleHighlight != null)
            roleHighlight.Show(config.roleHighlightTarget);
    }

    private void TrySelectRole(string roleName)
    {
        if (roleManager == null || string.IsNullOrWhiteSpace(roleName))
            return;

        roleManager.SendMessage("SelectRoleByName", roleName, SendMessageOptions.DontRequireReceiver);
        roleManager.SendMessage("SwitchRoleByName", roleName, SendMessageOptions.DontRequireReceiver);
        roleManager.SendMessage("SetCurrentRoleByName", roleName, SendMessageOptions.DontRequireReceiver);
    }

    private DayConfig GetCurrentDayConfig()
    {
        if (dayConfigs == null)
            return null;

        for (int i = 0; i < dayConfigs.Length; i++)
        {
            if (dayConfigs[i] != null && dayConfigs[i].day == currentDay)
                return dayConfigs[i];
        }

        return null;
    }

    public void SetPhase(TutorialPhase newPhase)
    {
        if (currentPhase == newPhase)
            return;

        currentPhase = newPhase;

        if (newPhase != TutorialPhase.TakeOrder && newPhase != TutorialPhase.ConfirmOrder)
        {
            notepadOpened = false;
            orderConfirmed = false;
        }

        if (newPhase != TutorialPhase.CollectPayment &&
            newPhase != TutorialPhase.CashierWaitForMoney &&
            newPhase != TutorialPhase.CashierProcessPayment)
        {
            cashierOpened = false;
            cashierConfirmed = false;
        }

        RefreshRuntimeTargets();
        RefreshUI();

        if (currentPhase == TutorialPhase.Complete)
        {
            CompleteCurrentDay();
            return;
        }

        if (currentPhase == TutorialPhase.Intro)
            return;

        if (showDialoguePerPhase && dialogueUI != null)
            ShowPhaseDialogue(currentPhase);
    }

    public void AdvancePhase()
    {
        switch (currentDay)
        {
            case TutorialDay.Day1Host:
                switch (currentPhase)
                {
                    case TutorialPhase.GreetCustomer:
                        SetPhase(TutorialPhase.AssignTable);
                        break;

                    case TutorialPhase.AssignTable:
                        StartPracticeOrComplete();
                        break;
                }
                break;

            case TutorialDay.Day2Waiter:
                switch (currentPhase)
                {
                    case TutorialPhase.TakeOrder:
                    case TutorialPhase.ConfirmOrder:
                        SetPhase(TutorialPhase.SubmitOrder);
                        break;

                    case TutorialPhase.SubmitOrder:
                        SetPhase(TutorialPhase.ServeFood);
                        break;

                    case TutorialPhase.ServeFood:
                        SetPhase(TutorialPhase.PickupBill);
                        break;

                    case TutorialPhase.PickupBill:
                        SetPhase(TutorialPhase.DeliverBill);
                        break;

                    case TutorialPhase.DeliverBill:
                        SetPhase(TutorialPhase.PickupMoney);
                        break;

                    case TutorialPhase.PickupMoney:
                        SetPhase(TutorialPhase.CollectPayment);
                        break;

                    case TutorialPhase.CollectPayment:
                        StartPracticeOrComplete();
                        break;
                }
                break;

            case TutorialDay.Day3Cashier:
                switch (currentPhase)
                {
                    case TutorialPhase.CashierWaitForMoney:
                        SetPhase(TutorialPhase.CashierProcessPayment);
                        break;

                    case TutorialPhase.CashierProcessPayment:
                        StartPracticeOrComplete();
                        break;
                }
                break;

            case TutorialDay.Day4Busser:
                if (currentPhase == TutorialPhase.CleanTray)
                    StartPracticeOrComplete();
                break;

            case TutorialDay.Day5AllTogether:
                if (currentPhase == TutorialPhase.AllTogetherGameplay)
                    SetPhase(TutorialPhase.Complete);
                break;
        }
    }

    private void StartPracticeOrComplete()
    {
        DayConfig config = GetCurrentDayConfig();
        if (config == null || !config.enablePractice)
        {
            SetPhase(TutorialPhase.Complete);
            return;
        }

        StartPracticePhase();
    }

    private void StartPracticePhase()
    {
        DayConfig config = GetCurrentDayConfig();
        if (config == null)
        {
            Debug.LogWarning("[TutorialManager] StartPracticePhase failed: DayConfig is null.");
            SetPhase(TutorialPhase.Complete);
            return;
        }

        practiceRunning = true;
        practiceTimer = 0f;
        practiceSpawnTimer = 0f;
        practiceProgressCount = 0;

        Debug.Log("[TutorialManager] Practice started for " + currentDay +
                " | autoSpawnDuringPractice=" + config.autoSpawnDuringPractice +
                " | duration=" + config.practiceDurationSeconds +
                " | interval=" + config.practiceSpawnIntervalSeconds);

        SetPhase(TutorialPhase.PracticeGameplay);

        if (config.autoSpawnDuringPractice)
            SpawnPracticeWave();
    }


    private void UpdatePracticeMode()
    {
        if (practiceTimerText != null && (!tutorialStarted || !practiceRunning || currentPhase != TutorialPhase.PracticeGameplay))
            practiceTimerText.text = "";

        if (!tutorialStarted || !practiceRunning || currentPhase != TutorialPhase.PracticeGameplay)
            return;

        DayConfig config = GetCurrentDayConfig();
        if (config == null)
            return;

        practiceTimer += Time.deltaTime;

        float remaining = Mathf.Max(0f, config.practiceDurationSeconds - practiceTimer);

        if (practiceTimerText != null)
        {
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            practiceTimerText.text = $"{minutes:00}:{seconds:00}";
        }

        if (config.autoSpawnDuringPractice && groupSpawner != null && config.practiceSpawnIntervalSeconds > 0f)
        {
            practiceSpawnTimer += Time.deltaTime;

            if (practiceSpawnTimer >= config.practiceSpawnIntervalSeconds)
            {
                practiceSpawnTimer = 0f;
                SpawnPracticeWave();
            }
        }

        bool timerDone = config.practiceDurationSeconds > 0f && practiceTimer >= config.practiceDurationSeconds;
        bool targetDone = config.practiceTargetCount > 0 && practiceProgressCount >= config.practiceTargetCount;

        if (timerDone || targetDone)
            SetPhase(TutorialPhase.Complete);
    }

    private void SpawnPracticeWave()
    {
        DayConfig config = GetCurrentDayConfig();
        if (config == null)
        {
            Debug.LogWarning("[TutorialManager] SpawnPracticeWave failed: DayConfig is null.");
            return;
        }

        if (groupSpawner == null)
        {
            Debug.LogWarning("[TutorialManager] SpawnPracticeWave failed: GroupSpawner is missing.");
            return;
        }

        int count = Mathf.Max(1, config.practiceSpawnCountPerWave);
        Debug.Log("[TutorialManager] Spawning practice wave. Count=" + count);

        for (int i = 0; i < count; i++)
        {
            CustomerGroup group = groupSpawner.SpawnGroup();
            if (group == null)
            {
                Debug.LogWarning("[TutorialManager] GroupSpawner returned null during practice wave.");
                continue;
            }

            spawnedGroups.Add(group);
            RegisterGroupWatcher(group);
            ConfigureSpawnedGroupForCurrentDay(group, true);
            Debug.Log("[TutorialManager] Practice group spawned: " + group.name);
        }

        activeTutorialGroup = GetBestGroupForCurrentDay(null);
    }

    private void RegisterPracticeProgress()
    {
        if (!practiceRunning)
            return;

        practiceProgressCount++;

        DayConfig config = GetCurrentDayConfig();
        if (config != null && config.practiceTargetCount > 0 && practiceProgressCount >= config.practiceTargetCount)
            SetPhase(TutorialPhase.Complete);
    }

    private void CompleteCurrentDay()
    {
        if (completionShown)
            return;

        completionShown = true;
        practiceRunning = false;
        practiceTimer = 0f;
        practiceSpawnTimer = 0f;

        DayConfig config = GetCurrentDayConfig();
        string message = config != null && !string.IsNullOrWhiteSpace(config.completionMessage)
            ? config.completionMessage
            : "Tutorial day complete.";

        if (arrowManager != null)
            arrowManager.ForceHide();

        if (roleHighlight != null)
            roleHighlight.Hide();

        if (showCompletionPanel && tutorialCompletePanel != null)
        {
            tutorialCompletePanel.SetActive(true);

            if (tutorialCompleteText != null)
                tutorialCompleteText.text = message;
        }
        else if (dialogueUI != null && !string.IsNullOrWhiteSpace(message))
        {
            dialogueUI.ShowAuto("Manager", message, 3f);
        }
    }

    private void ClearGuidance()
    {
        if (arrowManager != null)
            arrowManager.ForceHide();

        if (roleHighlight != null)
            roleHighlight.Hide();
    }

    private void ShowPhaseDialogue(TutorialPhase phase)
    {
        if (dialogueUI == null)
            return;

        string message = GetDetailedPhaseDialogue(phase);
        if (!string.IsNullOrEmpty(message))
            dialogueUI.ShowAuto("Manager", message, phaseDialogueDuration);
    }

    private string GetDetailedPhaseDialogue(TutorialPhase phase)
    {
        CustomerGroup group = activeTutorialGroup;
        CustomerGroup.GroupState state = group != null ? group.state : CustomerGroup.GroupState.Spawning;
        DayConfig config = GetCurrentDayConfig();

        if (phase == TutorialPhase.PracticeGameplay)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.practiceStartMessage))
                return config.practiceStartMessage;
        }

        switch (currentDay)
        {
            case TutorialDay.Day1Host:
                switch (phase)
                {
                    case TutorialPhase.GreetCustomer:
                        return "This is the Host job. Tap the customer group and greet them first.";

                    case TutorialPhase.AssignTable:
                        return "Good. Now assign them to an empty table so they can sit down.";

                    case TutorialPhase.PracticeGameplay:
                        return "Now do the Host job yourself. Greet and assign customer groups to tables.";
                }
                break;

            case TutorialDay.Day2Waiter:
                switch (phase)
                {
                    case TutorialPhase.TakeOrder:
                        if (state == CustomerGroup.GroupState.WaitingToOrder)
                            return "Wait for the table to be ready. When the order bubble appears, tap it.";

                        if (state == CustomerGroup.GroupState.ReadyToOrder)
                            return "Tap the order bubble above the customer table to open the notepad.";

                        return "Go to the customer table and prepare to take the order.";

                    case TutorialPhase.ConfirmOrder:
                        return "This is the notepad. Read the order shown at the top. Match the exact food and drink, then confirm.";

                    case TutorialPhase.SubmitOrder:
                        return "Good. Now bring that order to the counter so the kitchen can prepare it. Wait for the tray to appear there.";

                    case TutorialPhase.ServeFood:
                        return "When the tray is ready, pick it up and deliver the correct tray back to the same customer table.";

                    case TutorialPhase.PickupBill:
                        return "After the customers finish eating, a bill request will appear. Go to the cashier station and get the bill for that table there.";

                    case TutorialPhase.DeliverBill:
                        return "Now bring the bill back to the same customer table and give it to them.";

                    case TutorialPhase.PickupMoney:
                        return "After you give the bill, watch the table. When the cash bubble appears, tap the money at the table and pick it up.";

                    case TutorialPhase.CollectPayment:
                        return "Bring the money to the cashier booth. For Waiter day, your job ends when the money leaves the waiter hands at the cashier.";

                    case TutorialPhase.PracticeGameplay:
                        return "Now do the Waiter flow yourself. Take the order, send it, serve food, get the bill, deliver it, pick up money, and bring it to cashier.";
                }
                break;

            case TutorialDay.Day3Cashier:
                switch (phase)
                {
                    case TutorialPhase.CashierWaitForMoney:
                        return "This is the Cashier job. Wait for the waiter to bring the payment to the POS.";

                    case TutorialPhase.CashierProcessPayment:
                        return "The payment is here. Open the POS and process it correctly.";

                    case TutorialPhase.PracticeGameplay:
                        return "Now do the Cashier job yourself. Process incoming payments.";
                }
                break;

            case TutorialDay.Day4Busser:
                switch (phase)
                {
                    case TutorialPhase.CleanTray:
                        return "This is the Busser job. Pick up the dirty tray and clean the table.";

                    case TutorialPhase.PracticeGameplay:
                        return "Now do the Busser job yourself. Clean the dirty trays and keep tables ready.";
                }
                break;

            case TutorialDay.Day5AllTogether:
                switch (phase)
                {
                    case TutorialPhase.AllTogetherGameplay:
                        return "Play normally now. Watch the arrows, messages, and indicators. I will explain mood, mistakes, and flow while you play.";
                }
                break;
        }

        return string.Empty;
    }

    private void RefreshUI()
    {
        if (dayText != null)
            dayText.text = GetDayTitle();

        if (phaseText != null)
            phaseText.text = GetPhaseTitle(currentPhase);

        if (objectiveText != null)
            objectiveText.text = GetObjectiveText(currentPhase);

        if (progressBar != null)
            progressBar.value = GetProgress01();
    }

    private string GetDayTitle()
    {
        if (!openingSequenceFinished && currentPhase == TutorialPhase.Intro)
            return "Tutorial Opening";

        DayConfig config = GetCurrentDayConfig();
        if (config != null && !string.IsNullOrWhiteSpace(config.dayTitle))
            return config.dayTitle;

        switch (currentDay)
        {
            case TutorialDay.Day1Host: return "Day 1 - Host";
            case TutorialDay.Day2Waiter: return "Day 2 - Waiter";
            case TutorialDay.Day3Cashier: return "Day 3 - Cashier";
            case TutorialDay.Day4Busser: return "Day 4 - Busser";
            case TutorialDay.Day5AllTogether: return "Day 5 - All Together";
        }

        return "Tutorial";
    }

    private string GetPhaseTitle(TutorialPhase phase)
    {
        switch (phase)
        {
            case TutorialPhase.Intro: return "Introduction";
            case TutorialPhase.GreetCustomer: return "Greet Customer";
            case TutorialPhase.AssignTable: return "Assign Table";
            case TutorialPhase.TakeOrder: return "Take Order";
            case TutorialPhase.ConfirmOrder: return "Confirm Order";
            case TutorialPhase.SubmitOrder: return "Submit Order";
            case TutorialPhase.ServeFood: return "Serve Food";
            case TutorialPhase.PickupBill: return "Get Bill";
            case TutorialPhase.DeliverBill: return "Give Bill";
            case TutorialPhase.PickupMoney: return "Pick Up Money";
            case TutorialPhase.CollectPayment: return "Bring Money to Cashier";
            case TutorialPhase.CashierWaitForMoney: return "Wait for Payment";
            case TutorialPhase.CashierProcessPayment: return "Use POS";
            case TutorialPhase.CleanTray: return "Clean Tray";
            case TutorialPhase.PracticeGameplay: return "Main Task";
            case TutorialPhase.AllTogetherGameplay: return "All Together";
            case TutorialPhase.Complete: return "Complete";
            default: return "";
        }
    }

    private string GetObjectiveText(TutorialPhase phase)
    {
        CustomerGroup group = activeTutorialGroup;
        CustomerGroup.GroupState state = group != null ? group.state : CustomerGroup.GroupState.Spawning;

        if (!openingSequenceFinished && phase == TutorialPhase.Intro)
            return "Learn the role of each staff member.";

        if (phase == TutorialPhase.PracticeGameplay)
        {
            DayConfig cfg = GetCurrentDayConfig();
            if (cfg == null)
                return "Do the task yourself.";

            int target = Mathf.Max(0, cfg.practiceTargetCount);
            int current = Mathf.Max(0, practiceProgressCount);

            if (target > 0)
                return $"Main task: {current}/{target}";

            return "Main task is active.";
        }

        switch (currentDay)
        {
            case TutorialDay.Day1Host:
                switch (phase)
                {
                    case TutorialPhase.GreetCustomer:
                        if (group == null)
                            return "Wait for the first customer group.";
                        if (!group.hasBeenGreeted)
                            return "Tap the customer group and greet them.";
                        return "Good. Move to the next step.";

                    case TutorialPhase.AssignTable:
                        if (group == null)
                            return "Wait for the active customer group.";
                        if (group.assignedBooth == null)
                            return "Assign the group to an empty table.";
                        if (state == CustomerGroup.GroupState.WalkingToBooth)
                            return "Good. Wait for them to reach the table.";
                        return "Good. They are seated.";
                }
                break;

            case TutorialDay.Day2Waiter:
                switch (phase)
                {
                    case TutorialPhase.TakeOrder:
                        if (group == null)
                            return "Wait for the seated table.";
                        if (state == CustomerGroup.GroupState.WaitingToOrder)
                            return "Wait for the order bubble above the table.";
                        if (state == CustomerGroup.GroupState.ReadyToOrder)
                            return "Tap the order bubble to open the notepad.";
                        return "Go to the correct table and take the order.";

                    case TutorialPhase.ConfirmOrder:
                        return "Match the exact food and drink shown in the notepad, then confirm.";

                    case TutorialPhase.SubmitOrder:
                        return "Bring the order to the counter and wait for the tray.";

                    case TutorialPhase.ServeFood:
                        if (group == null)
                            return "Pick up the tray and deliver it to the correct table.";
                        if (state == CustomerGroup.GroupState.OrderTaken)
                            return "Pick up the tray and deliver it to the correct table.";
                        if (state == CustomerGroup.GroupState.Eating)
                            return "Good. They are eating. Wait for the bill request.";
                        return "Serve the correct tray.";

                    case TutorialPhase.PickupBill:
                        if (group == null)
                            return "Wait for the bill request, then go to the cashier station.";
                        if (state == CustomerGroup.GroupState.Eating)
                            return "Wait for the customers to finish eating.";
                        if (state == CustomerGroup.GroupState.NeedsBill)
                            return "Go to the cashier station and get the bill for this table.";
                        return "Wait for the bill request.";

                    case TutorialPhase.DeliverBill:
                        return "Bring the bill back to the same table and deliver it.";

                    case TutorialPhase.PickupMoney:
                        return "Watch for the money bubble, then pick up the cash from the table.";

                    case TutorialPhase.CollectPayment:
                        return "Bring the money to the cashier booth. Do not use the POS in Waiter day.";
                }
                break;

            case TutorialDay.Day3Cashier:
                switch (phase)
                {
                    case TutorialPhase.CashierWaitForMoney:
                        return "Wait for the waiter to bring the payment.";

                    case TutorialPhase.CashierProcessPayment:
                        return "Open the POS and process the payment.";
                }
                break;

            case TutorialDay.Day4Busser:
                if (phase == TutorialPhase.CleanTray)
                {
                    if (activeDirtyTray == null)
                        return "Wait for a dirty tray.";
                    return "Pick up the dirty tray and clean it.";
                }
                break;

            case TutorialDay.Day5AllTogether:
                if (phase == TutorialPhase.AllTogetherGameplay)
                    return "Play the flow while the tutorial explains what the game is tracking.";
                break;
        }

        if (phase == TutorialPhase.Complete)
            return "Tutorial day complete.";

        return "Press Start to begin.";
    }

    private float GetProgress01()
    {
        if (!openingSequenceFinished && currentPhase == TutorialPhase.Intro)
        {
            if (introSteps == null || introSteps.Length == 0)
                return 0f;

            return Mathf.Clamp01((float)(currentIntroIndex + 1) / introSteps.Length);
        }

        float guidedSteps = GetCurrentDayGuidedStepCount();
        float totalSteps = guidedSteps + (CurrentDayUsesPractice() ? 1f : 0f);

        if (totalSteps <= 0f)
            totalSteps = 1f;

        float currentSteps = GetCurrentDayGuidedProgressUnits();

        if (currentPhase == TutorialPhase.PracticeGameplay)
            currentSteps = guidedSteps + GetPracticeProgress01();

        if (currentPhase == TutorialPhase.Complete)
            currentSteps = totalSteps;

        return Mathf.Clamp01(currentSteps / totalSteps);
    }

    private bool CurrentDayUsesPractice()
    {
        DayConfig config = GetCurrentDayConfig();
        return config != null && config.enablePractice;
    }

    private float GetPracticeProgress01()
    {
        DayConfig config = GetCurrentDayConfig();
        if (config == null)
            return 0f;

        float time01 = 0f;
        float target01 = 0f;

        if (config.practiceDurationSeconds > 0f)
            time01 = Mathf.Clamp01(practiceTimer / config.practiceDurationSeconds);

        if (config.practiceTargetCount > 0)
            target01 = Mathf.Clamp01((float)practiceProgressCount / config.practiceTargetCount);

        if (config.practiceDurationSeconds > 0f && config.practiceTargetCount > 0)
            return Mathf.Max(time01, target01);

        if (config.practiceTargetCount > 0)
            return target01;

        return time01;
    }

    private int GetCurrentDayGuidedStepCount()
    {
        switch (currentDay)
        {
            case TutorialDay.Day1Host: return 2;
            case TutorialDay.Day2Waiter: return 8;
            case TutorialDay.Day3Cashier: return 2;
            case TutorialDay.Day4Busser: return 1;
            case TutorialDay.Day5AllTogether: return 1;
        }

        return 1;
    }

    private float GetCurrentDayGuidedProgressUnits()
    {
        switch (currentDay)
        {
            case TutorialDay.Day1Host:
                switch (currentPhase)
                {
                    case TutorialPhase.GreetCustomer: return 1f;
                    case TutorialPhase.AssignTable: return 2f;
                    case TutorialPhase.PracticeGameplay:
                    case TutorialPhase.Complete: return 2f;
                }
                break;

            case TutorialDay.Day2Waiter:
                switch (currentPhase)
                {
                    case TutorialPhase.TakeOrder: return 1f;
                    case TutorialPhase.ConfirmOrder: return 2f;
                    case TutorialPhase.SubmitOrder: return 3f;
                    case TutorialPhase.ServeFood: return 4f;
                    case TutorialPhase.PickupBill: return 5f;
                    case TutorialPhase.DeliverBill: return 6f;
                    case TutorialPhase.PickupMoney: return 7f;
                    case TutorialPhase.CollectPayment:
                    case TutorialPhase.PracticeGameplay:
                    case TutorialPhase.Complete: return 8f;
                }
                break;

            case TutorialDay.Day3Cashier:
                switch (currentPhase)
                {
                    case TutorialPhase.CashierWaitForMoney: return 1f;
                    case TutorialPhase.CashierProcessPayment:
                    case TutorialPhase.PracticeGameplay:
                    case TutorialPhase.Complete: return 2f;
                }
                break;

            case TutorialDay.Day4Busser:
                switch (currentPhase)
                {
                    case TutorialPhase.CleanTray:
                    case TutorialPhase.PracticeGameplay:
                    case TutorialPhase.Complete: return 1f;
                }
                break;

            case TutorialDay.Day5AllTogether:
                switch (currentPhase)
                {
                    case TutorialPhase.AllTogetherGameplay:
                    case TutorialPhase.Complete: return 1f;
                }
                break;
        }

        return 0f;
    }

    private void RefreshRuntimeTargets()
    {
        if (!tutorialStarted)
            return;

        if (activeTutorialGroup == null || !IsUsableTutorialGroup(activeTutorialGroup))
            activeTutorialGroup = GetBestGroupForCurrentDay(null);

        if (activeDirtyTray == null)
            activeDirtyTray = GetBestTrayForCurrentDay(null);
    }

    private bool IsUsableTutorialGroup(CustomerGroup group)
    {
        if (group == null)
            return false;

        if (!group.gameObject.activeInHierarchy)
            return false;

        switch (group.state)
        {
            case CustomerGroup.GroupState.Leaving:
            case CustomerGroup.GroupState.AngryLeft:
            case CustomerGroup.GroupState.UnhappyLeft:
                return false;
        }

        return true;
    }

    private CustomerGroup GetBestGroupForCurrentDay(CustomerGroup preferred)
    {
        if (IsValidCurrentGroup(preferred))
            return preferred;

        DayConfig config = GetCurrentDayConfig();

        CustomerGroup group = FindBestGroupInList(config != null ? config.preplacedGroups : null);
        if (group != null)
            return group;

        group = FindBestGroupInList(spawnedGroups);
        if (group != null)
            return group;

        CustomerGroup[] sceneGroups = FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneGroups.Length; i++)
        {
            if (sceneGroups[i] == null)
                continue;

            RegisterGroupWatcher(sceneGroups[i]);
        }

        return FindBestGroupInList(sceneGroups);
    }

    private FoodTray GetBestTrayForCurrentDay(FoodTray preferred)
    {
        if (preferred != null)
            return preferred;

        DayConfig config = GetCurrentDayConfig();

        FoodTray tray = GetFirstValidTray(config != null ? config.preplacedDirtyTrays : null);
        if (tray != null)
            return tray;

        FoodTray[] trays = FindObjectsByType<FoodTray>(FindObjectsSortMode.None);
        return GetFirstValidTray(trays);
    }

    private bool IsValidCurrentGroup(CustomerGroup group)
    {
        if (group == null)
            return false;

        if (currentPhase == TutorialPhase.PracticeGameplay || currentPhase == TutorialPhase.AllTogetherGameplay)
            return true;

        switch (currentDay)
        {
            case TutorialDay.Day1Host:
                if (currentPhase == TutorialPhase.GreetCustomer)
                    return !group.hasBeenGreeted;

                if (currentPhase == TutorialPhase.AssignTable)
                    return group.hasBeenGreeted && group.assignedBooth == null;

                return true;

            case TutorialDay.Day2Waiter:
                switch (currentPhase)
                {
                    case TutorialPhase.TakeOrder:
                    case TutorialPhase.ConfirmOrder:
                        return group.state == CustomerGroup.GroupState.WaitingToOrder
                            || group.state == CustomerGroup.GroupState.ReadyToOrder
                            || group.state == CustomerGroup.GroupState.OrderTaken;

                    case TutorialPhase.SubmitOrder:
                        return group.state == CustomerGroup.GroupState.OrderTaken;

                    case TutorialPhase.ServeFood:
                        return group.state == CustomerGroup.GroupState.OrderTaken
                            || group.state == CustomerGroup.GroupState.Eating;

                    case TutorialPhase.PickupBill:
                    case TutorialPhase.DeliverBill:
                        return group.state == CustomerGroup.GroupState.Eating
                            || group.state == CustomerGroup.GroupState.NeedsBill;

                    case TutorialPhase.PickupMoney:
                    case TutorialPhase.CollectPayment:
                        return true;
                }
                return true;

            case TutorialDay.Day3Cashier:
            case TutorialDay.Day5AllTogether:
                return true;

            case TutorialDay.Day4Busser:
                return false;
        }

        return true;
    }

    private CustomerGroup FindBestGroupInList(IList<CustomerGroup> groups)
    {
        if (groups == null)
            return null;

        CustomerGroup fallback = null;

        for (int i = 0; i < groups.Count; i++)
        {
            CustomerGroup group = groups[i];
            if (group == null)
                continue;

            if (fallback == null)
                fallback = group;

            if (MatchesCurrentDayPriority(group))
                return group;
        }

        return fallback;
    }

    private bool MatchesCurrentDayPriority(CustomerGroup group)
    {
        if (group == null)
            return false;

        if (currentPhase == TutorialPhase.PracticeGameplay || currentPhase == TutorialPhase.AllTogetherGameplay)
        {
            switch (currentDay)
            {
                case TutorialDay.Day1Host:
                    return !group.hasBeenGreeted || (group.hasBeenGreeted && group.assignedBooth == null);

                case TutorialDay.Day2Waiter:
                    return group.state == CustomerGroup.GroupState.WaitingToOrder
                        || group.state == CustomerGroup.GroupState.ReadyToOrder
                        || group.state == CustomerGroup.GroupState.OrderTaken
                        || group.state == CustomerGroup.GroupState.Eating
                        || group.state == CustomerGroup.GroupState.NeedsBill;

                case TutorialDay.Day3Cashier:
                case TutorialDay.Day5AllTogether:
                    return true;
            }
        }

        switch (currentDay)
        {
            case TutorialDay.Day1Host:
                if (currentPhase == TutorialPhase.GreetCustomer)
                    return !group.hasBeenGreeted;

                if (currentPhase == TutorialPhase.AssignTable)
                    return group.hasBeenGreeted && group.assignedBooth == null;

                return true;

            case TutorialDay.Day2Waiter:
                switch (currentPhase)
                {
                    case TutorialPhase.TakeOrder:
                        return group.state == CustomerGroup.GroupState.WaitingToOrder
                            || group.state == CustomerGroup.GroupState.ReadyToOrder;

                    case TutorialPhase.ConfirmOrder:
                        return group.state == CustomerGroup.GroupState.ReadyToOrder
                            || group.state == CustomerGroup.GroupState.OrderTaken;

                    case TutorialPhase.SubmitOrder:
                        return group.state == CustomerGroup.GroupState.OrderTaken;

                    case TutorialPhase.ServeFood:
                        return group.state == CustomerGroup.GroupState.OrderTaken
                            || group.state == CustomerGroup.GroupState.Eating;

                    case TutorialPhase.PickupBill:
                    case TutorialPhase.DeliverBill:
                        return group.state == CustomerGroup.GroupState.Eating
                            || group.state == CustomerGroup.GroupState.NeedsBill;

                    case TutorialPhase.PickupMoney:
                    case TutorialPhase.CollectPayment:
                        return true;
                }
                return true;

            case TutorialDay.Day3Cashier:
            case TutorialDay.Day5AllTogether:
                return true;
        }

        return true;
    }

    private FoodTray GetFirstValidTray(IList<FoodTray> trays)
    {
        if (trays == null)
            return null;

        for (int i = 0; i < trays.Count; i++)
        {
            if (trays[i] != null)
                return trays[i];
        }

        return null;
    }

    public bool IsPhase(TutorialPhase phase)
    {
        return currentPhase == phase;
    }

    public bool IsActiveGroup(CustomerGroup group)
    {
        return group != null && group == activeTutorialGroup;
    }

    public bool IsActiveDirtyTray(FoodTray tray)
    {
        return tray != null && tray == activeDirtyTray;
    }

    public void RegisterCustomerGreeted(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day1Host)
        {
            if (currentPhase == TutorialPhase.GreetCustomer)
            {
                if (!IsActiveGroup(group)) return;
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                ShowAutoHint("Good. Now assign them to a table.");
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Good. Greeting starts the customer flow.");
    }

    public void RegisterTableAssigned(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day1Host)
        {
            if (currentPhase == TutorialPhase.AssignTable)
            {
                if (!IsActiveGroup(group)) return;
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                RegisterPracticeProgress();
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Assigning the correct table keeps the flow moving.");
    }

    public void RegisterOrderTaken(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day2Waiter)
        {
            if (currentPhase == TutorialPhase.TakeOrder || currentPhase == TutorialPhase.ConfirmOrder)
            {
                if (group != null && activeTutorialGroup != null && !IsActiveGroup(group)) return;
                SetPhase(TutorialPhase.SubmitOrder);
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                ShowAutoHint("Good. Keep following the waiter flow.");
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Taking orders correctly is part of the full lobby flow.");
    }

    public void RegisterOrderSubmitted(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day2Waiter)
        {
            if (currentPhase == TutorialPhase.SubmitOrder)
            {
                if (group != null && activeTutorialGroup != null && !IsActiveGroup(group)) return;
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                ShowAutoHint("Good. The order is now on its way to the kitchen.");
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Good. The order was sent to the kitchen.");
    }

    public void RegisterFoodServed(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day2Waiter)
        {
            if (currentPhase == TutorialPhase.ServeFood)
            {
                if (group != null && activeTutorialGroup != null && !IsActiveGroup(group)) return;
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                ShowAutoHint("Nice. Keep moving through the waiter flow.");
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Nice. Serving the correct tray keeps customers happy.");
    }

    public void RegisterBillPickedUp(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day2Waiter)
        {
            if (currentPhase == TutorialPhase.PickupBill)
            {
                if (group != null && activeTutorialGroup != null && !IsActiveGroup(group)) return;

                ShowAutoHint("Good. You got the bill from the cashier. Now bring it back to the customer table.");
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                ShowAutoHint("Good. Now bring the bill to the correct customer table.");
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("The bill must be collected from the cashier before you can deliver it.");
    }

    public void RegisterBillDelivered(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day2Waiter)
        {
            if (currentPhase == TutorialPhase.DeliverBill)
            {
                if (group != null && activeTutorialGroup != null && !IsActiveGroup(group)) return;

                ShowAutoHint("Good. Now watch the table. When the money bubble appears, pick up the cash.");
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                ShowAutoHint("Good. Now wait for the money bubble to appear.");
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Good. Bills must be delivered before payment.");
    }

    public void RegisterMoneyPickedUp(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day2Waiter)
        {
            if (currentPhase == TutorialPhase.PickupMoney)
            {
                if (group != null && activeTutorialGroup != null && !IsActiveGroup(group)) return;

                ShowAutoHint("Good. Bring that money to the cashier booth. The POS is for Cashier day, not Waiter day.");
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                ShowAutoHint("Good. Bring that money to the cashier booth.");
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Pick up the payment and complete the service.");
    }

    public void RegisterPaymentCollected(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day2Waiter)
        {
            if (currentPhase == TutorialPhase.PickupMoney || currentPhase == TutorialPhase.CollectPayment)
            {
                if (group != null && activeTutorialGroup != null && !IsActiveGroup(group)) return;
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                RegisterPracticeProgress();
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Payment complete. Mistakes here can affect the flow.");
    }

    public void RegisterCashierPaymentProcessed(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day3Cashier)
        {
            if (currentPhase == TutorialPhase.CashierProcessPayment)
            {
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                RegisterPracticeProgress();
                return;
            }
        }
    }

    public void RegisterTrayCleaned(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day4Busser)
        {
            if (currentPhase == TutorialPhase.CleanTray)
            {
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                RegisterPracticeProgress();
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Clean tables keep the restaurant ready for the next customers.");
    }

    public void RegisterDirtyTrayCleaned(FoodTray tray)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day4Busser)
        {
            if (currentPhase == TutorialPhase.CleanTray)
            {
                if (tray != null && activeDirtyTray != null && !IsActiveDirtyTray(tray)) return;
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                RegisterPracticeProgress();
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Dirty trays must be cleared so the next customers can use the table.");
    }

    public void RegisterAllTogetherCompleted()
    {
        if (!tutorialStarted) return;
        if (!IsLobbyMasteryDay) return;
        if (currentPhase != TutorialPhase.AllTogetherGameplay) return;

        AdvancePhase();
    }

    public void OnNotepadOpened(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (!IsPhase(TutorialPhase.TakeOrder) && !IsPhase(TutorialPhase.AllTogetherGameplay)) return;
        if (group != null && activeTutorialGroup != null && !IsActiveGroup(group) && !IsLobbyMasteryDay) return;
        if (notepadOpened) return;

        notepadOpened = true;

        if (currentDay == TutorialDay.Day2Waiter && currentPhase == TutorialPhase.TakeOrder)
        {
            ShowAutoHint("This is the notepad. Read the order on top, then match the same food and drink.");
            SetPhase(TutorialPhase.ConfirmOrder);
            return;
        }

        ShowAutoHint("This is the notepad. Read the order on top, then match the same food and drink.");
    }

    public void OnOrderConfirmed(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (!IsPhase(TutorialPhase.TakeOrder) && !IsPhase(TutorialPhase.ConfirmOrder) && !IsPhase(TutorialPhase.AllTogetherGameplay)) return;
        if (group != null && activeTutorialGroup != null && !IsActiveGroup(group) && !IsLobbyMasteryDay) return;
        if (orderConfirmed) return;

        orderConfirmed = true;

        if (currentDay == TutorialDay.Day2Waiter)
        {
            ShowAutoHint("Good. You matched the correct order.");
            SetPhase(TutorialPhase.SubmitOrder);
            return;
        }

        if (IsLobbyMasteryDay)
        {
            ShowAutoHint("Correct orders help avoid unhappy customers.");
            return;
        }

        ShowAutoHint("Good. You matched the correct order.");
    }

    public void OnCashierOpened(CustomerGroup group, int expectedChange)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day2Waiter)
        {
            ShowAutoHint("For Waiter day, your job ends when the money is handed to the cashier booth. The POS will be taught in Cashier day.");
            return;
        }

        if (!IsPhase(TutorialPhase.CollectPayment) &&
            !IsPhase(TutorialPhase.CashierWaitForMoney) &&
            !IsPhase(TutorialPhase.CashierProcessPayment) &&
            !IsPhase(TutorialPhase.AllTogetherGameplay)) return;

        if (cashierOpened) return;

        cashierOpened = true;

        if (currentDay == TutorialDay.Day3Cashier)
        {
            if (currentPhase == TutorialPhase.CashierWaitForMoney)
                SetPhase(TutorialPhase.CashierProcessPayment);

            ShowAutoHint($"This is the POS. Process the payment correctly. Expected value: {expectedChange}.");
            return;
        }

        ShowAutoHint($"This is the cashier. Give the exact change: {expectedChange}.");
    }

    public void OnCashierConfirmed(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (!IsPhase(TutorialPhase.CollectPayment) &&
            !IsPhase(TutorialPhase.CashierProcessPayment) &&
            !IsPhase(TutorialPhase.AllTogetherGameplay)) return;
        if (cashierConfirmed) return;

        cashierConfirmed = true;

        if (currentDay == TutorialDay.Day3Cashier && currentPhase == TutorialPhase.CashierProcessPayment)
        {
            ShowAutoHint("Correct. The payment is processed.");
            AdvancePhase();
            return;
        }

        if (currentDay == TutorialDay.Day3Cashier && currentPhase == TutorialPhase.PracticeGameplay)
        {
            RegisterPracticeProgress();
            return;
        }

        ShowAutoHint("Correct. Payment complete.");
    }

    public void OnMoneyGivenToCashier(CustomerGroup group)
    {
        if (!tutorialStarted) return;

        if (currentDay == TutorialDay.Day2Waiter)
        {
            if (currentPhase == TutorialPhase.CollectPayment)
            {
                ShowAutoHint("Good. The money left the waiter hands at the cashier booth. That completes the waiter payment step.");
                AdvancePhase();
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                ShowAutoHint("Good. Payment handoff complete.");
                RegisterPracticeProgress();
                return;
            }

            return;
        }

        if (currentDay == TutorialDay.Day3Cashier)
        {
            if (currentPhase == TutorialPhase.CashierWaitForMoney)
            {
                ShowAutoHint("The waiter delivered the money. Use the POS now.");
                SetPhase(TutorialPhase.CashierProcessPayment);
                return;
            }

            if (currentPhase == TutorialPhase.PracticeGameplay)
            {
                ShowAutoHint("New payment received. Process it in the POS.");
                return;
            }
        }

        if (IsLobbyMasteryDay && currentPhase == TutorialPhase.AllTogetherGameplay)
            ShowAutoHint("Money reaches the cashier before the table fully finishes.");
    }

    public void OnCustomerAngry(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (!IsLobbyMasteryDay) return;
        if (currentPhase != TutorialPhase.AllTogetherGameplay) return;

        ShowAutoHint("That customer got unhappy. The game tracks delays and mistakes.");
    }

    public void OnCustomerMistakeRecognized(string message)
    {
        if (!tutorialStarted) return;
        if (!IsLobbyMasteryDay) return;
        if (currentPhase != TutorialPhase.AllTogetherGameplay) return;

        ShowAutoHint(message);
    }

    private void ShowAutoHint(string message)
    {
        if (dialogueUI == null || string.IsNullOrWhiteSpace(message))
            return;

        dialogueUI.ShowAuto("Manager", message, 2.8f);
    }

    public void StartNextDay()
    {
        if (!tutorialStarted)
            return;

        if (currentDay < TutorialDay.Day5AllTogether)
        {
            currentDay++;
            StartCurrentDayFlow();
        }
    }

    private void OnFinishTutorial()
    {
        Debug.Log("[TutorialManager] Tutorial finished.");
    }

    private void ConfigureSpawnedGroupForCurrentDay(CustomerGroup group, bool isPracticeSpawn)
    {
        if (group == null)
            return;

        DayConfig config = GetCurrentDayConfig();
        if (config == null)
            return;

        if (currentDay == TutorialDay.Day1Host)
        {
            group.SetTutorialDisableAutoOrderFlow(true);
            group.SetOrderPause(true);
            group.TutorialClearServiceUI();
            return;
        }

        if (currentDay == TutorialDay.Day2Waiter)
        {
            Booth booth = GetNextWaiterTutorialBooth(config);
            if (booth != null)
            {
                group.TutorialPlaceGroupAtBooth(
                    booth,
                    true,
                    config.waiterOrderDelay,
                    config.waiterMarkGroupAsGreeted
                );
            }
            else
            {
                group.MarkGreeted();
                group.TutorialBeginWaiterFlow(config.waiterOrderDelay);
            }

            return;
        }
    }

    private Booth GetNextWaiterTutorialBooth(DayConfig config)
    {
        if (config == null || config.waiterTutorialBooths == null || config.waiterTutorialBooths.Count == 0)
            return null;

        int total = config.waiterTutorialBooths.Count;

        for (int i = 0; i < total; i++)
        {
            int index = (waiterBoothCursor + i) % total;
            Booth booth = config.waiterTutorialBooths[index];
            if (booth == null)
                continue;

            waiterBoothCursor = (index + 1) % total;
            return booth;
        }

        return null;
    }

    public bool ShouldBlockPOSForCurrentDay()
    {
        if (!tutorialStarted)
            return false;

        return currentDay == TutorialDay.Day2Waiter;
    }

    public bool ShouldAllowPOSForCurrentDay()
    {
        return !ShouldBlockPOSForCurrentDay();
    }
}