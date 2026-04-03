using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoleSlot : MonoBehaviour
{
    public EmployeeRole roleType;
    public EmployeeData assignedEmployee;
    public bool isLocked;

    public TMP_Text employeeText;

    private void Start()
    {
        if (EmployeeManager.Instance == null) return;

        if (EmployeeManager.Instance.SlotsLocked)
            isLocked = true;

        foreach (var emp in EmployeeManager.Instance.allEmployees)
        {
            if (emp.assignedSlotName == name)
            {
                assignedEmployee      = emp;
                emp.currentSlot       = this;
                emp.assignedSlot      = this; 
                emp.assigned          = true; 
                break;
            }
        }

        UpdateUI();
    }

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

        // Remove existing occupant
        if (assignedEmployee != null)
        {
            assignedEmployee.assigned        = false;
            assignedEmployee.currentSlot     = null;
            assignedEmployee.assignedSlot    = null;
            assignedEmployee.assignedSlotName = "";
        }

        // Assign new
        assignedEmployee              = employee;
        employee.assigned             = true;
        employee.currentSlot          = this;
        employee.assignedSlot         = this;
        employee.assignedSlotName     = name;

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

    /// <summary>Locks this slot so no new employee can be assigned until ResetForNewDay.</summary>
    public void Lock() => isLocked = true;

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
