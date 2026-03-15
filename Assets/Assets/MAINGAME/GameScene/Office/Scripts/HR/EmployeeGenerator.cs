using System.Collections.Generic;
using UnityEngine;

public class EmployeeGenerator : MonoBehaviour
{
    public List<EmployeeData> employees = new List<EmployeeData>();

    string[] names =
    {
        "Maria","Kelvin","Josh","Nina",
        "Sam","Leo","Kyle","Mark","Michael",
        "Ron", "Doyle","Johnvic","Mary","Grace",
        "Paul", "Kenneth", "Bandoc", "Fumi", "Riley",
        "Neo", "Tom", "Hasang", "Tachu", "Floribel", "Ferrer",
        "Montefaro", "Miguel", "Byron", "Darnell", "Noel",
        "Christian", "Joseph", "Namuag"
    };

    public int employeesPerDay = 12;

    public void GenerateEmployees()
    {
        employees.Clear();

        List<string> usedNames = new List<string>();

        for(int i = 0; i < employeesPerDay; i++)
        {
            string name;

            do
            {
                name = names[Random.Range(0, names.Length)];
            }
            while(usedNames.Contains(name));

            usedNames.Add(name);

            int cooking = Random.Range(1,11);
            int service = Random.Range(1,11);

            EmployeeData newEmployee = new EmployeeData(name, cooking, service);

            employees.Add(newEmployee);
        }
    }
}