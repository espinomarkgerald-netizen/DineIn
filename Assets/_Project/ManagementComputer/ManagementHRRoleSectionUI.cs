using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One role with separate horizontally scrollable employed/applicant rails.</summary>
public sealed class ManagementHRRoleSectionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text roleTitle;
    [SerializeField] private TMP_Text roleSummary;
    [SerializeField] private RectTransform employedContent;
    [SerializeField] private RectTransform applicantContent;
    [SerializeField] private ScrollRect employedScroll;
    [SerializeField] private ScrollRect applicantScroll;
    [SerializeField] private GameObject employedLabelRoot;
    [SerializeField] private GameObject applicantLabelRoot;

    [Header("Single Rail Layout (Editable)")]
    [SerializeField] private Vector2 singleRailLabelPosition = new Vector2(0f, -68f);
    [SerializeField] private Vector2 singleRailScrollPosition = new Vector2(0f, -230f);
    [SerializeField, Min(120f)] private float singleRailScrollHeight = 286f;
    [SerializeField, Min(240f)] private float singleRailSectionHeight = 380f;

    [Header("Applicant Reflow (Editable)")]
    [SerializeField, Min(0.08f)] private float applicantReflowDuration = 0.16f;

    public EmployeeRole Role { get; private set; }
    public RectTransform EmployedContent => employedContent;
    public RectTransform ApplicantContent => applicantContent;
    public ScrollRect EmployedScroll => employedScroll;
    public ScrollRect ApplicantScroll => applicantScroll;

    public void ConfigureReferences(
        TMP_Text configuredRoleTitle,
        TMP_Text configuredRoleSummary,
        RectTransform configuredEmployedContent,
        RectTransform configuredApplicantContent,
        ScrollRect configuredEmployedScroll,
        ScrollRect configuredApplicantScroll)
    {
        roleTitle = configuredRoleTitle;
        roleSummary = configuredRoleSummary;
        employedContent = configuredEmployedContent;
        applicantContent = configuredApplicantContent;
        employedScroll = configuredEmployedScroll;
        applicantScroll = configuredApplicantScroll;
    }

    public void Bind(
        EmployeeRole role,
        EmployeeManager manager,
        ManagementEmployeeCardUI cardPrefab,
        bool editable,
        Action onRosterChanged,
        bool showEmployed = true,
        bool showApplicants = true,
        Action<EmployeeRole> onEmptySlotSelected = null)
    {
        Role = role;
        ResolveRailRoots();
        if (employedLabelRoot != null) employedLabelRoot.SetActive(showEmployed);
        if (applicantLabelRoot != null) applicantLabelRoot.SetActive(showApplicants);
        if (employedScroll != null) employedScroll.gameObject.SetActive(showEmployed);
        if (applicantScroll != null) applicantScroll.gameObject.SetActive(showApplicants);
        ApplySingleRailLayout(showEmployed, showApplicants);
        Clear(employedContent);
        Clear(applicantContent);

        List<EmployeeData> employed = new List<EmployeeData>();
        List<EmployeeData> applicants = new List<EmployeeData>();
        foreach (EmployeeData employee in manager.allEmployees)
        {
            if (employee == null || employee.role != role)
                continue;
            (employee.hired ? employed : applicants).Add(employee);
        }

        employed.Sort(CompareEmployees);
        applicants.Sort(CompareEmployees);
        if (roleTitle != null) roleTitle.text = role.ToString().ToUpperInvariant();
        if (roleSummary != null)
            roleSummary.text = $"{employed.Count}/{manager.MaxHiredPerRole} EMPLOYED   •   {applicants.Count} APPLICANTS";

        if (showEmployed && employed.Count == 0)
        {
            ManagementEmployeeCardUI empty = Instantiate(cardPrefab, employedContent);
            empty.name = "Empty_" + role;
            UnityEngine.Events.UnityAction openApplicants = onEmptySlotSelected == null
                ? null
                : () => onEmptySlotSelected(role);
            empty.Bind(null, manager.salaryConfig, "EMPTY POSITION", string.Empty, null, false,
                string.Empty, null, false, openApplicants);
            PlayCardReveal(empty, 0);
        }
        else if (showEmployed)
        {
            int employeeRevealIndex = 0;
            foreach (EmployeeData employee in employed)
            {
                EmployeeData captured = employee;
                ManagementEmployeeCardUI card = Instantiate(cardPrefab, employedContent);
                card.name = "Employee_" + employee.employeeName;
                card.Bind(employee, manager.salaryConfig,
                    employee.assigned ? "ACTIVE THIS SHIFT" : "ROSTERED",
                    employee.assigned ? "ACTIVE" : "SET ACTIVE",
                    employee.assigned ? null : () =>
                    {
                        if (manager.AssignEmployeeForDay(captured)) onRosterChanged?.Invoke();
                    },
                    editable && !employee.assigned,
                    "FIRE",
                    () =>
                    {
                        if (manager.FireEmployee(captured)) onRosterChanged?.Invoke();
                    },
                    editable);
                PlayCardReveal(card, employeeRevealIndex++);
            }
        }

        bool hasSpace = employed.Count < manager.MaxHiredPerRole;
        if (showApplicants)
        {
            int applicantRevealIndex = 0;
            foreach (EmployeeData applicant in applicants)
            {
                EmployeeData captured = applicant;
                ManagementEmployeeCardUI card = Instantiate(cardPrefab, applicantContent);
                card.name = "Applicant_" + applicant.employeeName;
                string availability = applicant.applicantAvailableUntilDay > 0
                    ? "AVAILABLE THROUGH DAY " + applicant.applicantAvailableUntilDay
                    : "APPLICANT";
                card.Bind(applicant, manager.salaryConfig, availability,
                    hasSpace ? "HIRE" : "ROSTER FULL",
                    () =>
                    {
                        card.PlayPositiveFeedback(
                            () => manager.HireApplicant(captured),
                            () => onRosterChanged?.Invoke());
                    },
                    editable && hasSpace,
                    "DECLINE",
                    () =>
                    {
                        card.PlayDeclineRemoval(
                            () => manager.DeclineApplicant(captured),
                            () => BeginApplicantReflow(card, manager, onRosterChanged));
                    },
                    editable);
                PlayCardReveal(card, applicantRevealIndex++);
            }
        }

        Canvas.ForceUpdateCanvases();
        if (showEmployed) LayoutRebuilder.ForceRebuildLayoutImmediate(employedContent);
        if (showApplicants) LayoutRebuilder.ForceRebuildLayoutImmediate(applicantContent);
        if (employedScroll != null) employedScroll.horizontalNormalizedPosition = 0f;
        if (applicantScroll != null) applicantScroll.horizontalNormalizedPosition = 0f;

        float preferredHeight = showEmployed && showApplicants
            ? 760f
            : singleRailSectionHeight;
        if (transform is RectTransform rect)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout != null)
            layout.preferredHeight = preferredHeight;
    }

    private void BeginApplicantReflow(
        ManagementEmployeeCardUI removedCard,
        EmployeeManager manager,
        Action onRosterChanged)
    {
        if (removedCard == null || applicantContent == null)
        {
            onRosterChanged?.Invoke();
            return;
        }

        StartCoroutine(ReflowAfterRemoval(removedCard, manager, onRosterChanged));
    }

    private IEnumerator ReflowAfterRemoval(
        ManagementEmployeeCardUI removedCard,
        EmployeeManager manager,
        Action onRosterChanged)
    {
        List<RectTransform> remainingCards = new List<RectTransform>();
        List<Vector2> startPositions = new List<Vector2>();
        for (int i = 0; i < applicantContent.childCount; i++)
        {
            RectTransform child = applicantContent.GetChild(i) as RectTransform;
            if (child == null || child == removedCard.transform || !child.gameObject.activeSelf)
                continue;
            remainingCards.Add(child);
            startPositions.Add(child.anchoredPosition);
        }

        removedCard.gameObject.SetActive(false);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(applicantContent);

        if (remainingCards.Count == 0)
        {
            Destroy(removedCard.gameObject);
            onRosterChanged?.Invoke();
            yield break;
        }

        List<Vector2> targetPositions = new List<Vector2>(remainingCards.Count);
        for (int i = 0; i < remainingCards.Count; i++)
            targetPositions.Add(remainingCards[i].anchoredPosition);

        LayoutGroup layout = applicantContent.GetComponent<LayoutGroup>();
        if (layout != null)
            layout.enabled = false;
        for (int i = 0; i < remainingCards.Count; i++)
            remainingCards[i].anchoredPosition = startPositions[i];

        float duration = LevelOneUIAccessibility.ReducedMotion
            ? 0f
            : Mathf.Max(0.08f, applicantReflowDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < remainingCards.Count; i++)
                remainingCards[i].anchoredPosition = Vector2.Lerp(startPositions[i], targetPositions[i], progress);
            yield return null;
        }

        if (layout != null)
            layout.enabled = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(applicantContent);
        Destroy(removedCard.gameObject);
        UpdateRoleSummary(manager);
    }

    private void UpdateRoleSummary(EmployeeManager manager)
    {
        if (roleSummary == null || manager == null || manager.allEmployees == null)
            return;

        int employedCount = 0;
        int applicantCount = 0;
        for (int i = 0; i < manager.allEmployees.Count; i++)
        {
            EmployeeData employee = manager.allEmployees[i];
            if (employee == null || employee.role != Role)
                continue;
            if (employee.hired) employedCount++;
            else applicantCount++;
        }
        roleSummary.text = $"{employedCount}/{manager.MaxHiredPerRole} EMPLOYED   •   {applicantCount} APPLICANTS";
    }

    private static void PlayCardReveal(ManagementEmployeeCardUI card, int index)
    {
        card?.GetComponent<UIRevealAnimation>()?.Play(Mathf.Min(0.12f, index * 0.025f));
    }

    private void ApplySingleRailLayout(bool showEmployed, bool showApplicants)
    {
        if (showEmployed == showApplicants)
            return;

        GameObject labelRoot = showEmployed ? employedLabelRoot : applicantLabelRoot;
        ScrollRect activeScroll = showEmployed ? employedScroll : applicantScroll;
        if (labelRoot != null && labelRoot.transform is RectTransform labelRect)
            labelRect.anchoredPosition = singleRailLabelPosition;
        if (activeScroll != null && activeScroll.transform is RectTransform scrollRect)
        {
            scrollRect.anchoredPosition = singleRailScrollPosition;
            scrollRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                singleRailScrollHeight);
        }
    }

    private void ResolveRailRoots()
    {
        if (employedLabelRoot == null)
        {
            Transform label = transform.Find("EmployedLabel");
            if (label != null) employedLabelRoot = label.gameObject;
        }
        if (applicantLabelRoot == null)
        {
            Transform label = transform.Find("ApplicantsLabel");
            if (label != null) applicantLabelRoot = label.gameObject;
        }
    }

    private static int CompareEmployees(EmployeeData left, EmployeeData right)
    {
        int activeComparison = right.assigned.CompareTo(left.assigned);
        if (activeComparison != 0) return activeComparison;
        int starComparison = right.stars.CompareTo(left.stars);
        return starComparison != 0
            ? starComparison
            : string.Compare(left.employeeName, right.employeeName, StringComparison.OrdinalIgnoreCase);
    }

    private static void Clear(RectTransform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
}
