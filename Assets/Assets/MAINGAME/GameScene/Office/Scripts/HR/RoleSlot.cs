using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoleSlot : MonoBehaviour
{
    public EmployeeRole roleType;
    public EmployeeData assignedEmployee;
    public bool isLocked;

    public TMP_Text employeeText;

    public bool AssignEmployee(EmployeeData employee)
    {
        if (isLocked)
        {
            Debug.Log($"Slot {name} is locked for today.");
            return false;
        }

        if (employee.role != roleType) return false;

        // Remove from previous slot
        if (employee.currentSlot != null)
            employee.currentSlot.RemoveEmployee();

        assignedEmployee        = employee;
        employee.assigned       = true;
        employee.currentSlot    = this;
        isLocked                = true;

        UpdateUI();
        return true;
    }

    public void RemoveEmployee()
    {
        if (assignedEmployee != null)
        {
            assignedEmployee.assigned    = false;
            assignedEmployee.currentSlot = null;
            assignedEmployee             = null;

            UpdateUI();
        }
    }

    /// <summary>Clears lock and assignment at the start of a new day.</summary>
    public void ResetForNewDay()
    {
        isLocked         = false;
        assignedEmployee = null;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (employeeText == null) return;
        employeeText.text = assignedEmployee != null ? assignedEmployee.employeeName : "Empty";
    }
}