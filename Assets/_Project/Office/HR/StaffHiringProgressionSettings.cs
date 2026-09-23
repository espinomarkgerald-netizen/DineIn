using UnityEngine;

/// <summary>Shared campaign hiring milestones; extra hires are reserves unless a station supports them.</summary>
[CreateAssetMenu(menuName = "Dine In/Staff Hiring Progression")]
public sealed class StaffHiringProgressionSettings : ScriptableObject
{
    [Min(1)] public int secondLobbyHireDay = 10;
    [Min(1)] public int secondCashierHireDay = 15;
    private static StaffHiringProgressionSettings cached;
    public static StaffHiringProgressionSettings Current => cached != null ? cached :
        cached = Resources.Load<StaffHiringProgressionSettings>("StaffHiringProgression");
    public static int CurrentDay => GameFlowManager.Instance != null ?
        Mathf.Max(1, GameFlowManager.Instance.CurrentDay) : 1;
    public static int UnlockDay(EmployeeRole role) => role == EmployeeRole.Busser
        ? Mathf.Max(1, Current != null ? Current.secondLobbyHireDay : 10)
        : role == EmployeeRole.Cashier
            ? Mathf.Max(1, Current != null ? Current.secondCashierHireDay : 15) : 1;

    public static int Limit(EmployeeRole role, int normalLimit, int hiredCount)
    {
        if (CampaignSaveStore.ProtectedSession ||
            (role != EmployeeRole.Busser && role != EmployeeRole.Cashier)) return normalLimit;
        // Preserve existing staff without opening additional slots beyond the milestone cap.
        return Mathf.Max(hiredCount, CurrentDay >= UnlockDay(role) ? 2 : 1);
    }
}
