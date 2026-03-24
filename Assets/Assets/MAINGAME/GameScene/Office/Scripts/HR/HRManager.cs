using UnityEngine;

public class HRManager : MonoBehaviour
{
    public RoleRowUI[] rows; // assign in inspector
    public EmployeeData selectedEmployee;

    void Start()
    {
        // Ensure employees exist
        if (EmployeeManager.Instance.allEmployees.Count == 0)
            EmployeeManager.Instance.GenerateEmployees();

        // Populate each RoleRowUI
        foreach (var row in rows)
        {
            var group = EmployeeManager.Instance.employeesByRole
                .Find(g => g.role == row.roleType);
            if (group != null)
                row.Populate(group.employees, this);
        }
    }

    public void SelectEmployee(EmployeeData employee)
    {
        selectedEmployee = employee;
    }

    public bool AssignEmployee(RoleSlot targetSlot)
    {
        if (selectedEmployee == null) return false;

        EmployeeManager.Instance.AssignEmployee(selectedEmployee, targetSlot);
        selectedEmployee = null;

        // Refresh the row UI
        foreach (var row in rows)
        {
            if (row.roleType == targetSlot.roleType)
            {
                var group = EmployeeManager.Instance.employeesByRole
                    .Find(g => g.role == row.roleType);
                row.Populate(group.employees, this);
                break;
            }
        }

        return true;
    }
}