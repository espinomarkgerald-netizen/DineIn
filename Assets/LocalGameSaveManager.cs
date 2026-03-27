using UnityEngine;

public class LocalGameSaveManager : MonoBehaviour
{
    public static LocalGameSaveManager Instance { get; private set; }

    private const string MoneyKey = "DineIn_Money";

    [Header("Auto Save/Load")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool saveOnApplicationQuit = true;
    [SerializeField] private bool saveOnApplicationPause = true;

    private bool hasLoaded;

    private InventoryManager subscribedInventory;
    private MoneyManager subscribedMoney;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        TrySubscribe();

        if (loadOnStart)
            LoadAllOnce();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    private void OnApplicationQuit()
    {
        if (saveOnApplicationQuit)
            SaveAll();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && saveOnApplicationPause)
            SaveAll();
    }

    private void TrySubscribe()
    {
        if (InventoryManager.Instance != null && subscribedInventory != InventoryManager.Instance)
        {
            if (subscribedInventory != null)
                subscribedInventory.OnStockChanged -= HandleStockChanged;

            subscribedInventory = InventoryManager.Instance;
            subscribedInventory.OnStockChanged += HandleStockChanged;
        }

        if (MoneyManager.Instance != null && subscribedMoney != MoneyManager.Instance)
        {
            if (subscribedMoney != null)
                subscribedMoney.OnMoneyChanged -= HandleMoneyChanged;

            subscribedMoney = MoneyManager.Instance;
            subscribedMoney.OnMoneyChanged += HandleMoneyChanged;
        }
    }

    private void TryUnsubscribe()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnStockChanged -= HandleStockChanged;
            subscribedInventory = null;
        }

        if (subscribedMoney != null)
        {
            subscribedMoney.OnMoneyChanged -= HandleMoneyChanged;
            subscribedMoney = null;
        }
    }

    private void HandleStockChanged(ItemType type, int amount)
    {
        SaveInventory();
        Debug.Log($"[LocalGameSaveManager] Auto-saved stock: {type} = {amount}");
    }

    private void HandleMoneyChanged(int amount)
    {
        SaveMoney();
        Debug.Log($"[LocalGameSaveManager] Auto-saved money: {amount}");
    }

    public void SaveMoney()
    {
        if (MoneyManager.Instance == null)
            return;

        PlayerPrefs.SetInt(MoneyKey, MoneyManager.Instance.Money);
        PlayerPrefs.Save();

        Debug.Log($"[LocalGameSaveManager] Saved Money: {MoneyManager.Instance.Money}");
    }

    public void LoadMoney()
    {
        if (MoneyManager.Instance == null)
            return;

        if (!PlayerPrefs.HasKey(MoneyKey))
            return;

        int savedMoney = PlayerPrefs.GetInt(MoneyKey);
        int currentMoney = MoneyManager.Instance.Money;
        int difference = savedMoney - currentMoney;

        if (difference > 0)
            MoneyManager.Instance.Earn(difference, "Load Save");
        else if (difference < 0)
            MoneyManager.Instance.Spend(-difference, "Load Save");

        Debug.Log($"[LocalGameSaveManager] Loaded Money: {savedMoney}");
    }

    public void SaveInventory()
    {
        if (InventoryManager.Instance == null)
            return;

        foreach (ItemType itemType in System.Enum.GetValues(typeof(ItemType)))
        {
            string key = GetItemStockKey(itemType);
            int stock = InventoryManager.Instance.GetStock(itemType);
            PlayerPrefs.SetInt(key, stock);
        }

        PlayerPrefs.Save();
        Debug.Log("[LocalGameSaveManager] Inventory saved.");
    }

    public void LoadInventory()
    {
        if (InventoryManager.Instance == null)
            return;

        foreach (ItemType itemType in System.Enum.GetValues(typeof(ItemType)))
        {
            string key = GetItemStockKey(itemType);

            if (!PlayerPrefs.HasKey(key))
                continue;

            int savedStock = PlayerPrefs.GetInt(key);
            int currentStock = InventoryManager.Instance.GetStock(itemType);
            int difference = savedStock - currentStock;

            if (difference > 0)
                InventoryManager.Instance.AddStock(itemType, difference);
            else if (difference < 0)
                InventoryManager.Instance.UseStock(itemType, -difference);
        }

        Debug.Log("[LocalGameSaveManager] Inventory loaded.");
    }

    public void SaveAll()
    {
        SaveMoney();
        SaveInventory();
    }

    public void LoadAll()
    {
        LoadMoney();
        LoadInventory();
    }

    public void LoadAllOnce()
    {
        if (hasLoaded)
            return;

        hasLoaded = true;
        LoadAll();
    }

    private string GetItemStockKey(ItemType itemType)
    {
        return $"DineIn_Stock_{itemType}";
    }
}