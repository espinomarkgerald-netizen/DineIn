using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class FastFoodCozyPolishAuthoring
{
    static T EnsurePolish<T>(GameObject go) where T:Component
    {
        var component=go.GetComponent<T>();
        return component!=null?component:Undo.AddComponent<T>(go);
    }
    [MenuItem("Dine In/Fast Food/Apply Perspective Kitchen Polish")]
    public static string ApplyPerspective()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode before authoring.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="Lobby2")throw new InvalidOperationException("Open Lobby2.");
        var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FastFoodCookingView>(true)).Single();
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject,"Perspective kitchen polish");
        var data=new SerializedObject(view);
        T Ref<T>(string name) where T:UnityEngine.Object=>(T)data.FindProperty(name).objectReferenceValue;
        var station=Ref<RectTransform>("station");
        var font=Ref<TMP_Text>("heading").font;
        var source=station.Find("Stations").GetComponent<UnityEngine.UI.Button>();
        MakeNavigation(source,station,"Previous Station","<",new Vector2(.5f,1),new Vector2(-145,-24),new Vector2(58,58),view.PreviousStation);
        MakeNavigation(source,station,"Next Station",">",new Vector2(.5f,1),new Vector2(145,-24),new Vector2(58,58),view.NextStation);
        var navLabel=Child(station,"Station Navigation Label");
        Fixed(navLabel,new Vector2(.5f,1),new Vector2(0,-30),new Vector2(215,42));
        var navText=EnsurePolish<TextMeshProUGUI>(navLabel.gameObject);
        navText.font=font;navText.text="SWITCH STATION";navText.fontSize=23;navText.color=Color.white;
        navText.alignment=TextAlignmentOptions.Center;navText.raycastTarget=false;

        var toastRoot=Child(station,"Floating Kitchen Cue");Fixed(toastRoot,new Vector2(.5f,.76f),Vector2.zero,new Vector2(770,88));
        var toast=EnsurePolish<FastFoodCookingToast>(toastRoot.gameObject);
        toast.motion=toastRoot;
        toast.group=EnsurePolish<CanvasGroup>(toastRoot.gameObject);
        toast.group.alpha=0;toast.group.blocksRaycasts=false;toast.group.interactable=false;
        var textRoot=Child(toastRoot,"Outlined Anton Message");Stretch(textRoot,new Vector2(.1f,0),Vector2.one);
        var label=EnsurePolish<TextMeshProUGUI>(textRoot.gameObject);
        string outlinePath=Batch+"Anton Kitchen Cue Outline.mat";
        var outline=AssetDatabase.LoadAssetAtPath<Material>(outlinePath);
        if(outline==null){outline=new Material(font.material){name="Anton Kitchen Cue Outline"};AssetDatabase.CreateAsset(outline,outlinePath);}
        outline.SetColor("_OutlineColor",Color.black);outline.SetFloat("_OutlineWidth",.22f);outline.EnableKeyword("OUTLINE_ON");EditorUtility.SetDirty(outline);
        label.font=font;label.fontSharedMaterial=outline;label.fontSize=30;label.enableAutoSizing=false;
        label.color=Color.white;label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;label.text="Ready for the next dish";
        toast.label=label;
        var iconRoot=Child(toastRoot,"Dish Icon");Fixed(iconRoot,new Vector2(0,.5f),new Vector2(0,0),new Vector2(66,66));
        var icon=EnsurePolish<UnityEngine.UI.Image>(iconRoot.gameObject);
        icon.preserveAspect=true;icon.raycastTarget=false;toast.icon=icon;
        data.FindProperty("floatingCue").objectReferenceValue=toast;
        Ref<TMP_Text>("staffActivity").transform.parent.gameObject.SetActive(false);
        Ref<TMP_Text>("feedback").transform.parent.gameObject.SetActive(false);
        data.FindProperty("collectStepText").stringValue="Move the ready food to the prep table";

        var canvas=Ref<Canvas>("canvas");
        canvas.gameObject.SetActive(true);
        var fadeRoot=Child(canvas.transform,"Station Transition Fade");Stretch(fadeRoot,Vector2.zero,Vector2.one);fadeRoot.SetAsLastSibling();
        var fadeImage=EnsurePolish<UnityEngine.UI.Image>(fadeRoot.gameObject);
        fadeImage.color=new Color(.025f,.07f,.1f,.92f);fadeImage.raycastTarget=true;
        var fade=EnsurePolish<CanvasGroup>(fadeRoot.gameObject);
        fade.alpha=0;fade.blocksRaycasts=false;fadeRoot.gameObject.SetActive(false);
        data.FindProperty("stationFade").objectReferenceValue=fade;
        string ghostPath=Batch+"Kitchen Drag Ghost.mat";
        var ghost=AssetDatabase.LoadAssetAtPath<Material>(ghostPath);
        if(ghost==null)
        {
            ghost=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Kitchen Drag Ghost"};
            AssetDatabase.CreateAsset(ghost,ghostPath);
        }
        ghost.SetColor("_BaseColor",new Color(.3f,1,.5f,.7f));ghost.SetFloat("_Surface",1);
        ghost.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ghost.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ghost.SetFloat("_ZWrite",0);ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");ghost.renderQueue=3000;
        EditorUtility.SetDirty(ghost);data.FindProperty("ghostMaterial").objectReferenceValue=ghost;
        data.ApplyModifiedProperties();
        navText.fontSharedMaterial=outline;
        var template=Ref<FastFoodCookingTicketView>("ticketTemplate");
        var batchFill=Ref<UnityEngine.UI.Image>("progressFill");
        var batchTrack=(RectTransform)batchFill.transform.parent;
        Stretch(batchTrack,new Vector2(.035f,.035f),new Vector2(.965f,.18f));
        StripePolish(batchFill,610,false);
        string ticketPath=FastFoodCookingAuthoring.TemplateFolder+"/Order Ticket.prefab";
        var ticketRoot=PrefabUtility.LoadPrefabContents(ticketPath);
        try{PolishTicket(ticketRoot.GetComponent<FastFoodCookingTicketView>());PrefabUtility.SaveAsPrefabAsset(ticketRoot,ticketPath);}
        finally{PrefabUtility.UnloadPrefabContents(ticketRoot);}
        // Apply structural changes to the prefab first; scene instances inherit them.
        foreach(var child in template.transform.Cast<Transform>().Where(t=>t.name=="Ticket Motion"&&!template.title.transform.IsChildOf(t)).ToArray())
            if(PrefabUtility.IsAddedGameObjectOverride(child.gameObject))Undo.DestroyObjectImmediate(child.gameObject);
        PolishTicket(template);
        // Keep the user's perspective framing, including their latest camera adjustments.
        foreach(var rig in view.GetComponentsInChildren<FastFoodCookingStation>(true))
        {
            if(rig.cookingViewAnchor!=null)rig.cookingViewAnchor.SetPositionAndRotation(rig.stationCamera.transform.position,rig.stationCamera.transform.rotation);
            if(rig.mode==FastFoodStationMode.Fry)
            {
                if(rig.preparationViewAnchor==null)
                {
                    var anchor=new GameObject("Preparation Camera Anchor");Undo.RegisterCreatedObjectUndo(anchor,"Fryer prep camera");
                    anchor.transform.SetParent(rig.transform,false);rig.preparationViewAnchor=anchor.transform;
                    anchor.transform.position=new Vector3(3.24f,-2.3f,51.6f);
                    anchor.transform.rotation=Quaternion.LookRotation(new Vector3(3.24f,-5.9f,56.01f)-anchor.transform.position);
                }
                rig.preparationFieldOfView=48;
                rig.prepFoodAnchor.position=rig.preparation.transform.position+Vector3.up*.055f;
                if(rig.completedFoodAnchors.Length==0)
                {
                    rig.completedFoodAnchors=new Transform[6];
                    for(int i=0;i<6;i++)
                    {
                        var anchor=new GameObject("Prep Table Food "+(i+1));Undo.RegisterCreatedObjectUndo(anchor,"Fryer plated staging");anchor.transform.SetParent(rig.transform,false);
                        anchor.transform.position=new Vector3(2.4f+i%3*.86f,-5.83f,55.5f+i/3*.85f);
                        rig.completedFoodAnchors[i]=anchor.transform;
                    }
                }
            }
            if(rig.mode==FastFoodStationMode.Assembler)
            {
                var target=rig.prepFoodAnchor.position;
                // Face the working edge instead of looking down the length of both prep counters.
                rig.stationCamera.transform.position=target+new Vector3(0,3.1f,4.2f);
                rig.stationCamera.transform.rotation=Quaternion.LookRotation(target-rig.stationCamera.transform.position);
                rig.cookingViewAnchor.SetPositionAndRotation(rig.stationCamera.transform.position,rig.stationCamera.transform.rotation);
                rig.preparation.transform.rotation=Quaternion.identity;
                rig.preparation.transform.localScale=new Vector3(2.6f/2.1f,1,2.6f/1.32f);
                var hit=rig.preparation.GetComponent<BoxCollider>();
                if(hit!=null){hit.center=new Vector3(0,.04f,0);hit.size=new Vector3(2.1f,.12f,1.32f);}
                for(int i=0;i<rig.trayAnchors.Length;i++)
                    rig.trayAnchors[i].position=target+new Vector3((i%3-1)*.85f,.1f,(i/3-1)*.85f);
            }
            EditorUtility.SetDirty(rig);
        }
        AuthorUprightDrinks();
        EditorUtility.SetDirty(view);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Lobby2 was not saved.");
        return "Saved perspective kitchen arrows/fade, outlined icon cues, slim animated tickets, fryer prep anchors, side-facing assembler and upright filled drink prefabs.";
    }
    static void MakeNavigation(UnityEngine.UI.Button source,Transform parent,string name,string text,Vector2 anchor,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
    {
        var found=parent.Find(name);
        var button=found!=null?found.GetComponent<UnityEngine.UI.Button>():UnityEngine.Object.Instantiate(source,parent);
        button.name=name;button.gameObject.SetActive(true);
        Fixed((RectTransform)button.transform,anchor,position,size);
        button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(button.onClick,action);
        var label=button.GetComponentInChildren<TMP_Text>(true);label.text=text;label.fontSize=30;label.color=Ink;
        EditorUtility.SetDirty(button);
    }
    static void PolishTicket(FastFoodCookingTicketView ticket)
    {
        var root=(RectTransform)ticket.transform;
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,340);
        var size=EnsurePolish<UnityEngine.UI.LayoutElement>(root.gameObject);
        size.preferredHeight=340;
        var motion=root.Find("Ticket Motion") as RectTransform;
        if(motion==null)
        {
            var oldChildren=root.Cast<Transform>().ToArray();
            motion=Child(root,"Ticket Motion");Stretch(motion,Vector2.zero,Vector2.one);
            foreach(var child in oldChildren)child.SetParent(motion,false);
            var oldImage=root.GetComponent<UnityEngine.UI.Image>();
            if(oldImage!=null)
            {
                var image=motion.gameObject.AddComponent<UnityEngine.UI.Image>();
                UnityEditorInternal.ComponentUtility.CopyComponent(oldImage);
                UnityEditorInternal.ComponentUtility.PasteComponentValues(image);
                oldImage.enabled=false;
            }
        }
        ticket.motion=motion;
        ticket.visibility=EnsurePolish<CanvasGroup>(motion.gameObject);
        EnsurePolish<UnityEngine.UI.LayoutElement>(root.gameObject);
        ticket.visibility.blocksRaycasts=false;ticket.visibility.interactable=false;
        var reveal=root.GetComponent<UIRevealAnimation>();if(reveal!=null)reveal.enabled=false;
        var track=Child(motion,"Order Progress Track");
        Stretch(track,new Vector2(.045f,.14f),new Vector2(.955f,.20f));
        var imageTrack=EnsurePolish<UnityEngine.UI.Image>(track.gameObject);
        imageTrack.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Restaurant/CookingAssets/Theme/Grey depth_flat.asset");
        imageTrack.type=UnityEngine.UI.Image.Type.Sliced;imageTrack.color=Color.white;imageTrack.raycastTarget=false;
        var fillRoot=Child(track,"Order Progress Fill");Stretch(fillRoot,new Vector2(.012f,.17f),new Vector2(.988f,.87f));
        var fill=EnsurePolish<UnityEngine.UI.Image>(fillRoot.gameObject);
        fill.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Restaurant/CookingAssets/Theme/Green depth_flat.asset");
        fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;fill.fillOrigin=0;fill.fillAmount=0;fill.color=Color.white;fill.raycastTarget=false;
        ticket.progressFill=fill;
        StripePolish(fill,315,true);
        // Reserve space under the products so neither row is clipped by the progress track.
        var productArea=ticket.products.parent as RectTransform;
        if(productArea!=null&&productArea!=motion)
        {
            Stretch(productArea,Vector2.zero,Vector2.one);
            productArea.offsetMin=new Vector2(16,78);productArea.offsetMax=new Vector2(-16,-50);
        }
        Stretch(ticket.timer.rectTransform,new Vector2(.05f,.02f),new Vector2(.95f,.12f));
        ticket.timer.fontSize=22;ticket.timer.enableAutoSizing=false;
        EditorUtility.SetDirty(ticket);
    }
    static void StripePolish(UnityEngine.UI.Image fill,float width,bool filled)
    {
        if(filled)EnsurePolish<UnityEngine.UI.Mask>(fill.gameObject).showMaskGraphic=true;
        else EnsurePolish<UnityEngine.UI.RectMask2D>(fill.gameObject);
        for(int i=0;i<Mathf.CeilToInt(width/24)+2;i++)
        {
            var stripe=Child(fill.transform,"HUD Stripe "+i);
            Fixed(stripe,new Vector2(0,.5f),new Vector2(i*24-8,0),new Vector2(9,42));
            stripe.localRotation=Quaternion.Euler(0,0,-18);
            var image=EnsurePolish<UnityEngine.UI.Image>(stripe.gameObject);image.color=new Color(1,1,1,.2f);image.raycastTarget=false;
        }
    }
    static void AuthorUprightDrinks()
    {
        foreach(var recipe in MenuCatalog.Default.Products.Where(r=>r.category==MenuProductCategory.Drink))
        {
            var path=AssetDatabase.GetAssetPath(recipe.kitchenServingPrefab);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var model=root.transform.Find("Model");model.localPosition=Vector3.zero;model.localScale=Vector3.one;model.localRotation=Quaternion.identity;
                var glass=model.GetComponentsInChildren<MeshFilter>(true).First();
                glass.transform.localPosition=Vector3.zero;glass.transform.localRotation=Quaternion.Euler(-90,0,0);glass.transform.localScale=Vector3.one;
                var renderer=glass.GetComponent<MeshRenderer>();
                // The imported mesh's long axis is Z, not Y.
                float height=renderer.bounds.size.y;model.localScale=Vector3.one*(.58f/height);
                var bounds=renderer.bounds;model.position-=new Vector3(bounds.center.x-root.transform.position.x,bounds.min.y-root.transform.position.y,bounds.center.z-root.transform.position.z);
                Color color=recipe.name.Contains("Pineapple")?new Color(1,.64f,.055f):recipe.name.Contains("Tea")?new Color(.58f,.20f,.035f):new Color(.105f,.035f,.016f);
                string materialPath=Batch+recipe.name+" Liquid.mat";
                var liquid=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(liquid==null){liquid=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=recipe.name+" Liquid"};AssetDatabase.CreateAsset(liquid,materialPath);}
                liquid.SetColor("_BaseColor",color);liquid.SetFloat("_Smoothness",.6f);liquid.SetFloat("_Metallic",0);
                string glassPath=Batch+"Kitchen Clear Glass.mat";
                var clearGlass=AssetDatabase.LoadAssetAtPath<Material>(glassPath);
                if(clearGlass==null){clearGlass=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Kitchen Clear Glass"};AssetDatabase.CreateAsset(clearGlass,glassPath);}
                clearGlass.SetColor("_BaseColor",new Color(.65f,.94f,1,.23f));
                clearGlass.SetFloat("_Surface",1);clearGlass.SetFloat("_Smoothness",.85f);clearGlass.SetFloat("_Metallic",0);
                clearGlass.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                clearGlass.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                clearGlass.SetFloat("_ZWrite",0);clearGlass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");clearGlass.renderQueue=3000;
                EditorUtility.SetDirty(clearGlass);
                var materials=renderer.sharedMaterials;materials[0]=clearGlass;materials[1]=liquid;renderer.sharedMaterials=materials;
                // Existing liquid submesh is just a top disc. A saved inner volume gives the drink visible depth.
                var existing=root.transform.Find("Liquid Fill");
                var fill=existing!=null?existing.gameObject:GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fill.name="Liquid Fill";fill.transform.SetParent(root.transform,false);
                var collider=fill.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);
                bounds=renderer.bounds;
                float radius=Mathf.Min(bounds.size.x,bounds.size.z)*.39f;
                fill.transform.localScale=new Vector3(radius*2,.21f,radius*2);
                fill.transform.localPosition=new Vector3(0,.24f,0);
                fill.GetComponent<MeshRenderer>().sharedMaterial=liquid;fill.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                EditorUtility.SetDirty(liquid);PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
