using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public enum TutorialPhase
    {
        None,
        Intro,
        GreetCustomer,
        AssignTable,
        TakeOrder,
        SubmitOrder,
        ServeFood,
        DeliverBill,
        CollectPayment,
        CleanTray,
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

    [Header("Scene References")]
    [SerializeField] private GroupSpawner groupSpawner;
    [SerializeField] private LobbyLineManager lobbyLineManager;
    [SerializeField] private RoleManager roleManager;
    [SerializeField] private OrderFlowManager orderFlowManager;
    [SerializeField] private BillManager billManager;
    [SerializeField] private KitchenManager kitchenManager;

    [Header("Tutorial Spawn")]
    [SerializeField] private bool spawnCustomerOnStart = true;
    [SerializeField] private float firstSpawnDelay = 0.5f;

    [Header("UI")]
    [SerializeField] private GameObject tutorialIntroPanel;
    [SerializeField] private Button startTutorialButton;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private Slider progressBar;

    [Header("Dialogue")]
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private bool showDialoguePerPhase = true;
    [SerializeField] private float phaseDialogueDuration = 2.6f;

    [Header("Role Intro Sequence")]
    [SerializeField] private RoleCameraController roleCameraController;
    [SerializeField] private IntroStep[] introSteps;
    [SerializeField] private bool returnCameraAfterIntro = true;
    [SerializeField] private Transform gameplayCameraAnchorAfterIntro;

    [Header("Completion UI")]
    [SerializeField] private GameObject tutorialCompletePanel;
    [SerializeField] private TMP_Text tutorialCompleteText;
    [SerializeField] private Button finishButton;

    [Header("Runtime")]
    [SerializeField] private TutorialPhase currentPhase = TutorialPhase.None;
    [SerializeField] private CustomerGroup activeTutorialGroup;
    [SerializeField] private bool tutorialStarted;

    private const int TotalPlayablePhases = 8;

    public TutorialPhase CurrentPhase => currentPhase;
    public CustomerGroup ActiveTutorialGroup => activeTutorialGroup;
    public bool TutorialStarted => tutorialStarted;

    private int currentIntroIndex = -1;
    private bool notepadOpened;
    private bool orderConfirmed;
    private bool cashierOpened;
    private bool cashierConfirmed;

    private TutorialRoleHighlight roleHighlight;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (tutorialIntroPanel != null)
            tutorialIntroPanel.SetActive(true);

        if (tutorialCompletePanel != null)
            tutorialCompletePanel.SetActive(false);

        ResolveSceneReferences();

        roleHighlight = FindFirstObjectByType<TutorialRoleHighlight>();
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
        RefreshUI();
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
    }

    public void StartTutorial()
    {
        Debug.Log("[TutorialManager] StartTutorial called");

        tutorialStarted = true;
        currentIntroIndex = -1;
        activeTutorialGroup = null;

        if (tutorialIntroPanel != null)
            tutorialIntroPanel.SetActive(false);

        if (tutorialCompletePanel != null)
            tutorialCompletePanel.SetActive(false);

        SetPhase(TutorialPhase.Intro);
        PlayNextIntroStep();
    }

    private void PlayNextIntroStep()
    {
        currentIntroIndex++;

        if (introSteps == null || introSteps.Length == 0 || currentIntroIndex >= introSteps.Length)
        {
            FinishIntroSequence();
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
                step.roleName,
                step.message,
                PlayNextIntroStep
            );
        }
        else
        {
            PlayNextIntroStep();
        }
    }

    private void FinishIntroSequence()
    {
        if (roleHighlight != null)
            roleHighlight.Hide();

        if (returnCameraAfterIntro && roleCameraController != null && gameplayCameraAnchorAfterIntro != null)
            roleCameraController.PanToTarget(gameplayCameraAnchorAfterIntro);

        if (dialogueUI != null)
        {
            dialogueUI.ShowManual(
                "Manager",
                "Now that you know the roles, let’s begin with the Host. Greet the first customer.",
                BeginFirstPhase
            );
        }
        else
        {
            BeginFirstPhase();
        }
    }

    private void BeginFirstPhase()
    {
        SetPhase(TutorialPhase.GreetCustomer);

        if (spawnCustomerOnStart)
            Invoke(nameof(SpawnInitialCustomerGroup), firstSpawnDelay);
    }

    private void SpawnInitialCustomerGroup()
    {
        if (!tutorialStarted)
            return;

        if (groupSpawner == null)
        {
            Debug.LogWarning("[TutorialManager] Missing GroupSpawner.");
            return;
        }

        if (activeTutorialGroup != null)
            return;

        activeTutorialGroup = groupSpawner.SpawnGroup();

        if (activeTutorialGroup == null)
        {
            Debug.LogWarning("[TutorialManager] Failed to spawn tutorial customer group.");
            return;
        }

        TutorialGroupWatcher watcher = activeTutorialGroup.GetComponent<TutorialGroupWatcher>();
        if (watcher == null)
            watcher = activeTutorialGroup.gameObject.AddComponent<TutorialGroupWatcher>();

        watcher.Init(activeTutorialGroup);

        TutorialSceneWatcher sceneWatcher = GetComponent<TutorialSceneWatcher>();
        if (sceneWatcher != null)
            sceneWatcher.ResetWatcher();

        Debug.Log("[TutorialManager] Tutorial group spawned: " + activeTutorialGroup.name);
    }

    public void SetPhase(TutorialPhase newPhase)
    {
        currentPhase = newPhase;
        RefreshUI();

        Debug.Log("[TutorialManager] Phase -> " + currentPhase);

        if (currentPhase == TutorialPhase.Complete)
        {
            ShowCompletionUI();
            return;
        }

        if (currentPhase == TutorialPhase.Intro)
            return;

        if (showDialoguePerPhase && dialogueUI != null)
            ShowPhaseDialogue(currentPhase);
    }

    public void AdvancePhase()
    {
        switch (currentPhase)
        {
            case TutorialPhase.GreetCustomer:
                SetPhase(TutorialPhase.AssignTable);
                break;

            case TutorialPhase.AssignTable:
                SetPhase(TutorialPhase.TakeOrder);
                break;

            case TutorialPhase.TakeOrder:
                SetPhase(TutorialPhase.SubmitOrder);
                break;

            case TutorialPhase.SubmitOrder:
                SetPhase(TutorialPhase.ServeFood);
                break;

            case TutorialPhase.ServeFood:
                SetPhase(TutorialPhase.DeliverBill);
                break;

            case TutorialPhase.DeliverBill:
                SetPhase(TutorialPhase.CollectPayment);
                break;

            case TutorialPhase.CollectPayment:
                SetPhase(TutorialPhase.CleanTray);
                break;

            case TutorialPhase.CleanTray:
                SetPhase(TutorialPhase.Complete);
                break;
        }
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

        switch (phase)
        {
            case TutorialPhase.GreetCustomer:
                return "Tap the customer. Then tap the greet bubble.";

            case TutorialPhase.AssignTable:
                return "Now tap the assign bubble. Then tap the table with the arrow.";

            case TutorialPhase.TakeOrder:
                if (state == CustomerGroup.GroupState.WaitingToOrder)
                    return "Wait. The customer will call when ready.";

                if (state == CustomerGroup.GroupState.ReadyToOrder)
                    return "Tap the order bubble to open the notepad.";

                return "Use the notepad. Check the food and drink. Then confirm the same order.";

            case TutorialPhase.SubmitOrder:
                return "Bring the confirmed order to the counter. Wait for the tray to spawn.";

            case TutorialPhase.ServeFood:
                if (state == CustomerGroup.GroupState.OrderTaken)
                    return "Pick up the tray first. Then follow the arrow to the customer.";

                return "Good. The customer is eating now. Wait until they finish.";

            case TutorialPhase.DeliverBill:
                if (state == CustomerGroup.GroupState.Eating)
                    return "Wait. The customer is still eating.";

                return "The customer is done. Deliver the bill now.";

            case TutorialPhase.CollectPayment:
                return "Pick up the money from the table. Then bring it to the cashier station.";

            case TutorialPhase.CleanTray:
                return "Pick up the dirty tray. Then bring it to the sink.";
        }

        return string.Empty;
    }

    private void ShowCompletionUI()
    {
        if (tutorialCompletePanel != null)
            tutorialCompletePanel.SetActive(true);

        if (tutorialCompleteText != null)
            tutorialCompleteText.text = "Lobby Tutorial Complete";

        TutorialArrowManager arrowManager = GetComponent<TutorialArrowManager>();
        if (arrowManager != null)
            arrowManager.ForceHide();

        if (dialogueUI != null)
            dialogueUI.Hide();

        if (roleHighlight != null)
            roleHighlight.Hide();
    }

    private void RefreshUI()
    {
        if (phaseText != null)
            phaseText.text = GetPhaseTitle(currentPhase);

        if (objectiveText != null)
            objectiveText.text = GetObjectiveText(currentPhase);

        if (progressBar != null)
            progressBar.value = GetProgress01();
    }

    private string GetPhaseTitle(TutorialPhase phase)
    {
        switch (phase)
        {
            case TutorialPhase.Intro: return "Introduction";
            case TutorialPhase.GreetCustomer: return "Phase 1";
            case TutorialPhase.AssignTable: return "Phase 2";
            case TutorialPhase.TakeOrder: return "Phase 3";
            case TutorialPhase.SubmitOrder: return "Phase 4";
            case TutorialPhase.ServeFood: return "Phase 5";
            case TutorialPhase.DeliverBill: return "Phase 6";
            case TutorialPhase.CollectPayment: return "Phase 7";
            case TutorialPhase.CleanTray: return "Phase 8";
            case TutorialPhase.Complete: return "Complete";
            default: return "";
        }
    }

    private string GetObjectiveText(TutorialPhase phase)
    {
        CustomerGroup group = activeTutorialGroup;
        CustomerGroup.GroupState state = group != null ? group.state : CustomerGroup.GroupState.Spawning;

        switch (phase)
        {
            case TutorialPhase.Intro:
                return "Learn the role of each staff member.";

            case TutorialPhase.GreetCustomer:
                if (group == null)
                    return "Wait for a customer group.";

                if (!group.hasBeenGreeted)
                    return "Tap the customer. Then tap the greet bubble.";

                return "Good. Wait for the assign bubble.";

            case TutorialPhase.AssignTable:
                if (group == null)
                    return "Wait for the customer group.";

                if (group.assignedBooth == null)
                    return "Tap the assign bubble. Then tap a table.";

                if (state == CustomerGroup.GroupState.WalkingToBooth)
                    return "Good. Wait for them to sit.";

                return "Good. They are seated.";

            case TutorialPhase.TakeOrder:
                if (group == null)
                    return "Wait for the customer.";

                if (state == CustomerGroup.GroupState.WaitingToOrder)
                    return "Wait for the order bubble.";

                if (state == CustomerGroup.GroupState.ReadyToOrder)
                    return "Tap the order bubble. Open the notepad.";

                return "Read the notepad. Match the food and drink. Then confirm.";

            case TutorialPhase.SubmitOrder:
                return "Bring the order to the counter. Wait for the tray to appear.";

            case TutorialPhase.ServeFood:
                if (group == null)
                    return "Pick up the tray. Bring it to the customer.";

                if (state == CustomerGroup.GroupState.OrderTaken)
                    return "Pick up the tray. Then bring it to the customer.";

                if (state == CustomerGroup.GroupState.Eating)
                    return "Good. They are eating. Wait.";

                return "Serve the correct tray.";

            case TutorialPhase.DeliverBill:
                if (group == null)
                    return "Wait for the bill request.";

                if (state == CustomerGroup.GroupState.Eating)
                    return "Wait for them to finish eating.";

                if (state == CustomerGroup.GroupState.NeedsBill)
                    return "Give the bill to the customer.";

                return "Wait for the bill request.";

            case TutorialPhase.CollectPayment:
                return "Pick up the money. Bring it to the cashier station.";

            case TutorialPhase.CleanTray:
                return "Pick up the dirty tray. Bring it to the sink.";

            case TutorialPhase.Complete:
                return "Tutorial complete!";

            default:
                return "Press Start to begin.";
        }
    }

    private float GetProgress01()
    {
        int index = GetPlayablePhaseIndex(currentPhase);
        return Mathf.Clamp01((float)index / TotalPlayablePhases);
    }

    private int GetPlayablePhaseIndex(TutorialPhase phase)
    {
        switch (phase)
        {
            case TutorialPhase.GreetCustomer: return 1;
            case TutorialPhase.AssignTable: return 2;
            case TutorialPhase.TakeOrder: return 3;
            case TutorialPhase.SubmitOrder: return 4;
            case TutorialPhase.ServeFood: return 5;
            case TutorialPhase.DeliverBill: return 6;
            case TutorialPhase.CollectPayment: return 7;
            case TutorialPhase.CleanTray: return 8;
            case TutorialPhase.Complete: return 8;
            default: return 0;
        }
    }

    public bool IsPhase(TutorialPhase phase)
    {
        return currentPhase == phase;
    }

    public bool IsActiveGroup(CustomerGroup group)
    {
        return group != null && group == activeTutorialGroup;
    }

    public void RegisterCustomerGreeted(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (currentPhase != TutorialPhase.GreetCustomer) return;
        if (!IsActiveGroup(group)) return;

        AdvancePhase();
    }

    public void RegisterTableAssigned(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (currentPhase != TutorialPhase.AssignTable) return;
        if (!IsActiveGroup(group)) return;

        AdvancePhase();
    }

    public void RegisterOrderTaken(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (currentPhase != TutorialPhase.TakeOrder) return;
        if (!IsActiveGroup(group)) return;

        AdvancePhase();
    }

    public void RegisterOrderSubmitted(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (currentPhase != TutorialPhase.SubmitOrder) return;
        if (!IsActiveGroup(group)) return;

        AdvancePhase();
    }

    public void RegisterFoodServed(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (currentPhase != TutorialPhase.ServeFood) return;
        if (!IsActiveGroup(group)) return;

        AdvancePhase();
    }

    public void RegisterBillDelivered(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (currentPhase != TutorialPhase.DeliverBill) return;
        if (!IsActiveGroup(group)) return;

        AdvancePhase();
    }

    public void RegisterPaymentCollected(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (currentPhase != TutorialPhase.CollectPayment) return;
        if (group != null && !IsActiveGroup(group)) return;

        AdvancePhase();
    }

    public void RegisterTrayCleaned(CustomerGroup group)
    {
        if (!tutorialStarted) return;
        if (currentPhase != TutorialPhase.CleanTray) return;
        if (group != null && !IsActiveGroup(group)) return;

        AdvancePhase();
    }

    private void OnFinishTutorial()
    {
        Debug.Log("[TutorialManager] Tutorial finished.");
    }

    public void OnNotepadOpened(CustomerGroup group)
    {
        if (!IsPhase(TutorialPhase.TakeOrder)) return;
        if (!IsActiveGroup(group)) return;
        if (notepadOpened) return;

        notepadOpened = true;

        if (dialogueUI != null)
        {
            dialogueUI.ShowAuto(
                "Manager",
                "This is the notepad. Read the order on top. Match the same food and drink below.",
                3f
            );
        }
    }

    public void OnOrderConfirmed(CustomerGroup group)
    {
        if (!IsPhase(TutorialPhase.TakeOrder)) return;
        if (!IsActiveGroup(group)) return;
        if (orderConfirmed) return;

        orderConfirmed = true;

        if (dialogueUI != null)
        {
            dialogueUI.ShowAuto(
                "Manager",
                "Good. You matched the correct order.",
                2f
            );
        }
    }

    public void OnCashierOpened(CustomerGroup group, int expectedChange)
    {
        if (!IsPhase(TutorialPhase.CollectPayment)) return;
        if (cashierOpened) return;

        cashierOpened = true;

        if (dialogueUI != null)
        {
            dialogueUI.ShowAuto(
                "Manager",
                $"This is the cashier. Give the exact change: {expectedChange}.",
                3f
            );
        }
    }

    public void OnCashierConfirmed(CustomerGroup group)
    {
        if (!IsPhase(TutorialPhase.CollectPayment)) return;
        if (cashierConfirmed) return;

        cashierConfirmed = true;

        if (dialogueUI != null)
        {
            dialogueUI.ShowAuto(
                "Manager",
                "Correct. Payment complete.",
                2f
            );
        }
    }
}