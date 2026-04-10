using System;
using System.Collections.Generic;

[Serializable]
public class InventorySaveEntry
{
    public ItemType itemType;
    public int stock;
}

[Serializable]
public class GameSaveData
{
    public int currentDay = 1;
    public int currentPhase = 0;
    public int currentDayHalf = 1;
    public bool lobbyCompleted;
    public bool kitchenCompleted;

    public int money = 500;
    public int approval = 50;

    public List<string> unlockedRecipeIDs = new List<string>();
    public List<string> unlockedEquipmentIDs = new List<string>();
    public List<int> unlockedKitchenItems = new List<int>();

    public List<InventorySaveEntry> inventoryStocks = new List<InventorySaveEntry>();
}