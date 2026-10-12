using UnityEngine;

/// <summary>
/// Selects the matching restaurant catalog before the tutorial clones its runtime data.
/// </summary>
[DefaultExecutionOrder(-9002)]
[DisallowMultipleComponent]
public sealed class TutorialRestaurantCatalogContext : MonoBehaviour
{
    private RestaurantType Type => gameObject.scene.name == FastFoodScene.Tutorial
        ? RestaurantType.FastFood : gameObject.scene.name == FineDiningScene.Tutorial
            ? RestaurantType.FineDining : RestaurantType.CasualDining;
    private void Awake() => MenuCatalog.SetActiveRestaurantType(Type);
    private void OnEnable() => MenuCatalog.SetActiveRestaurantType(Type);

    private void OnDestroy()
    {
        MenuCatalog.ClearActiveRestaurantOverride();
    }
}
