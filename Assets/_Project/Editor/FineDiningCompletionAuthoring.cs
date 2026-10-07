#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Focused, repeat-safe scene authoring for Lobby3's existing systems.</summary>
public static class FineDiningCompletionAuthoring
{
    private static Scene scene;
    private static Transform[] nodes;
    private static Transform root;
    private static T Ensure<T>(GameObject go) where T:Component { var c=go.GetComponent<T>();if(c==null)c=go.AddComponent<T>();return c; }
    private static T Find<T>() where T:Component => nodes.Select(n=>n.GetComponent<T>()).FirstOrDefault(c=>c!=null);
    private static Transform Node(string name)=>nodes.First(n=>n.name==name);
    private static Transform Child(Transform parent,string name,Vector3 position)
    {
        var t=parent.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(parent,false);}t.position=position;return t;
    }
    private static void Ref(UnityEngine.Object owner,string name,UnityEngine.Object value)
    {var so=new SerializedObject(owner);var p=so.FindProperty(name);if(p==null)throw new Exception(owner.name+" missing "+name);p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static Vector3 Walkable(Vector3 p)
    {if(!NavMesh.SamplePosition(p,out var h,2.5f,NavMesh.AllAreas))throw new Exception("No navigation at "+p);return h.position;}

    [MenuItem("Dine In/Fine Dining/Apply Completion Configuration")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first");
        scene=SceneManager.GetSceneByName("Lobby3");if(!scene.IsValid()||!scene.isLoaded)throw new Exception("Open Lobby3 first");
        SceneManager.SetActiveScene(scene);
        nodes=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        root=Node("Fine Dining Service Bindings");
        var reservations=Find<FineDiningReservations>();
        reservations.reservationShare=.85f;reservations.requestsPerDay=6;reservations.requestCutoffHour=20;reservations.requestCadenceMinutes=20;reservations.requestLeadMinutes=30;
        var bindings=Find<FineDiningSceneBindings>();
        int[] days={1,1,1,3,5,2,4,6};
        for(int i=0;i<bindings.tables.Length;i++)
        {var t=bindings.tables[i];t.binding.unlockDay=t.binding.equipment!=null?t.binding.equipment.dayToUnlock:days[i];t.binding.diningSurface=bindings.FindFurniture(t.furniture).GetComponent<MeshRenderer>();EditorUtility.SetDirty(t.binding);}
        var waiting=Ensure<FineDiningWaitingArea>(root.gameObject);
        reservations.waitingArea=waiting;waiting.reservations=reservations;waiting.receptionLine=reservations.receptionLine;
        var seatRoot=Child(root,"Walk-in Waiting Seats",Vector3.zero);
        var chairs=bindings.importedModel.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("4 Seats.009")||r.name.StartsWith("4 Seats.010")).OrderBy(r=>r.bounds.center.z).ThenBy(r=>r.bounds.center.x).ToArray();
        waiting.seats=chairs.Select((r,i)=>{
            var center=r.bounds.center;var toward=new Vector3(-27,center.y,-76)-center;toward.y=0;toward.Normalize();
            var seat=Child(seatRoot,"Waiting Seat "+(i+1),new Vector3(center.x,r.bounds.min.y+.95f,center.z));seat.rotation=Quaternion.LookRotation(toward);
            var approach=Child(seatRoot,"Waiting Approach "+(i+1),Walkable(new Vector3(center.x,.56f,center.z)+toward*1.8f));
            return new FineDiningWaitingArea.Seat{seat=seat,approach=approach};}).ToArray();
        var progression=Ensure<FineDiningProgression>(root.gameObject);
        progression.tables=reservations.tables;
        progression.recipes=AssetDatabase.FindAssets("t:Recipe",new[]{"Assets/_Project/Office/Manager/Recipe/Fine Dining"}).Select(g=>AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        string[][] menu={new[]{"Pumpkin Soup","Chicken Alfredo","Sparkling Water","Grape Juice"},new[]{"Fish and Chips","Citrus Mocktail"},new[]{"Lasagna","Red Wine"},new[]{"Rib-Eye Steak","White Wine"},new[]{"Lobster","Martini"},new[]{"Old Fashioned","Sparkling Wine"}};
        foreach(var recipe in progression.recipes){int day=Array.FindIndex(menu,row=>row.Contains(recipe.recipeName))+1;if(day<1)throw new Exception("Missing recipe progression: "+recipe.name);recipe.dayToUnlock=day;EditorUtility.SetDirty(recipe);}
        ConfigureCashier(bindings);
        ConfigureNotification(reservations);
        ConfigurePiano();
        ConfigureEnvironment(bindings);
        EditorUtility.SetDirty(reservations);EditorUtility.SetDirty(waiting);EditorUtility.SetDirty(progression);
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        Debug.Log("[FineDining] Completion configuration saved: "+waiting.seats.Length+" physical waiting seats, 14 progressive recipes, 8 progressive tables.");
    }
    private static void ConfigureCashier(FineDiningSceneBindings bindings)
    {
        var cashier=Find<CashierBoothInteractable>();
        var bar=bindings.FindFurniture("Bar").GetComponent<Renderer>().bounds;
        var point=new Vector3(bar.min.x+1.8f,bar.max.y,bar.min.z+2f);
        cashier.name="Bar Cashier Target";cashier.transform.position=point;
        var approach=Node("Cashier Desk Approach");approach.position=Walkable(new Vector3(bar.min.x-1.5f,.56f,point.z));approach.rotation=Quaternion.LookRotation(point-approach.position);
        Ref(cashier,"standPoint",approach);
        var home=Node("CashierHomePoint");home.position=approach.position;home.rotation=approach.rotation;
        var actor=Node("Cashier");actor.position=home.position;actor.rotation=home.rotation;
        var visual=nodes.FirstOrDefault(t=>t.name=="Bar Cash Register");
        if(visual==null){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/3D Models/Cash Register Open.prefab");visual=((GameObject)PrefabUtility.InstantiatePrefab(prefab,scene)).transform;visual.SetParent(root,true);visual.name="Bar Cash Register";}
        visual.position=point;visual.rotation=Quaternion.Euler(0,90,0);visual.localScale=Vector3.one;
        foreach(var counter in visual.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Middle Counter")) counter.enabled=false;
        var rs=visual.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
        visual.localScale*=1.7f/Mathf.Max(bounds.size.x,bounds.size.z);
        bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);visual.position+=point-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        foreach(var c in visual.GetComponentsInChildren<Collider>())c.enabled=false;
        // Binding reapplication must keep the cashier at the bar, never return it to reception.
        foreach(var a in bindings.anchors.Where(a=>a.target==cashier.transform||a.target==approach||a.target==home))
        {a.furniture="Bar";a.normalizedOffset=Vector3.zero;a.floorLevel=false;var modelPoint=bindings.importedModel.InverseTransformPoint(a.target.position);var mesh=bindings.FindFurniture("Bar").GetComponent<MeshFilter>();var center=bindings.importedModel.InverseTransformPoint(mesh.transform.TransformPoint(mesh.sharedMesh.bounds.center));a.offset=modelPoint-center;}
    }
    private static void ConfigureNotification(FineDiningReservations reservations)
    {
        var station=nodes.Select(t=>t.GetComponent<ManagementComputerStation>()).First(x=>x!=null&&x.Purpose==RestaurantComputerPurpose.Reservations);
        var owner=Child(root,"Reception Request Notification",station.transform.position+Vector3.up*3f);
        var notification=Ensure<FineDiningReceptionNotification>(owner.gameObject);
        var canvas=Ensure<Canvas>(owner.gameObject);canvas.renderMode=RenderMode.WorldSpace;owner=canvas.transform;
        nodes=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var rect=owner.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(100,100);rect.localScale=Vector3.one*.018f;
        var badge=owner.Find("Badge");if(badge==null){badge=new GameObject("Badge",typeof(RectTransform),typeof(UnityEngine.UI.Image)).transform;badge.SetParent(owner,false);}
        var image=badge.GetComponent<UnityEngine.UI.Image>();var panel=AssetDatabase.LoadAssetAtPath<ManagementReservationPanel>("Assets/_Project/ManagementComputer/Prefabs/ManagementReservationsPanel.prefab");image.sprite=panel.redButton;image.type=UnityEngine.UI.Image.Type.Sliced;image.raycastTarget=false;
        var text=badge.GetComponentInChildren<TMPro.TextMeshProUGUI>();if(text==null){var go=new GameObject("Count",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));go.transform.SetParent(badge,false);text=go.GetComponent<TMPro.TextMeshProUGUI>();}
        text.font=panel.requestsLabel.font;text.fontSize=44;text.alignment=TMPro.TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
        notification.reservations=reservations;notification.badge=badge.gameObject;notification.count=text;notification.pulseTarget=badge;
    }
    private static void ConfigurePiano()
    {
        var worker=nodes.Select(t=>t.GetComponent<KitchenWorkerBot>()).First(x=>x!=null&&x.EmployeeRole==EmployeeRole.Pianist);
        var piano=Node("Meshy_Golden Grand Piano_mesh_node").GetComponent<Renderer>().bounds;
        var emitter=Child(root,"Piano Music Notes",piano.center+Vector3.up*2.5f);
        var ps=Ensure<ParticleSystem>(emitter.gameObject);ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=true;main.playOnAwake=false;main.startLifetime=2.8f;main.startSpeed=.45f;main.startSize=.6f;main.maxParticles=8;main.simulationSpace=ParticleSystemSimulationSpace.World;
        emitter.rotation=Quaternion.Euler(-90,0,0);var emission=ps.emission;emission.rateOverTime=1.2f;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.6f;
        string path="Assets/_Project/Restaurant/Assets/FineDiningDecorMaterials/Music Note.asset";
        var note=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(note==null){var combines=new System.Collections.Generic.List<CombineInstance>();foreach(var spec in new[]{new Vector4(-.16f,-.2f,.24f,.17f),new Vector4(.03f,.12f,.065f,.5f),new Vector4(.18f,.34f,.3f,.075f)}){var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);combines.Add(new CombineInstance{mesh=primitive.GetComponent<MeshFilter>().sharedMesh,transform=Matrix4x4.TRS(new Vector3(spec.x,spec.y,0),Quaternion.identity,new Vector3(spec.z,spec.w,.05f))});UnityEngine.Object.DestroyImmediate(primitive);}note=new Mesh{name="Stylized music note"};note.CombineMeshes(combines.ToArray());AssetDatabase.CreateAsset(note,path);}
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=note;renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Restaurant/Assets/FineDiningDecorMaterials/Warm Stone.mat");renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.85f,.5f),0)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});color.color=gradient;
        Ref(worker,"musicNotes",ps);
        var so=new SerializedObject(worker);var hands=so.FindProperty("performanceHands");hands.arraySize=2;
        var bones=worker.GetComponentsInChildren<Transform>(true);hands.GetArrayElementAtIndex(0).objectReferenceValue=bones.First(t=>t.name=="hand.L");hands.GetArrayElementAtIndex(1).objectReferenceValue=bones.First(t=>t.name=="hand.R");so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void ConfigureEnvironment(FineDiningSceneBindings bindings)
    {
        var lighting=Find<FineDiningEveningLighting>();
        var exterior=nodes.Select(t=>t.GetComponent<Light>()).Where(l=>l!=null&&(l.name.Contains("Perimeter")||l.name=="Warm Lantern Light")).ToArray();lighting.exteriorLights=exterior;
        foreach(var light in exterior){light.lightmapBakeType=LightmapBakeType.Realtime;light.shadows=LightShadows.None;light.color=new Color(1,.76f,.48f);}
        var fountain=Node("Fountain");var bounds=fountain.GetComponent<Renderer>().bounds;
        var waterRoot=Child(fountain,"Running Water",Vector3.zero);
        if(waterRoot.childCount==0){var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/MainMenu/GameMenu/Art/OutdoorProps/District Materials/Park Rounded Low Poly.asset");var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/MainMenu/GameMenu/Art/OutdoorProps/District Materials/Fountain Water Glints.mat");
            for(int i=0;i<3;i++){float angle=i*Mathf.PI*2/3;LevelSelectDistrictAuthoring.CreateWaterParticles(waterRoot,mesh,"Fountain jet "+(i+1),new Vector3(bounds.center.x+Mathf.Cos(angle)*.2f,bounds.max.y-.1f,bounds.center.z+Mathf.Sin(angle)*.2f),mat,3.4f,.8f,16,.07f,.15f,1);}
            LevelSelectDistrictAuthoring.CreateWaterParticles(waterRoot,mesh,"Fountain shimmer",new Vector3(bounds.center.x,bounds.min.y+.25f,bounds.center.z),mat,.12f,1.3f,5,.1f,bounds.extents.x*.65f,0);}
        // Two small service-area arrangements reuse the existing tabletop vase.
        var decor=Node("Fine Dining Decorations");
        var accent=nodes.First(t=>t.name=="Table 1 Table Accent");
        foreach(var placement in new[]{new {name="Reception Table Accent",position=new Vector3(-24.2f,3.61f,-72.7f)},new {name="Bar Table Accent",position=new Vector3(-8.5f,3.8f,-71.5f)}})
        {if(decor.Find(placement.name)==null){var clone=UnityEngine.Object.Instantiate(accent.gameObject,decor).transform;clone.name=placement.name;clone.position=placement.position;}}
        var outside=Node("Fine Dining Surroundings");
        var bench=nodes.First(t=>t.name=="Arrival Bench");
        if(outside.Find("Fountain Bench")==null){var clone=UnityEngine.Object.Instantiate(bench.gameObject,outside).transform;clone.name="Fountain Bench";clone.position=new Vector3(-40,.7f,-36);clone.rotation=Quaternion.Euler(0,90,0);}
var source=nodes.First(t=>t.name=="Interior Planter 3");
        var positions=new[]{new Vector3(-36,.56f,-78),new Vector3(-18,.56f,-88),new Vector3(-12,.56f,-83),new Vector3(59,.56f,-73),new Vector3(63,.56f,-69)};
        for(int i=0;i<positions.Length;i++){string name="Cozy Planter "+(i+1);if(decor.Find(name)!=null)continue;var clone=UnityEngine.Object.Instantiate(source.gameObject,decor).transform;clone.name=name;clone.position=positions[i];}
        EditorUtility.SetDirty(lighting);
    }
}
#endif


