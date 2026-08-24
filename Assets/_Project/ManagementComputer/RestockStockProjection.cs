using UnityEngine;

public enum RestockCoverageState
{
    Enough,
    CoveredByDelivery,
    Low,
    StillLow,
    Overstocked,
    SpoilageRisk
}

/// <summary>
/// One stock forecast shared by the supplier catalog and the pre-open checklist.
/// Confirmed orders count as incoming supply, but never become usable inventory
/// until their physical boxes are stored.
/// </summary>
public sealed class RestockStockProjection
{
    public ItemData Item { get; private set; }
    public int OnHandUnits { get; private set; }
    public int FreshUnits { get; private set; }
    public int ExpiredUnits { get; private set; }
    public int UnitsPerBox { get; private set; }
    public int TargetUnits { get; private set; }
    public int PendingContainers { get; private set; }
    public int InTransitContainers { get; private set; }
    public int AtTruckContainers { get; private set; }
    public int HotbarContainers { get; private set; }
    public int ProjectedUnits { get; private set; }
    public int RecommendedContainers { get; private set; }
    public int NextFreshExpiryDay { get; private set; }
    public RestockCoverageState State { get; private set; }

    public bool HasIncoming => PendingContainers > 0;
    public bool IsCovered => RecommendedContainers <= 0;
    public bool IsCoveredByIncoming => HasIncoming && IsCovered;

    public static RestockStockProjection Calculate(
        ItemData item,
        int expectedCustomers,
        RestockOrderManager orderManager = null)
    {
        RestockStockProjection result = new RestockStockProjection
        {
            Item = item,
            UnitsPerBox = item != null ? Mathf.Max(1, item.unitsPerBox) : 1
        };

        if (item == null)
            return result;

        InventoryManager inventory = InventoryManager.Instance;
        int day = GameFlowManager.Instance != null
            ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
            : 1;

        if (inventory != null)
        {
            result.OnHandUnits = Mathf.Max(0, inventory.GetStock(item.itemType));
            result.ExpiredUnits = Mathf.Max(0, inventory.GetExpiredStock(item.itemType, day));
            result.FreshUnits = Mathf.Max(0, inventory.GetFreshStock(item.itemType, day));
            result.NextFreshExpiryDay = inventory.GetNextFreshExpiryDay(item.itemType, day);
        }

        result.TargetUnits = Mathf.CeilToInt(
            Mathf.Max(1, expectedCustomers) * Mathf.Max(0f, item.averageUsagePerCustomer));

        orderManager ??= RestockOrderManager.Instance;
        if (orderManager != null)
        {
            result.PendingContainers = orderManager.GetPendingContainers(item);
            result.InTransitContainers = orderManager.GetContainersInStates(
                item,
                RestockOrderState.Ordered,
                RestockOrderState.InDelivery);
            result.AtTruckContainers = orderManager.GetContainersInStates(
                item,
                RestockOrderState.Delivered);
            result.HotbarContainers = orderManager.GetContainersInStates(
                item,
                RestockOrderState.Collected,
                RestockOrderState.PartiallyStored);
        }

        result.ProjectedUnits = result.FreshUnits +
                                result.PendingContainers * result.UnitsPerBox;
        int missingUnits = Mathf.Max(0, result.TargetUnits - result.ProjectedUnits);
        result.RecommendedContainers = Mathf.CeilToInt(
            missingUnits / (float)result.UnitsPerBox);

        bool expiringSoon = result.FreshUnits > result.TargetUnits * 2 &&
                            result.NextFreshExpiryDay > 0 &&
                            result.NextFreshExpiryDay <= day + 1;
        if (result.ExpiredUnits > 0 || expiringSoon)
            result.State = RestockCoverageState.SpoilageRisk;
        else if (result.RecommendedContainers > 0)
            result.State = result.HasIncoming
                ? RestockCoverageState.StillLow
                : RestockCoverageState.Low;
        else if (result.TargetUnits > 0 && result.ProjectedUnits > result.TargetUnits * 2)
            result.State = RestockCoverageState.Overstocked;
        else if (result.HasIncoming)
            result.State = RestockCoverageState.CoveredByDelivery;
        else
            result.State = RestockCoverageState.Enough;

        return result;
    }

    public string GetDeliveryStageLabel()
    {
        if (HotbarContainers > 0)
            return "NEEDS STORAGE";
        if (AtTruckContainers > 0)
            return "AT TRUCK";
        if (InTransitContainers > 0)
            return "INCOMING";
        return HasIncoming ? "ORDERED" : string.Empty;
    }
}
