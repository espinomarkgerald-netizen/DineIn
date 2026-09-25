using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One-time, idempotent authoring of the saved batch kitchen. Existing authored assets are reused.</summary>
public static class FastFoodBatchAuthoring
{
    const string Folder="Assets/_Project/Restaurant/CookingAssets/Batch";
    const string Models="Assets/_Project/Restaurant/Prefabs/FastFood/Food/";
    [MenuItem("Dine In/Fast Food/Author Batch Cooking and Prep")]
    public static void Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Author the kitchen outside Play Mode.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="Lobby2")throw new InvalidOperationException("Open Lobby2 first.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Project/Restaurant/CookingAssets","Batch");
        var controller=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FastFoodCookingController>(true)).Single();
        var rigs=controller.GetComponentsInChildren<FastFoodCookingStation>(true);
        foreach(var recipe in MenuCatalog.Default.Products.Where(r=>r!=null&&r.category==MenuProductCategory.Food))AuthorRecipe(recipe);
        var renderers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var table=renderers.Single(r=>r.name=="Cube.028"&&r.transform.parent.name=="Fast Food Revamp");
        foreach(var rig in rigs)
        {
            Undo.RecordObject(rig,"Author batch kitchen references");
            var slots=rig.GetComponentsInChildren<FastFoodCookingDropTarget>(true).Where(t=>t.kind==0&&!t.collectionSurface).OrderBy(t=>t.transform.position.x).ToArray();
            rig.cookingSlots=rig.mode==FastFoodStationMode.Assembler?Array.Empty<FastFoodCookingDropTarget>():slots;
            rig.cookingViewAnchor=Anchor(rig.transform,"Cooking View",rig.stationCamera.transform.position,rig.stationCamera.transform.rotation);
            if(rig.mode!=FastFoodStationMode.Assembler)
            {
                for(int i=0;i<slots.Length;i++)
                {
                    var bay=slots[i];Undo.RecordObject(bay,"Bind cooking bay");bay.slotIndex=i;
                    bay.foodAnchor=Anchor(rig.transform,"Bay "+(i+1)+" Food",bay.transform.position+Vector3.up*.10f,Quaternion.identity);
                    if(bay.status==null)
                    {
                        var panel=UnityEngine.Object.Instantiate(rig.status.transform.parent.gameObject,rig.labels);
                        Undo.RegisterCreatedObjectUndo(panel,"Author bay timer");panel.name="Bay "+(i+1)+" Timer";
                        var rect=(RectTransform)panel.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(112,34);
                        bay.status=panel.GetComponentInChildren<TMPro.TMP_Text>(true);bay.status.fontSize=26;bay.status.text="0:08";bay.status.gameObject.SetActive(true);
                        panel.SetActive(false);
                    }
                    if(bay.cookingFeedback==null&&rig.cookingFeedback!=null)
                    {
                        var effect=UnityEngine.Object.Instantiate(rig.cookingFeedback,bay.foodAnchor);
                        Undo.RegisterCreatedObjectUndo(effect,"Author bay cooking effect");effect.name="Cooking Bubbles or Steam";
                        effect.transform.localPosition=Vector3.up*.08f;effect.SetActive(false);bay.cookingFeedback=effect;
                        foreach(var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
                        {particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);particles.useAutoRandomSeed=false;particles.randomSeed=(uint)(123+i*73);}
                    }
                    EditorUtility.SetDirty(bay);
                }
            }
            if(rig.mode==FastFoodStationMode.Grill && rig.preparationViewAnchor==null)
            {
                var top=new Vector3(table.bounds.center.x,table.bounds.max.y+.035f,table.bounds.center.z);
                rig.workSurface=table;
                var board=rig.preparation.transform;Undo.RecordObject(board,"Place burger preparation board on prep table");
                board.position=top+Vector3.left*1.2f;board.rotation=Quaternion.identity;board.localScale=new Vector3(1.5f,.05f,1.3f);
                rig.prepFoodAnchor.position=board.position+Vector3.up*.055f;rig.prepFoodAnchor.rotation=Quaternion.identity;rig.prepFoodAnchor.localScale=Vector3.one;
                Vector3 camera=top+new Vector3(0,3.7f,-4.2f);
                rig.preparationViewAnchor=Anchor(rig.transform,"Prep Table View",camera,Quaternion.LookRotation(top-camera));
                rig.completedFoodAnchors=new Transform[6];
                for(int i=0;i<6;i++)rig.completedFoodAnchors[i]=Anchor(rig.transform,"Finished Burger "+(i+1),top+new Vector3(.35f+(i%3)*.77f,.06f,-.45f+(i/3)*.9f),Quaternion.identity);
            }
            EditorUtility.SetDirty(rig);
        }
        var lights=Anchor(controller.transform,"Kitchen Work Lights",Vector3.zero,Quaternion.identity);
        AddLight(lights,"Fryer Overhead",new Vector3(-1.6f,-2.4f,64.9f));
        AddLight(lights,"Grill Overhead",new Vector3(10.4f,-2.4f,65.1f));
        AddLight(lights,"Burger Prep Overhead",new Vector3(10.8f,-2.4f,56));
        AddLight(lights,"Fry Prep Overhead",new Vector3(3.2f,-2.4f,56));
        AddLight(lights,"Assembler Overhead",new Vector3(0,-2.4f,42.4f));
        foreach(var r in renderers.Where(r=>r.transform.parent!=null&&r.transform.parent.name=="Fast Food Revamp"&&
            (r.name.StartsWith("Fryer")||r.name.StartsWith("Grill")||r.name=="Cube.028"||r.name=="Cube.033"||r.name.StartsWith("Prep"))))
        {Undo.RecordObject(r,"Interior kitchen shadow receivers");r.receiveShadows=false;r.sharedMaterials=r.sharedMaterials.Select(InteriorMaterial).ToArray();EditorUtility.SetDirty(r);}
        foreach(var r in controller.GetComponentsInChildren<MeshRenderer>(true))
        {Undo.RecordObject(r,"Kitchen effect shadow receivers");r.receiveShadows=false;EditorUtility.SetDirty(r);}
        DisableOilShadows();
        var texts=new SerializedObject(controller);
        texts.FindProperty("cookGuidance").FindPropertyRelative("detail").stringValue="Fill the free bays. Tap ready grill food into your hotbar; raised fryer baskets transfer food to the drying rack.";
        texts.FindProperty("prepareGuidance").FindPropertyRelative("detail").stringValue="Drag each pictured ingredient onto the board. Finish with the top bun.";
        texts.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save Lobby2.");
        Debug.Log("Saved batch kitchen: four fryer bays, two grill bays, recipe layers, prep camera and interior work lights.");
    }
    static Transform Anchor(Transform parent,string name,Vector3 position,Quaternion rotation)
    {
        var existing=parent.Find(name);if(existing!=null)return existing;
        var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Author kitchen anchor");go.transform.SetParent(parent,false);
        go.transform.SetPositionAndRotation(position,rotation);return go.transform;
    }
    static void AddLight(Transform parent,string name,Vector3 position)
    {
        if(parent.Find(name)!=null)return;
        var root=Anchor(parent,name,position,Quaternion.Euler(90,0,0));var light=Undo.AddComponent<Light>(root.gameObject);
        light.type=LightType.Spot;light.range=8;light.spotAngle=100;light.innerSpotAngle=70;light.intensity=2.5f;
        light.color=new Color(1,.97f,.9f);light.shadows=LightShadows.None;
    }
    static Material InteriorMaterial(Material source)
    {
        if(source==null||!source.HasProperty("_ReceiveShadows"))return source;
        string sourcePath=AssetDatabase.GetAssetPath(source);
        if(sourcePath.StartsWith(Folder+"/"))return source;
        string path=Folder+"/Interior "+source.name+" "+AssetDatabase.AssetPathToGUID(sourcePath).Substring(0,8)+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
        material=new Material(source){name="Interior "+source.name};material.SetFloat("_ReceiveShadows",0);material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        AssetDatabase.CreateAsset(material,path);return material;
    }
    static void AuthorRecipe(Recipe recipe)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Models+recipe.DisplayName+".prefab");if(source==null)throw new Exception("Food model missing: "+recipe.DisplayName);
        Undo.RecordObject(recipe,"Author kitchen recipe visuals");
        bool sandwich=recipe.kitchenItemType==ItemTypeKitchen.Burger||recipe.kitchenItemType==ItemTypeKitchen.ChickenSandwich||recipe.kitchenItemType==ItemTypeKitchen.FishFilletSandwich;
        if(sandwich)
        {
            recipe.cookingStation=recipe.kitchenItemType==ItemTypeKitchen.Burger?FastFoodStationMode.Grill:FastFoodStationMode.Fry;
            recipe.preparationStation=FastFoodStationMode.Grill;
            string protein=recipe.kitchenItemType==ItemTypeKitchen.Burger?"BurgerPatty":recipe.kitchenItemType==ItemTypeKitchen.ChickenSandwich?"Chicken":"FishFillet";
            var bun=recipe.ingredients.First(i=>i.item.itemType==ItemType.Bun).item;
            var meat=FastFoodCookingState.Steps(recipe)[0].item;
            if(recipe.kitchenAssemblySteps==null||recipe.kitchenAssemblySteps.Count==0)
            {
                var steps=new List<KitchenAssemblyStep>();float height=0;
                AddStep("Bottom bun",bun,n=>n.StartsWith("BottomBun"));
                AddStep("Cooked "+protein,meat,n=>n==protein);
                var cheese=recipe.ingredients.FirstOrDefault(i=>i.item.itemType==ItemType.Cheese);
                if(cheese!=null)AddStep("Cheese",cheese.item,n=>n=="Cheese");
                AddStep("Top bun",bun,n=>n.StartsWith("Topbun")||n.StartsWith("Seame"));
                recipe.kitchenAssemblySteps=steps;
                void AddStep(string label,ItemData item,Func<string,bool> filter)
                {
                    var visual=Visual(recipe.DisplayName+" - "+label,source,filter,.72f,height,false);
                    var bounds=BoundsOf(visual);height=bounds.max.y;
                    steps.Add(new KitchenAssemblyStep {item=item,visual=visual,label=label,icon=item.sprite});
                }
            }
            if(recipe.kitchenCookingPrefab==null)recipe.kitchenCookingPrefab=Visual(recipe.DisplayName+" - Cooking protein",source,n=>n==protein,.72f,0,true);
            if(recipe.kitchenServingPrefab==null)
            {
                string path=Folder+"/"+recipe.DisplayName+" - Finished.prefab";
                var root=new GameObject(recipe.DisplayName+" - Finished");
                try {foreach(var step in recipe.kitchenAssemblySteps)UnityEngine.Object.Instantiate(step.visual,root.transform);recipe.kitchenServingPrefab=PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally {UnityEngine.Object.DestroyImmediate(root);}
            }
        }
        else
        {
            if(recipe.kitchenCookingPrefab==null)recipe.kitchenCookingPrefab=Visual(recipe.DisplayName+" - Cooking",source,n=>!n.StartsWith("Carton"),.60f,0,true,recipe.kitchenItemType!=ItemTypeKitchen.Chicken);
            if(recipe.kitchenServingPrefab==null)recipe.kitchenServingPrefab=Visual(recipe.DisplayName+" - Finished",source,n=>true,.55f,0,false);
        }
        if(recipe.kitchenPreviewPrefab==null)recipe.kitchenPreviewPrefab=recipe.kitchenServingPrefab;
        EditorUtility.SetDirty(recipe);
    }
    static Bounds BoundsOf(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<MeshRenderer>(true);var bounds=renderers[0].bounds;
        foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
    }
    static GameObject Visual(string name,GameObject source,Func<string,bool> include,float width,float baseHeight,bool draggable,bool layFlat=false)
    {
        string path=Folder+"/"+name+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
        var root=new GameObject(name);var geometry=new GameObject("Model");geometry.transform.SetParent(root.transform,false);
        try
        {
            var selected=source.GetComponentsInChildren<MeshRenderer>(true).Where(r=>include(r.name)).ToArray();
            if(selected.Length==0)throw new Exception("No mesh selected for "+name);
            foreach(var r in selected)
            {
                var mesh=new GameObject(r.name,typeof(MeshFilter),typeof(MeshRenderer));mesh.transform.SetParent(geometry.transform,false);
                mesh.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);mesh.transform.localScale=r.transform.lossyScale;
                mesh.GetComponent<MeshFilter>().sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;
                var renderer=mesh.GetComponent<MeshRenderer>();renderer.sharedMaterials=r.sharedMaterials.Select(InteriorMaterial).ToArray();renderer.receiveShadows=false;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            if(layFlat)geometry.transform.rotation=Quaternion.Euler(90,0,0);
            var bounds=BoundsOf(root);float scale=width/Mathf.Max(bounds.size.x,bounds.size.z);
            geometry.transform.localScale*=scale;bounds=BoundsOf(root);
            geometry.transform.position+=new Vector3(-bounds.center.x,baseHeight-bounds.min.y,-bounds.center.z);
            if(draggable)
            {
                bounds=BoundsOf(root);var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=new Vector3(Mathf.Max(.1f,bounds.size.x),Mathf.Max(.1f,bounds.size.y),Mathf.Max(.1f,bounds.size.z));root.AddComponent<FastFoodCookingDragHandle>();
            }
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
    static void DisableOilShadows()
    {
        const string path="Assets/_Project/Restaurant/CookingAssets/Oil/Fryer Oil.shadergraph";
        if(!System.IO.File.Exists(path))return;
        Type Find(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
        var type=Find("UnityEditor.ShaderGraph.GraphData");var graph=Activator.CreateInstance(type,true);
        var json=Find("UnityEditor.ShaderGraph.Serialization.MultiJson");
        json.GetMethod("Deserialize",BindingFlags.Static|BindingFlags.Public).MakeGenericMethod(type).Invoke(null,new object[]{graph,System.IO.File.ReadAllText(path),null,false});
        var targets=(IEnumerable)type.GetProperty("activeTargets").GetValue(graph);
        foreach(var target in targets)target.GetType().GetProperty("receiveShadows")?.SetValue(target,false);
        type.GetMethod("OnEnable").Invoke(graph,null);type.GetMethod("ValidateGraph").Invoke(graph,null);
        Find("UnityEditor.ShaderGraph.FileUtilities").GetMethod("WriteShaderGraphToDisk",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{path,graph});
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
    }
}
