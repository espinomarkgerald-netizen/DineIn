using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class RoleGroup
{
    public EmployeeRole role;
    public List<EmployeeData> employees = new List<EmployeeData>();
}