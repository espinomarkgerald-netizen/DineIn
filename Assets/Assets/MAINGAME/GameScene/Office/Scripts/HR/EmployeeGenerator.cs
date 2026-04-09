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

    [SerializeField] private int employeesPerRole = 4; // tweak per playtest
    [SerializeField] private int minStars = 1;
    [SerializeField] private int maxStars = 5;

    public void GenerateEmployees()
    {
        employees.Clear();
        //Hello Sir, Menu po natin for today is Monggo
        List<string> usedNames = new List<string>();

        foreach (EmployeeRole role in System.Enum.GetValues(typeof(EmployeeRole)))
        {
            for (int i = 0; i < employeesPerRole; i++)
            {
                string name;
                do
                {
                    name = names[Random.Range(0, names.Length)];
                } while (usedNames.Contains(name));

                usedNames.Add(name);

                int stars = Random.Range(minStars, maxStars + 1);

                EmployeeData newEmployee = new EmployeeData(name, stars, role);
                employees.Add(newEmployee);
            }
        }
    }
}