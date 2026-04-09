using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the OfficeTutorial scene through a single-day tutorial flow:
/// Intro → HRButton → EmployeeBoard (gated close) → RestockButton →
/// RestockShop (gated close) → EquipmentButton → EquipmentShop (gated close) →
/// RecipeButton → RecipeBook → Complete → load KitchenTutorial.
/// </summary>
public class OfficeTutorialManager : MonoBehaviour
{
    public static OfficeTutorialManager Instance { get; private set; }

    // ─── Phase definition ────────────────────────────────────────────────────

    public enum OfficeTutorialPhase
    {
        None,
        Intro,
        HRButton,
        EmployeeBoard,
        RestockButton,
        RestockShop,
        EquipmentButton,
        EquipmentShop,
        RecipeButton,
        RecipeBook,
        Complete
    }

    // ─── Serialized references ───────────────────────────────────────────────

    [Header("Tutorial Helpers")]
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private OfficeTutorialArrowDriver arrowDriver;

    [Header("HUD Buttons (open-panel triggers)")]
    [SerializeField] private Button hrButton;
    [SerializeField] private Button restockButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private Button recipeButton;

    [Header("Panels")]
    [SerializeField] private GameObject employeeBoardPanel;
    [SerializeField] private GameObject restockShopPanel;
    [SerializeField] private GameObject equipmentShopPanel;
    [SerializeField] private GameObject recipeBookPanel;

    [Header("Close Buttons (one per panel)")]
    [SerializeField] private Button employeeBoardCloseButton;
    [SerializeField] private Button restockShopCloseButton;
    [SerializeField] private Button equipmentShopCloseButton;
    [SerializeField] private Button recipeBookCloseButton;

    [Header("Highlight Overlays (optional child GameObjects on each button)")]
    [SerializeField] private GameObject hrButtonHighlight;
    [SerializeField] private GameObject restockButtonHighlight;
    [SerializeField] private GameObject equipmentButtonHighlight;
    [SerializeField] private GameObject recipeButtonHighlight;

    [Header("Completion UI")]
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private Button finishButton;

    [Header("HR – roles that must be assigned before closing EmployeeBoardLobby")]
    [Tooltip("Only include the roles shown in EmployeeBoardLobby (Host, Waiter, Cashier, Busser). " +
             "Chef and Barista live in EmployeeBoardKitchen and must NOT be listed here.")]
    [SerializeField] private List<EmployeeRole> requiredLobbyRoles = new List<EmployeeRole>
    {
        EmployeeRole.Host,
        EmployeeRole.Waiter,
        EmployeeRole.Cashier,
        EmployeeRole.Busser
    };

    [Header("Restock requirements (same list as OfficeStartButtons)")]
    [SerializeField] private List<InventoryEntry> kitchenRequirements;

    [Header("Scene transition")]
    [SerializeField] private string kitchenTutorialSceneName = "KitchenTutorial";

    [Header("Dialogue – Speaker label")]
    [SerializeField] private string speakerName = "Manager";

    [Header("Dialogue – Messages")]
    [SerializeField] [TextArea(2, 5)]
    private string introMessage = "Welcome to the Office! Before you start the day, you need to set everything up. I'll guide you through each section.";

    [SerializeField] [TextArea(2, 5)]
    private string hrArrowMessage = "First, tap the HR button to manage your employees.";

    [SerializeField] [TextArea(2, 5)]
    private string hrBoardMessage = "Assign at least one employee to each role. Close the board when all roles are filled.";

    [SerializeField] [TextArea(2, 5)]
    private string restockArrowMessage = "Good! Now tap the Restock button to buy ingredients for today.";

    [SerializeField] [TextArea(2, 5)]
    private string restockShopMessage = "Buy enough stock for all the ingredients your recipes need. Close the shop when everything is stocked.";

    [SerializeField] [TextArea(2, 5)]
    private string equipmentArrowMessage = "Next, tap the Equipment button to purchase kitchen equipment.";

    [SerializeField] [TextArea(2, 5)]
    private string equipmentShopMessage = "Buy at least one piece of equipment to upgrade your kitchen. Close the shop when you're done.";

    [SerializeField] [TextArea(2, 5)]
    private string recipeArrowMessage = "Finally, check out the Recipe Book to see what dishes you can prepare today.";

    [SerializeField] [TextArea(2, 5)]
    private string recipeBookMessage = "These are all the recipes available today. Study them well! Close when you're ready.";

    [SerializeField] [TextArea(2, 5)]
    private string completionMessage = "Great work! The office is ready. Time to move to the Kitchen tutorial!";

    // ─── PlayerPrefs key ─────────────────────────────────────────────────────

