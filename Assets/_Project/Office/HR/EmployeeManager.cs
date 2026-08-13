using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class EmployeeManager : MonoBehaviour
{
    public static EmployeeManager Instance { get; private set; }
    public EmployeeGenerator generator;

    [Header("Salary")]
    public SalaryConfig salaryConfig;

    [Header("All Employees")]
    public List<EmployeeData> allEmployees = new List<EmployeeData>();

    [Header("Employees Grouped by Role")]
    public List<RoleGroup> employeesByRole = new List<RoleGroup>();

    [Header("HR Roster")]
    [SerializeField, Min(1)] private int maxHiredPerRole = 3;
    [SerializeField, Min(1)] private int targetApplicantsPerRole = 3;

    public int MaxHiredPerRole => maxHiredPerRole;

    /// <summary>True once the lobby shift starts; prevents reassignment for the rest of the day.</summary>
    public bool SlotsLocked { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize all roles
        employeesByRole.Clear();
        foreach (EmployeeRole role in Enum.GetValues(typeof(EmployeeRole)))
            employeesByRole.Add(new RoleGroup { role = role });
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (generator == null)
        {
            generator = FindObjectOfType<EmployeeGenerator>();
        }
    }

    public void GenerateEmployees()
    {
        if (generator == null)
        {
            Debug.LogError("EmployeeManager: generator not assigned!");
            return;
        }

        generator.GenerateEmployees();
        allEmployees = generator.employees;

        // Clear role groups
        foreach (var group in employeesByRole)
            group.employees.Clear();

        // Assign employees to role groups
        foreach (var emp in allEmployees)
        {
            var group = employeesByRole.Find(g => g.role == emp.role);
            if (group != null)
                group.employees.Add(emp);
        }
    }

    public void EnsureEmployeesGenerated()
    {
        if (allEmployees == null || allEmployees.Count == 0)
            GenerateEmployees();

        MigrateLegacyKitchenRoles();
        EnsureApplicantPools();
    }

    /// <summary>Assigns one employee to their role for the coming shift.</summary>
    public bool AssignEmployeeForDay(EmployeeData employee)
    {
        if (employee == null || !employee.hired || SlotsLocked || !EmployeeRoleCatalog.IsSupported(employee.role))
            return false;

        foreach (EmployeeData candidate in allEmployees)
        {
            if (candidate != null && candidate.role == employee.role)
                candidate.assigned = false;
        }

        employee.assigned = true;
        employee.assignedSlotName = employee.role.ToString();
        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public bool HireApplicant(EmployeeData employee)
    {
        if (employee == null || employee.hired || SlotsLocked ||
            !EmployeeRoleCatalog.IsSupported(employee.role) ||
            GetHiredCount(employee.role) >= maxHiredPerRole)
            return false;

        employee.hired = true;
        if (GetAssignedEmployee(employee.role) == null)
            AssignEmployeeForDay(employee);

        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public bool FireEmployee(EmployeeData employee)
    {
        if (employee == null || !employee.hired || SlotsLocked)
            return false;

        EmployeeRole role = employee.role;
        bool wasAssigned = employee.assigned;
        RemoveEmployee(employee);

        if (wasAssigned)
        {
            EmployeeData replacement = allEmployees.Find(candidate =>
                candidate != null && candidate.hired && candidate.role == role);
            if (replacement != null)
                AssignEmployeeForDay(replacement);
        }

        EnsureApplicantPool(role);
        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public bool DeclineApplicant(EmployeeData employee)
    {
        if (employee == null || employee.hired || SlotsLocked)
            return false;

        EmployeeRole role = employee.role;
        RemoveEmployee(employee);
        EnsureApplicantPool(role);
        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public int GetHiredCount(EmployeeRole role)
    {
        int count = 0;
        foreach (EmployeeData employee in allEmployees)
        {
            if (employee != null && employee.hired && employee.role == role)
                count++;
        }
        return count;
    }

    public EmployeeData GetAssignedEmployee(EmployeeRole role) =>
        allEmployees.Find(employee => employee != null && employee.assigned && employee.role == role);

    public bool UnassignEmployeeForDay(EmployeeData employee)
    {
        if (employee == null || SlotsLocked)
            return false;

        employee.assigned = false;
        employee.assignedSlot = null;
        employee.assignedSlotName = string.Empty;
        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public int AssignedEmployeeCount
    {
        get
        {
            int count = 0;
            foreach (EmployeeData employee in allEmployees)
            {
                if (employee != null && employee.assigned)
                    count++;
            }
            return count;
        }
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.employees.Clear();
        foreach (EmployeeData employee in allEmployees)
        {
            if (employee == null)
                continue;

            data.employees.Add(new EmployeeSaveEntry
            {
                employeeName = employee.employeeName,
                stars = employee.stars,
                role = employee.role,
                assigned = employee.assigned,
                hired = employee.hired,
                speed = employee.speed,
                accuracy = employee.accuracy,
                reliability = employee.reliability,
                useManualSalary = employee.useManualSalary,
                manualSalary = employee.manualSalary,
                performanceMultiplier = employee.performanceMultiplier,
                bonusFlat = employee.bonusFlat
            });
        }
    }

    public void ApplySaveData(GameSaveData data)
    {
        if (data?.employees == null || data.employees.Count == 0)
            return;

        allEmployees.Clear();
        foreach (EmployeeSaveEntry entry in data.employees)
        {
            EmployeeRole migratedRole = EmployeeRoleCatalog.MigrateLegacyRole(entry.role);
            EmployeeData employee = new EmployeeData(entry.employeeName, entry.stars, migratedRole)
            {
                assigned = entry.assigned,
                hired = entry.hired || entry.assigned,
                speed = entry.speed > 0 ? Mathf.Clamp(entry.speed, 50, 200) : 100,
                accuracy = entry.accuracy > 0 ? Mathf.Clamp(entry.accuracy, 50, 100) : 80,
                reliability = entry.reliability > 0 ? Mathf.Clamp(entry.reliability, 50, 100) : 80,
                assignedSlotName = entry.assigned ? migratedRole.ToString() : string.Empty,
                useManualSalary = entry.useManualSalary,
                manualSalary = entry.manualSalary,
                performanceMultiplier = entry.performanceMultiplier <= 0f ? 1f : entry.performanceMultiplier,
                bonusFlat = entry.bonusFlat
            };
            allEmployees.Add(employee);
        }

        RebuildRoleGroups();
        EnsureApplicantPools();
    }

    private void RebuildRoleGroups()
    {
        foreach (RoleGroup group in employeesByRole)
            group.employees.Clear();

        foreach (EmployeeData employee in allEmployees)
        {
            RoleGroup group = employeesByRole.Find(candidate => candidate.role == employee.role);
            if (group != null)
                group.employees.Add(employee);
        }
    }

    public void AssignEmployee(EmployeeData employee, RoleSlot slot)
    {
        if (employee.role != slot.roleType)
        {
            Debug.Log("Role mismatch");
            return;
        }

        if (!slot.AssignEmployee(employee)) return;

        employee.hired = true;
        employee.assignedSlot     = slot;
        employee.assignedSlotName = slot.name;

        var group = employeesByRole.Find(g => g.role == slot.roleType);
        if (group != null && !group.employees.Contains(employee))
            group.employees.Add(employee);
    }

    /// <summary>
    /// Locks all role slots so no reassignment can happen for the rest of the day.
    /// Call this when the lobby shift starts.
    /// </summary>
    public void LockAllSlots()
    {
        SlotsLocked = true;

        // Lock any slots already loaded in the scene.
        RoleSlot[] allSlots = FindObjectsByType<RoleSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var slot in allSlots)
            slot.Lock();
    }

    /// <summary>Clears all daily assignments and slot locks. Call at the start of each new day.</summary>
    public void ResetDailyAssignments()
    {
        SlotsLocked = false;

        foreach (var emp in allEmployees)
        {
            emp.assigned         = false;
            emp.assignedSlot     = null;
            emp.assignedSlotName = string.Empty;
            emp.currentSlot      = null;
        }

        RoleSlot[] allSlots = FindObjectsByType<RoleSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var slot in allSlots)
            slot.ResetForNewDay();
    }

    public int CalculateTotalPayroll()
    {
        if (salaryConfig == null)
        {
            Debug.LogError("EmployeeManager: salaryConfig is not assigned!");
            return 0;
        }

        int total = 0;

        foreach (var emp in allEmployees)
        {
            if (emp.assigned)
                total += emp.GetSalary(salaryConfig);
        }

        return total;
    }

    private void MigrateLegacyKitchenRoles()
    {
        bool changed = false;
        foreach (EmployeeData employee in allEmployees)
        {
            if (employee == null)
                continue;

            EmployeeRole migrated = EmployeeRoleCatalog.MigrateLegacyRole(employee.role);
            if (migrated == employee.role)
                continue;

            employee.role = migrated;
            employee.assignedSlotName = employee.assigned ? migrated.ToString() : string.Empty;
            changed = true;
        }

        if (changed)
            RebuildRoleGroups();
    }

    private void EnsureApplicantPools()
    {
        foreach (EmployeeRole role in EmployeeRoleCatalog.LobbyRoles)
            EnsureApplicantPool(role);
        foreach (EmployeeRole role in EmployeeRoleCatalog.KitchenRoles)
            EnsureApplicantPool(role);
    }

    private void EnsureApplicantPool(EmployeeRole role)
    {
        if (generator == null)
            return;

        int applicantCount = 0;
        foreach (EmployeeData employee in allEmployees)
        {
            if (employee != null && !employee.hired && employee.role == role)
                applicantCount++;
        }

        while (applicantCount < targetApplicantsPerRole)
        {
            List<string> names = new List<string>();
            foreach (EmployeeData employee in allEmployees)
            {
                if (employee != null && !string.IsNullOrWhiteSpace(employee.employeeName))
                    names.Add(employee.employeeName);
            }

            EmployeeData generated = generator.GenerateApplicant(role, names);
            if (!allEmployees.Contains(generated))
                allEmployees.Add(generated);
            applicantCount++;
        }

        RebuildRoleGroups();
    }

    private void RemoveEmployee(EmployeeData employee)
    {
        employee.assigned = false;
        employee.assignedSlot = null;
        employee.assignedSlotName = string.Empty;
        employee.currentSlot = null;
        allEmployees.Remove(employee);
        generator?.employees.Remove(employee);
        RebuildRoleGroups();
    }

    /// <summary>
    /// Wipes all generated employees and clears every role group. Call on a full
    /// run reset so the player hires fresh staff from Day 1.
    /// </summary>
    public void ClearAllEmployees()
    {
        allEmployees.Clear();

        foreach (var group in employeesByRole)
            group.employees.Clear();

        SlotsLocked = false;

        RoleSlot[] allSlots = FindObjectsByType<RoleSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var slot in allSlots)
            slot.ResetForNewDay();
    }
}
