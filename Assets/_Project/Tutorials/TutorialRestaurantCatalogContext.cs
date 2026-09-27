using UnityEngine;

/// <summary>
/// Lobby1Tutorial is not listed in the Casual Dining catalog's normal scene mapping.
/// This scene-local context selects the same catalog as current Casual Dining gameplay.
/// </summary>
[DefaultExecutionOrder(-9002)]
[DisallowMultipleComponent]
public sealed class TutorialRestaurantCatalogContext : MonoBehaviour
{
    private RestaurantType Type => gameObject.scene.name == FastFoodScene.Tutorial
        ? RestaurantType.FastFood : RestaurantType.CasualDining;
    private void Awake() => MenuCatalog.SetActiveRestaurantType(Type);
    private void OnEnable() => MenuCatalog.SetActiveRestaurantType(Type);

    private void OnDestroy()
    {
        MenuCatalog.ClearActiveRestaurantOverride();
    }
}
