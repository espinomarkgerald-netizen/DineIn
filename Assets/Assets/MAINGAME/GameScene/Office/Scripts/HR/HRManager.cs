using UnityEngine;
using System.Collections.Generic;

public class HRManager : MonoBehaviour
{
    public RoleSlot[] allSlots;
    public EmployeeGenerator generator;
    public EmployeeCard[] cards;
    public EmployeeData selectedEmployee;
    public RoleRowUI[] rows;

    Dictionary<EmployeeRole, List<EmployeeData>> employeesByRole 
        = new Dictionary<EmployeeRole, List<EmployeeData>>();

    void Start()
    {
        Debug.Log($"HRManager Start() running, generator: {generator}, rows length: {rows.Length}");
        generator.GenerateEmployees();

        // Initialize dictionary
        foreach (EmployeeRole role in System.Enum.GetValues(typeof(EmployeeRole)))
        {
            employeesByRole[role] = new List<EmployeeData>();
        }

        // Group employees
        foreach (var emp in generator.employees)
        {
            employeesByRole[emp.role].Add(emp);
        }

        // Populate UI
        foreach (var row in rows)
        {
            var list = employeesByRole[row.roleType];
            row.Populate(list, this);
        }
    }

    public void SelectEmployee(EmployeeData employee)
    {
        selectedEmployee = employee;
    }

    public void AssignEmployee(RoleSlot targetSlot)
    {
        if (selectedEmployee == null) return;

        bool success = targetSlot.AssignEmployee(selectedEmployee);

        if (success)
            selectedEmployee = null;
    }
}