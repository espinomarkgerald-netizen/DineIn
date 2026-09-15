using UnityEngine;

public sealed partial class MultiplayerRestaurantBridge
{
    private const string EconomyKey = "restaurant.economy.v3";
    private bool managementDirty = true, economyDirty = true, subscribed;
    private MoneyManager moneySource;
    private InventoryManager inventorySource;
    private AlienApprovalManager approvalSource;
    private EmployeeManager employeeSource;
    private EquipmentManager equipmentSource;
    private MenuAvailabilityManager menuSource;
    public static void MarkDirty() { if (Active != null) Active.managementDirty = true; }
    private void EconomyChanged(int ignored) => economyDirty = true;
    private void InventoryChanged(ItemType ignored, int count) => economyDirty = true;
    private void ManagementChanged() => managementDirty = true;
    private void SubscribeChanges()
    {
        if (subscribed || !MultiplayerProgressionContext.LocallyPrepared) return;
        moneySource = MoneyManager.Instance; inventorySource = InventoryManager.Instance; approvalSource = AlienApprovalManager.Instance;
        employeeSource = EmployeeManager.Instance; equipmentSource = EquipmentManager.Instance; menuSource = MenuAvailabilityManager.Instance;
        if (moneySource == null || inventorySource == null || approvalSource == null) return;
        moneySource.OnMoneyChanged += EconomyChanged;
        inventorySource.OnStockChanged += InventoryChanged;
        approvalSource.OnApprovalChanged += EconomyChanged;
        if (employeeSource != null) { employeeSource.AssignmentsChanged += ManagementChanged; employeeSource.ApplicantsRefreshed += ManagementChanged; }
        if (equipmentSource != null) equipmentSource.PurchasesChanged += ManagementChanged;
        if (menuSource != null) menuSource.MenuChanged += ManagementChanged;
        subscribed = true;
    }
    private Snapshot CaptureEconomy()
    {
        var data = new GameSaveData { currentDay = GameFlowManager.Instance.CurrentDay };
        MoneyManager.Instance?.FillSaveData(data);
        InventoryManager.Instance?.FillSaveData(data);
        AlienApprovalManager.Instance?.FillSaveData(data);
        return new Snapshot { run = session.RunId, economyOnly = true, data = data,
            objectives = DailyObjectiveManager.Instance?.CaptureNetworkState(), finance = DailyFinanceBridge.Instance?.CaptureNetworkState() };
    }
    private void ReadEconomy()
    {
        if (Photon.Pun.PhotonNetwork.CurrentRoom?.CustomProperties[EconomyKey] is not string json) return;
        try { Apply(JsonUtility.FromJson<Snapshot>(json)); } catch (System.ArgumentException) { }
    }
    private void OnDestroy()
    {
        if (moneySource != null) moneySource.OnMoneyChanged -= EconomyChanged;
        if (inventorySource != null) inventorySource.OnStockChanged -= InventoryChanged;
        if (approvalSource != null) approvalSource.OnApprovalChanged -= EconomyChanged;
        if (employeeSource != null) { employeeSource.AssignmentsChanged -= ManagementChanged; employeeSource.ApplicantsRefreshed -= ManagementChanged; }
        if (equipmentSource != null) equipmentSource.PurchasesChanged -= ManagementChanged;
        if (menuSource != null) menuSource.MenuChanged -= ManagementChanged;
    }
}
