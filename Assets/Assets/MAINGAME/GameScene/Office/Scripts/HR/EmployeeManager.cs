using UnityEngine;
using System;
using System.Collections.Generic;

public class EmployeeManager : MonoBehaviour
{
    public static EmployeeManager Instance { get; private set; }
    public EmployeeGenerator generator;

    [Header("All Employees")]
    public List<EmployeeData> allEmployees = new List<EmployeeData>();

    [Header("Employees Grouped by Role")]
    public List<RoleGroup> employeesByRole = new List<RoleGroup>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize all roles
        employeesByRole.Clear();
        foreach (EmployeeRole role in Enum.GetValues(typeof(EmployeeRole)))
            employeesByRole.Add(new RoleGroup { role = role });
    }

    public void GenerateEmployees()
    {
        if (generator == null)
        {
            Debug.LogError("EmployeeManager: generator not assigned!");
            return;
        }

        generator.GenerateEmployees();
        allEmployees = generator.employees;

        // Clear role groups
        foreach (var group in employeesByRole)
            group.employees.Clear();

        // Assign employees to role groups
        foreach (var emp in allEmployees)
        {
            var group = employeesByRole.Find(g => g.role == emp.role);
            if (group != null)
                group.employees.Add(emp);
        }
    }

    public void AssignEmployee(EmployeeData employee, RoleSlot slot)
    {
        if (employee.role != slot.roleType)
        {
            Debug.Log("Role mismatch");
            return;
        }

        if (!slot.AssignEmployee(employee)) return;

        employee.assignedSlot     = slot;
        employee.assignedSlotName = slot.name;

        var group = employeesByRole.Find(g => g.role == slot.roleType);
        if (group != null && !group.employees.Contains(employee))
            group.employees.Add(employee);
    }

    /// <summary>Clears all daily assignments and slot locks. Call at the start of each new day.</summary>
    public void ResetDailyAssignments()
    {
        foreach (var emp in allEmployees)
        {
            emp.assigned         = false;
            emp.assignedSlot     = null;
            emp.assignedSlotName = string.Empty;
            emp.currentSlot      = null;
        }

        RoleSlot[] allSlots = FindObjectsByType<RoleSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var slot in allSlots)
            slot.ResetForNewDay();
    }
}