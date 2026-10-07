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

    private static readonly EmployeeRole[] FineDiningKitchenRoles =
        { EmployeeRole.Chef, EmployeeRole.SousChef, EmployeeRole.Bartender };

    private static readonly EmployeeRole[] FineDiningLobbyRoles =
        { EmployeeRole.Host, EmployeeRole.Waiter, EmployeeRole.Cashier, EmployeeRole.Busser, EmployeeRole.Pianist };
    public static IReadOnlyList<EmployeeRole> LobbyRoles => UsesFineDiningRoles ? FineDiningLobbyRoles : LobbyRoleArray;
    public static bool UsesFineDiningRoles => CampaignSaveStore.IsFineDining && !CampaignSaveStore.ProtectedSession;
    public static bool UsesFastFoodRoles => FastFoodMultiplayerSession.IsActive || CampaignSaveStore.IsFastFood && !CampaignSaveStore.ProtectedSession;
    private static readonly EmployeeRole[] FastFoodKitchenRoles =
        { EmployeeRole.GrillStation, EmployeeRole.FryStation, EmployeeRole.FastFoodAssembler };
    public static IReadOnlyList<EmployeeRole> KitchenRoles => UsesFastFoodRoles ? FastFoodKitchenRoles :
        UsesFineDiningRoles ? FineDiningKitchenRoles : KitchenRoleArray;

    public static string DisplayName(EmployeeRole role) => role switch
    {
        EmployeeRole.Busser when UsesFastFoodRoles => "Lobby Person",
        EmployeeRole.GrillStation => "Grill Station",
        EmployeeRole.FryStation => "Fry Station",
        EmployeeRole.FastFoodAssembler => "Assembler",
        EmployeeRole.SousChef => "Sous Chef",
        EmployeeRole.Host when UsesFineDiningRoles => "Receptionist",
        _ => role.ToString()
    };

    public static IReadOnlyList<EmployeeRole> GetRoles(EmployeeDepartment department) =>
        department == EmployeeDepartment.Kitchen ? KitchenRoles : LobbyRoles;

    public static bool IsSupported(EmployeeRole role) =>
        role == EmployeeRole.Host || role == EmployeeRole.Waiter ||
        role == EmployeeRole.Cashier || role == EmployeeRole.Busser ||
        role == EmployeeRole.Chef ||
        (UsesFineDiningRoles ? role == EmployeeRole.SousChef || role == EmployeeRole.Bartender || role == EmployeeRole.Pianist : role == EmployeeRole.Barista) ||
        (UsesFastFoodRoles && (role == EmployeeRole.GrillStation || role == EmployeeRole.FryStation || role == EmployeeRole.FastFoodAssembler));

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
