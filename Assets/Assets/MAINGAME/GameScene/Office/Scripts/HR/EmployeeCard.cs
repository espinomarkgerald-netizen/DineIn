using UnityEngine;
using TMPro;

public class EmployeeCard : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text cookingText;
    public TMP_Text serviceText;

    public EmployeeData employee;
    public HRManager hrManager;

    public void Setup(EmployeeData data)
    {
        employee = data;

        nameText.text = data.employeeName;
        cookingText.text = "Cook: " + data.cooking;
        serviceText.text = "Service: " + data.service;
    }

    public void SelectCard()
    {
        hrManager.SelectEmployee(employee);
        Debug.Log($"Selected {employee.employeeName}");
    }
}