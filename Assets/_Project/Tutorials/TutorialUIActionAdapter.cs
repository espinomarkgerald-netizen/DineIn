using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Observes real UI clicks and requires their real gameplay/UI state change.</summary>
[DisallowMultipleComponent]
public sealed class TutorialUIActionAdapter : MonoBehaviour
{
    public static EmployeeData LastHiredEmployee { get; private set; }
    public static Recipe PriceEditedRecipe { get; private set; }
    public static int EditedMenuPrice { get; private set; } = -1;

    private TutorialSystem tutorial;
    private TutorialSystem.TutorialStep step;
    private Button button;
    private TMP_InputField input;
    private TutorialUIActionClickRelay clickRelay;
    private bool clicked;
    private int clickedFrame;
    private EmployeeData employee;
    private Recipe recipe;
    private ItemData item;
    private bool originalEmployeeHired;
    private bool originalAvailability;
    private int originalPrice;
    private int originalOrderCount;
    private int expectedSavedPrice = -1;
    private int lastLoggedSavedPrice = int.MinValue;
    private bool saveClickLogged;

    /// <returns>True when the required real state was already satisfied.</returns>
    public bool Begin(TutorialSystem owner, RectTransform target)
    {
        StopWaiting();
        if (owner == null || target == null || string.IsNullOrEmpty(owner.CurrentStep?.ActionKey))
            return false;

        tutorial = owner;
        step = owner.CurrentStep;
        CaptureRealState(target);
        if (string.Equals(step.ActionKey, "Management.MenuSavePrice", StringComparison.Ordinal))
        {
            Debug.Log($"[TutorialMenu] Step {owner.CurrentStepIndex} waiting for: " +
                      $"id={step.Id}, type={step.StepType}, requiredAction={step.RequiredAction}, " +
                      $"actionKey={step.ActionKey}, context={(step.RequiredContext != null ? step.RequiredContext.name : "<none>")}, " +
                      $"uiTargetKey={step.UITargetKey}", this);
            Debug.Log($"[TutorialMenu] Recipe: {(recipe != null ? recipe.DisplayName : "<null>")}; " +
                      $"Original price: {originalPrice}; Typed price: {expectedSavedPrice}; " +
                      "observer=TutorialSystem/TutorialUIActionAdapter", this);
        }
        if (string.Equals(step.ActionKey, "Management.StaffSetActive", StringComparison.Ordinal) &&
            employee != null && employee.assigned)
        {
            TutorialSystem completedOwner = tutorial;
            string completedKey = step.ActionKey;
            StopWaiting();
            completedOwner.NotifyAction(completedKey);
            return true;
        }

        if (string.Equals(step.ActionKey, "Management.MenuPriceFocus", StringComparison.Ordinal))
        {
            if (input == null)
            {
                Debug.LogError("[Tutorial] Price focus target has no real TMP_InputField.", target);
                return false;
            }
            input.onSelect.AddListener(OnInputSelected);
            if (IsInputSelected()) MarkObserved();
            return false;
        }

        if (string.Equals(step.ActionKey, "Management.MenuPriceChanged", StringComparison.Ordinal))
        {
            if (input == null || recipe == null)
            {
                Debug.LogError("[Tutorial] Price typing target is missing its real input or selected recipe.", target);
                return false;
            }
            input.onValueChanged.AddListener(OnInputValueChanged);
            if (TryReadChangedPrice(out _)) MarkObserved();
            return false;
        }

        // Physical Restock steps are verified by TutorialRestockFlowBridge against
        // the authoritative order/room state. Some targets use hold or drag input
        // rather than a Unity UI Button, so no synthetic click listener belongs here.
        if (step.ActionKey.StartsWith("Restock.", StringComparison.Ordinal))
            return false;

        button = string.Equals(step.ActionKey, "Management.MenuSavePrice", StringComparison.Ordinal)
            ? FindButtonByLabel(target, "SAVE")
            : string.Equals(step.ActionKey, "Management.StaffSetActive", StringComparison.Ordinal)
                ? FindButtonByLabel(target, "SET ACTIVE")
                : target.GetComponent<Button>() ?? target.GetComponentInChildren<Button>(false);
        if (button == null)
        {
            Debug.LogError("[Tutorial] Action target has no real Button: " + step.Id, target);
            return false;
        }
        button.onClick.AddListener(OnClicked);
        clickRelay = button.GetComponent<TutorialUIActionClickRelay>();
        if (clickRelay == null) clickRelay = button.gameObject.AddComponent<TutorialUIActionClickRelay>();
        clickRelay.Begin(OnClicked);
        return false;
    }

