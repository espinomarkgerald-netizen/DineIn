using UnityEngine;

public class HRManager : MonoBehaviour
{
    public RoleSlot[] allSlots;
    public EmployeeGenerator generator;

    public EmployeeCard[] cards;

    public EmployeeData selectedEmployee;

    void Start()
    {
        generator.GenerateEmployees();

        for(int i = 0; i < cards.Length; i++)
        {
            cards[i].Setup(generator.employees[i]);
            Debug.Log("Assigning employee to card " + i);
        }
    }

    public void SelectEmployee(EmployeeData employee)
    {
        selectedEmployee = employee;
    }

    public void AssignEmployee(RoleSlot targetSlot)
    {
        if (selectedEmployee == null) return;

    // Remove employee from any slot they are currently in
    foreach (RoleSlot s in allSlots)
    {
        if (s.assignedEmployee == selectedEmployee)
        {
            s.RemoveEmployee();
        }
    }

    // If the target slot already has someone, free them
    if (targetSlot.assignedEmployee != null)
    {
        targetSlot.assignedEmployee.assigned = false;
        targetSlot.assignedEmployee.assignedRole = "";
    }

    // Assign employee to the new slot
    targetSlot.AssignEmployee(selectedEmployee);

    selectedEmployee = null;
    }
}