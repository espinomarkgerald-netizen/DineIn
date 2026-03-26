using UnityEngine;

public class HRManager : MonoBehaviour
{
    [Header("Department Rows")]
    public RoleRowUI[] kitchenRows; // assign in inspector
    public RoleRowUI[] lobbyRows;   // assign in inspector

    [HideInInspector] public EmployeeData selectedEmployee;

    public enum DayPhase { Morning, Afternoon }
    public DayPhase currentPhase;

    void Start()
    {
        // Ensure employees exist
        if (EmployeeManager.Instance.allEmployees.Count == 0)
            EmployeeManager.Instance.GenerateEmployees();

        // Populate only the rows for the current phase
        PopulateCurrentPhaseRows();
    }

    public void PopulateCurrentPhaseRows()
    {
        RoleRowUI[] rowsToPopulate = currentPhase == DayPhase.Morning ? kitchenRows : lobbyRows;

        foreach (var row in rowsToPopulate)
        {
            var group = EmployeeManager.Instance.employeesByRole.Find(g => g.role == row.roleType);
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

        // Refresh only the rows of the current phase
        RoleRowUI[] rowsToRefresh = currentPhase == DayPhase.Morning ? kitchenRows : lobbyRows;

        foreach (var row in rowsToRefresh)
        {
            if (row.roleType == targetSlot.roleType)
            {
                var group = EmployeeManager.Instance.employeesByRole.Find(g => g.role == row.roleType);
                row.Populate(group.employees, this);
                break;
            }
        }

        return true;
    }

    public void PopulateRows(RoleRowUI[] rowsToPopulate)
    {
        foreach (var row in rowsToPopulate)
        {
            var group = EmployeeManager.Instance.employeesByRole
                .Find(g => g.role == row.roleType);

            if (group != null)
                row.Populate(group.employees, this);
        }
    }
}