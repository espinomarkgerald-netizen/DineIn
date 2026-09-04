using UnityEngine;

/// <summary>
/// Lobby1Tutorial is not listed in the Casual Dining catalog's normal scene mapping.
/// This scene-local context selects the same catalog as current Casual Dining gameplay.
/// </summary>
[DefaultExecutionOrder(-8999)]
[DisallowMultipleComponent]
public sealed class TutorialRestaurantCatalogContext : MonoBehaviour
{
    private void Awake() => MenuCatalog.SetActiveRestaurantType(RestaurantType.CasualDining);
    private void OnEnable() => MenuCatalog.SetActiveRestaurantType(RestaurantType.CasualDining);

    private void OnDestroy()
    {
        MenuCatalog.ClearActiveRestaurantOverride();
    }
}
