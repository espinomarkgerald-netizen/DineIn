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

    /// <summary>Calculates the employee's salary using the provided config. Falls back to manualSalary if toggled.</summary>
    public int GetSalary(SalaryConfig config)
    {
        if (useManualSalary)
            return manualSalary;

        int baseSalary = config.GetBaseSalary(role);
        int starValue = stars * config.salaryPerStar;

        float total = (baseSalary + starValue) * performanceMultiplier + bonusFlat;

        return Mathf.RoundToInt(total);
    }
}