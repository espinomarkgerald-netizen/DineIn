#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the Lobby HUD as a normal editable prefab. Runtime code only falls
/// back to generating it when this asset is missing from an imported project.
/// </summary>
[InitializeOnLoad]
internal static class LobbyHUDRedesignAuthoring
{
    private const int CurrentVisualVersion = 2;
    private const string PrefabPath = "Assets/_Project/Resources/UI/LobbyHUDRedesign.prefab";
    private const string BlueFramePath =
        "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Blue/Double/button_rectangle_depth_flat.png";
    private const string BlueArrowPath =
        "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Blue/Double/arrow_basic_e.png";
    private const string NeutralButtonFramePath =
        "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Grey/Double/button_square_depth_flat.png";
    private const string CameraIconPath =
        "Assets/_Project/Art/Icons/GameIcons/HUD/CameraIcon.png";
    private const string ComputerIconPath =
        "Assets/_Project/Art/Icons/ComputerIcons/DashboardIcon.png";
    private const string NewspaperIconPath =
        "Assets/_Project/Art/Icons/GameIcons/HUD/NewspaperIcon.png";
    private const string FontPath =
        "Assets/_Project/UI/Assets/Legacy/Fonts/Fredoka,Lilita_One/Fredoka/Fredoka-VariableFont_wdth,wght SDF.asset";

    static LobbyHUDRedesignAuthoring()
    {
        EditorApplication.delayCall += EnsurePrefab;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += EnsurePrefab;
    }

    private static void EnsurePrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null && prefab.GetComponent<LobbyHUDRedesign>() != null &&
            prefab.GetComponent<LobbyHUDRedesign>().AuthoredVisualVersion >= CurrentVisualVersion &&
            prefab.transform.Find("SafeArea/LivePanel") != null &&
            prefab.transform.Find("SafeArea/ComputerButton") != null)
            return;

        BuildAndSavePrefab();
    }

    [MenuItem("Tools/Dine In/UI/Rebuild Lobby HUD Redesign Prefab")]
    private static void RebuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Rebuild Lobby HUD?",
                "This recreates the editable LobbyHUDRedesign prefab from its serialized style defaults.",
                "Rebuild",
                "Cancel"))
            return;
        BuildAndSavePrefab();
    }

    [MenuItem("Tools/Dine In/UI/Open Lobby HUD Redesign Prefab")]
    private static void OpenPrefab()
    {
        Object prefab = AssetDatabase.LoadAssetAtPath<Object>(PrefabPath);
        if (prefab != null)
            AssetDatabase.OpenAsset(prefab);
    }

    private static void BuildAndSavePrefab()
    {
        GameObject root = new GameObject(
            "LobbyHUDRedesign",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(LobbyHUDRedesign));

        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 225;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            LobbyHUDRedesign hud = root.GetComponent<LobbyHUDRedesign>();
            hud.ConfigureForEditor(
                AssetDatabase.LoadAssetAtPath<Sprite>(BlueFramePath),
                AssetDatabase.LoadAssetAtPath<Sprite>(NeutralButtonFramePath),
                AssetDatabase.LoadAssetAtPath<Sprite>(BlueArrowPath),
                AssetDatabase.LoadAssetAtPath<Sprite>(CameraIconPath),
                AssetDatabase.LoadAssetAtPath<Sprite>(ComputerIconPath),
                AssetDatabase.LoadAssetAtPath<Sprite>(NewspaperIconPath),
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath),
                CurrentVisualVersion);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[LobbyHUDRedesign] Saved the editable mobile-first Lobby HUD prefab.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
#endif
