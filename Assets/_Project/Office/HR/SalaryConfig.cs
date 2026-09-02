using System;
using UnityEngine;

public enum RestaurantSalaryTier
{
    FastFood,
    CasualDining,
    FineDining
}

/// <summary>
/// ScriptableObject that drives all salary calculations.
/// Create via: Assets > Create > HR > Salary Config
/// </summary>
[CreateAssetMenu(fileName = "SalaryConfig", menuName = "HR/Salary Config")]
public class SalaryConfig : ScriptableObject
{
    [Serializable]
    public struct RoleSalaryEntry
    {
        public EmployeeRole role;

        [Tooltip("Daily base salary for this role before star and performance modifiers.")]
        public int baseSalary;
    }

    [Header("Base Salary Per Role")]
    public RoleSalaryEntry[] roleEntries;

    [Header("Star Rating")]
    [Tooltip("Flat salary added per star rating point.")]
    public int salaryPerStar = 20;

    [Header("Restaurant Salary Band")]
    [Tooltip("Generated staff salaries stay inside the selected restaurant's daily range.")]
    public RestaurantSalaryTier restaurantTier = RestaurantSalaryTier.CasualDining;

    /// <summary>Returns the configured base salary for the given role. Falls back to 0 if unconfigured.</summary>
    public int GetBaseSalary(EmployeeRole role)
    {
        foreach (var entry in roleEntries)
        {
            if (entry.role == role)
                return entry.baseSalary;
        }

        Debug.LogWarning($"SalaryConfig: No entry found for role '{role}'. Returning 0.");
        return 0;
    }

    public int GetSalary(EmployeeData employee)
    {
        return GetSalaryForTier(employee, restaurantTier);
    }

    public static int GetSalaryForTier(EmployeeData employee, RestaurantSalaryTier tier)
    {
        if (employee == null)
            return 0;

        GetRange(tier, out int minimum, out int maximum);
        float stars = Mathf.InverseLerp(1f, 5f, Mathf.Clamp(employee.stars, 1, 5));
        float speed = Mathf.InverseLerp(70f, 150f, Mathf.Clamp(employee.speed, 50, 200));
        float accuracy = Mathf.InverseLerp(60f, 100f, Mathf.Clamp(employee.accuracy, 50, 100));
        float reliability = Mathf.InverseLerp(60f, 100f, Mathf.Clamp(employee.reliability, 50, 100));
        float stats = (speed + accuracy + reliability) / 3f;
        float quality = Mathf.Clamp01(stars * 0.65f + stats * 0.35f);

        float salary = Mathf.Lerp(minimum, maximum, quality);
        salary = salary * Mathf.Clamp(employee.performanceMultiplier, 0.9f, 1.1f) +
                 employee.bonusFlat;
        int rounded = Mathf.RoundToInt(salary / 10f) * 10;
        return Mathf.Clamp(rounded, minimum, maximum);
    }

    public static void GetRange(RestaurantSalaryTier tier, out int minimum, out int maximum)
    {
        switch (tier)
        {
            case RestaurantSalaryTier.FastFood:
                minimum = 350;
                maximum = 550;
                break;
            case RestaurantSalaryTier.FineDining:
                minimum = 700;
                maximum = 1000;
                break;
            default:
                minimum = 550;
                maximum = 700;
                break;
        }
    }
}
