using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small, resumable campaign guides hosted by the existing Big Boss tips object.</summary>
public sealed class RestaurantUnlockCoach : MonoBehaviour
{
    private enum Step { None, Introduction, Computer, App, Applicants, Hire, Summary, Equipment }
    private RestaurantBossTips owner;
    private RestaurantUnlockCoachUI view;
    private EmployeeManager employeeManager;
    private ManagementComputerController computer;
    private ManagementComputerHRPanel focusedPanel;
    private Step step;
    private EmployeeRole role;
    private string milestone;
    private string session;
    private string deferredSession;
    private bool equipmentGuide;
    private bool focusedApplicants;
    private int equipmentIndex;
    private readonly HashSet<string> pendingEquipment = new();
    private readonly List<Equipment> equipmentBatch = new();
    private float nextCheck;
    public bool IsActive => step != Step.None;
    public GameObject OverlayRoot => IsActive && view != null ? view.gameObject : null;
    public bool ConsumesPointer(Vector2 position) => view != null && view.ConsumesPointer(position);
    private static string Session => CampaignSaveStore.RestaurantScene + ":" + StaffHiringProgressionSettings.CurrentDay;
    private string MilestoneID(EmployeeRole value) => CampaignSaveStore.RestaurantScene + ":hire:" + value;
    private string EquipmentID(string id) => CampaignSaveStore.RestaurantScene + ":equipment:" + id;
    private string ActionWord => TutorialInputTerminology.ActivateWord;

    private void Awake() => owner = GetComponent<RestaurantBossTips>();
    private void OnDisable() => StopGuide(false);
    private void OnDestroy()
    {
        Unsubscribe();
        if (view != null) Destroy(view.gameObject);
    }

