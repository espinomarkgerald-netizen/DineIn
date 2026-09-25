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
    [MenuItem("Dine In/Fast Food/Polish Kitchen Ticket and Feedback")]
    public static void PolishTicketAndFeedback()
    {
        var scene=SceneManager.GetSceneByName("Lobby2");
        if(EditorApplication.isPlayingOrWillChangePlaymode || !scene.isLoaded)
            throw new InvalidOperationException("Open Lobby2 outside Play mode first.");
        string path=TemplateFolder+"/Order Ticket.prefab";
        var contents=PrefabUtility.LoadPrefabContents(path);
        try { FastFoodCookingView.ApplyCompactTicketInEditor(contents); PrefabUtility.SaveAsPrefabAsset(contents,path); }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        var view=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FastFoodCookingView>(true)).Single();
        view.ApplyTicketLayoutInEditor();
        string productPath=TemplateFolder+"/Ticket Product.prefab";
        var productRoot=PrefabUtility.LoadPrefabContents(productPath);
        try
        {
            var product=productRoot.GetComponent<FastFoodCookingDragHandle>();
            product.icon.rectTransform.anchorMin=new Vector2(.12f,.34f);
            product.icon.rectTransform.anchorMax=new Vector2(.88f,.92f);
            product.icon.rectTransform.offsetMin=product.icon.rectTransform.offsetMax=Vector2.zero;
            product.count.rectTransform.anchorMin=new Vector2(.08f,.10f);
            product.count.rectTransform.anchorMax=new Vector2(.92f,.34f);
            product.count.rectTransform.offsetMin=product.count.rectTransform.offsetMax=Vector2.zero;
            product.count.fontSize=20;
            PrefabUtility.SaveAsPrefabAsset(productRoot,productPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(productRoot); }
        foreach(var rig in view.GetComponentsInChildren<FastFoodCookingStation>(true))
        {
            if(rig.mode!=FastFoodStationMode.Assembler && rig.cookingFeedback==null)
            {
                var steam=new GameObject("Cooking Steam");
                Undo.RegisterCreatedObjectUndo(steam,"Author cooking feedback");
                steam.transform.SetParent(rig.foodAnchor,false);
                steam.transform.localRotation=Quaternion.Euler(-90,0,0);
                steam.layer=rig.foodAnchor.gameObject.layer;
                var particles=steam.AddComponent<ParticleSystem>();
                particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=particles.main;
                main.loop=true; main.playOnAwake=true; main.startLifetime=.8f;
                main.startSpeed=.12f; main.startSize=.08f; main.startColor=new Color(1,1,1,.16f);
                main.maxParticles=20; main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=particles.emission; emission.rateOverTime=8;
                var shape=particles.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.radius=.07f; shape.angle=8;
                var color=particles.colorOverLifetime; color.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});
                color.color=gradient;
                rig.cookingFeedback=steam;
                steam.SetActive(false);
            }
            if(rig.cookingFeedback!=null)
            {
                var renderer=rig.cookingFeedback.GetComponent<ParticleSystemRenderer>();
                if(renderer!=null && renderer.sharedMaterial==null)
                    renderer.sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            }
            EditorUtility.SetDirty(rig);
        }
        foreach(var component in view.GetComponentsInChildren<Component>(true))
            if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save kitchen polish.");
        Debug.Log(Validate(scene));
    }
    [MenuItem("Dine In/Fast Food/Simplify Kitchen Presentation")]
    public static void SimplifyKitchenPresentation()
    {
        var scene=SceneManager.GetSceneByName("Lobby2");
        if(EditorApplication.isPlayingOrWillChangePlaymode || !scene.isLoaded)
            throw new InvalidOperationException("Open Lobby2 outside Play mode first.");
        foreach(string name in new[]{"Ingredient Slot","Order Ticket"})
        {
            string path=TemplateFolder+"/"+name+".prefab";
            var contents=PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(name=="Ingredient Slot")FastFoodCookingView.ApplyCompactSlotInEditor(contents);
                else FastFoodCookingView.ApplyCompactTicketInEditor(contents);
                PrefabUtility.SaveAsPrefabAsset(contents,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        var view=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FastFoodCookingView>(true)).Single();
        view.ApplyCompactPresentationInEditor();
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save the compact kitchen presentation.");
        Debug.Log(Validate(scene));
    }
    [MenuItem("Dine In/Fast Food/Use Natural UI Asset Colors")]
    public static void UseNaturalUIAssetColors()
    {
        var scene=SceneManager.GetSceneByName("Lobby2");
        if(EditorApplication.isPlayingOrWillChangePlaymode || !scene.isLoaded)
            throw new InvalidOperationException("Open Lobby2 outside Play mode first.");
        var view=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FastFoodCookingView>(true)).Single();
        foreach(string name in new[]{"Ingredient Slot","Order Ticket","Ticket Product"})
        {
            string path=TemplateFolder+"/"+name+".prefab";
            var contents=PrefabUtility.LoadPrefabContents(path);
            try { FastFoodCookingView.ApplyNaturalUIAssetColors(contents); PrefabUtility.SaveAsPrefabAsset(contents,path); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        view.ApplyNaturalUIColorsInEditor();
        foreach(var component in view.GetComponentsInChildren<Component>(true))
            if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Kitchen UI saved: blue backgrounds, grey foregrounds, contrasting Anton text, green positive, red negative and yellow warning actions.");
    }
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
