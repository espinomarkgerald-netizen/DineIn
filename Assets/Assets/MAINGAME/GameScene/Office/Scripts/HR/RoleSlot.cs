using UnityEngine;
using UnityEngine.UI;

public class RoleSlot : MonoBehaviour
{
    public string roleName;          // Host, Waiter, etc.
    public EmployeeData assignedEmployee;
    public Text slotText;            // UI Text to show employee name

    public void AssignEmployee(EmployeeData employee)
    {
        assignedEmployee = employee;
        employee.assigned = true;
        employee.assignedRole = roleName;

        if(slotText != null)
            slotText.text = employee.employeeName;
    }

    public void RemoveEmployee()
    {
        if(assignedEmployee != null)
        {
            assignedEmployee.assigned = false;
            assignedEmployee.assignedRole = "";
            assignedEmployee = null;

            if(slotText != null)
                slotText.text = "";
        }
    }
}