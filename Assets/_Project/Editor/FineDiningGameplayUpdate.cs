#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Applies the offline-prepared Lobby3 asset bindings through Unity APIs on the next Editor load.</summary>
public static class FineDiningGameplayUpdate
{
    private const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby3.unity";
    private const string RecipeFolder = "Assets/_Project/Office/Manager/Recipe/Fine Dining/";
    private const string ArtFolder = "Assets/_Project/Art/Icons/GameIcons/WinePouring/";
    private static string UpdateKey => "DineIn.FineDiningGameplayUpdate.1." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        if (!EditorPrefs.GetBool(UpdateKey, false)) EditorApplication.delayCall += ApplyPendingUpdate;
    }

    [MenuItem("Dine In/Fine Dining/Apply Prepared Gameplay Update")]
    public static void ApplyPendingUpdate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        Sprite pitcher = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "WaterPitcher.png");
        Sprite fill = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "WaterFill.png");
        Recipe water = AssetDatabase.LoadAssetAtPath<Recipe>(RecipeFolder + "Sparkling Water.asset");
        Recipe white = AssetDatabase.LoadAssetAtPath<Recipe>(RecipeFolder + "White Wine.asset");
        if (pitcher == null || fill == null || water == null || white == null)
        {
            Debug.LogWarning("[FineDining] Prepared update is waiting for the new art/recipe imports. Use Dine In/Fine Dining/Apply Prepared Gameplay Update after import.");
            return;
        }
        try
        {
            BindPouringPrefab(pitcher, fill);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForUpdate = !scene.IsValid() || !scene.isLoaded;
            Scene previous = SceneManager.GetActiveScene();
            if (openedForUpdate) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                BindScene(scene, water, white, pitcher);
                if (!scene.isDirty) EditorPrefs.SetBool(UpdateKey, true);
            }
            finally
            {
                if (openedForUpdate) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private static void BindPouringPrefab(Sprite pitcher, Sprite fill)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WinePouringUIAuthoring.PrefabPath);
        try
        {
            WinePouringUI ui = root.GetComponent<WinePouringUI>();
            if (ui == null) throw new InvalidOperationException("WinePouringUI component is missing.");
            if (ui.waterPitcherSprite == pitcher && ui.waterFillSprite == fill) return;
            ui.waterPitcherSprite = pitcher;
            ui.waterFillSprite = fill;
            PrefabUtility.SaveAsPrefabAsset(root, WinePouringUIAuthoring.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Recipe water = AssetDatabase.LoadAssetAtPath<Recipe>(RecipeFolder + "Sparkling Water.asset");
        Recipe white = AssetDatabase.LoadAssetAtPath<Recipe>(RecipeFolder + "White Wine.asset");
        Sprite pitcher = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "WaterPitcher.png");
        Sprite fill = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "WaterFill.png");
        if (water == null || white == null || pitcher == null || fill == null) return;
        BindPouringPrefab(pitcher, fill);
        BindScene(scene, water, white, pitcher);
    }

    private static void BindScene(Scene scene, Recipe water, Recipe white, Sprite pitcher)
    {
        bool hadUnsavedChanges = scene.isDirty;
        bool changed = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var settings in root.GetComponentsInChildren<FineDiningReservations>(true))
            {
                // The absent water binding identifies the old configuration; retain any custom probability.
                if (settings.waterPourProduct == null)
                {
                    settings.waterPourProduct = water;
                    if (Mathf.Approximately(settings.additionalDrinkChance, .5f)) settings.additionalDrinkChance = .85f;
                    changed = true;
                }
                if (settings.waterPitcherIcon == null) { settings.waterPitcherIcon = pitcher; changed = true; }
                var wines = settings.wineRefillProducts ?? Array.Empty<Recipe>();
                if (!wines.Contains(white)) { settings.wineRefillProducts = wines.Concat(new[] { white }).ToArray(); changed = true; }
                if (changed) EditorUtility.SetDirty(settings);
            }
            foreach (var progression in root.GetComponentsInChildren<FineDiningProgression>(true))
            {
                var recipes = progression.recipes ?? Array.Empty<Recipe>();
                var active = recipes.Where(recipe => recipe == null || recipe.ProductId != "fine-sparkling-wine").ToArray();
                if (active.Length == recipes.Length) continue;
                progression.recipes = active;
                EditorUtility.SetDirty(progression);
                changed = true;
            }
        }
        if (!changed) return;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!hadUnsavedChanges) EditorSceneManager.SaveScene(scene);
        else Debug.LogWarning("[FineDining] Gameplay bindings updated in the open Lobby3 scene. Existing unsaved edits were preserved; save Lobby3 before building.");
    }
}
#endif
