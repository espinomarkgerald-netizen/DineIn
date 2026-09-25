using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class FastFoodCozyPolishAuthoring
{
    [MenuItem("Dine In/Fast Food/Apply Kitchen Interaction Polish")]
    public static string ApplyInteraction()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode before saving kitchen polish.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="Lobby2")throw new InvalidOperationException("Open Lobby2 first.");
        var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FastFoodCookingView>(true)).Single();
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject,"Kitchen interaction polish");
        var data=new SerializedObject(view);
        T Ref<T>(string key) where T:UnityEngine.Object => (T)data.FindProperty(key).objectReferenceValue;
        var station=Ref<RectTransform>("station");
        var canvas=Ref<Canvas>("canvas");
        var heading=Ref<TMP_Text>("heading");
        var header=(RectTransform)heading.transform.parent;
        header.anchorMin=new Vector2(.08f,1);header.anchorMax=new Vector2(.38f,1);header.pivot=new Vector2(.5f,1);
        header.offsetMin=new Vector2(0,-144);header.offsetMax=new Vector2(0,-24);
        Stretch(heading.rectTransform,new Vector2(.04f,.64f),new Vector2(.96f,.96f));
        Stretch(Ref<TMP_Text>("progress").rectTransform,new Vector2(.04f,.27f),new Vector2(.96f,.65f));
        var track=(RectTransform)Ref<UnityEngine.UI.Image>("progressFill").transform.parent;
        Stretch(track,new Vector2(.035f,.06f),new Vector2(.965f,.24f));
        foreach(var name in new[]{"Previous Station","Next Station"})
        {
            var rect=station.Find(name) as RectTransform;
            if(rect!=null)Fixed(rect,new Vector2(.5f,1),new Vector2(name=="Previous Station"?-145:145,-26),new Vector2(58,58));
        }
        var navigation=station.Find("Station Navigation Label") as RectTransform;
        if(navigation!=null)Fixed(navigation,new Vector2(.5f,1),new Vector2(0,-31),new Vector2(220,42));
        data.FindProperty("loadStepFormat").stringValue="Drag {0} to any open cooking spot";
        data.FindProperty("collectStepText").stringValue="Move the ready food to the prep table";
        var cue=Ref<FastFoodCookingToast>("floatingCue");
        cue.transform.SetParent(canvas.transform,false);
        Fixed((RectTransform)cue.transform,new Vector2(.5f,.64f),Vector2.zero,new Vector2(880,126));
        cue.label.alignment=TextAlignmentOptions.Center;cue.label.fontSize=36;
        Stretch(cue.label.rectTransform,Vector2.zero,Vector2.one);
        cue.label.rectTransform.offsetMin=new Vector2(82,0);cue.label.rectTransform.offsetMax=new Vector2(-82,0);
        cue.label.fontSharedMaterial.SetColor("_OutlineColor",Color.black);
        cue.label.fontSharedMaterial.SetFloat("_OutlineWidth",.24f);
        Fixed(cue.icon.rectTransform,new Vector2(0,.5f),new Vector2(10,0),new Vector2(68,68));
        cue.duration=2.4f;cue.rise=54;cue.popSeconds=.28f;cue.startScale=.65f;cue.overshootScale=1.18f;
        cue.group.alpha=0;EditorUtility.SetDirty(cue);
        var discard=Child(station,"Discard Box");
        Fixed(discard,new Vector2(1,0),new Vector2(-32,142),new Vector2(286,108));
        var discardImage=EnsurePolish<UnityEngine.UI.Image>(discard.gameObject);
        discardImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Restaurant/CookingAssets/Theme/Red depth_flat.asset");
        discardImage.type=UnityEngine.UI.Image.Type.Sliced;discardImage.color=Color.white;discardImage.raycastTarget=true;
        var discardLabel=Child(discard,"Discard Instructions");Stretch(discardLabel,new Vector2(.06f,.10f),new Vector2(.94f,.9f));
        var text=EnsurePolish<TextMeshProUGUI>(discardLabel.gameObject);
        text.font=heading.font;text.fontSize=28;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;
        text.text="DISCARD BURNT FOOD\nDrop it here";text.raycastTarget=false;
        Reveal(discard.gameObject);discard.gameObject.SetActive(false);data.FindProperty("discardBox").objectReferenceValue=discard;
        var equipment=new List<Outline>();
        foreach(var renderer in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true))
            .Where(r=>r.transform.parent!=null && r.transform.parent.name=="Fast Food Revamp" && (r.name.StartsWith("Grill.")||r.name.StartsWith("Fryer."))))
            equipment.Add(AuthorBlackOutline(renderer.gameObject,false,1.8f));
        var controller=view.GetComponent<FastFoodCookingController>();
        var settings=new SerializedObject(controller);
        settings.FindProperty("stationGraceSeconds").floatValue=5;
        settings.FindProperty("playerBatchSize").intValue=8;
        settings.ApplyModifiedProperties();
        foreach(var rig in view.GetComponentsInChildren<FastFoodCookingStation>(true))
        {
            foreach(var slot in rig.Slots)
            {
                slot.owner=FastFoodCookingSlotOwner.Player;
                var box=slot.GetComponent<BoxCollider>();
                if(box!=null && slot.foodAnchor!=null)
                {
                    box.center=slot.transform.InverseTransformPoint(slot.foodAnchor.position)-Vector3.up*.045f;
                    box.size=new Vector3(box.size.x,.07f,box.size.z);EditorUtility.SetDirty(box);
                }
                if(rig.mode==FastFoodStationMode.Grill && slot.cookingFeedback!=null)PolishGrillSmoke(slot.cookingFeedback);
                EditorUtility.SetDirty(slot);
            }
            if(rig.discard!=null)
            {
                rig.discard.gameObject.SetActive(false);
                if(rig.discard.hint!=null)rig.discard.hint.SetActive(false);
            }
            var prepRenderer=rig.preparation.GetComponent<MeshRenderer>();
            if(prepRenderer!=null)equipment.Add(AuthorBlackOutline(prepRenderer.gameObject,false,1.8f));
            var prepCollider=rig.preparation.GetComponent<BoxCollider>();
            if(prepCollider!=null){prepCollider.isTrigger=true;EditorUtility.SetDirty(prepCollider);}
            if(rig.mode==FastFoodStationMode.Grill)rig.cookingBobHeight=0;
            if(rig.completedFoodAnchors.Length==6)
            {
                var anchors=rig.completedFoodAnchors.ToList();
                var column=anchors[1].position-anchors[0].position;
                for(int i=0;i<2;i++)
                {
                    var go=new GameObject("Prep Table Food "+(7+i));Undo.RegisterCreatedObjectUndo(go,"Eight-item prep staging");
                    go.transform.SetParent(rig.transform,false);
                    go.transform.position=anchors[i*3].position+column*(rig.mode==FastFoodStationMode.Grill?3:-1);
                    anchors.Add(go.transform);
                }
                rig.completedFoodAnchors=anchors.ToArray();
            }
            EditorUtility.SetDirty(rig);
        }
        var outlineRefs=data.FindProperty("equipmentOutlines");outlineRefs.arraySize=equipment.Count;
        for(int i=0;i<equipment.Count;i++)outlineRefs.GetArrayElementAtIndex(i).objectReferenceValue=equipment[i];
        data.ApplyModifiedProperties();
        string ticketPath=FastFoodCookingAuthoring.TemplateFolder+"/Order Ticket.prefab";
        var ticketRoot=PrefabUtility.LoadPrefabContents(ticketPath);
        try{ThickenTicket(ticketRoot.GetComponent<FastFoodCookingTicketView>());PrefabUtility.SaveAsPrefabAsset(ticketRoot,ticketPath);}
        finally{PrefabUtility.UnloadPrefabContents(ticketRoot);}
        ThickenTicket(Ref<FastFoodCookingTicketView>("ticketTemplate"));
        int foods=AuthorCozyFoodAssets();
        EditorUtility.SetDirty(view);EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(cue.label.fontSharedMaterial);
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Lobby2 could not be saved.");
        return "Saved kitchen interaction polish: "+foods+" food prefabs, "+equipment.Count+" equipment outlines, 16 shared cooking positions, eight-item prep staging, discard box, centered cues and thicker tickets.";
    }
    static Outline AuthorBlackOutline(GameObject go,bool enabled,float width)
    {
        var outline=EnsurePolish<Outline>(go);
        outline.OutlineMode=Outline.Mode.OutlineVisible;outline.OutlineColor=Color.black;outline.OutlineWidth=width;
        outline.enabled=enabled;EditorUtility.SetDirty(outline);return outline;
    }
    static void ThickenTicket(FastFoodCookingTicketView ticket)
    {
        ticket.slideSeconds=.3f;ticket.slideDistance=120;
        ticket.minimumHeight=380;
        var track=(RectTransform)ticket.progressFill.transform.parent;
        track.anchorMin=new Vector2(.045f,0);track.anchorMax=new Vector2(.955f,0);track.pivot=new Vector2(.5f,0);
        track.offsetMin=new Vector2(0,56);track.offsetMax=new Vector2(0,86);
        Stretch(ticket.timer.rectTransform,new Vector2(.05f,0),new Vector2(.95f,0));
        ticket.timer.rectTransform.offsetMin=new Vector2(0,14);ticket.timer.rectTransform.offsetMax=new Vector2(0,44);
        var productArea=ticket.products.parent as RectTransform;
        if(productArea!=null){Stretch(productArea,Vector2.zero,Vector2.one);productArea.offsetMin=new Vector2(16,108);productArea.offsetMax=new Vector2(-16,-54);}
        ticket.FitProducts(6);EditorUtility.SetDirty(ticket);
    }
    static void PolishGrillSmoke(GameObject root)
    {
        string path=Batch+"Cozy Grill Smoke.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Dine In/Cozy Grill Smoke")){name="Cozy Grill Smoke"};AssetDatabase.CreateAsset(material,path);}
        foreach(var particles in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.transform.localRotation=Quaternion.Euler(-90,0,0);
            var main=particles.main;main.loop=true;main.playOnAwake=true;main.maxParticles=22;
            main.startSize=new ParticleSystem.MinMaxCurve(.12f,.22f);main.startLifetime=new ParticleSystem.MinMaxCurve(.7f,1.25f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.16f,.25f);
            main.startColor=new Color(.8f,.85f,.87f,.55f);main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=particles.emission;emission.rateOverTime=4;
            var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.55f,1,1.6f));
            var psRenderer=particles.GetComponent<ParticleSystemRenderer>();psRenderer.sharedMaterial=material;
            psRenderer.shadowCastingMode=ShadowCastingMode.Off;psRenderer.receiveShadows=false;
            EditorUtility.SetDirty(particles);EditorUtility.SetDirty(psRenderer);
        }
        root.SetActive(false);
    }
    static int AuthorCozyFoodAssets()
    {
        var assets=new Dictionary<string,Recipe>();
        foreach(var recipe in MenuCatalog.Default.Products.Where(r=>r.category==MenuProductCategory.Food))
        {
            var visuals=new[]{recipe.kitchenCookingPrefab,recipe.kitchenServingPrefab,recipe.kitchenPreviewPrefab}
                .Concat(FastFoodCookingState.AssemblySteps(recipe).Select(s=>s.visual));
            foreach(var visual in visuals)
            {
                string path=AssetDatabase.GetAssetPath(visual);
                if(!string.IsNullOrEmpty(path)&&path.StartsWith(Batch))assets[path]=recipe;
            }
        }
        foreach(var asset in assets)
        {
            var root=PrefabUtility.LoadPrefabContents(asset.Key);
            try
            {
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)materials[i]=CozySurface(asset.Value,renderer,materials[i]);
                    renderer.sharedMaterials=materials;renderer.receiveShadows=false;
                    AuthorBlackOutline(renderer.gameObject,true,1.6f);
                }
                var meshFilters=root.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh!=null).ToArray();
                if(meshFilters.Length>0)
                {
                    var bounds=new Bounds();bool first=true;
                    foreach(var filter in meshFilters)
                    {
                        var b=filter.sharedMesh.bounds;
                        for(int corner=0;corner<8;corner++)
                        {
                            var p=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                            p=root.transform.InverseTransformPoint(filter.transform.TransformPoint(p));
                            if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                        }
                    }
                    var grab=root.transform.Find("Grab Anchor");
                    if(grab==null){grab=new GameObject("Grab Anchor").transform;grab.SetParent(root.transform,false);}
                    grab.localPosition=bounds.center;
                    var handle=root.GetComponent<FastFoodCookingDragHandle>();
                    if(handle!=null)
                    {
                        handle.grabAnchor=grab;
                        var collider=root.GetComponent<BoxCollider>()??root.AddComponent<BoxCollider>();
                        collider.center=bounds.center;
                        collider.size=bounds.size+Vector3.one*handle.hitPadding*2;
                        collider.isTrigger=true;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,asset.Key);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        return assets.Count;
    }
    static Material CozySurface(Recipe recipe,MeshRenderer renderer,Material original)
    {
        if(original==null)return original;
        string mesh=renderer.name.ToLowerInvariant(),name=original.name.ToLowerInvariant();
        string role=original.name.Replace("Interior ","").Replace("Toon ","");
        // Carton colour comes from its palette texture, not its white material tint.
        if(mesh.Contains("carton"))
            return AssetDatabase.LoadAssetAtPath<Material>(Batch+"Interior "+role+".mat")??original;
        Color color=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):Color.white,raw=color;
        bool cooking=false;
        if(name.Contains("bone")){role="Bone";color=new Color(1,.94f,.79f);}
        else if(mesh.Contains("bun")){role=mesh.Contains("bottom")?"Bottom Bun":"Top Bun";color=mesh.Contains("bottom")?new Color(1,.76f,.4f):new Color(.98f,.62f,.20f);}
        else if(mesh.Contains("seame")||mesh.Contains("sesame")){role="Sesame";color=new Color(1,.94f,.72f);}
        else if(mesh.Contains("carton")||name.Contains("plate")||name.Contains("ceramic")||mesh.Contains("plate")||mesh.Contains("rim")){}
        else if(mesh.Contains("cheese")){role="Cheese";color=new Color(1,.77f,.16f);}
        else if(mesh.Contains("patty")&&recipe.kitchenItemType==ItemTypeKitchen.Burger)
        {role="Beef Patty";color=new Color(.38f,.16f,.085f);raw=new Color(.88f,.39f,.42f);cooking=true;}
        else if(mesh.Contains("fish")){role="Fish Crust";color=new Color(.98f,.72f,.28f);raw=new Color(.92f,.85f,.65f);cooking=true;}
        else if(mesh.Contains("chicken")||recipe.kitchenItemType==ItemTypeKitchen.Chicken)
        {role="Chicken Crust";color=new Color(.98f,.6f,.18f);raw=new Color(1,.71f,.63f);cooking=true;}
        else if(recipe.kitchenItemType==ItemTypeKitchen.ChickenNuggets)
        {role="Nugget Crust";color=new Color(.97f,.62f,.20f);raw=new Color(.94f,.83f,.59f);cooking=true;}
        else if(recipe.kitchenItemType==ItemTypeKitchen.Fries)
        {role="Golden Fries";color=new Color(1,.77f,.22f);raw=new Color(.98f,.89f,.64f);cooking=true;}
        string path=Batch+"Toon "+role.Replace("/","-")+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null)return material;
        material=new Material(Shader.Find("Dine In/Cozy Food")){name="Toon "+role};
        material.SetColor("_BaseColor",color);material.SetColor("_RawColor",raw);material.SetFloat("_CookingSurface",cooking?1:0);
        material.SetFloat("_ShadowStrength",.26f);material.SetFloat("_LightThreshold",.56f);material.SetFloat("_Feather",.055f);
        material.SetFloat("_HighlightStrength",cooking?.075f:.045f);AssetDatabase.CreateAsset(material,path);return material;
    }
}
