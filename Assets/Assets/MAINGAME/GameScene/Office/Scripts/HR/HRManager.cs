using UnityEngine;

public class HRManager : MonoBehaviour
{
    public EmployeeData selectedEmployee;

    // Called when player taps an employee card
    public void SelectEmployee(EmployeeData employee)
    {
        selectedEmployee = employee;
        Debug.Log("Selected: " + employee.employeeName);
    }

    // Called when player taps a slot
    public void AssignSelectedToSlot(RoleSlot slot)
    {
        if(selectedEmployee == null) return;
        if(selectedEmployee.assigned)
        {
            Debug.Log(selectedEmployee.employeeName + " is already assigned!");
            return;
        }

        slot.AssignEmployee(selectedEmployee);
        selectedEmployee = null; // reset selection
    }
}