    private void CaptureRealState(RectTransform target)
    {
        ManagementEmployeeCardUI employeeCard = target.GetComponentInParent<ManagementEmployeeCardUI>();
        employee = employeeCard != null ? employeeCard.Employee : null;
        originalEmployeeHired = employee != null && employee.hired;

        ManagementComputerCatalogPanelUI catalogPanel =
            target.GetComponentInParent<ManagementComputerCatalogPanelUI>();
        ManagementComputerCatalogCardUI catalogCard =
            target.GetComponentInParent<ManagementComputerCatalogCardUI>();
        input = target.GetComponent<TMP_InputField>() ?? target.GetComponentInChildren<TMP_InputField>(false);
        if (input == null && catalogPanel != null)
            input = catalogPanel.GetComponentInChildren<TMP_InputField>(false);
        recipe = catalogCard != null ? catalogCard.BoundProduct : GetSelectedRecipe(target);
        if (string.Equals(step?.ActionKey, "Management.MenuSavePrice", StringComparison.Ordinal) &&
            PriceEditedRecipe != null)
            recipe = PriceEditedRecipe;
        item = catalogCard != null ? catalogCard.BoundItem : null;
        originalAvailability = recipe != null && MenuAvailabilityManager.IsProductAvailable(recipe);
        originalPrice = recipe != null ? recipe.EffectiveSellPrice : -1;
        if (string.Equals(step?.ActionKey, "Management.MenuSavePrice", StringComparison.Ordinal))
        {
            expectedSavedPrice = EditedMenuPrice;
            if (input != null && int.TryParse(input.text, out int finalTypedPrice) && finalTypedPrice >= 0)
            {
                expectedSavedPrice = finalTypedPrice;
                // Step 56 may have observed the first digit of a multi-digit edit.
                // Step 58 owns the final value that the real SAVE callback will read.
                EditedMenuPrice = finalTypedPrice;
            }
        }
        originalOrderCount = RestockOrderManager.Instance != null
            ? RestockOrderManager.Instance.Orders.Count : 0;
    }

    private void OnClicked()
    {
        if (tutorial == null || tutorial.CurrentStep != step || !tutorial.IsWaitingForGameplayAction)
            return;
        if (!saveClickLogged && string.Equals(step.ActionKey, "Management.MenuSavePrice", StringComparison.Ordinal))
        {
            saveClickLogged = true;
            Debug.Log("[TutorialMenu] Save pointer/click detected.", this);
        }
        MarkObserved();
    }

    private void OnInputSelected(string _) => MarkObserved();

    private void OnInputValueChanged(string _) => MarkObserved();

    private void MarkObserved()
    {
        clicked = true;
        clickedFrame = Time.frameCount;
    }

    private void LateUpdate()
    {
        bool menuSave = step != null && string.Equals(
            step.ActionKey, "Management.MenuSavePrice", StringComparison.Ordinal);
        if (menuSave && recipe != null && recipe.EffectiveSellPrice != lastLoggedSavedPrice)
        {
            lastLoggedSavedPrice = recipe.EffectiveSellPrice;
            Debug.Log($"[TutorialMenu] Saved recipe price after UI rebuild: {lastLoggedSavedPrice}; " +
                      $"expected final typed price: {expectedSavedPrice}; " +
                      $"condition {(lastLoggedSavedPrice == expectedSavedPrice && lastLoggedSavedPrice != originalPrice ? "matched" : "did not match")}", this);
        }
        bool stateDrivenAction = step != null &&
            (menuSave ||
             step.ActionKey.StartsWith("Shift.", StringComparison.Ordinal));
        if ((!clicked && !stateDrivenAction) ||
            (clicked && Time.frameCount <= clickedFrame) || tutorial == null ||
            tutorial.CurrentStep != step || !tutorial.IsWaitingForGameplayAction)
            return;
        if (!IsRealActionComplete(step.ActionKey)) return;
        TutorialSystem owner = tutorial;
        string key = step.ActionKey;
        if (string.Equals(key, "Management.StaffHire", StringComparison.Ordinal))
            LastHiredEmployee = employee;
        else if (string.Equals(key, "Management.MenuPriceChanged", StringComparison.Ordinal) &&
                 TryReadChangedPrice(out int editedPrice))
        {
            PriceEditedRecipe = recipe;
            EditedMenuPrice = editedPrice;
        }
        if (menuSave)
            Debug.Log($"[TutorialMenu] NotifyAction: {key}", this);
        bool matched = owner.NotifyAction(key);
        if (menuSave)
            Debug.Log($"[TutorialMenu] Step condition {(matched ? "matched" : "did not match")}; " +
                      $"current index is now {owner.CurrentStepIndex}.", this);
    }

