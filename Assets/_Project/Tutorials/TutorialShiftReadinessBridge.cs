using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Supplies the rest of the tutorial roster after the Staff Management lesson.
/// The real pre-open checklist and real Start Shift confirmation remain authoritative.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialShiftReadinessBridge : MonoBehaviour
{
    private TutorialSystem tutorial;
    private bool prepared;

    private void Awake() => tutorial = GetComponent<TutorialSystem>();

    private void OnEnable()
    {
        if (tutorial == null) tutorial = GetComponent<TutorialSystem>();
        if (tutorial != null) tutorial.SpawnPermissionsChanged += OnSpawnPermissionsChanged;
    }

    private void Start()
    {
        if (tutorial != null && tutorial.AllowStaffSpawning)
            PrepareTutorialRoster();
    }

    private void OnDisable()
    {
        if (tutorial != null) tutorial.SpawnPermissionsChanged -= OnSpawnPermissionsChanged;
    }

    private void OnSpawnPermissionsChanged(bool customersAllowed, bool staffAllowed)
    {
        if (staffAllowed) PrepareTutorialRoster();
    }

    private void PrepareTutorialRoster()
    {
        if (prepared) return;
        EmployeeManager manager = EmployeeManager.Instance;
        if (manager == null) return;

        manager.EnsureEmployeesGenerated();
        if (manager.allEmployees == null) return;
        if (manager.generator == null)
            manager.generator = FindFirstObjectByType<EmployeeGenerator>(FindObjectsInactive.Include);

        List<string> usedNames = new List<string>();
        foreach (EmployeeData employee in manager.allEmployees)
            if (employee != null && !string.IsNullOrWhiteSpace(employee.employeeName))
                usedNames.Add(employee.employeeName);

        PrepareRoles(manager, EmployeeRoleCatalog.LobbyRoles, usedNames);
        PrepareRoles(manager, EmployeeRoleCatalog.KitchenRoles, usedNames);
        prepared = manager.HasAllRequiredRolesAssigned;

        if (!prepared)
            Debug.LogError("[Tutorial] Could not prepare every required role for the real Start Shift checklist.", this);
    }

    private static void PrepareRoles(
        EmployeeManager manager,
        IReadOnlyList<EmployeeRole> roles,
        List<string> usedNames)
    {
        for (int i = 0; i < roles.Count; i++)
        {
            EmployeeRole role = roles[i];
            if (manager.GetAssignedEmployee(role) != null) continue;

            EmployeeData employee = manager.allEmployees.Find(candidate =>
                candidate != null && candidate.role == role && candidate.hired);
            if (employee == null)
            {
                employee = manager.allEmployees.Find(candidate =>
                    candidate != null && candidate.role == role && !candidate.hired);
                if (employee == null && manager.generator != null)
                {
                    employee = manager.generator.GenerateApplicant(role, usedNames);
                    employee.EnsureIdentity();
                    employee.applicantAvailableUntilDay = int.MaxValue;
                    manager.allEmployees.Add(employee);
                    if (!string.IsNullOrWhiteSpace(employee.employeeName))
                        usedNames.Add(employee.employeeName);
                }

                if (employee != null && !manager.HireApplicant(employee))
                    employee = null;
            }

            if (employee != null && manager.GetAssignedEmployee(role) == null)
                manager.AssignEmployeeForDay(employee);
        }
    }
}
