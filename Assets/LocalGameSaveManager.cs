using UnityEngine;

public class LocalGameSaveManager : MonoBehaviour
{
    public static LocalGameSaveManager Instance { get; private set; }

    [Header("Migration Only")]
    [SerializeField] private bool loadInventoryOnStart = true;

    private bool hasLoaded;

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
        if (loadInventoryOnStart)
            LoadInventoryOnce();
    }

    public void LoadInventoryOnce()
    {
        if (hasLoaded)
            return;

        hasLoaded = true;
        LoadInventory();

        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.SaveGame();

        Debug.Log("[LocalGameSaveManager] Migration load complete. Inventory copied from PlayerPrefs into JSON save.");
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

        Debug.Log("[LocalGameSaveManager] Inventory loaded from PlayerPrefs.");
    }

    private string GetItemStockKey(ItemType itemType)
    {
        return $"DineIn_Stock_{itemType}";
    }
}