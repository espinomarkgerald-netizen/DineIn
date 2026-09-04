using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial-side references for UI instantiated by the real HUD / Management Computer.
/// This script never edits shared gameplay code; it only resolves and temporarily presents
/// the real scene UI while a tutorial step is focused on it.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialSceneBindings : MonoBehaviour
{
    [Serializable]
    public struct UITarget
    {
        public string key;
        public RectTransform target;
    }

    [SerializeField] private UITarget[] uiTargets = Array.Empty<UITarget>();

    private Canvas revealedCanvas;
    private bool previousCanvasEnabled;
    private LobbyHUDRedesign revealedLobbyHUD;
    private bool previousLobbyHUDEnabled;
    private PlayerTaskHUD revealedTaskHUD;
    private bool previousTaskHUDEnabled;
    private readonly List<(CanvasGroup group, float alpha, bool interactable, bool blocks)> groups = new();
    private readonly List<(GameObject obj, bool active)> objects = new();
    private Button revealedButton;
    private bool previousButtonInteractable;

    /// <summary>
    /// Reopens the exact department that owns the employee hired by this tutorial.
    /// The real HR panel rebuild remains authoritative; this only selects its view.
    /// </summary>
    public void PrepareForStep(string key)
    {
        if (key == "StaffApplicantsButton" || key == "StaffApplicantCard" ||
            key == "StaffHire" || key == "StaffSalary" || key == "StaffRating")
        {
            EnsureTutorialApplicant();

            ManagementComputerHRPanel applicantsPanel = FindActiveHRPanel();
            if (applicantsPanel != null && key != "StaffApplicantsButton" &&
                applicantsPanel.CurrentView != ManagementHRView.Applicants)
                applicantsPanel.ShowApplicants();
        }

        if (key != "StaffAssigned" && key != "StaffSetActive" && key != "StaffFire")
            return;

        EmployeeData employee = TutorialUIActionAdapter.LastHiredEmployee;
        ManagementComputerHRPanel panel = FindActiveHRPanel();
        if (employee == null || !employee.hired || panel == null)
            return;

        EmployeeDepartment department = IsKitchenRole(employee.role)
            ? EmployeeDepartment.Kitchen
            : EmployeeDepartment.Lobby;
        if (panel.CurrentView == ManagementHRView.Applicants || panel.CurrentDepartment != department)
            panel.ShowDepartment(department);
    }

    /// <summary>
    /// A saved campaign is allowed to have an empty applicant pool until its next
    /// refresh day. Lobby1Tutorial cannot wait for that calendar gate, so it adds
    /// one ordinary runtime applicant through the real generator when necessary.
    /// The normal roster, employee assets, and refresh rules are not edited.
    /// </summary>
    private static void EnsureTutorialApplicant()
    {
        EmployeeManager manager = EmployeeManager.Instance;
        if (manager == null)
            return;

        manager.EnsureEmployeesGenerated();
        if (manager.allEmployees == null)
            return;

        foreach (EmployeeData employee in manager.allEmployees)
            if (employee != null && !employee.hired &&
                manager.GetHiredCount(employee.role) < manager.MaxHiredPerRole)
                return;

        if (manager.generator == null)
            manager.generator = FindFirstObjectByType<EmployeeGenerator>(FindObjectsInactive.Include);
        if (manager.generator == null)
        {
            Debug.LogError("[Tutorial] Employee generator is unavailable for the Staff lesson.");
            return;
        }

        EmployeeRole? availableRole = null;
        foreach (EmployeeRole role in EmployeeRoleCatalog.LobbyRoles)
            if (manager.GetHiredCount(role) < manager.MaxHiredPerRole)
            {
                availableRole = role;
                break;
            }
        if (!availableRole.HasValue)
            foreach (EmployeeRole role in EmployeeRoleCatalog.KitchenRoles)
                if (manager.GetHiredCount(role) < manager.MaxHiredPerRole)
                {
                    availableRole = role;
                    break;
                }

        if (!availableRole.HasValue)
        {
            Debug.LogError("[Tutorial] Every Staff role is full; no applicant can be hired.");
            return;
        }

        List<string> usedNames = new List<string>();
        foreach (EmployeeData employee in manager.allEmployees)
            if (employee != null && !string.IsNullOrWhiteSpace(employee.employeeName))
                usedNames.Add(employee.employeeName);

        EmployeeData applicant = manager.generator.GenerateApplicant(availableRole.Value, usedNames);
        applicant.EnsureIdentity();
        applicant.applicantAvailableUntilDay = int.MaxValue;
        if (!manager.allEmployees.Contains(applicant))
            manager.allEmployees.Add(applicant);
    }

    public RectTransform ResolveUI(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        // Inspector bindings always win. This lets later tutorial lessons bind exact
        // authored controls without changing this script or the gameplay UI.
        foreach (UITarget binding in uiTargets)
            if (string.Equals(binding.key, key, StringComparison.Ordinal) && binding.target != null)
                return binding.target;

        switch (key)
        {
            // ── Progress HUD ──────────────────────────────────────────────────
            case "AlienApproval":
                return ResolveProgressTarget("ApprovalProgress");

            // The current green HUD bar is today's earned / required revenue goal,
            // not wallet cash. Keep this distinction in player-facing tutorial text.
            case "TodaySales":
                return ResolveProgressTarget("SalesProgress");
            case "TodaySalesValue":
                return ResolveProgressTarget("SalesProgress", "Value");
            case "TodaySalesTrack":
                return ResolveProgressTarget("SalesProgress", "Track");

            // Player-facing term is UNSATISFIED. Internal gameplay names stay Neutral.
            case "Unsatisfied":
            case "Neutral": // backwards compatibility with already-authored steps
                return ResolveProgressTarget("NeutralProgress");
            case "UnsatisfiedValue":
            case "NeutralValue":
                return ResolveProgressTarget("NeutralProgress", "Value");
            case "Angry":
                return ResolveProgressTarget("AngryProgress");
            case "AngryValue":
                return ResolveProgressTarget("AngryProgress", "Value");

            // ── Lobby HUD ─────────────────────────────────────────────────────
            case "LivePanel":
                return ResolveControl("LivePanel");
            case "LiveCounts":
                return ResolveControl("LivePanel/Counts");
            case "NewspaperButton":
            case "TaskButton":
            case "TaskMessage":
            case "CameraButton":
            case "ComputerButton":
                return ResolveControl(key);
            case "NewspaperClose":
                return FindNamedUI("Close Newspaper");

            // ── Management Computer navigation ───────────────────────────────
            case "DashboardButton":
                return ResolveManagementButton("DASHBOARD");
            case "StaffButton":
                return ResolveManagementButton("STAFF", "STAFF SCHEDULER");
            case "MenuButton":
                return ResolveManagementButton("MENU", "MENU EDITOR");
            case "EquipmentButton":
                return ResolveManagementButton("EQUIPMENT", "EQUIPMENT STORE");
            case "FinanceButton":
                return ResolveManagementButton("FINANCE");
            case "ObjectivesButton":
                return ResolveManagementButton("OBJECTIVES");
            case "RestockButton":
                return ResolveManagementButton("RESTOCK", "INGREDIENT RESTOCK");

            case "ManagementOverview":
            {
                ManagementComputerController computer = FindManagementComputer();
                return computer != null && computer.AppWindow != null && computer.AppWindow.Content != null
                    ? computer.AppWindow.Content.parent as RectTransform
                    : null;
            }

            // ── Management page details ────────────────────────────────────
            // These targets are runtime instances created by the real Management
            // Computer. Exact authored names are used first, with the current page
            // content as a safe focus fallback when an optional control is absent.
            case "StaffOverview": return ResolveManagementDetail("HRBoard");
            case "StaffSlots": return ResolveManagementDetail("RoleSections");
            case "StaffRoles": return ResolveManagementDetail("DepartmentTitle");
            case "StaffApplicantsButton": return ResolveManagementDetail("ApplicantsDepartmentTab");
            case "StaffLobbyButton": return ResolveManagementDetail("LobbyDepartmentTab");
            case "StaffKitchenButton": return ResolveManagementDetail("KitchenDepartmentTab");
            case "StaffApplicantCard": return ResolveStaffApplicantPart(null);
            case "StaffHire": return ResolveStaffApplicantPart("HIRE");
            case "StaffSalary": return ResolveStaffApplicantPart("Salary");
            case "StaffRating": return ResolveStaffApplicantPart("RatingStars", "Stats");
            case "StaffAssigned": return ResolveStaffAssignedCard();
            case "StaffSetActive": return ResolveStaffAssignedPart("SET ACTIVE");
            case "StaffFire": return ResolveStaffAssignedPart("FIRE");

            case "MenuCategories": return ResolveManagementDetail("CatalogCategoryTabs");
            case "MenuFoodTab": return ResolveManagementDetail("FoodTab");
            case "MenuItems":
            case "MenuItemSelect": return ResolveMenuCardPart(false, null);
            case "MenuItemDetails": return ResolveManagementDetail("MenuDetails", "MenuDescription");
            case "MenuPrice": return ResolveManagementDetail("PriceInput", "Price");
            case "MenuPriceEditor":
            {
                RectTransform price = ResolveManagementDetail("PriceInput");
                return price != null ? price.parent as RectTransform : null;
            }
            case "MenuSavePrice": return ResolveManagementDetail("SavePrice");
            case "MenuIngredientCost": return ResolveManagementDetail("MenuDescription");
            case "MenuAvailability": return ResolveManagementDetail("MenuAvailability");
            case "MenuLocked": return ResolveMenuProgressionStatus();

            case "EquipmentCatalog": return ResolveManagementComponent<ManagementEquipmentSectionUI>();
            case "EquipmentCard": return ResolveManagementComponent<ManagementEquipmentCardUI>();

            case "FinanceSales": return ResolveManagementDetail("SALES SO FAR Card");
            case "FinanceExpenses": return ResolveManagementDetail("EXPENSES SO FAR Card");
            case "FinanceNet": return ResolveManagementDetail("CURRENT NET Card", "Net Profit");
            case "FinanceBalance": return ResolveManagementDetail("CASH BALANCE Card");
            case "FinanceToday": return ResolveManagementDetail("Today Tab");
            case "FinanceHistory": return ResolveManagementDetail("History Tab");

            case "Objective":
            case "ObjectiveMandatory": return ResolveObjectivePart("MANDATORY");
            case "ObjectiveProgress":
            case "ObjectiveSecondary": return ResolveObjectivePart("SECONDARY");
            case "ObjectiveReward":
            case "ObjectiveBonus": return ResolveObjectivePart("BONUS");

            case "RestockCategories": return ResolveManagementDetail("CatalogCategoryTabs");
            case "RestockStatus": return ResolveRestockCardPart(RestockStorageType.Dry, "StatusBand");
            case "RestockInStock": return ResolveRestockCardPart(RestockStorageType.Dry, "InStockCell");
            case "RestockNeeded": return ResolveRestockCardPart(RestockStorageType.Dry, "NeededTodayCell");
            case "RestockQuantity": return ResolveRestockCardPart(RestockStorageType.Dry, "QuantityControls");
            case "RestockDryPlus": return ResolveRestockCardPart(RestockStorageType.Dry, "Plus");
            case "RestockColdPlus": return ResolveSecondRestockPlus();
            case "RestockCart": return ResolveManagementDetail("RestockCart");
            case "RestockCheckout": return ResolveManagementButtonByLabel("CHECKOUT");
            case "RestockOrderNow": return ResolveManagementButtonByLabel("ORDER NOW");
            case "ManagementExit": return ResolveManagementDesktopButton("ExitButton", "EXIT");
            case "ManagementStartShift": return ResolveManagementDesktopButton("StartShiftButton", "START SHIFT");
            case "RestockGetOrders":
            case "RestockHotbar":
            case "RestockDrySlot":
            case "RestockFrozenSlot":
            case "RestockSwitchRoom":
            case "RestockExit":
                return FindFirstObjectByType<TutorialRestockFlowBridge>(FindObjectsInactive.Include)?.ResolveUI(key);
        }

        // Future detailed page targets (staff slots, applicant cards, menu controls,
        // finance cards, restock quantity/cart/checkout, etc.) should normally be added
        // through the inspector uiTargets list, keeping gameplay scripts untouched.
        return null;
    }

    public Transform ResolveWorld(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return FindFirstObjectByType<TutorialRestockFlowBridge>(FindObjectsInactive.Include)?.ResolveWorld(key);
    }

    private static RectTransform ResolveControl(string path) =>
        LobbyHUDRedesign.Instance != null
            ? LobbyHUDRedesign.Instance.transform.Find("SafeArea/" + path) as RectTransform
            : null;

    private static RectTransform ResolveManagementDesktopButton(string objectName, string label)
    {
        ManagementComputerController computer = FindManagementComputer();
        if (computer == null) return null;
        foreach (Button button in computer.GetComponentsInChildren<Button>(true))
        {
            if (button.name == objectName) return button.transform as RectTransform;
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
                if (NormalizeLabel(text.text).IndexOf(NormalizeLabel(label), StringComparison.Ordinal) >= 0)
                    return button.transform as RectTransform;
        }
        return null;
    }

    private static ManagementComputerController FindManagementComputer() =>
        FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include);

    /// <summary>
    /// Resolves the real Management navigation button by its visible label instead of
    /// relying on brittle hierarchy paths. Exact labels are preferred; contained labels
    /// are only used as a fallback (e.g. STAFF SCHEDULER).
    /// </summary>
    private static RectTransform ResolveManagementButton(params string[] labels)
    {
        ManagementComputerController computer = FindManagementComputer();
        if (computer == null || labels == null || labels.Length == 0)
            return null;

        Button[] buttons = computer.GetComponentsInChildren<Button>(true);

        // Pass 1: exact visible label.
        foreach (Button button in buttons)
        {
            if (!button.gameObject.activeInHierarchy) continue;
            string visible = GetButtonLabel(button);
            foreach (string label in labels)
                if (string.Equals(visible, NormalizeLabel(label), StringComparison.Ordinal))
                    return button.transform as RectTransform;
        }

        // Pass 2: contained visible label. Prefer the shortest matching text so a
        // sidebar "STAFF" button wins over unrelated long descriptions when possible.
        RectTransform best = null;
        int bestLength = int.MaxValue;
        foreach (Button button in buttons)
        {
            if (!button.gameObject.activeInHierarchy) continue;
            string visible = GetButtonLabel(button);
            if (string.IsNullOrEmpty(visible))
                continue;

            foreach (string label in labels)
            {
                string wanted = NormalizeLabel(label);
                if (visible.IndexOf(wanted, StringComparison.Ordinal) >= 0 ||
                    wanted.IndexOf(visible, StringComparison.Ordinal) >= 0)
                {
                    if (visible.Length < bestLength)
                    {
                        best = button.transform as RectTransform;
                        bestLength = visible.Length;
                    }
                }
            }
        }

        return best;
    }

    private static string GetButtonLabel(Button button)
    {
        if (button == null)
            return string.Empty;

        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            string normalized = NormalizeLabel(label != null ? label.text : null);
            if (!string.IsNullOrEmpty(normalized))
                return normalized;
        }

        return NormalizeLabel(button.gameObject.name);
    }

    private static string NormalizeLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string[] pieces = value.Trim().ToUpperInvariant()
            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", pieces);
    }

    private static RectTransform FindNamedUI(string name, Transform parent = null)
    {
        RectTransform[] candidates = parent != null
            ? parent.GetComponentsInChildren<RectTransform>(true)
            : FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (RectTransform candidate in candidates)
            if (candidate.name == name)
                return candidate;

        return null;
    }

    private static RectTransform ResolveManagementDetail(params string[] names)
    {
        ManagementComputerController computer = FindManagementComputer();
        RectTransform content = computer != null && computer.AppWindow != null
            ? computer.AppWindow.Content
            : null;
        if (content == null)
            return null;

        PrepareLayout(content);
        RectTransform[] candidates = content.GetComponentsInChildren<RectTransform>(false);
        foreach (string name in names)
            foreach (RectTransform candidate in candidates)
                if (IsCurrentVisibleTarget(candidate, content) &&
                    string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase))
                    return candidate;

        foreach (string name in names)
            foreach (TMP_Text label in content.GetComponentsInChildren<TMP_Text>(false))
                if (IsCurrentVisibleTarget(label.rectTransform, content) &&
                    NormalizeLabel(label.text).IndexOf(NormalizeLabel(name), StringComparison.Ordinal) >= 0)
                    return label.transform as RectTransform;

        return null;
    }

    private static RectTransform ResolveManagementComponent<T>() where T : Component
    {
        ManagementComputerController computer = FindManagementComputer();
        RectTransform content = computer != null && computer.AppWindow != null
            ? computer.AppWindow.Content
            : null;
        if (content == null) return null;
        PrepareLayout(content);
        T[] components = content.GetComponentsInChildren<T>(false);
        foreach (T component in components)
            if (IsCurrentVisibleTarget(component.transform as RectTransform, content))
                return component.transform as RectTransform;
        return null;
    }

    private static RectTransform ResolveManagementButtonByLabel(params string[] labels)
    {
        ManagementComputerController computer = FindManagementComputer();
        RectTransform content = computer != null && computer.AppWindow != null
            ? computer.AppWindow.Content
            : null;
        if (content == null) return null;
        PrepareLayout(content);
        foreach (Button candidate in content.GetComponentsInChildren<Button>(false))
        {
            if (!IsCurrentVisibleTarget(candidate.transform as RectTransform, content)) continue;
            string visible = GetButtonLabel(candidate);
            foreach (string label in labels)
                if (visible.IndexOf(NormalizeLabel(label), StringComparison.Ordinal) >= 0)
                    return candidate.transform as RectTransform;
        }
        return null;
    }

    private static RectTransform ResolveManagementRowPart(string category, string childName)
    {
        ManagementComputerController computer = FindManagementComputer();
        RectTransform content = computer != null && computer.AppWindow != null
            ? computer.AppWindow.Content
            : null;
        if (content == null) return null;
        PrepareLayout(content);
        foreach (ManagementComputerRowUI row in content.GetComponentsInChildren<ManagementComputerRowUI>(false))
        {
            RectTransform rowRect = row.transform as RectTransform;
            if (!IsCurrentVisibleTarget(rowRect, content)) continue;
            string allText = string.Empty;
            foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(false))
                allText += " " + NormalizeLabel(text.text);
            if (allText.IndexOf(NormalizeLabel(category), StringComparison.Ordinal) < 0) continue;
            Transform part = string.IsNullOrEmpty(childName) ? null : row.transform.Find(childName);
            return part != null ? part as RectTransform : row.transform as RectTransform;
        }
        return null;
    }

    private static RectTransform ResolveObjectivePart(string category)
    {
        RectTransform row = ResolveManagementRowPart(category, null);
        if (row != null) return row;

        // The tutorial scene can legitimately have no DailyObjectiveManager yet.
        // In that case the real Objectives page shows its unavailable-state message
        // instead of category rows, so focus the real page content rather than leave
        // the lesson with an impossible target. A future objective lesson can bind a
        // specific row once the shared objective system is present in this scene.
        return CurrentManagementContent();
    }

    private static RectTransform ResolveStaffApplicantPart(params string[] partNames)
    {
        RectTransform content = CurrentManagementContent();
        if (content == null) return null;
        PrepareLayout(content);
        EmployeeManager manager = EmployeeManager.Instance;
        foreach (ManagementEmployeeCardUI card in content.GetComponentsInChildren<ManagementEmployeeCardUI>(false))
        {
            if (card.Employee == null || card.Employee.hired ||
                manager == null || manager.GetHiredCount(card.Employee.role) >= manager.MaxHiredPerRole ||
                !IsCurrentAnimatedCardTarget(card.transform as RectTransform, content)) continue;
            if (partNames == null || partNames.Length == 0 || partNames[0] == null)
                return card.transform as RectTransform;
            if (partNames[0] == "HIRE" && card.PrimaryButton != null)
                return card.PrimaryButton.transform as RectTransform;
            RectTransform part = FindDescendant(card.transform, partNames);
            if (part != null) return part;
            return card.transform as RectTransform;
        }
        return null;
    }

    private static RectTransform ResolveStaffAssignedCard()
    {
        RectTransform content = CurrentManagementContent();
        if (content == null) return null;
        EmployeeData expected = TutorialUIActionAdapter.LastHiredEmployee;
        foreach (ManagementEmployeeCardUI card in content.GetComponentsInChildren<ManagementEmployeeCardUI>(false))
            if (card.Employee != null && card.Employee.hired &&
                (expected == null || card.Employee == expected) &&
                IsCurrentAnimatedCardTarget(card.transform as RectTransform, content))
                return card.transform as RectTransform;
        return null;
    }

    private static RectTransform ResolveStaffAssignedPart(string label)
    {
        RectTransform card = ResolveStaffAssignedCard();
        if (card == null) return null;
        foreach (Button candidate in card.GetComponentsInChildren<Button>(false))
            if (GetButtonLabel(candidate).IndexOf(NormalizeLabel(label), StringComparison.Ordinal) >= 0)
                return candidate.transform as RectTransform;
        return card;
    }

    private static ManagementComputerHRPanel FindActiveHRPanel()
    {
        foreach (ManagementComputerHRPanel panel in FindObjectsByType<ManagementComputerHRPanel>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (panel != null && panel.gameObject.activeInHierarchy)
                return panel;
        return null;
    }

    private static bool IsKitchenRole(EmployeeRole role) =>
        role == EmployeeRole.Chef || role == EmployeeRole.Barista ||
        role == EmployeeRole.PrepCook || role == EmployeeRole.LineCook ||
        role == EmployeeRole.Assembler;

    private static RectTransform ResolveMenuCardPart(bool locked, string childName)
    {
        RectTransform content = CurrentManagementContent();
        if (content == null) return null;
        PrepareLayout(content);
        foreach (ManagementComputerCatalogCardUI card in content.GetComponentsInChildren<ManagementComputerCatalogCardUI>(false))
        {
            Recipe product = card.BoundProduct;
            if (product == null || product.IsUnlocked == locked ||
                !IsCurrentVisibleTarget(card.transform as RectTransform, content)) continue;
            if (string.IsNullOrEmpty(childName))
            {
                Button button = card.GetComponentInChildren<Button>(false);
                return button != null ? button.transform as RectTransform : card.transform as RectTransform;
            }
            return FindDescendant(card.transform, childName) ?? card.transform as RectTransform;
        }
        return null;
    }

    private static RectTransform ResolveMenuProgressionStatus()
    {
        // Prefer a real locked card when normal progression has one. Tutorial Day
        // deliberately makes every Casual Dining product usable, so its legitimate
        // fallback is the same real status band on an available product.
        return ResolveMenuCardPart(true, "StatusBand") ??
               ResolveMenuCardPart(false, "StatusBand");
    }

    private static RectTransform ResolveRestockCardPart(RestockStorageType storage, string childName)
    {
        RectTransform content = CurrentManagementContent();
        if (content == null) return null;
        PrepareLayout(content);
        foreach (ManagementComputerCatalogCardUI card in content.GetComponentsInChildren<ManagementComputerCatalogCardUI>(false))
        {
            if (card.BoundItem == null || card.BoundItem.requiredStorage != storage ||
                !IsCurrentVisibleTarget(card.transform as RectTransform, content)) continue;
            if (childName == "Plus" && card.PlusButton != null)
            {
                if (!card.PlusButton.interactable) continue;
                return card.PlusButton.transform as RectTransform;
            }
            return FindDescendant(card.transform, childName) ?? card.transform as RectTransform;
        }
        return null;
    }

    private static RectTransform ResolveSecondRestockPlus()
    {
        RectTransform content = CurrentManagementContent();
        if (content == null) return null;
        PrepareLayout(content);
        ManagementComputerCatalogCardUI firstDry = null;
        ManagementComputerCatalogCardUI fallback = null;
        foreach (ManagementComputerCatalogCardUI card in
                 content.GetComponentsInChildren<ManagementComputerCatalogCardUI>(false))
        {
            if (card.BoundItem == null || card.PlusButton == null || !card.PlusButton.interactable ||
                !IsCurrentVisibleTarget(card.transform as RectTransform, content)) continue;
            if (card.BoundItem.requiredStorage == RestockStorageType.Frozen)
                return card.PlusButton.transform as RectTransform;
            if (firstDry == null) firstDry = card;
            else if (fallback == null) fallback = card;
        }
        // Day 1 Casual Dining has no unlocked Frozen ingredient. Keep the real flow
        // playable by using a second unlocked ingredient; later days still prefer Frozen.
        return fallback != null ? fallback.PlusButton.transform as RectTransform : null;
    }

    private static RectTransform CurrentManagementContent()
    {
        ManagementComputerController computer = FindManagementComputer();
        return computer != null && computer.IsOpen && computer.AppWindow != null &&
               computer.AppWindow.gameObject.activeInHierarchy
            ? computer.AppWindow.Content : null;
    }

    private static void PrepareLayout(RectTransform content)
    {
        Canvas.ForceUpdateCanvases();
        if (content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
    }

    private static bool IsCurrentVisibleTarget(RectTransform candidate, RectTransform content)
    {
        if (candidate == null || content == null || !candidate.gameObject.activeInHierarchy ||
            !candidate.IsChildOf(content) || candidate.rect.width <= .5f || candidate.rect.height <= .5f)
            return false;
        for (Transform current = candidate; current != null && current != content.parent; current = current.parent)
        {
            CanvasGroup group = current.GetComponent<CanvasGroup>();
            if (group != null && group.alpha <= .01f) return false;
        }
        return true;
    }

    private static bool IsCurrentAnimatedCardTarget(RectTransform candidate, RectTransform content)
    {
        // Runtime Staff cards begin at alpha zero for their short reveal animation.
        // Active hierarchy + ownership by the current AppWindow content identifies
        // the live card without mistaking that transient alpha for an inactive copy.
        return candidate != null && content != null && candidate.gameObject.activeInHierarchy &&
               candidate.IsChildOf(content) && candidate.rect.width > .5f && candidate.rect.height > .5f;
    }

    private static RectTransform FindDescendant(Transform root, params string[] names)
    {
        if (root == null || names == null) return null;
        foreach (RectTransform candidate in root.GetComponentsInChildren<RectTransform>(false))
            foreach (string name in names)
                if (string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase))
                    return candidate;
        return null;
    }

    private static RectTransform ResolveProgressTarget(string rowName, string childPath = null)
    {
        // The real combined HUD is persistent and normally only visible in Lobby1.
        CasualDiningProgressHUD hud = CasualDiningProgressHUD.Instance;
        if (hud == null)
            return null;

        foreach (RectTransform rect in hud.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect.name != rowName || !rect.gameObject.activeInHierarchy)
                continue;

            return childPath == null ? rect : rect.Find(childPath) as RectTransform;
        }

        return null;
    }

    public void BeginUIFocus(RectTransform target)
    {
        EndUIFocus();
        if (target == null)
            return;

        // Card reveal animations own their text alpha. Restore the current real card
        // before measuring it; do not force child containers active or rebuild cards.
        target.GetComponentInParent<ManagementComputerCatalogCardUI>()
            ?.RestoreExpectedVisualState();

        // Only force-reveal the persistent HUD branches that normally exclude
        // Lobby1Tutorial. Modal windows such as Newspaper / Management / Restock must
        // still be opened by their real gameplay buttons and are never force-opened here.
        revealedLobbyHUD = target.GetComponentInParent<LobbyHUDRedesign>(true);
        if (target.GetComponentInParent<CasualDiningProgressHUD>(true) == null &&
            revealedLobbyHUD == null)
            return;

        // LobbyHUDRedesign hides lobby-only controls every Update outside Lobby1.
        // Pause that presentation refresh while its real target is focused so the
        // button remains active through EventSystem.Update and can receive a click.
        if (revealedLobbyHUD != null)
        {
            previousLobbyHUDEnabled = revealedLobbyHUD.enabled;
            revealedLobbyHUD.enabled = false;
        }
        revealedTaskHUD = target.GetComponentInParent<PlayerTaskHUD>(true);
        if (revealedTaskHUD != null)
        {
            previousTaskHUDEnabled = revealedTaskHUD.enabled;
            revealedTaskHUD.enabled = false;
        }

        revealedCanvas = target.GetComponentInParent<Canvas>(true);
        if (revealedCanvas != null)
            previousCanvasEnabled = revealedCanvas.enabled;

        for (Transform t = target; t != null; t = t.parent)
        {
            objects.Add((t.gameObject, t.gameObject.activeSelf));

            CanvasGroup group = t.GetComponent<CanvasGroup>();
            if (group != null)
                groups.Add((group, group.alpha, group.interactable, group.blocksRaycasts));

            // Lobby1Tutorial can keep a scene-level HUD holder disabled above the
            // Canvas. Capture and reveal the complete ancestor chain so the real
            // target is active and clickable, then EndUIFocus restores every state.
        }

        revealedButton = target.GetComponent<Button>() ?? target.GetComponentInChildren<Button>(true);
        if (revealedButton != null)
            previousButtonInteractable = revealedButton.interactable;

        LateUpdate();
    }

    private void LateUpdate()
    {
        // Presentation override only. Shared HUD still reads real gameplay data.
        // Reveal after its own Update may hide unsupported scenes.
        if (revealedCanvas != null)
            revealedCanvas.enabled = true;

        foreach (var state in objects)
            if (state.obj != null)
                state.obj.SetActive(true);

        foreach (var state in groups)
            if (state.group != null)
            {
                state.group.alpha = 1f;
                state.group.interactable = true;
                state.group.blocksRaycasts = true;
            }

        if (revealedButton != null)
            revealedButton.interactable = true;
    }

    public void EndUIFocus()
    {
        if (revealedLobbyHUD != null)
            revealedLobbyHUD.enabled = previousLobbyHUDEnabled;
        if (revealedTaskHUD != null)
            revealedTaskHUD.enabled = previousTaskHUDEnabled;

        if (revealedCanvas != null)
            revealedCanvas.enabled = previousCanvasEnabled;

        foreach (var state in groups)
            if (state.group != null)
            {
                state.group.alpha = state.alpha;
                state.group.interactable = state.interactable;
                state.group.blocksRaycasts = state.blocks;
            }

        foreach (var state in objects)
            if (state.obj != null)
                state.obj.SetActive(state.active);

        if (revealedButton != null)
            revealedButton.interactable = previousButtonInteractable;

        revealedCanvas = null;
        revealedLobbyHUD = null;
        revealedTaskHUD = null;
        revealedButton = null;
        groups.Clear();
        objects.Clear();
    }

    private void OnDisable() => EndUIFocus();
}
