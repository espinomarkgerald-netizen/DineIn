using System;
using System.Collections.Generic;

[Serializable]
public class InventorySaveEntry
{
    public ItemType itemType;
    public int stock;
}

[Serializable]
public class EmployeeSaveEntry
{
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
}

[Serializable]
public class GameSaveData
{
    // Version 1 introduced finite restaurant ingredient stock. The save manager
    // uses the serialized presence of this field to migrate older saves once.
    public int inventorySystemVersion = 1;

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
    public List<string> disabledMenuProductIDs = new List<string>();
    public List<string> purchasedEquipmentIDs = new List<string>();
    public List<EmployeeSaveEntry> employees = new List<EmployeeSaveEntry>();
}