    private const string OfficeTutorialDoneKey = "DineIn_OfficeTutorial_Done";

    // ─── Runtime state ───────────────────────────────────────────────────────

    [Header("Runtime")]
    [SerializeField] private OfficeTutorialPhase currentPhase = OfficeTutorialPhase.None;

    private bool equipmentPurchasedThisSession;

    // ─── Unity lifecycle ─────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResolveReferences();
        HideAllHighlights();

        if (completionPanel != null)
            completionPanel.SetActive(false);
    }

    private void Start()
    {
        WireCloseButtonListeners();
        WireFinishButton();
        WireEquipmentPurchaseTracking();

        BeginTutorial();
    }

    // ─── Initialisation helpers ──────────────────────────────────────────────

    private void ResolveReferences()
    {
        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<TutorialDialogueUI>(FindObjectsInactive.Include);

        if (arrowDriver == null)
            arrowDriver = FindFirstObjectByType<OfficeTutorialArrowDriver>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Adds tutorial gate listeners on top of the UIManager's existing persistent
    /// onClick listeners. We never call RemoveAllListeners here because that would
    /// strip the UIManager's close logic and the panels would never actually hide.
    /// Gating is enforced by toggling button.interactable, not by replacing listeners.
    /// </summary>
    private void WireCloseButtonListeners()
    {
        if (employeeBoardCloseButton != null)
            employeeBoardCloseButton.onClick.AddListener(OnEmployeeBoardCloseTapped);

        if (restockShopCloseButton != null)
            restockShopCloseButton.onClick.AddListener(OnRestockShopCloseTapped);

        if (equipmentShopCloseButton != null)
            equipmentShopCloseButton.onClick.AddListener(OnEquipmentShopCloseTapped);

        if (recipeBookCloseButton != null)
            recipeBookCloseButton.onClick.AddListener(OnRecipeBookCloseTapped);
    }

    private void WireFinishButton()
    {
        if (finishButton != null)
        {
            finishButton.onClick.RemoveAllListeners();
            finishButton.onClick.AddListener(LoadKitchenTutorial);
        }
    }

    /// <summary>
    /// Listens on every EquipmentItemUI buy button in the scene so we detect
    /// the first purchase attempt during the tutorial, regardless of which item.
    /// </summary>
    private void WireEquipmentPurchaseTracking()
    {
        EquipmentManager equipManager = EquipmentManager.Instance;
        if (equipManager == null)
            return;

        // Patch into EquipmentItemUI buy buttons that are already spawned.
        RefreshEquipmentBuyButtonListeners();
    }

    /// <summary>
    /// Called after the shop rebuilds (or when entering the EquipmentShop phase)
    /// to add our purchase-detection listener to every visible buy button.
    /// </summary>
    public void RefreshEquipmentBuyButtonListeners()
    {
        EquipmentItemUI[] items = FindObjectsByType<EquipmentItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EquipmentItemUI item in items)
        {
            if (item.buyButton == null)
                continue;

            item.buyButton.onClick.RemoveListener(OnEquipmentBuyTapped);
            item.buyButton.onClick.AddListener(OnEquipmentBuyTapped);
        }
    }

    // ─── Tutorial flow ───────────────────────────────────────────────────────

    private void BeginTutorial()
    {
        SetPhase(OfficeTutorialPhase.Intro);
    }

    /// <summary>Central phase-change entry point, mirrors TutorialManager.SetPhase.</summary>
    public void SetPhase(OfficeTutorialPhase newPhase)
    {
        if (currentPhase == newPhase)
            return;

        currentPhase = newPhase;
        HideAllHighlights();

        if (arrowDriver != null)
            arrowDriver.OnPhaseEntered(newPhase);

        switch (newPhase)
        {
            case OfficeTutorialPhase.Intro:
                ShowIntro();
                break;

            case OfficeTutorialPhase.HRButton:
                ShowHRButtonPhase();
                break;

            case OfficeTutorialPhase.EmployeeBoard:
                ShowEmployeeBoardPhase();
                break;

            case OfficeTutorialPhase.RestockButton:
                ShowRestockButtonPhase();
                break;

            case OfficeTutorialPhase.RestockShop:
                ShowRestockShopPhase();
                break;

            case OfficeTutorialPhase.EquipmentButton:
                ShowEquipmentButtonPhase();
                break;

            case OfficeTutorialPhase.EquipmentShop:
                ShowEquipmentShopPhase();
                break;

            case OfficeTutorialPhase.RecipeButton:
                ShowRecipeButtonPhase();
                break;

            case OfficeTutorialPhase.RecipeBook:
                ShowRecipeBookPhase();
                break;

            case OfficeTutorialPhase.Complete:
                ShowCompletion();
                break;
        }
    }

    // ── Phase handlers ──────────────────────────────────────────────────────

    private void ShowIntro()
    {
        ShowManualDialogue(introMessage, () => SetPhase(OfficeTutorialPhase.HRButton));
    }

    private void ShowHRButtonPhase()
    {
        SetHighlight(hrButtonHighlight, true);
        ShowManualDialogue(hrArrowMessage, null);

        // Advance when the player taps HRButton.
        WireOpenButtonOnce(hrButton, () => SetPhase(OfficeTutorialPhase.EmployeeBoard));
    }

    private void ShowEmployeeBoardPhase()
    {
        ShowAutoDialogue(hrBoardMessage, 4f);
        SetCloseButtonInteractable(employeeBoardCloseButton, false);
        StartCoroutine(PollEmployeeCondition());
    }

    private void ShowRestockButtonPhase()
    {
        SetHighlight(restockButtonHighlight, true);
        ShowManualDialogue(restockArrowMessage, null);

        WireOpenButtonOnce(restockButton, () => SetPhase(OfficeTutorialPhase.RestockShop));
    }

    private void ShowRestockShopPhase()
    {
        ShowAutoDialogue(restockShopMessage, 4f);
        SetCloseButtonInteractable(restockShopCloseButton, false);
        StartCoroutine(PollRestockCondition());
    }

    private void ShowEquipmentButtonPhase()
    {
        SetHighlight(equipmentButtonHighlight, true);
        ShowManualDialogue(equipmentArrowMessage, null);

        WireOpenButtonOnce(equipmentButton, () =>
        {
            RefreshEquipmentBuyButtonListeners();
            SetPhase(OfficeTutorialPhase.EquipmentShop);
        });
    }

    private void ShowEquipmentShopPhase()
    {
        equipmentPurchasedThisSession = false;
        ShowAutoDialogue(equipmentShopMessage, 4f);
        SetCloseButtonInteractable(equipmentShopCloseButton, false);
    }

    private void ShowRecipeButtonPhase()
    {
        SetHighlight(recipeButtonHighlight, true);
        ShowManualDialogue(recipeArrowMessage, null);

        WireOpenButtonOnce(recipeButton, () => SetPhase(OfficeTutorialPhase.RecipeBook));
    }

    private void ShowRecipeBookPhase()
    {
        ShowAutoDialogue(recipeBookMessage, 4f);
        // RecipeBook has no gate – close button is freely pressable.
        SetCloseButtonInteractable(recipeBookCloseButton, true);
    }

    private void ShowCompletion()
    {
        if (dialogueUI != null)
            dialogueUI.Hide();

        if (completionPanel != null)
        {
            completionPanel.SetActive(true);
        }
        else
        {
            ShowAutoDialogue(completionMessage, 3f);
            StartCoroutine(DelayedSceneLoad(3.5f));
        }

        MarkTutorialDone();
    }

    // ─── Close-button gating callbacks ───────────────────────────────────────

    // ─── Close-button gating callbacks ───────────────────────────────────────
    // These listeners sit on top of UIManager's own close logic.
    // The button is kept non-interactable until the condition is met, so by the
    // time any of these callbacks fire the gate has already passed. We only need
    // to advance the phase here — UIManager closes the panel on its own listener.

    private void OnEmployeeBoardCloseTapped()
    {
        SetPhase(OfficeTutorialPhase.RestockButton);
    }

    private void OnRestockShopCloseTapped()
    {
        SetPhase(OfficeTutorialPhase.EquipmentButton);
    }

    private void OnEquipmentShopCloseTapped()
    {
        SetPhase(OfficeTutorialPhase.RecipeButton);
    }

    private void OnRecipeBookCloseTapped()
    {
        SetPhase(OfficeTutorialPhase.Complete);
    }

    // ─── Equipment buy detection ──────────────────────────────────────────────

    private void OnEquipmentBuyTapped()
    {
        if (currentPhase != OfficeTutorialPhase.EquipmentShop)
            return;

        equipmentPurchasedThisSession = true;
        SetCloseButtonInteractable(equipmentShopCloseButton, true);
    }

    // ─── Condition checks ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when every role in <see cref="requiredLobbyRoles"/> has at least
    /// one employee assigned. Only covers EmployeeBoardLobby roles (Host, Waiter,
    /// Cashier, Busser). Chef and Barista are EmployeeBoardKitchen roles and are
    /// intentionally excluded so they don't block this gate.
    /// </summary>
    private bool AllRolesHaveAtLeastOneEmployee()
    {
        if (EmployeeManager.Instance == null)
        {
            Debug.LogWarning("[OfficeTutorial] EmployeeManager not found – skipping HR gate.");
            return true;
        }

        foreach (EmployeeRole role in requiredLobbyRoles)
        {
            RoleGroup group = EmployeeManager.Instance.employeesByRole.Find(g => g.role == role);
            if (group == null || group.employees == null || group.employees.Count == 0)
            {
                Debug.Log($"[OfficeTutorial] Role '{role}' has no assigned employee.");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns true when every ingredient required by at least one unlocked recipe
    /// meets its minimum stock threshold. Reuses the same logic as OfficeStartButtons.
    /// </summary>
    private bool AllRequiredIngredientsStocked()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[OfficeTutorial] InventoryManager not found – skipping restock gate.");
            return true;
        }

        foreach (InventoryEntry req in kitchenRequirements)
        {
            if (!IsRequiredByUnlockedRecipe(req.itemType))
                continue;

            int current = InventoryManager.Instance.GetStock(req.itemType);
            if (current < req.stock)
            {
                Debug.Log($"[OfficeTutorial] Not enough {req.itemType}: {current}/{req.stock}");
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns true if at least one unlocked recipe uses this ingredient.</summary>
    private bool IsRequiredByUnlockedRecipe(ItemType itemType)
    {
        if (RecipeManager.Instance == null)
            return true;

        foreach (var recipe in RecipeManager.Instance.AllRecipes)
        {
            if (!UnlockManager.Instance.IsRecipeUnlocked(recipe.recipeID))
                continue;

            foreach (var ingredient in recipe.ingredients)
            {
                if (ingredient.item != null && ingredient.item.itemType == itemType)
                    return true;
            }
        }

        return false;
    }

    // ─── Condition polling ────────────────────────────────────────────────────

    /// <summary>
    /// Polls every 0.5 s and unlocks the EmployeeBoard close button as soon as
    /// all required lobby roles have at least one employee assigned.
    /// </summary>
    private IEnumerator PollEmployeeCondition()
    {
        const float PollInterval = 0.5f;

        while (currentPhase == OfficeTutorialPhase.EmployeeBoard)
        {
            bool ready = AllRolesHaveAtLeastOneEmployee();
            SetCloseButtonInteractable(employeeBoardCloseButton, ready);
            yield return new WaitForSeconds(PollInterval);
        }
    }

    /// <summary>
    /// Polls every 0.5 s to unlock the restock close button as soon as all
    /// ingredients are stocked, giving immediate feedback without per-frame cost.
    /// </summary>
    private IEnumerator PollRestockCondition()
    {
        const float PollInterval = 0.5f;

        while (currentPhase == OfficeTutorialPhase.RestockShop)
        {
            bool ready = AllRequiredIngredientsStocked();
            SetCloseButtonInteractable(restockShopCloseButton, ready);
            yield return new WaitForSeconds(PollInterval);
        }
    }

    // ─── Utility ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Wires a one-shot listener onto an open-panel button so the tutorial
    /// can advance when the player taps it, without removing the original
    /// UIManager.Open*() persistent listener.
    /// </summary>
    private void WireOpenButtonOnce(Button button, System.Action callback)
    {
        if (button == null || callback == null)
            return;

        void Handler()
        {
            button.onClick.RemoveListener(Handler);
            callback();
        }

        button.onClick.AddListener(Handler);
    }

    private void SetCloseButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void HideAllHighlights()
    {
        SetHighlight(hrButtonHighlight, false);
        SetHighlight(restockButtonHighlight, false);
        SetHighlight(equipmentButtonHighlight, false);
        SetHighlight(recipeButtonHighlight, false);
    }

    private void SetHighlight(GameObject highlight, bool active)
    {
        if (highlight != null)
            highlight.SetActive(active);
    }

    private void ShowManualDialogue(string message, System.Action onNext)
    {
        if (dialogueUI == null)
        {
            onNext?.Invoke();
            return;
        }

        dialogueUI.ShowManual(speakerName, message, onNext);
    }

    private void ShowAutoDialogue(string message, float duration)
    {
        if (dialogueUI == null)
            return;

        dialogueUI.ShowAuto(speakerName, message, duration);
    }

    private void MarkTutorialDone()
    {
        PlayerPrefs.SetInt(OfficeTutorialDoneKey, 1);
        PlayerPrefs.Save();
    }

    private void LoadKitchenTutorial()
    {
        SceneManager.LoadScene(kitchenTutorialSceneName);
    }

    private IEnumerator DelayedSceneLoad(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadKitchenTutorial();
    }
    

    // ─── Public accessors ─────────────────────────────────────────────────────

    public OfficeTutorialPhase CurrentPhase => currentPhase;
}
