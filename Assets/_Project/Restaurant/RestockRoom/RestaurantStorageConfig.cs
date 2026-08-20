using UnityEngine;

[CreateAssetMenu(fileName = "RestaurantStorageConfig", menuName = "Dine In/Restaurant Storage Config")]
public sealed class RestaurantStorageConfig : ScriptableObject
{
    [Header("Restaurant")]
    [SerializeField] private string restaurantID = "casual-dining";
    [SerializeField] private string displayName = "Casual Dining";

    [Header("Physical Shelf Capacity")]
    [SerializeField, Min(0)] private int dryCapacity = 24;
    [SerializeField, Min(0)] private int frozenCapacity = 20;

    [Header("Forecast Fallback")]
    [SerializeField, Min(1)] private int expectedCustomers = 10;

    public string RestaurantID => string.IsNullOrWhiteSpace(restaurantID)
        ? "restaurant"
        : restaurantID.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? "Restaurant"
        : displayName.Trim();
    public int DryCapacity => Mathf.Max(0, dryCapacity);
    public int FrozenCapacity => Mathf.Max(0, frozenCapacity);
    public int ExpectedCustomers => Mathf.Max(1, expectedCustomers);

    public int GetCapacity(RestockStorageType storageType)
    {
        return storageType == RestockStorageType.Frozen
            ? FrozenCapacity
            : DryCapacity;
    }

    private void OnValidate()
    {
        restaurantID = restaurantID != null ? restaurantID.Trim() : string.Empty;
        displayName = displayName != null ? displayName.Trim() : string.Empty;
        dryCapacity = Mathf.Max(0, dryCapacity);
        frozenCapacity = Mathf.Max(0, frozenCapacity);
        expectedCustomers = Mathf.Max(1, expectedCustomers);
    }
}
