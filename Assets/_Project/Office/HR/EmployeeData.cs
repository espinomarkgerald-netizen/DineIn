using UnityEngine;

[System.Serializable]
public class EmployeeData
{
    [SerializeField, HideInInspector] private string employeeID;
    public string employeeName;
    public int stars; // 1–5
    public EmployeeRole role;

    [Header("Employment")]
    public bool hired;

    [Header("Work Profile")]
    [Range(50, 200)] public int speed = 100;
    [Range(50, 100)] public int accuracy = 80;
    [Range(50, 100)] public int reliability = 80;

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

    [Header("Progression")]
    [Min(0)] public int experience;
    [Min(0)] public int roleExperience;
    [Min(0)] public int daysEmployed;
    [Min(0)] public int daysWorked;
    [Range(0, 100)] public int recentPerformance = 75;
    [Range(0, 100)] public int previousPerformance = 75;
    public string traitID;
    [Min(0)] public int lastPromotionDay;

    public string EmployeeID
    {
        get
        {
            EnsureIdentity();
            return employeeID;
        }
    }

    public EmployeeData(string name, int starRating, EmployeeRole roleType)
    {
        employeeID = System.Guid.NewGuid().ToString("N");
        employeeName = name;
        stars = starRating;
        role = roleType;
        assigned = false;
        hired = false;
    }

    public void RestoreIdentity(string savedID)
    {
        employeeID = string.IsNullOrWhiteSpace(savedID)
            ? System.Guid.NewGuid().ToString("N")
            : savedID.Trim();
        EnsureTrait();
    }

    public void EnsureIdentity()
    {
        if (string.IsNullOrWhiteSpace(employeeID))
            employeeID = System.Guid.NewGuid().ToString("N");
        EnsureTrait();
    }

    public void EnsureTrait()
    {
        if (!string.IsNullOrWhiteSpace(traitID))
            return;
        if (speed >= accuracy + 18 && speed >= 115)
            traitID = "fast-worker";
        else if (accuracy >= reliability && accuracy >= 86)
            traitID = "careful";
        else if (reliability >= 86)
            traitID = "dependable";
        else
            traitID = "fast-learner";
    }

    public string GetTraitLabel()
    {
        EnsureTrait();
        return traitID switch
        {
            "fast-worker" => "Fast Worker",
            "careful" => "Careful",
            "dependable" => "Dependable",
            _ => "Fast Learner"
        };
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

    public string GetPrimaryPro()
    {
        EnsureTrait();
        if (traitID == "fast-worker") return "Fast worker";
        if (traitID == "careful") return "Very accurate";
        if (traitID == "dependable") return "Highly reliable";
        if (traitID == "fast-learner") return "Learns quickly";
        if (speed >= accuracy + 20 && speed >= 120) return "Fast worker";
        if (accuracy >= reliability && accuracy >= 88) return "Very accurate";
        if (reliability >= 88) return "Highly reliable";
        if (stars >= 4) return "Experienced";
        return "Affordable wage";
    }

    public string GetPrimaryCon()
    {
        if (speed < 90) return "Slower pace";
        if (accuracy < 75) return "More mistakes";
        if (reliability < 75) return "Less consistent";
        if (stars <= 2) return "Needs training";
        return "Higher wage";
    }
}
