using System;
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
        Action onRosterChanged)
    {
        Role = role;
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

        if (employed.Count == 0)
        {
            ManagementEmployeeCardUI empty = Instantiate(cardPrefab, employedContent);
            empty.name = "Empty_" + role;
            empty.Bind(null, manager.salaryConfig, "EMPTY POSITION", string.Empty, null, false,
                string.Empty, null, false);
        }
        else
        {
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
            }
        }

        bool hasSpace = employed.Count < manager.MaxHiredPerRole;
        foreach (EmployeeData applicant in applicants)
        {
            EmployeeData captured = applicant;
            ManagementEmployeeCardUI card = Instantiate(cardPrefab, applicantContent);
            card.name = "Applicant_" + applicant.employeeName;
            card.Bind(applicant, manager.salaryConfig, "APPLICANT",
                hasSpace ? "HIRE" : "ROSTER FULL",
                () =>
                {
                    if (manager.HireApplicant(captured)) onRosterChanged?.Invoke();
                },
                editable && hasSpace,
                "DECLINE",
                () =>
                {
                    if (manager.DeclineApplicant(captured)) onRosterChanged?.Invoke();
                },
                editable);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(employedContent);
        LayoutRebuilder.ForceRebuildLayoutImmediate(applicantContent);
        if (employedScroll != null) employedScroll.horizontalNormalizedPosition = 0f;
        if (applicantScroll != null) applicantScroll.horizontalNormalizedPosition = 0f;
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
