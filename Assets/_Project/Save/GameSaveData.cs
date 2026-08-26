using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventorySaveEntry
{
    public ItemType itemType;
    public int stock;
}

[Serializable]
public class InventoryStockBatchSaveEntry
{
    public string batchID;
    public ItemType itemType;
    public int unitsRemaining;
    public int receivedDay;
    public int expiresDay;
    public bool wrongStorage;
    public RestockStorageType currentStorage;
}

[Serializable]
public class MenuPriceOverrideSaveEntry
{
    public string productID;
    public int price;
}

[Serializable]
public class EmployeeSaveEntry
{
    public string employeeID;
    public string employeeName;
    public int stars;
    public EmployeeRole role;
    public bool assigned;
    public bool hired;
    public int speed = 100;
    public int accuracy = 80;
    public int reliability = 80;
    public bool useManualSalary;
    public int manualSalary;
    public float performanceMultiplier = 1f;
    public int bonusFlat;
    public int experience;
    public int roleExperience;
    public int daysEmployed;
    public int daysWorked;
    public int recentPerformance = 75;
    public int previousPerformance = 75;
    public string traitID;
    public int lastPromotionDay;
}

[Serializable]
public class SupplierPriceSaveEntry
{
    public string itemID;
    public ItemType itemType;
    public int baseCost;
    public int previousCost;
    public int currentCost;
    public int lastChangedDay;
    public string marketEvent;
}

[Serializable]
public class RestaurantRatingHistorySaveEntry
{
    public int day;
    public int previousScore;
    public int dailyQualityScore;
    public int resultingScore;
}

[Serializable]
public class RestaurantReviewSaveEntry
{
    public string reviewID;
    public string templateID;
    public int day;
    public bool positive;
    public string text;
}

[Serializable]
public class DailyRestaurantSnapshotSaveData
{
    public int day;
    public int approvalBefore;
    public int approvalAfter;
    public int ratingBefore;
    public int ratingAfter;
    public int groupsArrived;
    public int groupsSeated;
    public int customersServed;
    public int happyCustomers;
    public int neutralCustomers;
    public int angryCustomers;
    public int unaccommodated;
    public int waitedTooLong;
    public int wrongOrders;
    public int orderFailures;
    public int paymentErrors;
    public int dirtyTableDelays;
    public int stockoutRefusals;
    public int takeoutFailures;
    public int ordersCompleted;
    public int ordersFailed;
    public int revenue;
    public int ingredientCost;
    public int employeeCost;
    public int otherCosts;
    public int profit;
    public int discardedUnits;
    public int lowStockItems;
    public string topEmployeeID;
    public string topEmployeeName;
    public int topEmployeePerformance;
}

[Serializable]
public class NewspaperTemplateUseSaveEntry
{
    public string section;
    public string templateID;
    public int day;
}

[Serializable]
public class NewspaperIssueSaveEntry
{
    public string issueID;
    public int day;
    public int sourceDay;
    public int seed;
    public int presentationVersion;
    public bool viewed;
    public string headline;
    public string byline;
    public string renderedContent;
    public List<string> templateIDs = new List<string>();
}

[Serializable]
public class RestockStoredContainerSaveData
{
    public string containerID;
    public string stockBatchID;
    public string itemID;
    public ItemType itemType;
    public string shelfID;
    public int column;
    public int row;
    public float rotationY;
    public RestockStorageType storageType;
    public bool wrongStorage;
}

[Serializable]
public class GameSaveData
{
    public int saveSchemaVersion = 3;
    // Version 1 introduced finite restaurant ingredient stock. The save manager
    // uses the serialized presence of this field to migrate older saves once.
    public int inventorySystemVersion = 2;

    public int currentDay = 1;
    public int currentPhase = 0;
    public int currentDayHalf = 1;
    public bool lobbyCompleted;
    public bool kitchenCompleted;
    public bool campaignCompleted;

    public int money = 500;
    public int approval = 50;

    public List<string> unlockedRecipeIDs = new List<string>();
    public List<string> unlockedEquipmentIDs = new List<string>();
    public List<int> unlockedKitchenItems = new List<int>();

    public List<InventorySaveEntry> inventoryStocks = new List<InventorySaveEntry>();
    public List<InventoryStockBatchSaveEntry> inventoryStockBatches =
        new List<InventoryStockBatchSaveEntry>();
    public List<string> disabledMenuProductIDs = new List<string>();
    public List<MenuPriceOverrideSaveEntry> menuPriceOverrides = new List<MenuPriceOverrideSaveEntry>();
    public List<RestockOrderSaveData> restockOrders = new List<RestockOrderSaveData>();
    public List<string> purchasedEquipmentIDs = new List<string>();
    public List<EmployeeSaveEntry> employees = new List<EmployeeSaveEntry>();

    [Header("Casual Dining Level 1 Polish")]
    public int polishPreparedDay;
    public int polishLastFinalizedDay;
    public int polishDayStartApproval = 30;
    public int polishDayStartMoney = 500;
    public int restaurantRatingScore = 60;
    public int supplierMarketGeneratedDay;
    public int employeeApplicantNextRefreshDay = 8;
    public int discardedUnitsToday;
    public DailyRestaurantSnapshotSaveData lastDailyRestaurantSnapshot;
    public List<SupplierPriceSaveEntry> supplierPrices = new List<SupplierPriceSaveEntry>();
    public List<RestaurantRatingHistorySaveEntry> restaurantRatingHistory =
        new List<RestaurantRatingHistorySaveEntry>();
    public List<RestaurantReviewSaveEntry> restaurantReviews =
        new List<RestaurantReviewSaveEntry>();
    public List<NewspaperTemplateUseSaveEntry> newspaperTemplateHistory =
        new List<NewspaperTemplateUseSaveEntry>();
    public List<NewspaperIssueSaveEntry> newspaperIssues =
        new List<NewspaperIssueSaveEntry>();
    public List<RestockStoredContainerSaveData> restockStoredContainers =
        new List<RestockStoredContainerSaveData>();

    [Header("Manager Complaint Encounters")]
    public int managerComplaintWeekIndex = -1;
    public int managerComplaintsThisWeek;
    public int managerComplaintLastDay;
}
