using UnityEngine;

[DisallowMultipleComponent]
public class TutorialCustomerBehavior : MonoBehaviour
{
    public enum TutorialCustomerMode
    {
        FollowTutorialManager,
        HostOnlyNoOrdering,
        WaiterAlreadySeated,
        CashierSupportOnly,
        HiddenForBusser,
        DefaultGameplay
    }

    [Header("Lock")]
    [SerializeField] private bool resetToNormalWhenTutorialStops = true;

    [Header("Mode")]
    [SerializeField] private TutorialCustomerMode mode = TutorialCustomerMode.FollowTutorialManager;
    [SerializeField] private bool autoApplyWhenDayChanges = true;

    [Header("Waiter Setup")]
    [SerializeField] private Booth waiterTutorialBooth;
    [SerializeField] private bool seatImmediatelyForWaiterDay = true;
    [SerializeField] private float waiterOrderDelay = 0.25f;
    [SerializeField] private bool markGroupAsGreeted = true;

    [Header("Visibility")]
    [SerializeField] private bool hideOnCashierDay = true;
    [SerializeField] private bool hideOnBusserDay = true;

    private CustomerGroup group;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private Canvas[] cachedCanvases;

    private bool tutorialWasActive;
    private bool hasAppliedDay;
    private TutorialManager.TutorialDay appliedDay;

    private void Awake()
    {
        group = GetComponent<CustomerGroup>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedCanvases = GetComponentsInChildren<Canvas>(true);
    }

    private void OnEnable()
    {
        hasAppliedDay = false;
    }

    private void Update()
    {
        bool tutorialActive = IsLockedTutorialActive();

        if (!tutorialActive)
        {
            if (tutorialWasActive && resetToNormalWhenTutorialStops)
                ResetToNormalGameplay();

            tutorialWasActive = false;
            return;
        }

        tutorialWasActive = true;

        if (TutorialManager.Instance == null)
            return;

        TutorialManager.TutorialDay currentDay = TutorialManager.Instance.CurrentDay;

        if (!autoApplyWhenDayChanges && hasAppliedDay)
            return;

        if (hasAppliedDay && appliedDay == currentDay)
            return;

        ApplyForDay(currentDay);
    }

    [ContextMenu("Apply Current Tutorial Day")]
    public void ApplyCurrentTutorialDay()
    {
        if (!IsLockedTutorialActive() || TutorialManager.Instance == null)
            return;

        ApplyForDay(TutorialManager.Instance.CurrentDay);
    }

    public void ApplyForDay(TutorialManager.TutorialDay day)
    {
        if (group == null)
            group = GetComponent<CustomerGroup>();

        if (group == null)
            return;

        appliedDay = day;
        hasAppliedDay = true;

        TutorialCustomerMode resolvedMode = ResolveMode(day);
        ApplyMode(resolvedMode);
    }

    private bool IsLockedTutorialActive()
    {
        return TutorialSceneRuntimeMarker.IsTutorialRuntimeActive
            && TutorialManager.Instance != null
            && TutorialManager.Instance.TutorialStarted;
    }

    private TutorialCustomerMode ResolveMode(TutorialManager.TutorialDay day)
    {
        if (mode != TutorialCustomerMode.FollowTutorialManager)
            return mode;

        switch (day)
        {
            case TutorialManager.TutorialDay.Day1Host:
                return TutorialCustomerMode.HostOnlyNoOrdering;

            case TutorialManager.TutorialDay.Day2Waiter:
                return TutorialCustomerMode.WaiterAlreadySeated;

            case TutorialManager.TutorialDay.Day3Cashier:
                return TutorialCustomerMode.CashierSupportOnly;

            case TutorialManager.TutorialDay.Day4Busser:
                return TutorialCustomerMode.HiddenForBusser;

            case TutorialManager.TutorialDay.Day5AllTogether:
                return TutorialCustomerMode.DefaultGameplay;
        }

        return TutorialCustomerMode.DefaultGameplay;
    }

    private void ApplyMode(TutorialCustomerMode resolvedMode)
    {
        switch (resolvedMode)
        {
            case TutorialCustomerMode.HostOnlyNoOrdering:
                ApplyHostOnlyNoOrdering();
                break;

            case TutorialCustomerMode.WaiterAlreadySeated:
                ApplyWaiterAlreadySeated();
                break;

            case TutorialCustomerMode.CashierSupportOnly:
                ApplyCashierSupportOnly();
                break;

            case TutorialCustomerMode.HiddenForBusser:
                ApplyHiddenForBusser();
                break;

            case TutorialCustomerMode.DefaultGameplay:
                ApplyDefaultGameplay();
                break;
        }
    }

    private void ApplyHostOnlyNoOrdering()
    {
        ShowPresentation(true);

        group.SetSelected(false);
        group.SetTutorialDisableAutoOrderFlow(true);
        group.SetOrderPause(true);
        group.TutorialClearServiceUI();
    }

    private void ApplyWaiterAlreadySeated()
    {
        ShowPresentation(true);

        group.SetSelected(false);
        group.SetTutorialDisableAutoOrderFlow(false);
        group.SetOrderPause(false);
        group.TutorialClearServiceUI();

        if (seatImmediatelyForWaiterDay && waiterTutorialBooth != null)
        {
            group.TutorialPlaceGroupAtBooth(
                waiterTutorialBooth,
                true,
                waiterOrderDelay,
                markGroupAsGreeted
            );
        }
        else
        {
            if (markGroupAsGreeted)
                group.MarkGreeted();

            group.TutorialBeginWaiterFlow(waiterOrderDelay);
        }
    }

    private void ApplyCashierSupportOnly()
    {
        group.SetSelected(false);
        group.SetTutorialDisableAutoOrderFlow(true);
        group.SetOrderPause(true);
        group.TutorialClearServiceUI();

        ShowPresentation(!hideOnCashierDay);
    }

    private void ApplyHiddenForBusser()
    {
        group.SetSelected(false);
        group.SetTutorialDisableAutoOrderFlow(true);
        group.SetOrderPause(true);
        group.TutorialClearServiceUI();

        ShowPresentation(!hideOnBusserDay);
    }

    private void ApplyDefaultGameplay()
    {
        ShowPresentation(true);

        group.SetTutorialDisableAutoOrderFlow(false);
        group.SetOrderPause(false);
    }

    private void ResetToNormalGameplay()
    {
        ShowPresentation(true);

        if (group == null)
            return;

        group.SetTutorialDisableAutoOrderFlow(false);
        group.SetOrderPause(false);
    }

    private void ShowPresentation(bool visible)
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = visible;
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = visible;
        }

        for (int i = 0; i < cachedCanvases.Length; i++)
        {
            if (cachedCanvases[i] != null)
                cachedCanvases[i].enabled = visible;
        }

        if (!visible && group != null)
            group.SetSelected(false);
    }
}