    public void QueueEquipment(Equipment item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.itemID) || CampaignSaveStore.ProtectedSession ||
            owner.HasSeenCoach(EquipmentID(item.itemID))) return;
        if (pendingEquipment.Add(item.itemID)) GameSaveManager.Instance?.RequestSave();
    }

    public void FillSaveData(GameSaveData data) =>
        data.pendingEquipmentCoachIDs = new List<string>(pendingEquipment);

    public void ApplySaveData(GameSaveData data)
    {
        StopGuide(false);
        deferredSession = null;
        pendingEquipment.Clear();
        if (data?.pendingEquipmentCoachIDs != null)
            foreach (string id in data.pendingEquipmentCoachIDs)
                if (!string.IsNullOrWhiteSpace(id)) pendingEquipment.Add(id);
    }

    private bool InPreparation()
    {
        return CampaignSaveStore.RuntimeCampaign && !CampaignSaveStore.NeedsReload &&
            SceneManager.GetActiveScene().name == CampaignSaveStore.RestaurantScene &&
            !TutorialSystem.IsTutorialMode &&
            GameSaveManager.Instance != null && GameSaveManager.Instance.HasCompletedInitialLoad &&
            !GameSaveManager.Instance.IsApplyingSave &&
            GameDayManager.Instance != null && !GameDayManager.Instance.ServiceActive &&
            EmployeeManager.Instance != null && !EmployeeManager.Instance.SlotsLocked;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .2f;
        if (!InPreparation() || (IsActive && session != Session))
        {
            if (IsActive) StopGuide(true);
            return;
        }
        if (Time.timeScale <= 0f || HygieneManager.DecisionPaused ||
            ManagerComplaintSystem.Instance?.HasActiveComplaint == true)
        {
            if (IsActive) view.SetSuspended(true);
            return;
        }

        if (!IsActive)
        {
            if (deferredSession == Session || GameplayUIBlocker.IsBlocked() ||
                UnlockCelebrationManager.Instance == null ||
                !UnlockCelebrationManager.Instance.ReadyForCoaching ||
                EquipmentManager.Instance?.AllEquipment == null) return;
            // The authoring installer extracts this from Lobby1Tutorial; never fall back to a different-looking card.
            if (view == null && Resources.Load<GameObject>(RestaurantUnlockCoachUI.ResourcePath) == null) return;
            if (TryMilestone(EmployeeRole.Busser) || TryMilestone(EmployeeRole.Cashier)) return;
            equipmentBatch.Clear();
            foreach (var item in EquipmentManager.Instance.AllEquipment)
                if (item != null && pendingEquipment.Contains(item.itemID) &&
                    !owner.HasSeenCoach(EquipmentID(item.itemID))) equipmentBatch.Add(item);
            equipmentBatch.Sort(Equipment.CompareProgression);
            if (equipmentBatch.Count > 0) BeginEquipment();
            return;
        }

        view.SetSuspended(false);
        if (view.IsExplaining) return;
        if (step == Step.Equipment)
        {
            if (computer == null || !computer.IsOpen) { StopGuide(true); return; }
            if (GameplayUIBlocker.IsBlockedExcept(computer.DesktopRoot))
            {
                view.SetSuspended(true);
                return;
            }
            FocusEquipment(equipmentBatch[equipmentIndex]);
            return;
        }
        // Action steps allow the computer but yield to other modals.
        bool actionStep = step == Step.Computer || step == Step.App || step == Step.Applicants || step == Step.Hire;
        if (!actionStep) return;
        if (computer == null) computer = FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include);
        if (GameplayUIBlocker.IsBlockedExcept(computer != null ? computer.DesktopRoot : null))
        {
            view.SetSuspended(true);
            return;
        }
        view.SetSuspended(false);
        if (computer == null || !computer.IsOpen)
        {
            if (step != Step.Computer) StopGuide(true);
            else
            {
                view.SetTarget(LobbyHUDRedesign.Instance?.ComputerButton?.transform as RectTransform);
            }
            return;
        }
        ManagementComputerApp app = equipmentGuide ? ManagementComputerApp.Equipment : ManagementComputerApp.Staff;
        if (computer.SelectedApp != (int)app || computer.AppWindow == null || !computer.AppWindow.gameObject.activeInHierarchy)
        {
            if (step != Step.App) ShowAppStep(app);
            view.SetTarget(computer.GetAppButton(app)?.transform as RectTransform);
            return;
        }
        if (equipmentGuide) { ShowEquipmentPage(); return; }
        var panel = computer.AppWindow.GetComponentInChildren<ManagementComputerHRPanel>();
        if (panel == null || panel.ApplicantsTab == null) return;
        if (panel != focusedPanel) { focusedPanel = panel; focusedApplicants = false; }
        if (panel.CurrentView != ManagementHRView.Applicants)
        {
            focusedApplicants = false;
            if (step != Step.Applicants)
            {
                step = Step.Applicants;
                Show("2 / 3  •  FIND APPLICANTS",
                    ActionWord + " APPLICANTS to compare the available " + EmployeeRoleCatalog.DisplayName(role) +
                    " candidates. Check their skills and daily salary.", null, null, false);
            }
            view.SetTarget(panel.ApplicantsTab.transform as RectTransform);
            return;
        }
        if (!focusedApplicants)
        {
            panel.ShowApplicantsForRole(role);
            focusedApplicants = true;
            return; // Existing layout coroutine scrolls the role into view.
        }
        var card = panel.FindApplicantCard(role);
        if (step != Step.Hire)
        {
            step = Step.Hire;
            if (employeeManager.GetHiredCount(role) >= 2) { ShowSummary(true); return; }
            Show("3 / 3  •  CHOOSE YOUR HIRE",
                card != null
                    ? ActionWord + " HIRE on your preferred " + EmployeeRoleCatalog.DisplayName(role) +
                      ". Compare skills and salary first. The extra roster slot is optional."
                    : "There are no available " + EmployeeRoleCatalog.DisplayName(role) +
                      " applicants right now. New applicants arrive on Day " + employeeManager.ApplicantNextRefreshDay +
                      ". Choose Later to return to this guide.",
                null, null, false);
        }
        // Let the player compare and choose any candidate in this role's applicant rail.
        view.SetTarget(card != null ? card.transform.parent as RectTransform : null,
            card != null ? card.PrimaryButton.transform as RectTransform : null);
    }

    private bool TryMilestone(EmployeeRole candidate)
    {
        if (StaffHiringProgressionSettings.CurrentDay < StaffHiringProgressionSettings.UnlockDay(candidate) ||
            owner.HasSeenCoach(MilestoneID(candidate))) return false;
        role = candidate;
        milestone = MilestoneID(candidate);
        equipmentGuide = false;
        Begin();
        employeeManager = EmployeeManager.Instance;
        employeeManager.ApplicantHired += OnHired;
        string name = EmployeeRoleCatalog.DisplayName(role);
        step = Step.Introduction;
        bool alreadyHired = employeeManager.GetHiredCount(role) >= 2;
        Show("DAY " + StaffHiringProgressionSettings.UnlockDay(role) + "  •  NEW HIRE SLOT",
            alreadyHired
                ? "Your extra " + name + " hire slot is open. You already have extra staff, so let's review how scheduling works."
                : "Hey, the restaurant is getting busy. I've opened another " + name +
                  " hire slot, so you can hire someone else. Let me show you how.",
            alreadyHired ? "REVIEW" : "SHOW ME", () => { if (alreadyHired) ShowSummary(true); else ShowComputerStep(); }, true);
        return true;
    }

    private void Begin()
    {
        session = Session;
        owner.HideIdleTip();
        if (view == null) view = RestaurantUnlockCoachUI.Create(owner.BossPortrait);
        view.Later = () => StopGuide(true);
        view.Skip = Complete;
        focusedPanel = null;
        focusedApplicants = false;
    }

    private void BeginEquipment()
    {
        equipmentGuide = true;
        equipmentIndex = 0;
        Begin();
        step = Step.Introduction;
        Show("NEW EQUIPMENT  •  QUICK TOUR",
            "You've unlocked " + equipmentBatch.Count + " new equipment " +
            (equipmentBatch.Count == 1 ? "item" : "items") +
            ". Let's find them in the computer. I'll explain what each does. Buy only what fits your budget.",
            "SHOW ME", ShowComputerStep, true);
    }

    private void ShowComputerStep()
    {
        step = Step.Computer;
        focusedPanel = null;
        Show("1 / 3  •  OPEN THE COMPUTER",
            ActionWord + " the highlighted Computer button on your HUD to " +
            (equipmentGuide ? "view your new equipment." : "hire your new team member.") +
            " You can use this shortcut instead of finding the computer in the restaurant.",
            null, null, false);
        view.SetTarget(LobbyHUDRedesign.Instance?.ComputerButton?.transform as RectTransform);
    }

    private void ShowAppStep(ManagementComputerApp app)
    {
        step = Step.App;
        Show("1 / 3  •  " + app.ToString().ToUpperInvariant(),
            ActionWord + " " + app.ToString().ToUpperInvariant() + " on the computer.",
            null, null, false);
    }

    private void OnHired(EmployeeData employee)
    {
        if (!IsActive || equipmentGuide || employee == null || employee.role != role) return;
        if (employeeManager.GetHiredCount(role) >= 2) ShowSummary(false);
        else
        {
            step = Step.Applicants; // A missing first hire is valid; continue until the additional slot is filled.
            focusedApplicants = false;
        }
    }

    private void ShowSummary(bool existing)
    {
        step = Step.Summary;
        string advice = "The extra employee is a reserve. In STAFF > LOBBY, use SET ACTIVE to choose who works this shift. " +
                        "Only one employee in this role works at a time. Make changes before opening.";
        if (role == EmployeeRole.Busser && CampaignSaveStore.IsFastFood)
            advice = "Both Lobby People can work together now. Lobby #1 clears tables and used trays; Lobby #2 delivers requested meals. " +
                "Purchased clearing and delivery trolleys let them carry several trays. Use STAFF > LOBBY to manage their active slots before opening.";
        if (role == EmployeeRole.Cashier && CampaignSaveStore.IsFastFood)
            advice = FastFoodProgressionSettings.HasSecondCashier
                ? "Your second register is purchased, so two Cashiers can work together. Check STAFF > LOBBY: use SET ACTIVE for an available Cashier slot or REST to free one before opening."
                : "Your extra Cashier is a reserve until you buy the Second Cashier Station in EQUIPMENT. With that register purchased, you can set two Cashiers active before opening.";
        Show(existing ? "YOUR TEAM  •  SCHEDULING" : "HIRED!  •  SCHEDULING", advice, "GOT IT", Complete, true);
    }

    private void ShowEquipmentPage()
    {
        step = Step.Equipment;
        Equipment item = equipmentBatch[equipmentIndex];
        string tip = string.IsNullOrWhiteSpace(item.coachTip) ? item.description : item.coachTip;
        if (string.IsNullOrWhiteSpace(tip)) tip = "This item is now available for purchase.";
        string location = string.IsNullOrWhiteSpace(item.coachLocation) ? "Computer > Equipment" : item.coachLocation;
        Show("EQUIPMENT " + (equipmentIndex + 1) + " / " + equipmentBatch.Count,
            item.displayName + "\n" + tip + "\nFind it: " + location +
            (EquipmentManager.Instance.Purchased(item.itemID) ? "\nYou already own this item." : "\nUnlocked means available to buy; it is not purchased yet.") +
            " I'll highlight it after we're done talking. Buying is optional; choose CONTINUE to move on without buying.",
            "CONTINUE", () => view.BeginOptionalReview(() =>
            {
                owner.MarkCoachSeen(EquipmentID(item.itemID));
                pendingEquipment.Remove(item.itemID);
                GameSaveManager.Instance?.RequestSave();
                equipmentIndex++;
                if (equipmentIndex < equipmentBatch.Count) ShowEquipmentPage();
                else StopGuide(false);
            }), true, item.sprite);
        FocusEquipment(item);
    }

    private void FocusEquipment(Equipment item)
    {
        RectTransform target = null;
        if (computer != null && computer.AppWindow != null)
            foreach (var card in computer.AppWindow.GetComponentsInChildren<ManagementEquipmentCardUI>())
                if (card.Equipment != null && card.Equipment.itemID == item.itemID)
                {
                    target = card.transform as RectTransform;
                    break;
                }
        view.SetTarget(target);
    }

    private void Show(string caption, string text, string action, Action onAction, bool modal, Sprite icon = null) =>
        view.Show(caption, text, action, onAction, modal, icon);

    private void Complete()
    {
        if (equipmentGuide)
            foreach (var item in equipmentBatch)
            {
                owner.MarkCoachSeen(EquipmentID(item.itemID));
                pendingEquipment.Remove(item.itemID);
            }
        else if (!string.IsNullOrEmpty(milestone)) owner.MarkCoachSeen(milestone);
        GameSaveManager.Instance?.RequestSave();
        StopGuide(false);
    }

    private void Unsubscribe()
    {
        if (employeeManager != null) employeeManager.ApplicantHired -= OnHired;
        employeeManager = null;
    }

    private void StopGuide(bool defer)
    {
        if (defer) deferredSession = session;
        Unsubscribe();
        step = Step.None;
        focusedPanel = null;
        if (view != null) view.Hide();
    }
}
