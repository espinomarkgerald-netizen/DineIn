using UnityEngine;

[CreateAssetMenu(
    fileName = "ManagementComputerCatalogUIConfig",
    menuName = "Dine In/Management Computer Catalog UI Config")]
public sealed class ManagementComputerCatalogUIConfig : ScriptableObject
{
    [SerializeField] private ManagementComputerCatalogPanelUI catalogPanelPrefab;
    [SerializeField] private RestaurantStorageConfig storageConfig;

    public ManagementComputerCatalogPanelUI CatalogPanelPrefab => catalogPanelPrefab;
    public RestaurantStorageConfig StorageConfig => storageConfig;

#if UNITY_EDITOR
    public void EditorConfigure(
        ManagementComputerCatalogPanelUI panelPrefab,
        RestaurantStorageConfig configuredStorage)
    {
        catalogPanelPrefab = panelPrefab;
        storageConfig = configuredStorage;
    }
#endif
}
