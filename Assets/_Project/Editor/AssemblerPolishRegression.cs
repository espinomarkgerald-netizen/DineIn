#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AssemblerPolishRegression
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Key="DineIn.AssemblerPolishTest",Path="Assets/_Project/Editor/AssemblerPolishTestScene.unity";
    static GameObject root,placed,returned;
    static FastFoodCookingView view;
    static Vector3 finalPosition;
    static double started;
    static bool initialized;
    static void Set(object o,string field,object value)=>o.GetType().GetField(field,Private).SetValue(o,value);
    static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,Private).GetValue(o);
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Private).Invoke(o,args);
    static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException("[Assembler test] "+message);}
    static Bounds BoundsOf(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
        var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
    }
    static AssemblerPolishRegression()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){initialized=false;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update-=Tick;
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key+".previous",""));
                // This empty test scene was created by RunMotion and is not a game asset.
                AssetDatabase.DeleteAsset(Path);SessionState.SetBool(Key,false);
            }
        };
    }
    [MenuItem("Dine In/Fast Food/Validate Assembler Polish")]
    public static void Validate()
    {
        Check(!Application.isPlaying,"Use Edit Mode.");
        try{Fixture();Debug.Log("[Assembler test] PASS: all serving prefab bottoms, bounded footprints, unique slots, food/drink ledger serving, mouse/touch magnetic radii and rejected far drops.");}
        finally{Cleanup();}
    }
    [MenuItem("Dine In/Fast Food/Validate Assembler HUD Layout")]
    public static void ValidateLayout()
    {
        Check(!Application.isPlaying,"Use Edit Mode.");
        var source=UnityEngine.Object.FindObjectsByType<FastFoodCookingView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        var canvas=Get<Canvas>(source,"canvas");var rootRect=(RectTransform)canvas.transform;
        var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        var staff=(RectTransform)Get<TMPro.TMP_Text>(source,"staffActivity").transform.parent;
        var cue=(RectTransform)Get<FastFoodCookingToast>(source,"floatingCue").transform;
        var tickets=Get<RectTransform>(source,"tickets");
        var serve=(RectTransform)Get<UnityEngine.UI.Button>(source,"serve").transform;
        var header=(RectTransform)Get<TMPro.TMP_Text>(source,"heading").transform.parent;
        Rect Calculate(RectTransform rect,Rect screen)
        {
            if(rect==rootRect)return screen;
            var parent=Calculate((RectTransform)rect.parent,screen);
            var min=parent.min+Vector2.Scale(parent.size,rect.anchorMin)+rect.offsetMin;
            var max=parent.min+Vector2.Scale(parent.size,rect.anchorMax)+rect.offsetMax;
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        foreach(var pixels in new[]{new Vector2(1280,720),new Vector2(1920,1200),new Vector2(2340,1080),new Vector2(1920,1440)})
        foreach(float scale in new[]{.85f,1,1.15f})
        {
            float units=Mathf.Pow(pixels.x/scaler.referenceResolution.x,1-scaler.matchWidthOrHeight)*Mathf.Pow(pixels.y/scaler.referenceResolution.y,scaler.matchWidthOrHeight)*scale;
            var screen=new Rect(Vector2.zero,(pixels-new Vector2(100,0))/units);
            var rects=new[]{header,cue,tickets,serve}.Select(r=>Calculate(r,screen)).ToArray();
            for(int i=0;i<rects.Length;i++)
            {
                Check(rects[i].xMin>=-.1f && rects[i].yMin>=-.1f && rects[i].xMax<=screen.xMax+.1f && rects[i].yMax<=screen.yMax+.1f,"HUD clipped at "+pixels+" / "+scale);
                for(int j=0;j<i;j++)Check(!rects[i].Overlaps(rects[j]),"HUD lanes overlap at "+pixels+" / "+scale+" elements "+i+"/"+j);
            }
            Check(rects[3].height*units>=44,"Serve touch target too small.");
        }
        Debug.Log("[Assembler test] LAYOUT PASS: header/status/cue/ticket/serve bounds and separation at 16:9, 16:10, phone and tablet ratios; UI scales .85–1.15 with notch allowance.");
    }
    static void Fixture()
    {
        root=new GameObject("Isolated assembler test");
        var controller=root.AddComponent<FastFoodCookingController>();controller.enabled=false;
        view=root.AddComponent<FastFoodCookingView>();view.enabled=false;
        var state=new FastFoodCookingState(_=>1000,_=>true);
        typeof(FastFoodCookingController).GetProperty("State").SetValue(controller,state);Set(view,"owner",controller);
        var rig=new GameObject("Assembly fixture").AddComponent<FastFoodCookingStation>();rig.transform.SetParent(root.transform);
        rig.mode=FastFoodStationMode.Assembler;rig.assemblySurface=rig.prepFoodAnchor=rig.transform;rig.assemblyArea=new Vector2(2.1f,2.1f);
        rig.trayAnchors=Array.Empty<Transform>();
        var target=new GameObject("Tray",typeof(BoxCollider),typeof(FastFoodCookingDropTarget)).GetComponent<FastFoodCookingDropTarget>();
        target.transform.SetParent(rig.transform,false);target.kind=1;target.view=view;rig.preparation=target;
        var camera=new GameObject("Test camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(root.transform);
        camera.enabled=false;camera.pixelRect=new Rect(0,0,1920,1080);camera.transform.position=new Vector3(0,4,5);camera.transform.LookAt(Vector3.zero);
        rig.stationCamera=camera;
        var canvas=new GameObject("Test canvas",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();canvas.transform.SetParent(root.transform);
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        Set(view,"activeStation",rig);Set(view,"stationCamera",camera);Set(view,"canvas",canvas);Set(view,"prepTarget",target);
        var recipes=MenuCatalog.Default.Products.Where(r=>r!=null&&r.kitchenServingPrefab!=null).ToArray();
        Check(recipes.Length>1,"Missing food prefabs.");
        var ticket=new FastFoodCookingState.Ticket{number=713,active=true};
        foreach(var recipe in recipes)
        {
            var p=new FastFoodCookingState.Portion{recipe=recipe,order=713,stage=FastFoodCookingStage.Complete};
            ticket.portions.Add(p);state.Portions.Add(p);
        }
        state.Tickets.Add(ticket);state.Enter(FastFoodStationMode.Assembler);
        var bounds=new List<Bounds>();
        foreach(var portion in ticket.portions)
        {
            var food=UnityEngine.Object.Instantiate(portion.recipe.kitchenServingPrefab,rig.transform);food.SetActive(true);
            Call(view,"SeatAssemblyFood",food,portion);
            var b=BoundsOf(food);
            Check(Mathf.Abs(b.min.y-rig.assemblySurfaceOffset)<.001f,"Food floats: "+portion.recipe.name);
            foreach(var previous in bounds)Check(!b.Intersects(previous),"Food footprints overlap.");
            bounds.Add(b);UnityEngine.Object.DestroyImmediate(food);
        }
        for(int count=1;count<=24;count++)
        {
            var points=new HashSet<Vector3>();
            for(int i=0;i<count;i++)Check(points.Add(rig.AssemblySlot(i,count,out _)),"Repeated slot in large order.");
        }
        var handle=new GameObject("Serving source",typeof(RectTransform),typeof(FastFoodCookingDragHandle)).GetComponent<FastFoodCookingDragHandle>();
        handle.transform.SetParent(root.transform);handle.view=view;handle.portion=ticket.portions[0];
        Vector2 start=camera.WorldToScreenPoint(new Vector3(-.5f,.5f,1));
        view.BeginDrag(handle,start,false);
        var preview=Get<GameObject>(view,"preview");Check(preview!=null,"Serving drag rejected.");
        Vector2 landing=camera.WorldToScreenPoint((Vector3)Call(view,"AssemblyLandingPosition"));
        FastFoodCookingDropTarget Resolve(Vector2 p)=>(FastFoodCookingDropTarget)Call(view,"ResolveDropTarget",p,camera.ScreenPointToRay(p));
        float radius=(float)Call(view,"AssemblyCaptureRadius");
        Check(Resolve(landing+Vector2.right*(radius*.8f))==target,"Near mouse drop rejected.");
        Check(Resolve(landing+Vector2.right*(radius*1.1f))==null,"Far mouse drop accepted.");
        Set(view,"assemblyTouch",true);
        Check(Resolve(landing+Vector2.right*(radius*1.1f))==target,"Touch radius not forgiving.");
        Check(Resolve(new Vector2(5,5))==null,"Far drop accepted.");
        float height=preview.transform.position.y;
        view.MoveDrag(start+Vector2.right*80);
        Check(Mathf.Abs(preview.transform.position.y-height)<.001f,"Drag left its plane.");
        Check(state.Place(handle.portion),"Valid portion not placed.");
        Check(!state.Place(handle.portion),"Duplicate placement accepted.");
        Check(!state.Serve(ticket.number),"Incomplete order served.");
        if(Application.isPlaying)Call(view,"CancelDrag");
        else
        {
            var mesh=Get<Mesh>(view,"dropOutlineMesh");if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
            Set(view,"dropOutlineMesh",null);Set(view,"drag",null);Set(view,"preview",null);
        }
        foreach(var p in ticket.portions.Where(p=>!p.placed)){Check(state.BeginServingDrag(p),"Remaining portion not draggable.");Check(state.Place(p),"Remaining portion not placed.");}
        Check(state.Serve(ticket.number),"Completed order not served.");
        // Separate order for runtime animation, without campaign inventory or save callbacks.
        state.Tickets.Add(new FastFoodCookingState.Ticket{number=714,active=true,player=true});
        Set(view,"opened",true);
    }
    static void Cleanup()
    {
        if(view!=null)Set(view,"opened",false);
        if(root!=null)UnityEngine.Object.DestroyImmediate(root);
        root=null;view=null;
    }
    [MenuItem("Dine In/Fast Food/Test Assembler Motion In Play Mode")]
    public static void RunMotion()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play Mode first.");
        // Unity retains the open scenes' in-memory state across Play Mode.
        // Override only the test start scene; never save or close the user's scenes.
        Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(Path)==null,"Test path already exists; refusing to overwrite.");
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        EditorSceneManager.SaveScene(scene,Path);EditorSceneManager.CloseScene(scene,true);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        SessionState.SetString(Key+".previous",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Path);
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        try
        {
            if(!initialized)
            {
                Fixture();initialized=true;
                placed=GameObject.CreatePrimitive(PrimitiveType.Cube);placed.transform.SetParent(root.transform);
                returned=GameObject.CreatePrimitive(PrimitiveType.Cube);returned.transform.SetParent(root.transform);
                finalPosition=new Vector3(.5f,0,0);
                view.StartCoroutine((IEnumerator)Call(view,"MoveAssemblyFood",placed,Vector3.zero,finalPosition,.16f,false));
                view.StartCoroutine((IEnumerator)Call(view,"MoveAssemblyFood",returned,Vector3.one,Vector3.zero,.2f,true));
                started=EditorApplication.timeSinceStartup;return;
            }
            if(EditorApplication.timeSinceStartup-started<.45)return;
            Check(placed!=null&&Vector3.Distance(placed.transform.position,finalPosition)<.001f,"Snap did not finish.");
            Check(returned==null,"Invalid-drop return was not cleaned up.");
            Debug.Log("[Assembler test] PLAY PASS: fixture checks plus snap and invalid-drop return coroutines complete.");
            Finish();
        }
        catch(Exception e){Debug.LogException(e);Finish();}
    }
    static void Finish(){EditorApplication.update-=Tick;Cleanup();EditorApplication.ExitPlaymode();}
}
#endif