    private bool IsRealActionComplete(string actionKey)
    {
        switch (actionKey)
        {
            case "Newspaper.Open":
                return FindFirstObjectByType<DailyNewspaperPresenter>()?.IsOpen == true;
            case "Newspaper.Close":
                return FindFirstObjectByType<DailyNewspaperPresenter>()?.IsOpen == false;
            case "Computer.Open":
                return FindManagementComputer()?.IsOpen == true;
            case "Management.CloseAfterRestock":
                return FindManagementComputer()?.IsOpen == false;
            case "Shift.OpenChecklist":
            {
                ManagementComputerController computer = FindManagementComputer();
                return computer?.AppWindow != null &&
                       computer.AppWindow.gameObject.activeInHierarchy &&
                       computer.AppWindow.FooterButton != null &&
                       computer.AppWindow.FooterButton.gameObject.activeInHierarchy;
            }
            case "Shift.OpenDayIntro":
            {
                RectTransform play = null;
                foreach (RectTransform candidate in FindObjectsByType<RectTransform>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (candidate.name == "PlayButton")
                    {
                        play = candidate;
                        break;
                    }
                return FindManagementComputer()?.IsOpen == false &&
                       play != null && play.gameObject.activeInHierarchy;
            }
            case "Shift.Start":
                return GameDayManager.Instance != null && GameDayManager.Instance.ShiftRunning;

            case "Management.Dashboard":
            case "Management.Staff":
            case "Management.Menu":
            case "Management.Equipment":
            case "Management.Finance":
            case "Management.Objectives":
            case "Management.Restock":
                return FindManagementComputer()?.IsOpen == true &&
                       button != null && button.gameObject.activeInHierarchy;

            case "Management.StaffApplicants":
                return ActiveHRPanel()?.CurrentView == ManagementHRView.Applicants;
            case "Management.StaffHire":
                return employee != null && !originalEmployeeHired && employee.hired;
            case "Management.StaffLobby":
                return ActiveHRPanel()?.CurrentView == ManagementHRView.Lobby;
            case "Management.StaffSetActive":
                return employee != null && employee.hired && employee.assigned;

            case "Management.MenuFood":
                return VisibleCatalogCardsAre(MenuProductCategory.Food);
            case "Management.MenuSelect":
                return recipe != null && GetSelectedRecipe() == recipe;
            case "Management.MenuPriceFocus":
                return IsInputSelected();
            case "Management.MenuPriceChanged":
                return TryReadChangedPrice(out _);
            case "Management.MenuSavePrice":
                return recipe != null && recipe == PriceEditedRecipe && expectedSavedPrice >= 0 &&
                       recipe.EffectiveSellPrice == expectedSavedPrice &&
                       recipe.EffectiveSellPrice != originalPrice;
            case "Management.MenuAvailability":
                return recipe != null &&
                       MenuAvailabilityManager.IsProductAvailable(recipe) != originalAvailability;

            case "Management.RestockAddDry":
            case "Management.RestockAddCold":
                return item != null && CartShowsItem(item);
            case "Management.RestockCheckout":
                return ActiveButtonWithLabel("ORDER NOW") != null;
            case "Management.RestockOrder":
                return RestockOrderManager.Instance != null &&
                       RestockOrderManager.Instance.Orders.Count > originalOrderCount;

            case "Camera.Focus":
                return ManagerPlayer.Active != null;
            case "Task.Open":
                return PlayerTaskGuidance.Current.Source == "Lobby1Tutorial";
            default:
                return false;
        }
    }

    private static ManagementComputerHRPanel ActiveHRPanel()
    {
        foreach (ManagementComputerHRPanel panel in
                 FindObjectsByType<ManagementComputerHRPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (panel.gameObject.activeInHierarchy) return panel;
        return null;
    }

    private static Recipe GetSelectedRecipe(RectTransform target = null)
    {
        ManagementComputerCatalogPanelUI panel = target != null
            ? target.GetComponentInParent<ManagementComputerCatalogPanelUI>()
            : null;
        if (panel == null)
            foreach (ManagementComputerCatalogPanelUI candidate in
                     FindObjectsByType<ManagementComputerCatalogPanelUI>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.gameObject.activeInHierarchy)
                {
                    panel = candidate;
                    break;
                }
        if (panel == null || !panel.gameObject.activeInHierarchy) return null;
        TMP_Text[] labels = panel.GetComponentsInChildren<TMP_Text>(false);
        foreach (ManagementComputerCatalogCardUI card in
                 panel.GetComponentsInChildren<ManagementComputerCatalogCardUI>(false))
        {
            if (card.BoundProduct == null) continue;
            foreach (TMP_Text label in labels)
                if (label.transform.GetComponentInParent<ManagementComputerCatalogCardUI>() == null &&
                    string.Equals(label.text?.Trim(), card.BoundProduct.DisplayName,
                        StringComparison.OrdinalIgnoreCase))
                    return card.BoundProduct;
        }
        return null;
    }

    private static bool VisibleCatalogCardsAre(MenuProductCategory category)
    {
        ManagementComputerCatalogPanelUI panel =
            FindFirstObjectByType<ManagementComputerCatalogPanelUI>(FindObjectsInactive.Include);
        if (panel == null || !panel.gameObject.activeInHierarchy) return false;
        bool found = false;
        foreach (ManagementComputerCatalogCardUI card in
                 panel.GetComponentsInChildren<ManagementComputerCatalogCardUI>(false))
        {
            if (!card.gameObject.activeInHierarchy || card.BoundProduct == null) continue;
            found = true;
            if (card.BoundProduct.category != category) return false;
        }
        return found;
    }

    private static bool CartShowsItem(ItemData expected)
    {
        ManagementComputerCatalogPanelUI panel =
            FindFirstObjectByType<ManagementComputerCatalogPanelUI>(FindObjectsInactive.Include);
        if (panel == null || !panel.gameObject.activeInHierarchy) return false;
        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(false))
            if (text.GetComponentInParent<ManagementComputerCatalogCardUI>() == null &&
                text.text.IndexOf(expected.displayName, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    private static Button ActiveButtonWithLabel(string wanted)
    {
        ManagementComputerController computer = FindManagementComputer();
        if (computer?.AppWindow?.Content == null) return null;
        foreach (Button candidate in computer.AppWindow.Content.GetComponentsInChildren<Button>(false))
        {
            foreach (TMP_Text label in candidate.GetComponentsInChildren<TMP_Text>(false))
                if (label.text.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
        }
        return null;
    }

    private static Button FindButtonByLabel(RectTransform root, string wanted)
    {
        if (root == null) return null;
        foreach (Button candidate in root.GetComponentsInChildren<Button>(false))
            foreach (TMP_Text label in candidate.GetComponentsInChildren<TMP_Text>(false))
                if (label.text.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
        return null;
    }

    private static ManagementComputerController FindManagementComputer() =>
        FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include);

    private bool IsInputSelected()
    {
        if (input == null || !input.isFocused)
            return false;
        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject : null;
        return selected == null || selected == input.gameObject || selected.transform.IsChildOf(input.transform);
    }

    private bool TryReadChangedPrice(out int price)
    {
        price = -1;
        return input != null && recipe != null && int.TryParse(input.text, out price) &&
               price >= 0 && price != originalPrice;
    }

    public static void ClearSessionState()
    {
        LastHiredEmployee = null;
        PriceEditedRecipe = null;
        EditedMenuPrice = -1;
    }

    public void StopWaiting()
    {
        if (button != null) button.onClick.RemoveListener(OnClicked);
        if (clickRelay != null) clickRelay.End(OnClicked);
        if (input != null)
        {
            input.onSelect.RemoveListener(OnInputSelected);
            input.onValueChanged.RemoveListener(OnInputValueChanged);
        }
        button = null;
        clickRelay = null;
        input = null;
        clicked = false;
        tutorial = null;
        step = null;
        employee = null;
        recipe = null;
        item = null;
        expectedSavedPrice = -1;
        lastLoggedSavedPrice = int.MinValue;
        saveClickLogged = false;
    }

    private void OnDisable() => StopWaiting();
}

/// <summary>
/// Pointer/submit fallback for controls whose gameplay callback refreshes its
/// Button.onClick list while that same click is being dispatched.
/// </summary>
[DisallowMultipleComponent]
internal sealed class TutorialUIActionClickRelay : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    private Action observed;

    public void Begin(Action callback) => observed = callback;

    public void End(Action callback)
    {
        if (observed == callback) observed = null;
    }

    public void OnPointerClick(PointerEventData eventData) => observed?.Invoke();

    public void OnSubmit(BaseEventData eventData) => observed?.Invoke();
}
