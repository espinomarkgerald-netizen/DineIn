using System.Collections.Generic;

public enum EmployeeDepartment
{
    Lobby,
    Kitchen
}

/// <summary>Single source of truth for roles exposed by the new HR board.</summary>
public static class EmployeeRoleCatalog
{
    private static readonly EmployeeRole[] LobbyRoleArray =
    {
        EmployeeRole.Host,
        EmployeeRole.Waiter,
        EmployeeRole.Cashier,
        EmployeeRole.Busser
    };

    private static readonly EmployeeRole[] KitchenRoleArray =
    {
        EmployeeRole.Chef,
        EmployeeRole.Barista
    };

    public static IReadOnlyList<EmployeeRole> LobbyRoles => LobbyRoleArray;
    public static IReadOnlyList<EmployeeRole> KitchenRoles => KitchenRoleArray;

    public static IReadOnlyList<EmployeeRole> GetRoles(EmployeeDepartment department) =>
        department == EmployeeDepartment.Kitchen ? KitchenRoleArray : LobbyRoleArray;

    public static bool IsSupported(EmployeeRole role) =>
        role == EmployeeRole.Host || role == EmployeeRole.Waiter ||
        role == EmployeeRole.Cashier || role == EmployeeRole.Busser ||
        role == EmployeeRole.Chef || role == EmployeeRole.Barista;

    public static EmployeeRole MigrateLegacyRole(EmployeeRole role)
    {
        switch (role)
        {
            case EmployeeRole.PrepCook:
            case EmployeeRole.LineCook:
                return EmployeeRole.Chef;
            case EmployeeRole.Assembler:
                return EmployeeRole.Barista;
            default:
                return role;
        }
    }
}
