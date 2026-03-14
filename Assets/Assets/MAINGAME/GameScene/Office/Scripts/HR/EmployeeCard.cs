using UnityEngine;

public class EmployeeCard : MonoBehaviour
{
    public EmployeeData employee;
    public HRManager hrManager;

    public void SelectCard()
    {
        hrManager.SelectEmployee(employee);
    }
}