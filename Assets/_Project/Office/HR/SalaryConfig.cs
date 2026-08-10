using System;
using UnityEngine;

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
}
