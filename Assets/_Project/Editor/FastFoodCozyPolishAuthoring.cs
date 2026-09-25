using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class FastFoodCozyPolishAuthoring
{
    const string Batch="Assets/_Project/Restaurant/CookingAssets/Batch/";
    const string Hygiene="Assets/_Project/Resources/Hygiene/";
    const string GreyCircle="Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Grey/Double/icon_circle.png";
    static readonly Color Ink=new Color(.04f,.23f,.33f);

    [MenuItem("Dine In/Fast Food/Apply Cozy Kitchen Polish")]
    public static string Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode before authoring the saved kitchen.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="Lobby2")throw new InvalidOperationException("Open Lobby2 before authoring the kitchen.");
        var controller=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FastFoodCookingController>(true)).Single();
        var view=controller.GetComponent<FastFoodCookingView>();
        foreach(var rig in controller.GetComponentsInChildren<FastFoodCookingStation>(true).Where(r=>r.mode!=FastFoodStationMode.Assembler))
        {
            foreach(var bay in rig.cookingSlots)AuthorTimer(bay);
            Undo.RecordObject(rig,"Polish cooking movement");
            if(Mathf.Approximately(rig.cookingBobHeight,.003f))rig.cookingBobHeight=.012f;
        }
        AuthorStaffLabel(view);
        AuthorChicken();
        AuthorKitchenMarks(scene);
        var settings=HygieneSettings.Current;Undo.RecordObject(settings,"Balance kitchen dirt");
        settings.maxKitchenDirtCyclesPerDay=2;settings.kitchenDirtMultiplier=.5f;settings.autonomousKitchenUseSeconds=12;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Lobby2 save failed.");
        return "Saved: six circular timers, staff activity label, plated golden chicken, authored cooking marks and two daily kitchen mess cycles.";
    }

    static RectTransform Child(Transform parent,string name)
    {
        var found=parent.Find(name) as RectTransform;if(found!=null)return found;
        var go=new GameObject(name,typeof(RectTransform));Undo.RegisterCreatedObjectUndo(go,"Author kitchen presentation");
        go.transform.SetParent(parent,false);return (RectTransform)go.transform;
    }
    static void Stretch(RectTransform rect,Vector2 min,Vector2 max)
    {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}
    static void AuthorTimer(FastFoodCookingDropTarget bay)
    {
        if(bay.status==null)throw new InvalidOperationException("Missing saved timer label on "+bay.name);
        var root=(RectTransform)bay.status.transform.parent;
        Undo.RecordObject(root,"Shape circular cooking timer");root.sizeDelta=new Vector2(82,82);
        var old=root.GetComponent<UnityEngine.UI.Image>();if(old!=null){Undo.RecordObject(old,"Remove rectangular timer background");old.enabled=false;}
        foreach(var shadow in root.GetComponents<UnityEngine.UI.Shadow>()){Undo.RecordObject(shadow,"Remove timer card shadow");shadow.enabled=false;}
        var center=Child(root,"Circle Centre");Stretch(center,Vector2.one*.14f,Vector2.one*.86f);center.SetAsFirstSibling();
        var disc=center.GetComponent<UnityEngine.UI.Image>()??Undo.AddComponent<UnityEngine.UI.Image>(center.gameObject);
        disc.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(GreyCircle);disc.color=Color.white;disc.type=UnityEngine.UI.Image.Type.Simple;disc.preserveAspect=true;disc.raycastTarget=false;
        var ringRoot=Child(root,"Progress Ring");Stretch(ringRoot,Vector2.zero,Vector2.one);
        if(ringRoot.GetComponent<CanvasRenderer>()==null)Undo.AddComponent<CanvasRenderer>(ringRoot.gameObject);
        var ring=ringRoot.GetComponent<MultiplayerWorkCircleGraphic>()??Undo.AddComponent<MultiplayerWorkCircleGraphic>(ringRoot.gameObject);
        ring.raycastTarget=false;ring.color=new Color(.1f,.62f,.83f);
        var ringSettings=new SerializedObject(ring);ringSettings.FindProperty("innerRatio").floatValue=.82f;
        ringSettings.FindProperty("amount").floatValue=.65f;ringSettings.FindProperty("trackColor").colorValue=new Color(.73f,.79f,.84f);ringSettings.ApplyModifiedProperties();
        Undo.RecordObject(bay.status,"Style circular timer text");Stretch(bay.status.rectTransform,Vector2.one*.18f,Vector2.one*.82f);
        bay.status.fontSize=30;bay.status.enableAutoSizing=false;bay.status.color=Ink;bay.status.alignment=TextAlignmentOptions.Center;
        bay.status.text="8";bay.status.raycastTarget=false;bay.status.transform.SetAsLastSibling();
        var labelRoot=Child(root,"Bay Caption");Stretch(labelRoot,new Vector2(-.2f,1.02f),new Vector2(1.2f,1.32f));
        var label=labelRoot.GetComponent<TextMeshProUGUI>()??Undo.AddComponent<TextMeshProUGUI>(labelRoot.gameObject);
        label.font=bay.status.font;label.fontSize=19;label.alignment=TextAlignmentOptions.Center;label.color=Ink;label.text="STAFF";label.raycastTarget=false;
        var timer=root.GetComponent<FastFoodCookingTimer>()??Undo.AddComponent<FastFoodCookingTimer>(root.gameObject);
        timer.ring=ring;timer.seconds=bay.status;timer.caption=label;
        Undo.RecordObject(bay,"Bind circular timer");bay.timer=timer;EditorUtility.SetDirty(bay);EditorUtility.SetDirty(timer);
        root.gameObject.SetActive(false);
    }
    static void AuthorStaffLabel(FastFoodCookingView view)
    {
        var data=new SerializedObject(view);var heading=(TMP_Text)data.FindProperty("heading").objectReferenceValue;
        var goal=(TMP_Text)data.FindProperty("progress").objectReferenceValue;var panel=(RectTransform)heading.transform.parent;
        Undo.RecordObject(panel,"Make room for staff activity");panel.sizeDelta=new Vector2(panel.sizeDelta.x,144);
        Undo.RecordObject(heading.rectTransform,"Place station title");Stretch(heading.rectTransform,new Vector2(.04f,.72f),new Vector2(.97f,.96f));
        Undo.RecordObject(goal.rectTransform,"Place batch goal");Stretch(goal.rectTransform,new Vector2(.04f,.40f),new Vector2(.97f,.72f));
        var root=Child(panel,"Staff Activity");Stretch(root,new Vector2(.04f,.12f),new Vector2(.97f,.40f));
        var label=root.GetComponent<TextMeshProUGUI>()??Undo.AddComponent<TextMeshProUGUI>(root.gameObject);
        label.font=goal.font;label.fontSize=22;label.enableAutoSizing=true;label.fontSizeMin=18;label.fontSizeMax=22;
        label.color=Color.white;label.alignment=TextAlignmentOptions.MidlineLeft;label.overflowMode=TextOverflowModes.Ellipsis;
        label.text="Staff ready to help";label.raycastTarget=false;
        data.FindProperty("staffActivity").objectReferenceValue=label;data.ApplyModifiedProperties();
    }

    static Material FoodMaterial(string name,Material source,Color color,float smoothness)
    {
        string path=Batch+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
        material=new Material(source){name=name};material.SetColor("_BaseColor",color);material.SetColor("_Color",color);
        material.SetFloat("_Smoothness",smoothness);material.SetFloat("_Metallic",0);material.SetFloat("_ReceiveShadows",0);material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        AssetDatabase.CreateAsset(material,path);return material;
    }
    static void AuthorChicken()
    {
        var recipe=MenuCatalog.Default.Products.First(r=>r.kitchenItemType==ItemTypeKitchen.Chicken);
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Scenes/RoleBased/Kitchen/Materials/CookedchickenMaterial.mat");
        var gold=FoodMaterial("Cozy Chicken Crust",source,new Color(.94f,.57f,.20f),.18f);
        var bone=FoodMaterial("Cozy Chicken Bone",source,new Color(.98f,.91f,.77f),.15f);
        var plate=FoodMaterial("Cozy Ceramic Plate",source,new Color(.96f,.96f,.90f),.3f);
        string meshPath=Batch+"Fried Chicken Styled Mesh.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(mesh==null)
        {
            mesh=UnityEngine.Object.Instantiate(recipe.kitchenCookingPrefab.GetComponentInChildren<MeshFilter>().sharedMesh);mesh.name="Fried Chicken Styled Mesh";
            var vertices=mesh.vertices;var triangles=mesh.triangles;var meatTriangles=new List<int>();var boneTriangles=new List<int>();
            // The imported drumstick's narrow bone end is along negative local X.
            for(int i=0;i<triangles.Length;i+=3)
            {
                var target=(vertices[triangles[i]].x+vertices[triangles[i+1]].x+vertices[triangles[i+2]].x)/3f < -1.55f?boneTriangles:meatTriangles;
                target.Add(triangles[i]);target.Add(triangles[i+1]);target.Add(triangles[i+2]);
            }
            mesh.subMeshCount=2;mesh.SetTriangles(meatTriangles,0);mesh.SetTriangles(boneTriangles,1);AssetDatabase.CreateAsset(mesh,meshPath);
        }
        var sourcePlate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Scenes/RoleBased/Kitchen/Prefabs/Plate.prefab").transform.Find("VisualPlate").GetComponent<MeshFilter>().sharedMesh;
        foreach(var prefab in new[]{recipe.kitchenCookingPrefab,recipe.kitchenServingPrefab}.Distinct())
        {
            string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var food=root.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="FriedChicken");
                food.sharedMesh=mesh;food.GetComponent<MeshRenderer>().sharedMaterials=new[]{gold,bone};
                if(prefab==recipe.kitchenServingPrefab && root.transform.Find("Plate Visual")==null)
                {
                    var model=root.transform.Find("Model");model.localPosition+=Vector3.up*.035f;
                    var dish=new GameObject("Plate Visual",typeof(MeshFilter),typeof(MeshRenderer));dish.transform.SetParent(root.transform,false);
                    dish.GetComponent<MeshFilter>().sharedMesh=sourcePlate;
                    var bounds=sourcePlate.bounds;dish.transform.localScale=new Vector3(.82f/bounds.size.x,.028f/bounds.size.y,.82f/bounds.size.z);
                    dish.transform.localPosition=Vector3.up*.014f;dish.GetComponent<MeshRenderer>().sharedMaterial=plate;
                    dish.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
                    var rim=new GameObject("Plate Rim",typeof(MeshFilter),typeof(MeshRenderer));rim.transform.SetParent(root.transform,false);
                    rim.GetComponent<MeshFilter>().sharedMesh=PlateRim();rim.GetComponent<MeshRenderer>().sharedMaterial=plate;
                    rim.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        Undo.RecordObject(recipe,"Use plated fried chicken on serving trays");
        recipe.kitchenPreviewPrefab=recipe.kitchenServingPrefab;recipe.servingPrefab=recipe.kitchenServingPrefab;EditorUtility.SetDirty(recipe);
    }
    static Mesh PlateRim()
    {
        string path=Batch+"Cozy Plate Rim.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved!=null)return saved;
        var profile=new[]{new Vector2(.32f,.023f),new Vector2(.375f,.035f),new Vector2(.415f,.053f),new Vector2(.43f,.045f),new Vector2(.41f,.01f)};
        var vertices=new List<Vector3>();var triangles=new List<int>();const int segments=48;
        for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;foreach(var p in profile)vertices.Add(new Vector3(Mathf.Cos(a)*p.x,p.y,Mathf.Sin(a)*p.x));}
        for(int i=0;i<segments;i++)for(int j=0;j<profile.Length-1;j++)
        {int a=i*profile.Length+j,b=a+profile.Length;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
        var mesh=new Mesh{name="Cozy Plate Rim"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static Material Marks(string name,int style)
    {
        string path=Hygiene+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
        material=new Material(Shader.Find("DineIn/Kitchen Cooking Marks")){name=name};material.SetFloat("_MarkStyle",style);AssetDatabase.CreateAsset(material,path);return material;
    }
    public static void AuthorKitchenMarks(UnityEngine.SceneManagement.Scene scene)
    {
        var oil=Marks("KitchenCookingMarks",0);var patty=Marks("KitchenPattyMarks",1);var crumbs=Marks("KitchenPrepCrumbs",2);
        foreach(var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).Where(r=>r.transform.parent?.name=="Fast Food Revamp"))
        {
            bool cook=r.name.StartsWith("Grill")||r.name.StartsWith("Stove")||r.name.StartsWith("Fryer");
            bool prep=r.name.StartsWith("Prep")||r.name=="Cube.028"||r.name=="Cube.033";
            if(!cook&&!prep)continue;
            var surface=r.GetComponent<HygieneSurface>()??Undo.AddComponent<HygieneSurface>(r.gameObject);
            Undo.RecordObject(surface,"Author active kitchen surface");
            surface.area=HygieneArea.Kitchen;surface.kind=cook?HygieneSurfaceKind.Equipment:HygieneSurfaceKind.Counter;
            surface.kitchenUse=cook?HygieneKitchenUse.Cook:HygieneKitchenUse.Prep;
        }
        foreach(var surface in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<HygieneSurface>(true)).Where(s=>s.area==HygieneArea.Kitchen&&!s.exclude&&!s.floor))
        {
            Undo.RecordObject(surface,"Author cooking residue");
            var name=surface.name.ToLowerInvariant();surface.kitchenMarksMaterial=name.Contains("grill")||name.Contains("stove")?patty:surface.kitchenUse==HygieneKitchenUse.Cook||surface.kitchenUse==HygieneKitchenUse.Wash?oil:crumbs;
            var overlays=new List<MeshRenderer>();
            foreach(var renderer in surface.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name!="Hygiene Overlay").ToArray())
            {
                var mesh=renderer.GetComponent<MeshFilter>();if(mesh==null||mesh.sharedMesh==null)continue;
                var child=renderer.transform.Find("Hygiene Overlay");GameObject go;
                if(child!=null)go=child.gameObject;
                else{go=new GameObject("Hygiene Overlay",typeof(MeshFilter),typeof(MeshRenderer));Undo.RegisterCreatedObjectUndo(go,"Save editable cooking marks");go.transform.SetParent(renderer.transform,false);}
                go.layer=renderer.gameObject.layer;go.GetComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                var overlay=go.GetComponent<MeshRenderer>();overlay.sharedMaterials=Enumerable.Repeat(surface.kitchenMarksMaterial,mesh.sharedMesh.subMeshCount).ToArray();
                overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;overlay.enabled=false;overlays.Add(overlay);
            }
            surface.kitchenOverlays=overlays.ToArray();EditorUtility.SetDirty(surface);
        }
    }
}
