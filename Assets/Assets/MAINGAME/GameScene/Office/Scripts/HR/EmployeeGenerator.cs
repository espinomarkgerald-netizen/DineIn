using System.Collections.Generic;
using UnityEngine;

public class EmployeeGenerator : MonoBehaviour
{
    public List<EmployeeData> employees = new List<EmployeeData>();

    string[] names =
    {
        "Alex","Maria","Ken","Lina","Josh",
        "Sam","Dina","Leo","Nina","Mark"
    };

    public int employeesPerDay = 5;

    void Start()
    {
        GenerateEmployees();
    }

    void GenerateEmployees()
    {
        employees.Clear();

        for(int i = 0; i < employeesPerDay; i++)
        {
            string randomName = names[Random.Range(0, names.Length)];
            int cooking = Random.Range(1, 11);
            int service = Random.Range(1, 11);

            EmployeeData newEmployee =
                new EmployeeData(randomName, cooking, service);

            employees.Add(newEmployee);
        }
    }
}