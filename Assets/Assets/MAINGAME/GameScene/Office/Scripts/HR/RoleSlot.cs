using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoleSlot : MonoBehaviour
{
    public EmployeeRole roleType;
    public List<EmployeeData> assignedEmployees = new List<EmployeeData>();

    public TMP_Text[] employeeTexts;
    public int maxEmployees = 3;

    public bool AssignEmployee(EmployeeData employee)
    {
        if (employee.role != roleType) return false;
        if (assignedEmployees.Count >= maxEmployees) return false;
        if (employee.assigned) return false;

        assignedEmployees.Add(employee);
        employee.assigned = true;

        UpdateUI();
        return true;
    }

    void UpdateUI()
    {
        for (int i = 0; i < employeeTexts.Length; i++)
        {
            if (i < assignedEmployees.Count)
                employeeTexts[i].text = assignedEmployees[i].employeeName;
            else
                employeeTexts[i].text = "";
        }
    }
}