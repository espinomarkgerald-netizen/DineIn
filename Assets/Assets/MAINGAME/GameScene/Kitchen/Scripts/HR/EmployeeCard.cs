using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EmployeeCard : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text salaryText;
    public Image[] starImages; // 5 stars
    public Sprite filledStar;
    public Sprite emptyStar;

    public EmployeeData employee;
    public HRManager hrManager;

    public void Setup(EmployeeData data)
    {
        employee = data;

        nameText.text = data.employeeName;
        salaryText.text = $"₱{data.GetSalary()}/Day";

        for (int i = 0; i < starImages.Length; i++)
        {
            starImages[i].sprite = (i < data.stars) ? filledStar : emptyStar;
        }
    }

    public void SelectCard()
    {
        hrManager.SelectEmployee(employee);
        Debug.Log($"Selected {employee.employeeName}");
    }

    public void Refresh()
    {
        salaryText.text = $"${employee.GetSalary()}";
    }
}