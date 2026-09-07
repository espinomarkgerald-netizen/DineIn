using UnityEngine;

// Installed only in the multiplayer scene. Filters UI, not hiring or staff behavior.
public class MultiplayerStaffComputerFilter : MonoBehaviour
{
    [SerializeField] private MultiplayerStaffRosterController roster;

    private void OnEnable() => ManagementComputerHRPanel.BeforeBind += ConfigurePanel;
    private void OnDisable() => ManagementComputerHRPanel.BeforeBind -= ConfigurePanel;

    private void ConfigurePanel(ManagementComputerHRPanel panel)
    {
        if (panel.gameObject.scene == gameObject.scene && roster != null && roster.IsInitialized)
            panel.RoleVisibilityFilter = IsVisible;
    }

    private bool IsVisible(EmployeeRole role)
    {
        if (this == null || !isActiveAndEnabled || roster == null || !roster.IsInitialized) return true;
        switch (role)
        {
            case EmployeeRole.Host:
                return roster.IsRoleAvailableForAI(MultiplayerStaffRosterController.ServiceRole.Receptionist);
            case EmployeeRole.Waiter:
                return roster.IsRoleAvailableForAI(MultiplayerStaffRosterController.ServiceRole.Waiter);
            case EmployeeRole.Cashier:
                return roster.IsRoleAvailableForAI(MultiplayerStaffRosterController.ServiceRole.Cashier);
            case EmployeeRole.Busser:
                return roster.IsRoleAvailableForAI(MultiplayerStaffRosterController.ServiceRole.Busser);
            default: return true; // All kitchen roles retain their existing UI and hire actions.
        }
    }
}
