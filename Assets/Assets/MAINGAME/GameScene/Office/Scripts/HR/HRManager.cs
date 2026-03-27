using UnityEngine;

public class HRManager : MonoBehaviour
{
    [Header("Department Rows")]
    public RoleRowUI[] kitchenRows;
    public RoleRowUI[] lobbyRows;

    [HideInInspector] public EmployeeData selectedEmployee;

    public enum DayPhase
    {
        Morning,
        Afternoon
    }

    [SerializeField] private DayPhase currentPhase;

    public DayPhase CurrentPhase => currentPhase;

    private void Start()
    {
        if (EmployeeManager.Instance.allEmployees.Count == 0)
            EmployeeManager.Instance.GenerateEmployees();

        SyncPhaseFromGameFlow();
        PopulateCurrentPhaseRows();
    }

    public void SyncPhaseFromGameFlow()
    {
        if (GameFlowManager.Instance == null)
            return;

        switch (GameFlowManager.Instance.CurrentDayHalf)
        {
            case GameFlowManager.DayHalf.Morning:
                currentPhase = DayPhase.Morning;
                break;

            case GameFlowManager.DayHalf.Afternoon:
                currentPhase = DayPhase.Afternoon;
                break;
        }
    }

    public void SetPhase(DayPhase phase)
    {
        currentPhase = phase;
        PopulateCurrentPhaseRows();
    }

    public void PopulateCurrentPhaseRows()
    {
        SyncPhaseFromGameFlow();

        RoleRowUI[] rowsToPopulate = GetRowsForCurrentPhase();
        PopulateRows(rowsToPopulate);
    }

    public void SelectEmployee(EmployeeData employee)
    {
        selectedEmployee = employee;
    }

    public bool AssignEmployee(RoleSlot targetSlot)
    {
        if (selectedEmployee == null)
            return false;

        SyncPhaseFromGameFlow();

        EmployeeManager.Instance.AssignEmployee(selectedEmployee, targetSlot);
        selectedEmployee = null;

        RoleRowUI[] rowsToRefresh = GetRowsForCurrentPhase();

        foreach (var row in rowsToRefresh)
        {
            if (row == null)
                continue;

            if (row.roleType == targetSlot.roleType)
            {
                var group = EmployeeManager.Instance.employeesByRole.Find(g => g.role == row.roleType);
                if (group != null)
                    row.Populate(group.employees, this);

                break;
            }
        }

        return true;
    }

    public void PopulateRows(RoleRowUI[] rowsToPopulate)
    {
        if (rowsToPopulate == null)
            return;

        foreach (var row in rowsToPopulate)
        {
            if (row == null)
                continue;

            var group = EmployeeManager.Instance.employeesByRole.Find(g => g.role == row.roleType);
            if (group != null)
                row.Populate(group.employees, this);
        }
    }

    private RoleRowUI[] GetRowsForCurrentPhase()
    {
        return currentPhase == DayPhase.Morning ? lobbyRows : kitchenRows;
    }
}