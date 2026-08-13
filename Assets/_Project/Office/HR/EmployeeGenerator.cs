using System.Collections.Generic;
using UnityEngine;

public class EmployeeGenerator : MonoBehaviour
{
    public List<EmployeeData> employees = new List<EmployeeData>();

    [SerializeField] private string[] names = {"Maria","Kelvin","Josh","Nina","Sam","Leo","Kyle","Mark","Michael",
                                               "Ron","Johnvic","Mary",
                                               "Fumi","Riley","Neo","Tom","Tachu","Floribel",
                                               "Montefaro","Miguel","Noel","Christian",
                                               "Joseph","Aljon","Lucky"};

    [SerializeField] private int employeesPerRole = 3;
    [SerializeField] private int minStars = 1;
    [SerializeField] private int maxStars = 5;

    public void GenerateEmployees()
    {
        employees.Clear();

        List<string> usedNames = new List<string>();

        foreach (EmployeeRole role in EmployeeRoleCatalog.LobbyRoles)
            GenerateForRole(role, employeesPerRole, usedNames);

        foreach (EmployeeRole role in EmployeeRoleCatalog.KitchenRoles)
            GenerateForRole(role, employeesPerRole, usedNames);
    }

    public EmployeeData GenerateApplicant(EmployeeRole role, IEnumerable<string> unavailableNames)
    {
        List<string> usedNames = unavailableNames != null
            ? new List<string>(unavailableNames)
            : new List<string>();
        EmployeeData employee = CreateEmployee(role, usedNames);
        employees.Add(employee);
        return employee;
    }

    private void GenerateForRole(EmployeeRole role, int count, List<string> usedNames)
    {
        for (int i = 0; i < count; i++)
        {
            EmployeeData employee = CreateEmployee(role, usedNames);
            usedNames.Add(employee.employeeName);
            employees.Add(employee);
        }
    }

    private EmployeeData CreateEmployee(EmployeeRole role, List<string> usedNames)
    {
        List<int> starPool = BuildShuffledStarPool();
        int stars = starPool[Random.Range(0, starPool.Count)];
        EmployeeData employee = new EmployeeData(PickName(usedNames), stars, role)
        {
            hired = false,
            speed = Random.Range(72 + stars * 5, 106 + stars * 10),
            accuracy = Random.Range(60 + stars * 5, 76 + stars * 5),
            reliability = Random.Range(60 + stars * 5, 76 + stars * 5)
        };
        employee.speed = Mathf.Clamp(employee.speed, 50, 200);
        employee.accuracy = Mathf.Clamp(employee.accuracy, 50, 100);
        employee.reliability = Mathf.Clamp(employee.reliability, 50, 100);
        employee.performanceMultiplier = Mathf.Lerp(0.9f, 1.15f, (stars - 1f) / 4f);
        return employee;
    }

    /// <summary>Returns a shuffled list of star values between minStars and maxStars.</summary>
    private List<int> BuildShuffledStarPool()
    {
        List<int> pool = new List<int>();
        for (int s = minStars; s <= maxStars; s++)
            pool.Add(s);

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    /// <summary>Returns a unique name from the pool. Falls back to allowing duplicates if the pool is exhausted.</summary>
    private string PickName(List<string> usedNames)
    {
        if (usedNames.Count >= names.Length)
            return names[Random.Range(0, names.Length)];

        string name;
        do { name = names[Random.Range(0, names.Length)]; }
        while (usedNames.Contains(name));

        return name;
    }
}
