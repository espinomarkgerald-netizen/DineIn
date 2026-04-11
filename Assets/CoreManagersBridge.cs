using UnityEngine;

public class CoreManagersBridge : MonoBehaviour
{
    public static CoreManagersBridge Instance { get; private set; }

    public MoneyManager Money => MoneyManager.Instance;
    public InventoryManager Inventory => InventoryManager.Instance;
    public EmployeeManager Employees => EmployeeManager.Instance;

    public int CurrentMoney => Money != null ? Money.Money : 0;

    [Header("Debug")]
    [SerializeField] private ItemType debugItemType;
    [SerializeField] private int debugItemStock;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Debug.Log($"[CoreManagersBridge] MoneyManager: {(Money != null)}");
        Debug.Log($"[CoreManagersBridge] InventoryManager: {(Inventory != null)}");
        Debug.Log($"[CoreManagersBridge] EmployeeManager: {(Employees != null)}");
        Debug.Log($"[CoreManagersBridge] Current Money: {CurrentMoney}");

        RefreshDebugStock();
    }

    public int GetStock(ItemType itemType)
    {
        if (Inventory == null)
            return 0;

        return Inventory.GetStock(itemType);
    }

    public void RefreshDebugStock()
    {
        debugItemStock = GetStock(debugItemType);
        Debug.Log($"[CoreManagersBridge] Stock of {debugItemType}: {debugItemStock}");
    }
}