using UnityEngine;

[System.Serializable]
public class EmployeeData
{
    public string employeeName;
    public int cooking;
    public int service;

    public bool assigned;
    public string assignedRole;

    public EmployeeData(string name, int cook, int serv)
    {
        employeeName = name;
        cooking = cook;
        service = serv;
        assigned = false;
        assignedRole = "";
    }
}