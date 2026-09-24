using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates saved authoring objects once. Re-running validates and preserves existing edits.</summary>
public static class FastFoodCookingAuthoring
{
    public const string TemplateFolder="Assets/_Project/Restaurant/CookingAssets";
    [MenuItem("Dine In/Fast Food/Create Editable Kitchen in Lobby2")]
    [MenuItem("Dine In/Fast Food/Add Editable Cooking Settings to Lobby2")]
    public static void AddEditableSettings()
    {
        var scene=SceneManager.GetSceneByName("Lobby2");
        if(EditorApplication.isPlayingOrWillChangePlaymode || !scene.isLoaded || scene.isDirty)
            throw new InvalidOperationException("Open and save Lobby2 outside Play mode first. No changes were made.");
        var controller=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FastFoodCookingController>(true)).SingleOrDefault();
        if(controller!=null && controller.GetComponent<FastFoodCookingView>()?.IsAuthored==true)
        { Debug.Log(Validate(scene)); Selection.activeGameObject=controller.gameObject; return; }
        if(controller==null)
        {
            var go=new GameObject("Fast Food Cooking"); SceneManager.MoveGameObjectToScene(go,scene);
            Undo.RegisterCreatedObjectUndo(go,"Author kitchen");
            controller=Undo.AddComponent<FastFoodCookingController>(go);
        }
        var view=controller.GetComponent<FastFoodCookingView>();
        if(view==null)view=Undo.AddComponent<FastFoodCookingView>(controller.gameObject);
        view.BuildEditorPresentation();
        if(!AssetDatabase.IsValidFolder(TemplateFolder))AssetDatabase.CreateFolder("Assets/_Project/Restaurant","CookingAssets");
        view.SaveEditorTemplateAssets(TemplateFolder);
        var serialized=new SerializedObject(controller); serialized.FindProperty("view").objectReferenceValue=view; serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach(var recipe in MenuCatalog.Default.Products.Where(r=>r!=null && r.restaurantType==RestaurantType.FastFood))
        {
            Undo.RecordObject(recipe,"Author kitchen recipe");
            if(recipe.cookingStation==FastFoodStationMode.None)recipe.cookingStation=FastFoodCookingState.Station(recipe);
            if(recipe.firstCookingIngredient==null)recipe.firstCookingIngredient=FastFoodCookingState.Steps(recipe).FirstOrDefault()?.item;
            EditorUtility.SetDirty(recipe); AssetDatabase.SaveAssetIfDirty(recipe);
        }
        EditorUtility.SetDirty(view); EditorUtility.SetDirty(controller);
        Debug.Log(Validate(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Kitchen authored but Lobby2 could not be saved.");
        Selection.activeGameObject=controller.gameObject;
    }

    [MenuItem("Dine In/Fast Food/Validate Editable Kitchen")]
    public static void ValidateMenu() => Debug.Log(Validate(SceneManager.GetSceneByName("Lobby2")));
    public static string Validate(Scene scene)
    {
        var controllers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FastFoodCookingController>(true)).ToArray();
        if(controllers.Length!=1)throw new InvalidOperationException("Lobby2 must contain exactly one saved cooking controller.");
        var view=controllers[0].GetComponent<FastFoodCookingView>();
        if(view==null || !view.IsAuthored)throw new InvalidOperationException("Kitchen presentation references are incomplete.");
        var rigs=controllers[0].GetComponentsInChildren<FastFoodCookingStation>(true);
        if(rigs.Select(r=>r.mode).Distinct().Count()!=3)throw new InvalidOperationException("Each station must have one saved rig.");
        foreach(var rig in rigs)
        {
            if(rig.labels==null || rig.workload==null || rig.foodAnchor==null || rig.prepFoodAnchor==null || rig.trayAnchors.Length==0 ||
               rig.cookingFoodTemplate==null || rig.servingTemplate==null || rig.drinkTemplate==null || rig.dragPreviewTemplate==null)
                throw new InvalidOperationException(rig.name+" has missing references.");
            if(rig.mode!=FastFoodStationMode.Assembler && (rig.cooking==null || rig.discard==null))
                throw new InvalidOperationException(rig.name+" needs cooking and discard targets.");
            foreach(var target in rig.GetComponentsInChildren<FastFoodCookingDropTarget>(true))
                if(target.GetComponent<Collider>()==null)throw new InvalidOperationException(target.name+" has no drop collider.");
        }
        var buttons=controllers[0].GetComponentsInChildren<UnityEngine.UI.Button>(true);
        if(buttons.Any(b=>b.onClick.GetPersistentEventCount()==0))throw new InvalidOperationException("Kitchen buttons need saved click bindings.");
        if(controllers[0].GetComponentsInChildren<Transform>(true).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0))
            throw new InvalidOperationException("Kitchen contains a missing script.");
        return "Editable kitchen validated: one controller, three station rigs, "+buttons.Length+" persistently wired buttons, saved UI/visual templates.";
    }
}
public sealed class FastFoodCookingBuildValidation : IProcessSceneWithReport
{
    public int callbackOrder=>0;
    public void OnProcessScene(Scene scene,BuildReport report)
    { if(report!=null && scene.name=="Lobby2")FastFoodCookingAuthoring.Validate(scene); }
}
