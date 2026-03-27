using System;
using UnityEngine;
using UnityEngine.UI;

public class TutorialInteractionLocker : MonoBehaviour
{
    [Serializable]
    public class PhaseLockSet
    {
        public TutorialManager.TutorialPhase phase;

        [Header("Role Lock")]
        public bool lockRole = true;
        public StaffRole.Role requiredRole = StaffRole.Role.Waiter;

        [Header("Scene Objects")]
        public GameObject[] enableObjects;
        public GameObject[] disableObjects;

        [Header("Extra UI Buttons")]
        public Button[] enableButtons;
        public Button[] disableButtons;

        [Header("World Colliders")]
        public Collider[] enableColliders;
        public Collider[] disableColliders;

        [Header("Canvas Groups")]
        public CanvasGroup[] showCanvasGroups;
        public CanvasGroup[] hideCanvasGroups;
    }

    [Header("References")]
    [SerializeField] private TutorialManager tutorialManager;

    [Header("Role Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button waiterButton;
    [SerializeField] private Button cashierButton;
    [SerializeField] private Button busserButton;

    [Header("Phase Locks")]
    [SerializeField] private PhaseLockSet[] phaseLocks;

    private TutorialManager.TutorialPhase lastAppliedPhase = TutorialManager.TutorialPhase.None;

    private void Awake()
    {
        if (tutorialManager == null)
            tutorialManager = GetComponent<TutorialManager>();

        if (tutorialManager == null)
            tutorialManager = TutorialManager.Instance;
    }

    private void Update()
    {
        if (tutorialManager == null || !tutorialManager.TutorialStarted)
            return;

        if (lastAppliedPhase == tutorialManager.CurrentPhase)
            return;

        ApplyPhaseLocks(tutorialManager.CurrentPhase);
        lastAppliedPhase = tutorialManager.CurrentPhase;
    }

    private void ApplyPhaseLocks(TutorialManager.TutorialPhase phase)
    {
        PhaseLockSet set = GetSetForPhase(phase);
        if (set == null)
            return;

        ResetRoleButtons();
        ResetExtraButtons();
        ResetSceneObjects();
        ResetColliders();
        ResetCanvasGroups();

        if (set.lockRole)
        {
            ForceRole(set.requiredRole);
            LockRoleButtons(set.requiredRole);
        }

        SetObjectsActive(set.enableObjects, true);
        SetObjectsActive(set.disableObjects, false);

        SetButtonsInteractable(set.enableButtons, true);
        SetButtonsInteractable(set.disableButtons, false);

        SetCollidersEnabled(set.enableColliders, true);
        SetCollidersEnabled(set.disableColliders, false);

        SetCanvasGroups(set.showCanvasGroups, true);
        SetCanvasGroups(set.hideCanvasGroups, false);
    }

    private PhaseLockSet GetSetForPhase(TutorialManager.TutorialPhase phase)
    {
        if (phaseLocks == null)
            return null;

        for (int i = 0; i < phaseLocks.Length; i++)
        {
            if (phaseLocks[i] != null && phaseLocks[i].phase == phase)
                return phaseLocks[i];
        }

        return null;
    }

    private void ForceRole(StaffRole.Role role)
    {
        if (RoleManager.Instance == null)
            return;

        if (RoleManager.Instance.IsActiveRoleType(role))
            return;

        switch (role)
        {
            case StaffRole.Role.Host:
                RoleManager.Instance.SwitchToHost();
                break;

            case StaffRole.Role.Waiter:
                RoleManager.Instance.SwitchToWaiter();
                break;

            case StaffRole.Role.Cashier:
                RoleManager.Instance.SwitchToCashier();
                break;

            case StaffRole.Role.Busser:
                RoleManager.Instance.SwitchToBusser();
                break;
        }
    }

    private void ResetRoleButtons()
    {
        SetButtonInteractable(hostButton, true);
        SetButtonInteractable(waiterButton, true);
        SetButtonInteractable(cashierButton, true);
        SetButtonInteractable(busserButton, true);
    }

    private void LockRoleButtons(StaffRole.Role requiredRole)
    {
        SetButtonInteractable(hostButton, requiredRole == StaffRole.Role.Host);
        SetButtonInteractable(waiterButton, requiredRole == StaffRole.Role.Waiter);
        SetButtonInteractable(cashierButton, requiredRole == StaffRole.Role.Cashier);
        SetButtonInteractable(busserButton, requiredRole == StaffRole.Role.Busser);
    }

    private void ResetExtraButtons()
    {
        if (phaseLocks == null)
            return;

        for (int i = 0; i < phaseLocks.Length; i++)
        {
            PhaseLockSet set = phaseLocks[i];
            if (set == null)
                continue;

            SetButtonsInteractable(set.enableButtons, true);
            SetButtonsInteractable(set.disableButtons, true);
        }
    }

    private void ResetSceneObjects()
    {
        if (phaseLocks == null)
            return;

        for (int i = 0; i < phaseLocks.Length; i++)
        {
            PhaseLockSet set = phaseLocks[i];
            if (set == null)
                continue;

            SetObjectsActive(set.enableObjects, true);
            SetObjectsActive(set.disableObjects, true);
        }
    }

    private void ResetColliders()
    {
        if (phaseLocks == null)
            return;

        for (int i = 0; i < phaseLocks.Length; i++)
        {
            PhaseLockSet set = phaseLocks[i];
            if (set == null)
                continue;

            SetCollidersEnabled(set.enableColliders, true);
            SetCollidersEnabled(set.disableColliders, true);
        }
    }

    private void ResetCanvasGroups()
    {
        if (phaseLocks == null)
            return;

        for (int i = 0; i < phaseLocks.Length; i++)
        {
            PhaseLockSet set = phaseLocks[i];
            if (set == null)
                continue;

            SetCanvasGroups(set.showCanvasGroups, true);
            SetCanvasGroups(set.hideCanvasGroups, true);
        }
    }

    private void SetObjectsActive(GameObject[] objects, bool value)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(value);
        }
    }

    private void SetButtonsInteractable(Button[] buttons, bool value)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
            SetButtonInteractable(buttons[i], value);
    }

    private void SetButtonInteractable(Button button, bool value)
    {
        if (button == null)
            return;

        button.interactable = value;
    }

    private void SetCollidersEnabled(Collider[] colliders, bool value)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = value;
        }
    }

    private void SetCanvasGroups(CanvasGroup[] groups, bool visible)
    {
        if (groups == null)
            return;

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null)
                continue;

            groups[i].alpha = visible ? 1f : 0f;
            groups[i].interactable = visible;
            groups[i].blocksRaycasts = visible;
        }
    }

    public void RefreshNow()
    {
        if (tutorialManager == null)
            return;

        ApplyPhaseLocks(tutorialManager.CurrentPhase);
        lastAppliedPhase = tutorialManager.CurrentPhase;
    }
}