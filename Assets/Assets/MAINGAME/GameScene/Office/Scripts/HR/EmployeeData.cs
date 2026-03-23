public class EmployeeData
{
    public string employeeName;
    public int stars; // 1–5
    public EmployeeRole role;
    public RoleSlot currentSlot;

    public bool assigned;

    public EmployeeData(string name, int starRating, EmployeeRole roleType)
    {
        employeeName = name;
        stars = starRating;
        role = roleType;
        assigned = false;
    }
}