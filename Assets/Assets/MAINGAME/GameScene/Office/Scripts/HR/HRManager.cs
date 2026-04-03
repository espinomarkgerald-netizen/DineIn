using UnityEngine;
using UnityEngine.UI;

public class HRManager : MonoBehaviour
{
    [Header("Department Rows")]
    public RoleRowUI[] kitchenRows;
    public RoleRowUI[] lobbyRows;

    [Header("Department Buttons UI")]

    [HideInInspector] public EmployeeData selectedEmployee;

    public enum DayPhase
    {
        Morning,
        Afternoon
    }

    [SerializeField] private DayPhase currentPhase;

    public DayPhase CurrentPhase => currentPhase;

    private void OnEnable()
    {
        RefreshPhaseUI();
    }

    private void Start()
    {
        if (EmployeeManager.Instance.allEmployees.Count == 0)
            EmployeeManager.Instance.GenerateEmployees();

        RefreshPhaseUI();
    }

    public void RefreshPhaseUI()
    {
        SyncPhaseFromGameFlow();

        // 🔹 CHANGED: Show all rows instead of filtering by phase
        PopulateAllRows();

    }

    public void SyncPhaseFromGameFlow()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogWarning("GameFlowManager not found.");
            return;
        }

        if (GameFlowManager.Instance.IsMorning)
            currentPhase = DayPhase.Morning;
        else if (GameFlowManager.Instance.IsAfternoon)
            currentPhase = DayPhase.Afternoon;
    }

    public void SetPhase(DayPhase phase)
    {
        currentPhase = phase;

        // 🔹 CHANGED: Always show all rows
        PopulateAllRows();

    }

    // 🔹 NEW: Populate everything regardless of phase
    public void PopulateAllRows()
    {
        HideAllRows();

        PopulateRows(lobbyRows);
        PopulateRows(kitchenRows);

        SetRowsActive(lobbyRows, true);
        SetRowsActive(kitchenRows, true);
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

        // 🔹 CHANGED: Refresh across ALL rows instead of phase-based
        RoleRowUI[] allRows = CombineRows();

        foreach (var row in allRows)
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
        if (rowsToPopulate == null) return;
        if (EmployeeManager.Instance == null) return;

        foreach (var row in rowsToPopulate)
        {
            if (row == null) continue;

            var group = EmployeeManager.Instance.employeesByRole.Find(g => g.role == row.roleType);
            if (group != null)
                row.Populate(group.employees, this);
        }
    }

    // 🔹 KEPT but no longer used for filtering
    private RoleRowUI[] GetRowsForCurrentPhase()
    {
        return currentPhase == DayPhase.Morning ? lobbyRows : kitchenRows;
    }

    private void HideAllRows()
    {
        SetRowsActive(lobbyRows, false);
        SetRowsActive(kitchenRows, false);
    }

    private void SetRowsActive(RoleRowUI[] rows, bool isActive)
    {
        if (rows == null)
            return;

        foreach (var row in rows)
        {
            if (row == null)
                continue;

            row.gameObject.SetActive(isActive);
        }
    }

    // 🔹 NEW helper to merge both row arrays
    private RoleRowUI[] CombineRows()
    {
        int totalLength = (lobbyRows?.Length ?? 0) + (kitchenRows?.Length ?? 0);
        RoleRowUI[] combined = new RoleRowUI[totalLength];

        int index = 0;

        if (lobbyRows != null)
        {
            foreach (var row in lobbyRows)
                combined[index++] = row;
        }

        if (kitchenRows != null)
        {
            foreach (var row in kitchenRows)
                combined[index++] = row;
        }

        return combined;
    }
}