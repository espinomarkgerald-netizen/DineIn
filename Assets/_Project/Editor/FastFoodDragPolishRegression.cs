using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Runs against disposable objects and an isolated ledger; never consumes campaign stock.</summary>
public static class FastFoodDragPolishRegression
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("[Kitchen drag] "+message);}
    static Bounds BoundsOf(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
        var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);return bounds;
    }
    public static string Run()
    {
        Check(!Application.isPlaying,"Run outside Play Mode.");
        var source=UnityEngine.Object.FindObjectsByType<FastFoodCookingView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        var data=new SerializedObject(source);
        var material=(Material)data.FindProperty("ghostMaterial").objectReferenceValue;
        var destination=(Material)data.FindProperty("dropGhostMaterial").objectReferenceValue;
        Check(destination!=null,"Destination material is missing.");
        var scene=EditorSceneManager.NewPreviewScene();
        Mesh outline=null;
        int models=0;
        try
        {
            var root=new GameObject("Isolated kitchen drag check");SceneManager.MoveGameObjectToScene(root,scene);
            foreach(var template in MenuCatalog.Default.Products.SelectMany(r=>new[]{r.kitchenCookingPrefab,r.kitchenServingPrefab,r.kitchenPreviewPrefab}
                .Concat(FastFoodCookingState.AssemblySteps(r).Select(s=>s.visual))).Where(g=>g!=null).Distinct())
            {
                var original=UnityEngine.Object.Instantiate(template,root.transform);original.SetActive(true);
                var args=new object[]{original,material,Vector3.zero};
                var ghost=(GameObject)typeof(FastFoodCookingView).GetMethod("CreateFoodGhost",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
                ghost.transform.SetParent(root.transform,true);
                Check(Vector3.Distance(BoundsOf(ghost).center,ghost.transform.position)<.001f,template.name+" ghost pivot is off-centre.");
                Check(Vector3.Distance(BoundsOf(original).size,BoundsOf(ghost).size)<.001f,template.name+" ghost changed size.");
                Check(ghost.GetComponentsInChildren<Collider>(true).Length==0&&ghost.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"Ghost inherited behaviour or colliders.");
                UnityEngine.Object.DestroyImmediate(original);UnityEngine.Object.DestroyImmediate(ghost);models++;
            }
            var controller=root.AddComponent<FastFoodCookingController>();
            var view=root.AddComponent<FastFoodCookingView>();
            var ledger=new FastFoodCookingState(_=>1000,_=>true);
            ledger.ConfigureSlots(FastFoodStationMode.Grill,Enumerable.Repeat(FastFoodCookingSlotOwner.Player,8).ToArray());
            var recipe=MenuCatalog.Default.Products.First(r=>r.kitchenItemType==ItemTypeKitchen.Burger);
            ledger.EnsureReserve(new[]{recipe},8);ledger.Enter(FastFoodStationMode.Grill);
            typeof(FastFoodCookingController).GetProperty("State").SetValue(controller,ledger);
            Set(view,"owner",controller);
            var cameraGo=new GameObject("Perspective camera",typeof(Camera));cameraGo.transform.SetParent(root.transform);
            var camera=cameraGo.GetComponent<Camera>();camera.enabled=false;camera.pixelRect=new Rect(0,0,1920,1080);
            camera.transform.position=new Vector3(0,5,-6);camera.transform.LookAt(Vector3.zero);camera.fieldOfView=50;
            var canvasGo=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));canvasGo.transform.SetParent(root.transform);
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.scaleFactor=1;
            var rigGo=new GameObject("Grill fixture");rigGo.transform.SetParent(root.transform);
            var rig=rigGo.AddComponent<FastFoodCookingStation>();rig.mode=FastFoodStationMode.Grill;rig.stationCamera=camera;
            var slots=new FastFoodCookingDropTarget[2];
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Position "+i,typeof(BoxCollider),typeof(FastFoodCookingDropTarget));go.transform.SetParent(rig.transform);
                go.transform.position=new Vector3(i==0?-1.5f:1.5f,0,0);
                var box=go.GetComponent<BoxCollider>();box.size=new Vector3(.3f,.1f,.3f);
                var target=go.GetComponent<FastFoodCookingDropTarget>();target.kind=0;target.slotIndex=i;target.view=view;
                var anchor=new GameObject("Food anchor").transform;anchor.SetParent(go.transform,false);target.foodAnchor=anchor;slots[i]=target;
            }
            rig.cookingSlots=slots;rig.cooking=slots[0];
            var prep=new GameObject("Prep behind camera").transform;prep.SetParent(rig.transform);
            prep.position=camera.transform.position-camera.transform.forward*4;rig.prepFoodAnchor=prep;
            Set(view,"activeStation",rig);Set(view,"stationCamera",camera);Set(view,"canvas",canvas);
            Set(view,"ghostMaterial",material);Set(view,"dropGhostMaterial",destination);
            var handleGo=new GameObject("Hotbar ingredient",typeof(RectTransform),typeof(FastFoodCookingDragHandle));handleGo.transform.SetParent(root.transform);
            var handle=handleGo.GetComponent<FastFoodCookingDragHandle>();handle.item=FastFoodCookingState.Steps(recipe)[0].item;
            handle.previewTemplate=recipe.kitchenCookingPrefab;handle.view=view;
            Physics.SyncTransforms();
            Vector2 screen=camera.WorldToScreenPoint(slots[0].foodAnchor.position);
            view.BeginDrag(handle,screen);
            Check(Get<float>(view,"dragDepth")>1,"A behind-camera prep table collapsed the ghost depth.");
            var held=Get<GameObject>(view,"preview");Check(held!=null,"Drag failed to start.");
            outline=Get<Mesh>(view,"dropOutlineMesh");
            FastFoodCookingDropTarget Resolve(Vector2 point)=>(FastFoodCookingDropTarget)Call(view,"ResolveDropTarget",point,camera.ScreenPointToRay(point));
            Check(Resolve(screen+Vector2.up*45)==slots[0],"Magnet did not capture a near miss.");
            Check(Resolve(new Vector2(10,10))==null,"Far-away pointer snapped to a target.");
            var first=handle.portion;Check(ledger.Load(first,handle.item,0),"Fixture could not occupy the first position.");
            handle.portion=ledger.NextLoad(handle.item);
            Check(Resolve(screen)==null,"Occupied position remained magnetic.");
            Vector2 second=camera.WorldToScreenPoint(slots[1].foodAnchor.position);
            Check(Resolve(second)==slots[1],"Open second position was not available.");
            var item=handle.item;
            handle.item=FastFoodCookingState.Steps(MenuCatalog.Default.Products.First(r=>r.kitchenItemType==ItemTypeKitchen.Fries))[0].item;
            Check(Resolve(second)==null,"Wrong ingredient was accepted.");
            handle.item=item;slots[1].enabled=false;Check(Resolve(second)==null,"Disabled target was accepted.");slots[1].enabled=true;
            canvas.scaleFactor=2;
            Check(Resolve(second+Vector2.up*100)==slots[1],"Capture radius did not scale with the canvas.");
            canvas.scaleFactor=1;
            Check(Resolve(second+Vector2.up*100)==null,"Pointer outside the scaled radius was accepted.");
            Set(view,"drag",null);Set(view,"preview",null);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if(outline!=null)UnityEngine.Object.DestroyImmediate(outline);
        }
        FastFoodKitchenFixRegression.CheckTicket();
        return "PASS: "+models+" food ghost sizes and centred pivots; mesh-only copies; safe cooking depth; perspective near-miss snapping; occupied/wrong/disabled target rejection; scaled capture radius; ticket layout.";
    }
}
