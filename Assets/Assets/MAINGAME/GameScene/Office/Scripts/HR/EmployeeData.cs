using UnityEngine;

[System.Serializable]
public class EmployeeData
{
    public string employeeName;
    public int stars; // 1–5
    public EmployeeRole role;
    public RoleSlot currentSlot;
    [HideInInspector] public RoleSlot assignedSlot; // reference
    public string assignedSlotName; // inspector-visible
    public bool IsAssigned => assignedSlot != null;

    public bool assigned;

    public EmployeeData(string name, int starRating, EmployeeRole roleType)
    {
        employeeName = name;
        stars = starRating;
        role = roleType;
        assigned = false;
    }
}