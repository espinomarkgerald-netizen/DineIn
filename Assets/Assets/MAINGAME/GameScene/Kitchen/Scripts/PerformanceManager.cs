using UnityEngine;
using TMPro;

public class PerformanceManager : MonoBehaviour {
    public static PerformanceManager Instance;

    [Header("End of Shift Stats")]
    public int completedOrders = 0;
    public int failedOrders = 0;
    public int trashedItems = 0;
    public int burnedItems = 0;
    public int ingredientsUsed = 0;

    [Header("UI Setup")]
    public GameObject endOfShiftPanel;
    public TextMeshProUGUI completedText;
    public TextMeshProUGUI failedText;
    public TextMeshProUGUI trashedText;
    public TextMeshProUGUI burnedText;
    public TextMeshProUGUI usedText;

    void Awake() {
        Instance = this;
    }

    void Start() {
        // Hide the report card while playing!
        if (endOfShiftPanel != null) endOfShiftPanel.SetActive(false);
    }

    // --- GLOBAL HOOKS: Any script can call these instantly! ---
    public static void AddCompletedOrder() { if (Instance) Instance.completedOrders++; }
    public static void AddFailedOrder() { if (Instance) Instance.failedOrders++; }
    public static void AddTrashedItem() { if (Instance) Instance.trashedItems++; }
    public static void AddBurnedItem() { if (Instance) Instance.burnedItems++; }
    public static void AddIngredientUsed() { if (Instance) Instance.ingredientsUsed++; }

    // --- SHOW THE REPORT ---
    public void ShowReport() {
        if (endOfShiftPanel != null) {
            endOfShiftPanel.SetActive(true);

            completedText.text = "Completed Orders: " + completedOrders;
            failedText.text = "Failed Orders: " + failedOrders;
            trashedText.text = "Trashed Items: " + trashedItems;
            burnedText.text = "Burned Food: " + burnedItems;
            usedText.text = "Ingredients Used: " + ingredientsUsed;

            // Freeze the game so the player can look at their score
            Time.timeScale = 0f;

            Debug.Log("SHIFT OVER! Displaying Report Card.");
        }
    }
}