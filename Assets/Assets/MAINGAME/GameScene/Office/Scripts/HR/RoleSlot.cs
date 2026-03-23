using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoleSlot : MonoBehaviour
{
    public EmployeeRole roleType;
    public EmployeeData assignedEmployee;

    public TMP_Text employeeText;

    public bool AssignEmployee(EmployeeData employee)
    {
        if (employee.role != roleType) return false;

        // Remove from previous slot
        if (employee.currentSlot != null)
        {
            employee.currentSlot.RemoveEmployee();
        }

        // Replace current employee if occupied
        if (assignedEmployee != null)
        {
            assignedEmployee.assigned = false;
            assignedEmployee.currentSlot = null;
        }

        assignedEmployee = employee;
        employee.assigned = true;
        employee.currentSlot = this;

        UpdateUI();
        return true;
    }

    public void RemoveEmployee()
    {
        if (assignedEmployee != null)
        {
            assignedEmployee.assigned = false;
            assignedEmployee.currentSlot = null;
            assignedEmployee = null;

            UpdateUI();
        }
    }

    void UpdateUI()
    {
        if (assignedEmployee != null)
            employeeText.text = assignedEmployee.employeeName;
        else
            employeeText.text = "Empty";
    }
}