using System.Collections.Generic;
using UnityEngine;

public class EmployeeGenerator : MonoBehaviour
{
    public List<EmployeeData> employees = new List<EmployeeData>();

    [SerializeField] private string[] names = { "Maria","Kelvin","Josh","Nina","Sam","Leo","Kyle","Mark","Michael",
                                               "Ron","Doyle","Johnvic","Mary","Paul","Bandoc",
                                               "Fumi","Riley","Neo","Tom","Hasang","Tachu","Floribel","Ferrer",
                                               "Montefaro","Miguel","Byron","Darnell","Noel","Christian",
                                               "Joseph","Namuag" };

    [SerializeField] private int employeesPerRole = 3;
    [SerializeField] private int minStars = 1;
    [SerializeField] private int maxStars = 5;

    public void GenerateEmployees()
    {
        employees.Clear();

        List<string> usedNames = new List<string>();

        foreach (EmployeeRole role in System.Enum.GetValues(typeof(EmployeeRole)))
        {
            List<int> starPool = BuildShuffledStarPool();

            for (int i = 0; i < employeesPerRole; i++)
            {
                string name = PickName(usedNames);
                usedNames.Add(name);

                int stars = starPool[i % starPool.Count];

                EmployeeData newEmployee = new EmployeeData(name, stars, role);
                employees.Add(newEmployee);
            }
        }
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