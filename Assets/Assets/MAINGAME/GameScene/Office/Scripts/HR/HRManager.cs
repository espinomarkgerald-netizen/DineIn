using UnityEngine;

public class HRManager : MonoBehaviour
{
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

    public void AssignEmployee(RoleSlot slot)
    {
        if(selectedEmployee == null || slot == null) return;
        if(selectedEmployee.assigned) return;

        slot.AssignEmployee(selectedEmployee);

        selectedEmployee = null;
    }
}