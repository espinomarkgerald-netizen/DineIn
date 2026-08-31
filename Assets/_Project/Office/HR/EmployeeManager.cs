using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class EmployeeManager : MonoBehaviour
{
    public static EmployeeManager Instance { get; private set; }
    public event Action AssignmentsChanged;
    public event Action ApplicantsRefreshed;
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
    [SerializeField, Min(1)] private int applicantNextRefreshDay = 3;
    [SerializeField, Min(1)] private int applicantRefreshIntervalDays = 2;
    [SerializeField, Min(1)] private int applicantMinimumAvailabilityDays = 1;
    [SerializeField, Min(1)] private int applicantMaximumAvailabilityDays = 2;

    private bool applicantPoolsInitialized;
    private int applicantLastProcessedDay;
    private bool applicantsUnseen;

    public int MaxHiredPerRole => maxHiredPerRole;
    public int ApplicantNextRefreshDay => applicantNextRefreshDay;
    public bool HasUnseenApplicants => applicantsUnseen;

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
        for (int i = 0; i < allEmployees.Count; i++)
            allEmployees[i]?.EnsureIdentity();
        int currentDay = GameFlowManager.Instance != null
            ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
            : 1;
        AssignMissingApplicantExpiryDays(currentDay);
        applicantNextRefreshDay = currentDay + Mathf.Max(1, applicantRefreshIntervalDays);
        applicantLastProcessedDay = currentDay;
        applicantsUnseen = true;
        applicantPoolsInitialized = true;

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
        for (int i = 0; i < allEmployees.Count; i++)
            allEmployees[i]?.EnsureIdentity();

        int day = GameFlowManager.Instance != null
            ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
            : 1;

        if (!applicantPoolsInitialized)
        {
            EnsureApplicantPools(day);
            applicantPoolsInitialized = true;
        }

        RefreshApplicantsIfDue(day, applicantRefreshIntervalDays);
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
        AssignmentsChanged?.Invoke();
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
        employee.applicantAvailableUntilDay = 0;
        if (GetHiredCount(employee.role) == 1 && GetAssignedEmployee(employee.role) == null)
            AssignEmployeeForDay(employee);

        if (EnsureApplicantPool(employee.role, CurrentDay()))
            NotifyNewApplicants();

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

        AssignmentsChanged?.Invoke();
        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public bool DeclineApplicant(EmployeeData employee)
    {
        if (employee == null || employee.hired || SlotsLocked)
            return false;

        EmployeeRole role = employee.role;
        RemoveEmployee(employee);
        if (EnsureApplicantPool(role, CurrentDay()))
            NotifyNewApplicants();
        AssignmentsChanged?.Invoke();
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
        AssignmentsChanged?.Invoke();
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

    /// <summary>
    /// The restaurant can only open when every role exposed by the current HR
    /// board has one scheduled employee. Keeping this rule here gives the
    /// checklist and the authoritative shift controller the same answer.
    /// </summary>
    public bool HasAllRequiredRolesAssigned
    {
        get
        {
            IReadOnlyList<EmployeeRole> lobbyRoles = EmployeeRoleCatalog.LobbyRoles;
            for (int i = 0; i < lobbyRoles.Count; i++)
            {
                if (GetAssignedEmployee(lobbyRoles[i]) == null)
                    return false;
            }

            IReadOnlyList<EmployeeRole> kitchenRoles = EmployeeRoleCatalog.KitchenRoles;
            for (int i = 0; i < kitchenRoles.Count; i++)
            {
                if (GetAssignedEmployee(kitchenRoles[i]) == null)
                    return false;
            }

            return true;
        }
    }

    public List<EmployeeRole> GetMissingRequiredRoles()
    {
        List<EmployeeRole> missing = new List<EmployeeRole>();
        AddMissingRoles(EmployeeRoleCatalog.LobbyRoles, missing);
        AddMissingRoles(EmployeeRoleCatalog.KitchenRoles, missing);
        return missing;
    }

    private void AddMissingRoles(
        IReadOnlyList<EmployeeRole> requiredRoles,
        List<EmployeeRole> missing)
    {
        for (int i = 0; i < requiredRoles.Count; i++)
        {
            EmployeeRole role = requiredRoles[i];
            if (GetAssignedEmployee(role) == null)
                missing.Add(role);
        }
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.employees.Clear();
        data.employeeApplicantNextRefreshDay = Mathf.Max(1, applicantNextRefreshDay);
        data.employeeApplicantLastProcessedDay = Mathf.Max(0, applicantLastProcessedDay);
        data.employeeApplicantsUnseen = applicantsUnseen;
        foreach (EmployeeData employee in allEmployees)
        {
            if (employee == null)
                continue;

            data.employees.Add(new EmployeeSaveEntry
            {
                employeeID = employee.EmployeeID,
                employeeName = employee.employeeName,
                stars = employee.stars,
                role = employee.role,
                assigned = employee.assigned,
                hired = employee.hired,
                applicantAvailableUntilDay = employee.applicantAvailableUntilDay,
                speed = employee.speed,
                accuracy = employee.accuracy,
                reliability = employee.reliability,
                useManualSalary = employee.useManualSalary,
                manualSalary = employee.manualSalary,
                performanceMultiplier = employee.performanceMultiplier,
                bonusFlat = employee.bonusFlat,
                experience = employee.experience,
                roleExperience = employee.roleExperience,
                daysEmployed = employee.daysEmployed,
                daysWorked = employee.daysWorked,
                recentPerformance = employee.recentPerformance,
                previousPerformance = employee.previousPerformance,
                traitID = employee.traitID,
                lastPromotionDay = employee.lastPromotionDay
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
                applicantAvailableUntilDay = Mathf.Max(0, entry.applicantAvailableUntilDay),
                speed = entry.speed > 0 ? Mathf.Clamp(entry.speed, 50, 200) : 100,
                accuracy = entry.accuracy > 0 ? Mathf.Clamp(entry.accuracy, 50, 100) : 80,
                reliability = entry.reliability > 0 ? Mathf.Clamp(entry.reliability, 50, 100) : 80,
                assignedSlotName = entry.assigned ? migratedRole.ToString() : string.Empty,
                useManualSalary = entry.useManualSalary,
                manualSalary = entry.manualSalary,
                performanceMultiplier = entry.performanceMultiplier <= 0f ? 1f : entry.performanceMultiplier,
                bonusFlat = entry.bonusFlat,
                experience = Mathf.Max(0, entry.experience),
                roleExperience = Mathf.Max(0, entry.roleExperience),
                daysEmployed = Mathf.Max(0, entry.daysEmployed),
                daysWorked = Mathf.Max(0, entry.daysWorked),
                recentPerformance = entry.recentPerformance > 0
                    ? Mathf.Clamp(entry.recentPerformance, 0, 100)
                    : 75,
                previousPerformance = entry.previousPerformance > 0
                    ? Mathf.Clamp(entry.previousPerformance, 0, 100)
                    : 75,
                traitID = entry.traitID,
                lastPromotionDay = Mathf.Max(0, entry.lastPromotionDay)
            };
            employee.RestoreIdentity(entry.employeeID);
            allEmployees.Add(employee);
        }

        int loadedDay = Mathf.Max(1, data.currentDay);
        int latestAllowedRefresh = loadedDay + Mathf.Max(1, applicantRefreshIntervalDays);
        applicantNextRefreshDay = data.employeeApplicantNextRefreshDay > 0
            ? Mathf.Min(data.employeeApplicantNextRefreshDay, latestAllowedRefresh)
            : latestAllowedRefresh;
        applicantLastProcessedDay = Mathf.Max(0, data.employeeApplicantLastProcessedDay);
        applicantsUnseen = data.employeeApplicantsUnseen;
        applicantPoolsInitialized = HasApplicantForEverySupportedRole();
        RebuildRoleGroups();
        if (!applicantPoolsInitialized)
        {
            EnsureApplicantPools(loadedDay);
            applicantPoolsInitialized = true;
        }

        AssignMissingApplicantExpiryDays(loadedDay);
        AutoAssignSoleHires();

        AssignmentsChanged?.Invoke();
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

        AssignmentsChanged?.Invoke();
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

        AutoAssignSoleHires();

        AssignmentsChanged?.Invoke();
    }

    /// <summary>
    /// Removes pointless daily setup when a role has only one possible worker.
    /// Roles with multiple hires remain a real management choice.
    /// </summary>
    public void AutoAssignSoleHires()
    {
        foreach (EmployeeRole role in EmployeeRoleCatalog.LobbyRoles)
            AutoAssignSoleHire(role);
        foreach (EmployeeRole role in EmployeeRoleCatalog.KitchenRoles)
            AutoAssignSoleHire(role);
    }

    private void AutoAssignSoleHire(EmployeeRole role)
    {
        EmployeeData onlyHire = null;
        int count = 0;
        foreach (EmployeeData employee in allEmployees)
        {
            if (employee == null || !employee.hired || employee.role != role)
                continue;

            onlyHire = employee;
            count++;
            if (count > 1)
                return;
        }

        if (count == 1 && onlyHire != null && GetAssignedEmployee(role) == null)
        {
            onlyHire.assigned = true;
            onlyHire.assignedSlotName = role.ToString();
        }
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

    public void RefreshApplicantsIfDue(int currentDay, int refreshIntervalDays)
    {
        currentDay = Mathf.Max(1, currentDay);
        refreshIntervalDays = Mathf.Max(1, refreshIntervalDays);
        if (allEmployees == null || allEmployees.Count == 0 || generator == null)
            return;

        if (applicantNextRefreshDay <= 0)
            applicantNextRefreshDay = currentDay + refreshIntervalDays;

        // PrepareDay can call this more than once. Applicant expiry and the full
        // cohort roll must only happen once per in-game morning.
        if (applicantLastProcessedDay == currentDay)
            return;

        bool fullRefresh = currentDay >= applicantNextRefreshDay;
        int removed = RemoveExpiredApplicants(currentDay, fullRefresh);
        applicantPoolsInitialized = false;
        int added = EnsureApplicantPools(currentDay);
        applicantPoolsInitialized = true;
        applicantLastProcessedDay = currentDay;
        if (fullRefresh)
            applicantNextRefreshDay = currentDay + refreshIntervalDays;

        if (removed > 0 || added > 0)
            NotifyNewApplicants();

        GameSaveManager.Instance?.RequestSave();
    }

    public void MarkApplicantsSeen()
    {
        if (!applicantsUnseen)
            return;

        applicantsUnseen = false;
        ApplicantsRefreshed?.Invoke();
        GameSaveManager.Instance?.RequestSave();
    }

    public void ApplyDailyProgression(
        DailyRestaurantSnapshotSaveData snapshot,
        CasualDiningPolishSettings settings)
    {
        if (snapshot == null || settings == null || allEmployees == null)
            return;

        for (int i = 0; i < allEmployees.Count; i++)
        {
            EmployeeData employee = allEmployees[i];
            if (employee == null || !employee.hired)
                continue;

            employee.EnsureIdentity();
            employee.daysEmployed++;
            if (!employee.assigned)
                continue;

            employee.daysWorked++;
            employee.previousPerformance = employee.recentPerformance;
            employee.recentPerformance = CalculateRolePerformance(employee.role, snapshot);
            int earned = Mathf.Max(1,
                settings.baseExperiencePerShift + employee.recentPerformance / 10);
            if (employee.traitID == "fast-learner")
                earned = Mathf.CeilToInt(earned * 1.15f);
            employee.experience += earned;
            employee.roleExperience += earned;

            while (employee.stars < 5)
            {
                int threshold = Mathf.Max(10,
                    settings.firstPromotionExperience +
                    Mathf.Max(0, employee.stars - 1) * settings.promotionExperienceGrowth);
                if (employee.experience < threshold)
                    break;

                employee.experience -= threshold;
                employee.stars++;
                employee.lastPromotionDay = snapshot.day;
                ApplyPromotionStats(employee, settings.statPointsPerPromotion);
            }
        }
    }

    private static int CalculateRolePerformance(
        EmployeeRole role,
        DailyRestaurantSnapshotSaveData snapshot)
    {
        int arrivals = Mathf.Max(1, snapshot.groupsArrived);
        int orders = Mathf.Max(1, snapshot.ordersCompleted + snapshot.ordersFailed);
        float score = role switch
        {
            EmployeeRole.Host =>
                snapshot.groupsSeated * 100f / arrivals -
                snapshot.unaccommodated * 12f - snapshot.waitedTooLong * 5f,
            EmployeeRole.Waiter =>
                snapshot.ordersCompleted * 100f / orders -
                snapshot.wrongOrders * 15f - snapshot.orderFailures * 4f,
            EmployeeRole.Cashier =>
                95f - snapshot.paymentErrors * 20f,
            EmployeeRole.Busser =>
                90f - snapshot.dirtyTableDelays * 18f,
            EmployeeRole.Chef =>
                snapshot.ordersCompleted * 100f / orders -
                snapshot.orderFailures * 7f - snapshot.stockoutRefusals * 3f,
            EmployeeRole.Barista =>
                snapshot.ordersCompleted * 100f / orders -
                snapshot.orderFailures * 6f - snapshot.wrongOrders * 5f,
            _ => 75f
        };
        return Mathf.Clamp(Mathf.RoundToInt(score), 35, 100);
    }

    private static void ApplyPromotionStats(EmployeeData employee, int points)
    {
        points = Mathf.Clamp(points, 0, 10);
        employee.EnsureTrait();
        switch (employee.traitID)
        {
            case "fast-worker":
                employee.speed = Mathf.Clamp(employee.speed + points * 2, 50, 200);
                employee.reliability = Mathf.Clamp(employee.reliability + points, 50, 100);
                break;
            case "careful":
                employee.accuracy = Mathf.Clamp(employee.accuracy + points, 50, 100);
                employee.speed = Mathf.Clamp(employee.speed + points, 50, 200);
                break;
            case "dependable":
                employee.reliability = Mathf.Clamp(employee.reliability + points, 50, 100);
                employee.accuracy = Mathf.Clamp(employee.accuracy + points, 50, 100);
                break;
            default:
                employee.speed = Mathf.Clamp(employee.speed + points, 50, 200);
                employee.accuracy = Mathf.Clamp(employee.accuracy + points, 50, 100);
                employee.reliability = Mathf.Clamp(employee.reliability + points, 50, 100);
                break;
        }

        employee.performanceMultiplier = Mathf.Clamp(
            employee.performanceMultiplier + 0.025f,
            0.85f,
            1.35f);
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

    private int EnsureApplicantPools(int currentDay)
    {
        int added = 0;
        foreach (EmployeeRole role in EmployeeRoleCatalog.LobbyRoles)
            added += EnsureApplicantPool(role, currentDay) ? 1 : 0;
        foreach (EmployeeRole role in EmployeeRoleCatalog.KitchenRoles)
            added += EnsureApplicantPool(role, currentDay) ? 1 : 0;
        return added;
    }

    private bool HasApplicantForEverySupportedRole()
    {
        foreach (EmployeeRole role in EmployeeRoleCatalog.LobbyRoles)
        {
            if (!allEmployees.Exists(employee =>
                    employee != null && !employee.hired && employee.role == role))
                return false;
        }
        foreach (EmployeeRole role in EmployeeRoleCatalog.KitchenRoles)
        {
            if (!allEmployees.Exists(employee =>
                    employee != null && !employee.hired && employee.role == role))
                return false;
        }
        return true;
    }

    private bool EnsureApplicantPool(EmployeeRole role, int currentDay)
    {
        if (generator == null)
            return false;

        int applicantCount = 0;
        bool generatedAny = false;
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
            generated.EnsureIdentity();
            generated.applicantAvailableUntilDay = PickApplicantExpiryDay(currentDay);
            if (!allEmployees.Contains(generated))
                allEmployees.Add(generated);
            applicantCount++;
            generatedAny = true;
        }

        RebuildRoleGroups();
        return generatedAny;
    }

    private int RemoveExpiredApplicants(int currentDay, bool removeAll)
    {
        int removed = 0;
        for (int i = allEmployees.Count - 1; i >= 0; i--)
        {
            EmployeeData employee = allEmployees[i];
            if (employee == null || employee.hired)
                continue;

            bool expired = employee.applicantAvailableUntilDay > 0 &&
                           currentDay > employee.applicantAvailableUntilDay;
            if (!removeAll && !expired)
                continue;

            allEmployees.RemoveAt(i);
            generator?.employees.Remove(employee);
            removed++;
        }

        if (removed > 0)
            RebuildRoleGroups();
        return removed;
    }

    private void AssignMissingApplicantExpiryDays(int currentDay)
    {
        foreach (EmployeeData employee in allEmployees)
        {
            if (employee != null && !employee.hired && employee.applicantAvailableUntilDay <= 0)
                employee.applicantAvailableUntilDay = PickApplicantExpiryDay(currentDay);
        }
    }

    private int PickApplicantExpiryDay(int currentDay)
    {
        int minimum = Mathf.Max(1, applicantMinimumAvailabilityDays);
        int maximum = Mathf.Max(minimum, applicantMaximumAvailabilityDays);
        return Mathf.Max(1, currentDay) + UnityEngine.Random.Range(minimum, maximum + 1) - 1;
    }

    private void NotifyNewApplicants()
    {
        applicantsUnseen = true;
        ApplicantsRefreshed?.Invoke();
    }

    private static int CurrentDay() => GameFlowManager.Instance != null
        ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
        : 1;

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
        applicantPoolsInitialized = false;
        applicantNextRefreshDay = 3;
        applicantLastProcessedDay = 0;
        applicantsUnseen = false;

        RoleSlot[] allSlots = FindObjectsByType<RoleSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var slot in allSlots)
            slot.ResetForNewDay();
    }
}
