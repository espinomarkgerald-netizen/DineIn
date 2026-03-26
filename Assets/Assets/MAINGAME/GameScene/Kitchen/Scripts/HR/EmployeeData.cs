using UnityEngine;

[System.Serializable]
public class EmployeeData
{
    public string employeeName;
    public int stars; // 1–5
    public EmployeeRole role;

    public RoleSlot currentSlot;
    [HideInInspector] public RoleSlot assignedSlot;
    public string assignedSlotName;

    public bool assigned;

    [Header("Salary Settings")]
    public bool useManualSalary = false; // toggle
    public int manualSalary = 100;       // editable in Inspector

    [Header("Dynamic Modifiers")]
    public float performanceMultiplier = 1f;
    public int bonusFlat = 0;

    public EmployeeData(string name, int starRating, EmployeeRole roleType)
    {
        employeeName = name;
        stars = starRating;
        role = roleType;
        assigned = false;
    }

    public int GetSalary()
    {
        if (useManualSalary)
            return manualSalary;

        int baseSalary = GetBaseSalaryByRole();
        int starValue = stars * 20;

        float total = (baseSalary + starValue) * performanceMultiplier + bonusFlat;

        return Mathf.RoundToInt(total);
    }

    int GetBaseSalaryByRole()
    {
        switch (role)
        {
            case EmployeeRole.Chef: return 150 * 8;
            case EmployeeRole.Barista: return 130 * 8;
            case EmployeeRole.Cashier: return 110 * 8;
            case EmployeeRole.Waiter: return 100 * 8;
            case EmployeeRole.Host: return 90 * 8;
            case EmployeeRole.Busser: return 80 * 8;
            default: return 100;
        }
    }
}