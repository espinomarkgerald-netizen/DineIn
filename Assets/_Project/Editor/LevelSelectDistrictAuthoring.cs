#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class LevelSelectDistrictAuthoring
{
    private const string Art = "Assets/_Project/MainMenu/GameMenu/Art/OutdoorProps";
    private const string Materials = Art + "/District Materials";
    private static Transform root;
    private static Material grass, paving, curb, gold, dark, wood, foliage, pot;

    [MenuItem("Dine In/Polish/Author Restaurant District")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit play mode before authoring.");
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "NewGameMenu") throw new InvalidOperationException("Open NewGameMenu first.");
        if (GameObject.Find("Restaurant District") != null) throw new InvalidOperationException("District already exists; edit its authored objects directly.");
        var selector = UnityEngine.Object.FindFirstObjectByType<RestaurantSelector>();
        if (selector == null || selector.travelPoints.Length != 3) throw new InvalidOperationException("Expected three existing restaurant stops.");
        if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder(Art, "District Materials");
        grass = Mat("District Grass", new Color(.31f,.47f,.34f));
        paving = Mat("Warm Paving", new Color(.73f,.69f,.58f));
        curb = Mat("Cream Curb", new Color(.91f,.84f,.67f));
        gold = Mat("Restaurant Stop Gold", new Color(.95f,.64f,.21f));
        dark = Mat("Street Metal", new Color(.16f,.23f,.24f));
        wood = Mat("Bench Wood", new Color(.49f,.29f,.15f));
        foliage = Mat("Planter Leaves", new Color(.27f,.43f,.23f));
        pot = Mat("Planter Pot", new Color(.64f,.49f,.34f));
        root = new GameObject("Restaurant District").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Author restaurant district");
        // All decorations are ordinary editable scene objects. No runtime spawning.
        Cube("District lawn", new Vector3(-11,-.27f,2), new Vector3(32,.20f,49), grass);
        Cube("Connected promenade curb", new Vector3(-7.73f,-.12f,2), new Vector3(3.7f,.12f,34), curb);
        Cube("Connected promenade", new Vector3(-7.73f,-.045f,2), new Vector3(3.3f,.06f,34), paving);
        var restaurants = GameObject.Find("Restaurants").transform;
        for (int i=0;i<3;i++)
        {
            var point=selector.travelPoints[i];
            var renderers=restaurants.GetChild(i).GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;
            foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            // Match existing building positions, rather than shift the travel destinations.
            Cube("Restaurant " +(i+1)+" apron", new Vector3(bounds.center.x,-.06f,bounds.center.z), new Vector3(bounds.size.x+.8f,.10f,bounds.size.z+.5f), paving);
            Cube("Entrance link " +(i+1), new Vector3((bounds.max.x+point.position.x)*.5f,-.025f,point.position.z), new Vector3(Mathf.Abs(point.position.x-bounds.max.x)+.4f,.05f,2.4f), paving);
            Disk("Stop " +(i+1)+" rim", new Vector3(point.position.x,-.005f,point.position.z),2.0f,.025f,gold);
            Disk("Stop " +(i+1)+" paving", new Vector3(point.position.x,.025f,point.position.z),1.76f,.018f,curb);
            // Retain the original travel Transform and character height.
            foreach(var r in point.GetComponentsInChildren<Renderer>()) { Undo.RecordObject(r,"Hide old stop disc");r.enabled=false; }
            Prop("Street Light",new Vector3(-4.8f,0,point.position.z+2.7f),3.1f,90,dark);
            Prop("Planter",new Vector3(bounds.max.x+.28f,0,point.position.z-2.8f),.85f,90,pot);
        }
        // Seating and a shared fountain pocket sit off the chef's navigation lane.
        Disk("Fountain pocket curb",new Vector3(-2.4f,-.06f,5.9f),2.9f,.05f,curb);
        Disk("Fountain pocket paving",new Vector3(-2.4f,0,5.9f),2.64f,.025f,paving);
        Prop("Fountain",new Vector3(-2.4f,.06f,5.9f),.65f,0,null);
        Prop("Bench",new Vector3(-4.25f,0,-4.7f),1.1f,0,wood);
        Prop("Bench",new Vector3(-4.25f,0,9.0f),1.1f,0,wood);
        Prop("Planter",new Vector3(-3.9f,0,-6.3f),.8f,0,pot);
        Prop("Planter",new Vector3(-3.9f,0,10.7f),.8f,0,pot);
        for(float z=-13.6f;z<17;z+=1.7f)
        {
            if(selector.travelPoints.Any(p=>Mathf.Abs(p.position.z-z)<2.25f)) continue;
            Disk("Route marker",new Vector3(-7.73f,.012f,z),.14f,.01f,curb);
        }
        // A quiet rear border gives the district a finished edge without obscuring the buildings.
        for(float z=-16;z<=22;z+=3.8f)
            Cube("Low garden border",new Vector3(-22,.18f,z),new Vector3(1.0f,.55f,3.3f),foliage);
        var follow=UnityEngine.Object.FindFirstObjectByType<CameraFollow>();
        var camera=follow.GetComponent<Camera>();
        Undo.RecordObjects(new UnityEngine.Object[]{camera,camera.transform},"Frame restaurant district");
        var focus=selector.travelPoints[0].position+new Vector3(-3,0,1.3f);
        camera.transform.position=focus+new Vector3(11,16,-14);
        camera.transform.LookAt(focus);
        camera.orthographicSize=8.8f;
        var followSO=new SerializedObject(follow);
        followSO.FindProperty("compositionReference").objectReferenceValue=selector.travelPoints[0];followSO.ApplyModifiedProperties();
        AuthorLabel(selector);
        AuthorMusic(scene);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Restaurant District] Authored editable map, props, Anton label and menu music.");
    }
    private static Material Mat(string name,Color color)
    {
        var path=Materials+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);return m;
    }
    private static GameObject Shape(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(root);go.transform.position=pos;go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=mat;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    private static void Cube(string name,Vector3 pos,Vector3 scale,Material mat)=>Shape(name,PrimitiveType.Cube,pos,scale,mat);
    private static void Disk(string name,Vector3 pos,float radius,float halfHeight,Material mat)=>Shape(name,PrimitiveType.Cylinder,pos,new Vector3(radius*2,halfHeight,radius*2),mat);
    private static void Prop(string name,Vector3 pos,float height,float yaw,Material overrideMaterial)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/"+name+".fbx");
        if(asset==null) throw new InvalidOperationException("Missing prop "+name);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,root);go.name=name;
        go.transform.rotation=Quaternion.Euler(0,yaw,0)*asset.transform.rotation;
        var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
        go.transform.localScale*=height/b.size.y;
        b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
        go.transform.position+=pos-new Vector3(b.center.x,b.min.y,b.center.z);
        if(overrideMaterial!=null)foreach(var r in rs)r.sharedMaterials=r.sharedMaterials.Select(_=>overrideMaterial).ToArray();
        if(name=="Planter")
        {
            // The archive contains no planter texture: split the supplied mesh by planter/foliage height.
            foreach(var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                string path=Materials+"/Planter Colored.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null)
                {
                    mesh=UnityEngine.Object.Instantiate(mf.sharedMesh);mesh.name="Planter Colored";
                    var verts=mesh.vertices;var tris=mesh.triangles;var lower=new List<int>();var upper=new List<int>();
                    float cut=mf.GetComponent<Renderer>().bounds.min.y+mf.GetComponent<Renderer>().bounds.size.y*.45f;
                    for(int i=0;i<tris.Length;i+=3){var list=mf.transform.TransformPoint((verts[tris[i]]+verts[tris[i+1]]+verts[tris[i+2]])/3).y>cut?upper:lower;list.Add(tris[i]);list.Add(tris[i+1]);list.Add(tris[i+2]);}
                    mesh.subMeshCount=2;mesh.SetTriangles(lower,0);mesh.SetTriangles(upper,1);AssetDatabase.CreateAsset(mesh,path);
                }
                mf.sharedMesh=mesh;mf.GetComponent<Renderer>().sharedMaterials=new[]{pot,foliage};
            }
        }
        foreach(var r in rs)r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
    }
    private static void AuthorLabel(RestaurantSelector selector)
    {
        var canvas=GameObject.Find("GameCanvas").transform;
        var go=new GameObject("RestaurantName",typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=5;go.transform.SetParent(canvas,false);go.transform.SetSiblingIndex(2);
        var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(24,24);rect.sizeDelta=new Vector2(250,48);
        var label=go.GetComponent<TextMeshProUGUI>();label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Assets/Fonts/Anton/Anton-Regular SDF.asset");
        label.text="Casual Dining";label.fontSize=26;label.color=Color.white;label.raycastTarget=false;label.alignment=TextAlignmentOptions.BottomLeft;label.textWrappingMode=TextWrappingModes.NoWrap;
        var so=new SerializedObject(selector);so.FindProperty("restaurantNameLabel").objectReferenceValue=label;so.ApplyModifiedProperties();
    }
    private static void AuthorMusic(Scene destination)
    {
        if(UnityEngine.Object.FindFirstObjectByType<MusicManager>()!=null) return;
        var main=EditorSceneManager.OpenScene("Assets/_Project/Scenes/NewMenu/NewMainMenu.unity",OpenSceneMode.Additive);
        try
        {
            var manager=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MusicManager>(true)).First();
            var copy=UnityEngine.Object.Instantiate(manager.gameObject);copy.name="Menu Music";
            SceneManager.MoveGameObjectToScene(copy,destination);
            foreach(var source in copy.GetComponentsInChildren<AudioSource>())source.playOnAwake=false;
        }
        finally{EditorSceneManager.CloseScene(main,true);SceneManager.SetActiveScene(destination);}
    }
}
#endif
