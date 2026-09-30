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
    private static Mesh pebble;
    private static readonly List<ParticleSystem> effects = new List<ParticleSystem>();

    [MenuItem("Dine In/Polish/Upgrade Restaurant Park")]
    public static void Polish()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "NewGameMenu")
            throw new InvalidOperationException("Open NewGameMenu in Edit Mode first.");
        var district = GameObject.Find("Restaurant District");
        if (district == null) throw new InvalidOperationException("Author the district first.");
        if (district.transform.Find("Rolling park") != null) throw new InvalidOperationException("Park already authored; tune its scene objects.");
        var selector = UnityEngine.Object.FindFirstObjectByType<RestaurantSelector>();
        Undo.RegisterFullObjectHierarchyUndo(district, "Polish restaurant park");
        root = new GameObject("Rolling park").transform;
        root.SetParent(district.transform);
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Polish restaurant park");
        effects.Clear();
        grass = Mat("Park Grass", new Color(.40f,.67f,.29f));
        paving = Mat("Park Path Sand", new Color(.88f,.72f,.46f));
        curb = Mat("Park Path Cream", new Color(1,.88f,.62f));
        var grassLight = Mat("Park Grass Light", new Color(.43f,.69f,.31f));
        var grassDeep = Mat("Park Grass Deep", new Color(.38f,.64f,.28f));
        var pink = Mat("Park Coral Flowers", new Color(1,.32f,.44f));
        var lavender = Mat("Park Lilac Flowers", new Color(.65f,.43f,.88f));
        var yellow = Mat("Park Golden Flowers", new Color(1,.78f,.15f));
        var leaves = Mat("Park Leaf Green", new Color(.25f,.55f,.24f));
        var lime = Mat("Park Leaf Lime", new Color(.55f,.76f,.23f));
        var bark = Mat("Park Warm Bark", new Color(.46f,.29f,.15f));
        var water = Mat("Fountain Turquoise Water", new Color(.12f,.70f,.84f));
        water.SetFloat("_Smoothness", .65f);
        var stone = Mat("Fountain Warm Stone", new Color(.89f,.84f,.66f));
        var stoneTrim = Mat("Fountain Teal Trim", new Color(.24f,.57f,.60f));
        pebble = SaveMesh("Park Rounded Low Poly", RoundedMesh());
        foreach (Transform child in district.transform)
        {
            if (child.name == "District lawn" || child.name.StartsWith("Connected promenade") || child.name == "Low garden border" || child.name == "Route marker")
                child.gameObject.SetActive(false);
            if (child.name.Contains("apron") || child.name.StartsWith("Entrance link"))
                child.GetComponent<Renderer>().sharedMaterial = paving;
            if (child.name.EndsWith("paving")) child.GetComponent<Renderer>().sharedMaterial = curb;
        }
        // The old street slab intersects the flat lawn, causing dark depth-fighting patches.
        // Keep any colliders; only the obsolete ground renderers are superseded.
        foreach(var name in new[]{"Ground","Streets"})
        {
            var oldGround=GameObject.Find(name);
            if(oldGround==null)continue;
            foreach(var renderer in oldGround.GetComponentsInChildren<Renderer>())
            {Undo.RecordObject(renderer,"Replace district ground");renderer.enabled=false;}
        }
        MakeTerrain(new[] {grass, grassLight, grassDeep});
        var path = MakeRoute(selector);
        Ribbon("Winding promenade curb",path,1.82f,-.052f,curb);
        Ribbon("Winding promenade",path,1.58f,-.018f,paving);
        // Decorative stepping stones follow exactly the same curve as the chef.
        for (int i = 4; i < path.Length-4; i += 5)
        {
            if (selector.travelPoints.Any(p => Vector3.Distance(p.position,path[i]) < 2.1f)) continue;
            var p=path[i];p.y=.013f;
            Rock("Path inlay",p,new Vector3(.22f,.035f,.18f),curb);
        }
        // Small garden pockets leave the restaurants and travel lane unobstructed.
        Vector3[] gardens = {
            new Vector3(-4,0,-12), new Vector3(-2,0,-8), new Vector3(1,0,-3),
            new Vector3(1,0,10), new Vector3(-3,0,16), new Vector3(-19,0,20),
            new Vector3(-23,0,13), new Vector3(-23,0,3), new Vector3(-23,0,-8),
            new Vector3(-15,0,-16), new Vector3(-8,0,-17)
        };
        for(int i=0;i<gardens.Length;i++) Garden(gardens[i],i,leaves,lime,pink,lavender,yellow);
        Vector3[] trees = {
            new Vector3(-25,0,-13),new Vector3(-26,0,-4),new Vector3(-25,0,6),new Vector3(-23,0,18),
            new Vector3(-16,0,23),new Vector3(-7,0,22),new Vector3(3,0,17),new Vector3(6,0,6),
            new Vector3(5,0,-7),new Vector3(-2,0,-20),new Vector3(-13,0,-21)
        };
        for(int i=0;i<trees.Length;i++) Tree(trees[i], i, bark, i%3==0?lime:leaves, grassLight);
        // A low planting island breaks up the open side of the promenade.
        Rock("Fountain garden island",new Vector3(1,-.15f,5.5f),new Vector3(3.8f,.38f,6),grassLight);
        var fountain = district.transform.Find("Fountain");
        RestoreFountain(fountain, stone, stoneTrim, water);
        // Reposition nearby furniture away from the curved path.
        foreach(Transform child in district.transform)
            if(child.name=="Bench") child.position += new Vector3(1.0f,0,0);
        ConfigureLabel(selector);
        ConfigureCamera(selector);
        var selectorSO=new SerializedObject(selector);
        var route=selectorSO.FindProperty("promenadePath");route.arraySize=path.Length;
        for(int i=0;i<path.Length;i++) route.GetArrayElementAtIndex(i).vector3Value=path[i];
        selectorSO.FindProperty("moveSpeed").floatValue=5.5f;
        selectorSO.FindProperty("turnSpeed").floatValue=540;
        selectorSO.FindProperty("moveAcceleration").floatValue=16;
        var happy=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Animations/PlayerAnimation/Happy Idle.fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
        selectorSO.FindProperty("waveClip").objectReferenceValue=happy;
        selectorSO.ApplyModifiedProperties();
        var presentation=Undo.AddComponent<RestaurantDistrictPresentation>(root.gameObject);
        var so=new SerializedObject(presentation);
        so.FindProperty("selector").objectReferenceValue=selector;
        var rims=so.FindProperty("stopRims");rims.arraySize=3;
        for(int i=0;i<3;i++) rims.GetArrayElementAtIndex(i).objectReferenceValue=district.transform.Find("Stop "+(i+1)+" rim");
        var ps=so.FindProperty("ambientEffects");ps.arraySize=effects.Count;
        for(int i=0;i<effects.Count;i++)ps.GetArrayElementAtIndex(i).objectReferenceValue=effects[i];
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        Debug.Log("[Restaurant Park] Authored rolling ground, curved travel route, gardens, fountain, overview and presentation.");
    }

    private static float GroundHeight(float x,float z)
    {
        // Flat foundations and walking area; gradual rolling hills outside the district.
        float dx=Mathf.Max(-23-x,0,x-4), dz=Mathf.Max(-16-z,0,z-19);
        float blend=Mathf.SmoothStep(0,1,Mathf.Sqrt(dx*dx+dz*dz)/7);
        return -.18f + blend * (.35f+1.45f*Mathf.PerlinNoise((x+103)*.055f,(z+89)*.055f));
    }
    private static void MakeTerrain(Material[] mats)
    {
        const int cells=90;const float step=2;
        var vertices=new List<Vector3>();var lists=new[]{new List<int>(),new List<int>(),new List<int>()};
        for(int z=0;z<cells;z++)for(int x=0;x<cells;x++)
        {
            float px=-100+x*step,pz=-88+z*step;
            Vector3 a=new Vector3(px,GroundHeight(px,pz),pz),b=new Vector3(px+step,GroundHeight(px+step,pz),pz);
            Vector3 c=new Vector3(px,GroundHeight(px,pz+step),pz+step),d=new Vector3(px+step,GroundHeight(px+step,pz+step),pz+step);
            int start=vertices.Count;vertices.AddRange(new[]{a,c,b,b,c,d});
            float noise=Mathf.PerlinNoise((px+41)*.10f,(pz+32)*.10f);
            int color=noise>.62f?1:noise<.34f?2:0;
            for(int j=0;j<6;j++)lists[color].Add(start+j);
        }
        var mesh=new Mesh {name="Rolling park ground"};mesh.vertices=vertices.ToArray();mesh.subMeshCount=3;
        for(int i=0;i<3;i++)mesh.SetTriangles(lists[i],i);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        MeshObject("Continuous rolling lawn",SaveMesh("Rolling park ground",mesh),mats);
    }
    private static Vector3[] MakeRoute(RestaurantSelector selector)
    {
        var points=new List<Vector3>();
        for(int segment=0;segment<selector.travelPoints.Length-1;segment++)
        {
            var a=selector.travelPoints[segment].position;var b=selector.travelPoints[segment+1].position;
            int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/.35f);
            for(int i=0;i<steps;i++)
            {
                float t=(float)i/steps;var p=Vector3.Lerp(a,b,t);
                p.x+=Mathf.Pow(Mathf.Sin(t*Mathf.PI),2)*1.15f;
                points.Add(p);
            }
        }
        points.Add(selector.travelPoints[selector.travelPoints.Length-1].position);
        return points.ToArray();
    }
    private static void Ribbon(string name,Vector3[] path,float halfWidth,float y,Material mat)
    {
        var v=new Vector3[path.Length*2];var tri=new List<int>();
        for(int i=0;i<path.Length;i++)
        {
            Vector3 tangent=path[Mathf.Min(i+1,path.Length-1)]-path[Mathf.Max(0,i-1)];
            Vector3 side=Vector3.Cross(Vector3.up,tangent).normalized*halfWidth;
            v[i*2]=path[i]-side;v[i*2+1]=path[i]+side;v[i*2].y=v[i*2+1].y=y;
            if(i==path.Length-1)continue;
            int a=i*2;tri.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});
        }
        var mesh=new Mesh{name=name,vertices=v,triangles=tri.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();
        MeshObject(name,SaveMesh(name,mesh),new[]{mat});
    }
    private static GameObject MeshObject(string name,Mesh mesh,Material[] mats)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=mats;
        return go;
    }
    private static Mesh SaveMesh(string name,Mesh mesh)
    {
        string path=Materials+"/"+name+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;
    }
    private static Mesh RoundedMesh()
    {
        var v=new List<Vector3>();var tris=new List<int>();const int sides=10,rings=6;
        for(int r=0;r<=rings;r++)for(int s=0;s<=sides;s++)
        {
            float latitude=Mathf.PI*r/rings, angle=2*Mathf.PI*s/sides;
            v.Add(new Vector3(Mathf.Sin(latitude)*Mathf.Cos(angle),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(angle))*.5f);
        }
        for(int r=0;r<rings;r++)for(int s=0;s<sides;s++)
        {int a=r*(sides+1)+s,b=a+sides+1;tris.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
        var mesh=new Mesh{name="Park Rounded Low Poly",vertices=v.ToArray(),triangles=tris.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    private static GameObject Rock(string name,Vector3 position,Vector3 size,Material material)
    {
        var go=MeshObject(name,pebble,new[]{material});go.transform.position=position;go.transform.localScale=size;return go;
    }
    private static void Garden(Vector3 center,int seed,Material leaf,Material lime,params Material[] flowers)
    {
        int firstChild=root.childCount;
        center.y=GroundHeight(center.x,center.z);
        Rock("Soft grass pocket",center+Vector3.up*.02f,new Vector3(3.1f,.35f,2.3f),grass);
        for(int j=0;j<3;j++)
        {
            float a=j*2.1f+seed;var p=center+new Vector3(Mathf.Cos(a)*.75f,.35f,Mathf.Sin(a)*.65f);
            Rock("Rounded shrub",p,new Vector3(.95f,.7f,.85f),j%2==0?leaf:lime);
        }
        for(int j=0;j<8;j++)
        {
            float angle=j*2.39996f+seed;float radius=.8f+(j%3)*.23f;
            var p=center+new Vector3(Mathf.Cos(angle)*radius,.25f,Mathf.Sin(angle)*radius);
            Rock("Flower leaves",p,new Vector3(.42f,.16f,.38f),leaf);
            p.y+=.16f;
            for(int k=0;k<5;k++)
            {float a=k*2*Mathf.PI/5;Rock("Flower petal",p+new Vector3(Mathf.Cos(a)*.1f,0,Mathf.Sin(a)*.1f),new Vector3(.19f,.12f,.19f),flowers[seed%flowers.Length]);}
            Rock("Flower center",p+Vector3.up*.06f,new Vector3(.11f,.1f,.11f),curb);
        }
        CombineGarden(firstChild,seed,center);
    }
    private static void CombineGarden(int firstChild, int seed, Vector3 center)
    {
        var sources=new List<MeshFilter>();
        for(int i=firstChild;i<root.childCount;i++) sources.Add(root.GetChild(i).GetComponent<MeshFilter>());
        var mats=sources.Select(f=>f.GetComponent<Renderer>().sharedMaterial).Distinct().ToArray();
        var meshes=new List<Mesh>();
        foreach(var mat in mats)
        {
            var combine=sources.Where(f=>f.GetComponent<Renderer>().sharedMaterial==mat).Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=Matrix4x4.Translate(-center)*f.transform.localToWorldMatrix}).ToArray();
            var part=new Mesh();part.CombineMeshes(combine,true,true);meshes.Add(part);
        }
        var result=new Mesh();result.CombineMeshes(meshes.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);
        foreach(var source in sources)UnityEngine.Object.DestroyImmediate(source.gameObject);
        foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
        var garden=MeshObject("Flower garden "+(seed+1),SaveMesh("Flower garden "+(seed+1),result),mats);garden.transform.position=center;
    }
    private static void Tree(Vector3 p,int index,Material trunk,Material leaf,Material light)
    {
        p.y=GroundHeight(p.x,p.z);float height=2.5f+(index%3)*.3f;
        Shape("Tree trunk",PrimitiveType.Cylinder,p+Vector3.up*height*.38f,new Vector3(.28f,height*.38f,.28f),trunk);
        Rock("Tree canopy",p+Vector3.up*height,new Vector3(2.5f,2.7f,2.3f),leaf);
        Rock("Tree canopy lobe",p+new Vector3(.7f,height-.5f,.1f),new Vector3(1.7f,1.9f,1.8f),light);
        Rock("Tree canopy lobe",p+new Vector3(-.65f,height-.3f,.2f),new Vector3(1.6f,2,1.7f),leaf);
    }
    private static void RestoreFountain(Transform fountain,Material stone,Material trim,Material water)
    {
        if(fountain==null)return;
        var renderers=fountain.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
        foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        // Preserve the supplied geometry; replace the noisy green atlas with intentional material regions.
        foreach(var mf in fountain.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=UnityEngine.Object.Instantiate(mf.sharedMesh);var vertices=mesh.vertices;var triangles=mesh.triangles;
            var regions=new[]{new List<int>(),new List<int>(),new List<int>()};
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=mf.transform.TransformPoint(vertices[triangles[i]]);var b=mf.transform.TransformPoint(vertices[triangles[i+1]]);var c=mf.transform.TransformPoint(vertices[triangles[i+2]]);
                var p=(a+b+c)/3;float y=(p.y-bounds.min.y)/bounds.size.y;
                float radius=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(bounds.center.x,bounds.center.z));
                bool horizontal=Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y)>.75f;
                int slot=horizontal && y<.45f && radius<bounds.extents.x*.8f ? 2 : y<.22f || y>.82f ? 1 : 0;
                regions[slot].AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
            }
            mesh.subMeshCount=3;for(int i=0;i<3;i++)mesh.SetTriangles(regions[i],i);
            mf.sharedMesh=SaveMesh("Fountain color regions",mesh);mf.GetComponent<Renderer>().sharedMaterials=new[]{stone,trim,water};
            PrefabUtility.RecordPrefabInstancePropertyModifications(mf);PrefabUtility.RecordPrefabInstancePropertyModifications(mf.GetComponent<Renderer>());
        }
        var center=new Vector3(bounds.center.x,bounds.min.y+.15f,bounds.center.z);
        Disk("Clear turquoise fountain pool",center,bounds.extents.x*.72f,.012f,water);
        var foam=Mat("Fountain Water Glints",new Color(.73f,.97f,1));
        for(int i=0;i<3;i++)
        {
            float a=i*2*Mathf.PI/3;
            var p=new Vector3(center.x+Mathf.Cos(a)*.16f,bounds.max.y-.02f,center.z+Mathf.Sin(a)*.16f);
            WaterParticles("Fountain jet "+(i+1),p,foam,3.4f,.8f,16,.055f,.15f,1);
        }
        WaterParticles("Fountain shimmer",center+Vector3.up*.10f,foam,.12f,1.3f,5,.075f,1.2f,0);
    }
    private static void WaterParticles(string name,Vector3 p,Material material,float speed,float life,float rate,float size,float radius,float gravity)
    {
        var go=new GameObject(name,typeof(ParticleSystem));go.transform.SetParent(root,false);go.transform.position=p;
        go.transform.rotation=Quaternion.Euler(-90,0,0);
        var ps=go.GetComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.duration=3;main.loop=true;main.startLifetime=life;main.startSpeed=speed;main.startSize=new ParticleSystem.MinMaxCurve(size*.6f,size);main.maxParticles=64;main.gravityModifier=gravity;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=true;
        var emission=ps.emission;emission.rateOverTime=rate;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=radius;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=pebble;renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        var sizeLife=ps.sizeOverLifetime;sizeLife.enabled=true;sizeLife.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
        effects.Add(ps);
    }
    private static void ConfigureLabel(RestaurantSelector selector)
    {
        var label=GameObject.Find("RestaurantName").GetComponent<TextMeshProUGUI>();Undo.RecordObject(label,"Enlarge restaurant label");
        label.fontSize=32;label.color=Color.white;
        label.rectTransform.sizeDelta=new Vector2(265,54);label.rectTransform.anchoredPosition=new Vector2(24,24);
        var fontMaterial=new Material(label.fontSharedMaterial){name="Restaurant Label Contrast"};
        fontMaterial.SetFloat("_OutlineWidth",.12f);fontMaterial.SetColor("_OutlineColor",new Color(.12f,.22f,.20f,1));
        string path=Materials+"/Restaurant Label Contrast.mat";AssetDatabase.CreateAsset(fontMaterial,path);label.fontSharedMaterial=fontMaterial;
    }
    private static void ConfigureCamera(RestaurantSelector selector)
    {
        var follow=UnityEngine.Object.FindFirstObjectByType<CameraFollow>();var camera=follow.GetComponent<Camera>();
        Undo.RecordObjects(new UnityEngine.Object[]{follow,camera,camera.transform},"Frame park overview");
        camera.orthographic=true;camera.transform.rotation=Quaternion.Euler(35,-38,0);
        camera.farClipPlane=Mathf.Max(camera.farClipPlane,250);
        var so=new SerializedObject(follow);so.FindProperty("districtOverview").boolValue=false;
        so.FindProperty("districtBounds").boundsValue=new Bounds(new Vector3(-10,2.4f,2),new Vector3(22,7,35));
        so.FindProperty("selectionPan").floatValue=.055f;so.FindProperty("framePadding").floatValue=1.12f;
        so.FindProperty("compositionReference").objectReferenceValue=selector.travelPoints[0];
        so.ApplyModifiedProperties();
        camera.orthographicSize=10.5f;
        camera.transform.position=selector.travelPoints[0].position+new Vector3(-3,0,1.3f)-camera.transform.forward*35;
        follow.smoothSpeed=4;
    }
}
#endif
