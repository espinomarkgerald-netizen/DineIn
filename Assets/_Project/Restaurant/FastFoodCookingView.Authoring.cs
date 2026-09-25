#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>One-time Editor authoring only; excluded from player builds. Never called during gameplay.</summary>
public sealed partial class FastFoodCookingView
{
    private Sprite panelSprite;
    private TMP_FontAsset font;
    private GameObject worldRoot;
    private RectTransform surfaceLabels;
    private TMP_Text worldStatus;
    private float surfaceScale;
    private Transform editorTemplates;
    private readonly List<(Transform target, RectTransform label)> targetLabels=new();
    private readonly Dictionary<FastFoodStationMode,TMP_Text> stationCounts=new();
    private readonly List<Material> materials=new();
    private readonly Color ink=Color.white, accent=new Color(1,.68f,.22f);
    public void BuildEditorPresentation()
    {
        if (canvas != null) throw new InvalidOperationException("Kitchen is already authored; existing edits were preserved.");
        var template = Resources.Load<GameObject>("RestockFlow/RestockFlowHUD");
        if (template != null)
        {
            var image = template.GetComponentsInChildren<UnityEngine.UI.Image>(true).FirstOrDefault(i => i.sprite != null && i.type == UnityEngine.UI.Image.Type.Sliced);
            panelSprite = image != null ? image.sprite : null;
            font = template.GetComponentInChildren<TMP_Text>(true)?.font;
        }
        var lobbyTemplate = Resources.Load<GameObject>("UI/LobbyHUD");
        if (lobbyTemplate != null)
            font = lobbyTemplate.GetComponentsInChildren<TMP_Text>(true).Where(t => t.font != null)
                .GroupBy(t => t.font).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? font;
        var root = new GameObject("Fast Food Kitchen HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 180;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        help = Button(root.transform, "Help Kitchen", new Vector2(.39f,.91f), new Vector2(.61f,.98f), Open);
        selection = Panel(root.transform, "Station Selection", new Vector2(.14f,.18f), new Vector2(.86f,.84f));
        Label(selection, "HELP THE KITCHEN", new Vector2(.06f,.84f), new Vector2(.94f,.96f), 42);
        Label(selection, "Choose your station. Your team handles the rush.", new Vector2(.06f,.75f), new Vector2(.94f,.84f), 26);
        var modes = new[] { FastFoodStationMode.Grill, FastFoodStationMode.Fry, FastFoodStationMode.Assembler };
        for (int i = 0; i < modes.Length; i++)
        {
            var mode = modes[i];
            var card = Panel(selection, mode.ToString(), new Vector2(.035f+i*.32f,.22f), new Vector2(.325f+i*.32f,.72f));
            card.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            var recipe = MenuCatalog.Default.Products.FirstOrDefault(r => r != null && r.sprite != null &&
                (mode == FastFoodStationMode.Assembler ? r.category == MenuProductCategory.Drink : FastFoodCookingState.Station(r) == mode));
            var picture = Rect(card,"Station food",new Vector2(.22f,.46f),new Vector2(.78f,.93f)).gameObject.AddComponent<UnityEngine.UI.Image>();
            picture.sprite = recipe != null ? recipe.sprite : null; picture.preserveAspect = true; picture.raycastTarget = false;
            Label(card, mode == FastFoodStationMode.Grill ? "Burgers & sandwiches" : mode == FastFoodStationMode.Fry ? "Fries, nuggets & chicken" : "Orders & drinks",
                new Vector2(.04f,.33f),new Vector2(.96f,.47f),23);
            stationCounts[mode]=Label(card,"",new Vector2(.04f,.23f),new Vector2(.96f,.33f),20);
            Button(card,"Enter "+mode,new Vector2(.08f,.045f),new Vector2(.92f,.22f),mode == FastFoodStationMode.Grill ? EnterGrill : mode == FastFoodStationMode.Fry ? EnterFry : EnterAssembler);
        }
        Button(selection, "Back to restaurant", new Vector2(.33f,.045f), new Vector2(.67f,.16f), Exit);
        station = Rect(root.transform, "Station", Vector2.zero, Vector2.one);
        var header = Panel(station, "Station Header", new Vector2(.025f,.86f), new Vector2(.70f,.98f));
        heading = Label(header, "", new Vector2(.025f,.48f), new Vector2(.97f,.94f), 34);
        progress = Label(header, "", new Vector2(.025f,.13f), new Vector2(.97f,.48f), 26);
        var bar = Panel(header, "Batch Progress", new Vector2(.025f,.025f), new Vector2(.97f,.09f));
        progressFill = Panel(bar, "Fill", Vector2.zero, Vector2.one).GetComponent<UnityEngine.UI.Image>();
        Button(station, "Stations", new Vector2(.72f,.91f), new Vector2(.83f,.98f), Back);
        Button(station, "Exit Kitchen", new Vector2(.85f,.91f), new Vector2(.975f,.98f), Exit);
        hotbar = Panel(station, "Ingredient Hotbar", new Vector2(.025f,.025f), new Vector2(.975f,.19f));
        tickets = Panel(station, "Live Orders", new Vector2(.025f,.58f), new Vector2(.70f,.84f));
        var scroll = tickets.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.horizontal=true; scroll.vertical=false;
        scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
        var viewport=Panel(tickets,"Viewport",Vector2.zero,Vector2.one);
        viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
        ticketContent=Rect(viewport,"Content",Vector2.zero,new Vector2(0,1)); ticketContent.pivot=new Vector2(0,.5f);
        scroll.viewport=viewport; scroll.content=ticketContent;
        var feedbackPanel = Panel(station,"Pointer guidance",new Vector2(.19f,.205f),new Vector2(.74f,.265f));
        feedback = Label(feedbackPanel, "", new Vector2(.02f,0), new Vector2(.98f,1), 24);
        serve = Button(station, "Serve order", new Vector2(.4f,.29f), new Vector2(.6f,.36f), ServeOrder);
        notification = Panel(station, "Restaurant Notifications", new Vector2(.76f,.61f), new Vector2(.975f,.81f));
        noticeButtonLabel = Button(station, "Supplies ready", new Vector2(.76f,.82f), new Vector2(.975f,.89f),
            ToggleNotices).GetComponentInChildren<TMP_Text>();
        alerts = Label(notification, "", new Vector2(.06f,.29f), new Vector2(.94f,.96f), 24);
        Button(notification, "Go to Restock", new Vector2(.06f,.04f), new Vector2(.94f,.25f), GoToRestock);
        notification.gameObject.SetActive(false);
        BuildEditorTemplates();
        stations = new[] { BuildEditorStation(FastFoodStationMode.Grill), BuildEditorStation(FastFoodStationMode.Fry), BuildEditorStation(FastFoodStationMode.Assembler) };
        selection.gameObject.SetActive(true); station.gameObject.SetActive(false);
        ApplyNaturalUIColorsInEditor();
        ApplyCompactPresentationInEditor();
    }
    private RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
    }
    private RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rt = Rect(parent, name, min, max); var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = panelSprite; image.type = UnityEngine.UI.Image.Type.Sliced; image.color = ink; image.raycastTarget = false; return rt;
    }
    private TMP_Text Label(Transform parent, string text, Vector2 min, Vector2 max, float size)
    {
        var rt = Rect(parent, "Label", min, max); var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font; label.text = text; label.fontSize = size; label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; return label;
    }
    private UnityEngine.UI.Button Button(Transform parent, string text, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        var rt = Panel(parent, text, min, max); var image = rt.GetComponent<UnityEngine.UI.Image>(); image.color = Color.white; image.raycastTarget = true;
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        button.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick,action); Label(rt,text,new Vector2(.03f,.08f),new Vector2(.97f,.92f),28); return button;
    }

    public void ApplyNaturalUIColorsInEditor()
    {
        ApplyNaturalUIAssetColors(gameObject);
        hintColor=successColor=errorColor=feedback!=null?feedback.color:Color.white;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    // Explicit Editor styling only. Gameplay never overwrites the saved sprite choices.
    public static void ApplyNaturalUIAssetColors(GameObject root)
    {
        const string folder="Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/";
        const string themeFolder="Assets/_Project/Restaurant/CookingAssets/Theme";
        if(!UnityEditor.AssetDatabase.IsValidFolder(themeFolder))
            UnityEditor.AssetDatabase.CreateFolder("Assets/_Project/Restaurant/CookingAssets","Theme");
        var anton=UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Assets/Fonts/Anton/Anton-Regular SDF.asset");
        if(anton==null)throw new InvalidOperationException("Kitchen requires the existing Anton-Regular SDF font.");
        var textMaterial=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(themeFolder+"/Anton White.mat");
        if(textMaterial==null)
        {
            textMaterial=new Material(anton.material);
            textMaterial.SetColor("_FaceColor",Color.white);
            textMaterial.SetColor("_OutlineColor",new Color(.1f,.12f,.16f,1));
            textMaterial.SetFloat("_OutlineWidth",.18f);
            UnityEditor.AssetDatabase.CreateAsset(textMaterial,themeFolder+"/Anton White.mat");
        }
        var sprites=new Dictionary<string,Sprite>();
        Sprite Get(string color,string variant)
        {
            string path=folder+color+"/Double/button_square_"+variant+".png";
            if(!sprites.TryGetValue(path,out var sprite))
            {
                var source=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(source==null)throw new InvalidOperationException("Missing kitchen UI sprite: "+path);
                string assetPath=themeFolder+"/"+color+" "+variant+".asset";
                sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if(sprite==null)
                {
                    // Reuse the original pixels; normalize only the kitchen's slice borders.
                    var border=variant.StartsWith("depth_",StringComparison.Ordinal)?new Vector4(8,16,8,8):new Vector4(8,8,8,8);
                    sprite=Sprite.Create(source.texture,source.rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);
                    sprite.name=color+" "+variant;
                    UnityEditor.AssetDatabase.CreateAsset(sprite,assetPath);
                }
                sprites.Add(path,sprite);
            }
            return sprite;
        }
        // Resolve every variant before changing any object.
        foreach(string color in new[]{"Blue","Grey","Green","Yellow","Red"})
            foreach(string variant in new[]{"depth_flat","depth_gloss","flat"})Get(color,variant);
        foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            var path=UnityEditor.AssetDatabase.GetAssetPath(image.sprite);
            if(!path.StartsWith(folder,StringComparison.Ordinal) && !path.StartsWith(themeFolder,StringComparison.Ordinal))continue;
            var button=image.GetComponent<UnityEngine.UI.Button>();
            bool background=image.name=="Station Selection" || image.name=="Station Header" || image.name=="Live Orders" ||
                image.name=="Viewport" || image.name=="Ingredient Hotbar" || image.name=="Restaurant Notifications";
            string color=background?"Blue":"Grey";
            if(image.name=="Fill")color="Green";
            if(image.name=="DISCARD BURNT FOOD")color="Red";
            if(button!=null)
                color=image.name.StartsWith("Enter ",StringComparison.Ordinal) || image.name=="Serve order" || image.name=="Help Kitchen" ? "Green" :
                    image.name=="Exit Kitchen" || image.name=="Back to restaurant" ? "Red" :
                    image.name=="Go to Restock" ? "Green" : "Grey";
            UnityEditor.Undo.RecordObject(image,"Use natural kitchen sprite");
            image.sprite=Get(color,"depth_flat"); image.color=Color.white;
            image.type=UnityEngine.UI.Image.Type.Sliced; image.pixelsPerUnitMultiplier=1;
            UnityEditor.EditorUtility.SetDirty(image);
            if(button==null)continue;
            UnityEditor.Undo.RecordObject(button,"Use authored button state sprites");
            button.transition=UnityEngine.UI.Selectable.Transition.SpriteSwap;
            var colors=button.colors;
            colors.normalColor=colors.highlightedColor=colors.selectedColor=colors.pressedColor=colors.disabledColor=Color.white;
            colors.colorMultiplier=1; button.colors=colors;
            button.spriteState=new UnityEngine.UI.SpriteState {
                highlightedSprite=Get(color,"depth_gloss"),selectedSprite=Get(color,"depth_gloss"),
                pressedSprite=Get(color,"flat"),disabledSprite=Get("Grey","depth_flat") };
            UnityEditor.EditorUtility.SetDirty(button);
        }
        foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            UnityEditor.Undo.RecordObject(label,"Readable labels on natural sprites");
            // Use the nearest surface, so colored buttons inside grey cards stay white.
            bool onGrey=false;
            for(var parent=label.transform.parent;parent!=null;parent=parent.parent)
            {
                var surface=parent.GetComponent<UnityEngine.UI.Image>();
                if(surface==null || surface.sprite==null || surface.color.a<=0)continue;
                string surfacePath=UnityEditor.AssetDatabase.GetAssetPath(surface.sprite);
                onGrey=surfacePath.StartsWith(themeFolder+"/Grey ",StringComparison.Ordinal) ||
                    surfacePath.StartsWith(folder+"Grey/",StringComparison.Ordinal);
                break;
            }
            label.font=anton;
            label.fontSharedMaterial=onGrey?anton.material:textMaterial;
            label.color=onGrey?new Color32(0x09,0x3D,0x59,0xFF):Color.white;
            label.UpdateMeshPadding();
            UnityEditor.EditorUtility.SetDirty(label);
        }
    }

    // Explicit migration of the saved hierarchy. Runtime only binds state to this layout.
    public void ApplyCompactPresentationInEditor()
    {
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject,"Simplify kitchen presentation");
        RectTransform Find(string name) => station.GetComponentsInChildren<RectTransform>(true).First(t=>t.name==name);
        void Fixed(RectTransform rt,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
        {
            rt.anchorMin=rt.anchorMax=anchor; rt.pivot=pivot; rt.anchoredPosition=position; rt.sizeDelta=size;
        }
        void Stretch(RectTransform rt,Vector2 min,Vector2 max)
        { rt.anchorMin=min; rt.anchorMax=max; rt.offsetMin=rt.offsetMax=Vector2.zero; }
        void Tray(FastFoodCookingDropTarget target,float width)
        {
            if(target.transform.Find("Drying Rack")!=null)return;
            var source=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Restaurant/Assets/Level1/GameObjects/RestaurantObjects/Customers/Food Tray.prefab");
            var mesh=source.GetComponentInChildren<MeshFilter>().sharedMesh;
            var bounds=mesh.bounds; float scale=width/bounds.size.x;
            // The existing tray mesh is authored in XY; lay it flat on the counter.
            var rotation=Quaternion.Euler(-90,0,0);
            float height=bounds.size.z*scale;
            var visual=target.transform.Find("Tray visual");
            if(visual==null)
            {
                var go=new GameObject("Tray visual",typeof(MeshFilter),typeof(MeshRenderer));
                UnityEditor.Undo.RegisterCreatedObjectUndo(go,"Author kitchen tray");
                visual=go.transform; visual.SetParent(target.transform,false);
            }
            target.transform.localScale=Vector3.one;
            target.GetComponent<Renderer>().enabled=false;
            visual.localScale=Vector3.one*scale;
            visual.localPosition=-(rotation*bounds.center)*scale+Vector3.up*(height*.5f);
            visual.localRotation=rotation;
            visual.GetComponent<MeshFilter>().sharedMesh=mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials=source.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            var collider=target.GetComponent<BoxCollider>();
            collider.center=Vector3.up*(height*.5f);
            collider.size=new Vector3(width,Mathf.Max(.05f,height),bounds.size.y*scale);
        }
        var header=Find("Station Header");
        Fixed(header,new Vector2(0,1),new Vector2(0,1),new Vector2(156,-24),new Vector2(650,112));
        Stretch(heading.rectTransform,new Vector2(.035f,.60f),new Vector2(.965f,.94f)); heading.fontSize=24;
        Stretch(progress.rectTransform,new Vector2(.035f,.20f),new Vector2(.965f,.62f)); progress.fontSize=26;
        heading.alignment=progress.alignment=TextAlignmentOptions.MidlineLeft;
        Stretch((RectTransform)progressFill.transform.parent,new Vector2(.035f,.09f),new Vector2(.965f,.14f));
        Fixed(Find("Stations"),Vector2.one,Vector2.one,new Vector2(-210,-24),new Vector2(164,64));
        Fixed(Find("Exit Kitchen"),Vector2.one,Vector2.one,new Vector2(-32,-24),new Vector2(164,64));
        Fixed((RectTransform)noticeButtonLabel.transform.parent,Vector2.one,Vector2.one,new Vector2(-32,-100),new Vector2(164,52));
        noticeButtonLabel.fontSize=24;
        Fixed(notification,Vector2.one,Vector2.one,new Vector2(-32,-164),new Vector2(300,188));
        Fixed((RectTransform)feedback.transform.parent,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,254),new Vector2(540,46));
        feedback.fontSize=24;
        Fixed((RectTransform)serve.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,194),new Vector2(240,56));
        hotbarContainer=Find("Supplies scroll");
        Fixed(hotbarContainer,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,28),new Vector2(760,152));
        var scroll=hotbarContainer.GetComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal=true; scroll.vertical=false;
        Stretch(scroll.viewport,Vector2.zero,Vector2.one);
        hotbar.anchorMin=new Vector2(.5f,0); hotbar.anchorMax=new Vector2(.5f,1);
        hotbar.pivot=new Vector2(.5f,.5f); hotbar.anchoredPosition=Vector2.zero; hotbar.sizeDelta=Vector2.zero;
        hotbar.GetComponent<UnityEngine.UI.Image>().enabled=false;
        var grid=hotbar.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize=new Vector2(128,128); grid.spacing=new Vector2(12,0); grid.padding=new RectOffset(12,12,12,12);
        grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount; grid.constraintCount=1;
        grid.childAlignment=TextAnchor.MiddleCenter;
        var fit=hotbar.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        fit.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        ApplyTicketLayoutInEditor();
        tickets.GetComponent<UnityEngine.UI.Image>().enabled=false;
        tickets.GetComponent<UnityEngine.UI.ScrollRect>().viewport.GetComponent<UnityEngine.UI.Image>().enabled=false;
        idleWorkload=""; workloadFormat=""; idleProduction="Next batch soon";
        batchFormat="{0}  {1}/{2}  ·  {4}"; trayFormat="Assemble your order"; waitingAssignment="Next order soon";
        pointerHint=""; idleStatus=""; foodStatusFormat="{1} {2}";
        noticeTitle="Restock"; noticeCountFormat="Restock ({0})";
        placedMessage=""; returnedMessage="Try the highlighted area"; waitMessage="Next batch soon";
        feedback.text=""; feedback.transform.parent.gameObject.SetActive(false);
        notification.gameObject.SetActive(false);
        ApplyCompactSlotInEditor(ingredientTemplate.gameObject);
        ApplyCompactTicketInEditor(ticketTemplate.gameObject);
        int kitchenLayer=LayerMask.NameToLayer("KitchenInteraction");
        if(kitchenLayer<0)
        {
            var tags=new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tags.FindProperty("layers");
            for(int i=8;i<layers.arraySize;i++)
                if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                { kitchenLayer=i; layers.GetArrayElementAtIndex(i).stringValue="KitchenInteraction"; break; }
            if(kitchenLayer<0)throw new InvalidOperationException("No free layer for kitchen pointer interactions.");
            tags.ApplyModifiedProperties(); UnityEditor.AssetDatabase.SaveAssetIfDirty(tags.targetObject);
        }
        foreach(var rig in stations)
        {
            rig.interactionLayers=1<<kitchenLayer;
            rig.stationCamera.cullingMask|=1<<kitchenLayer;
            rig.stationCamera.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>().eventMask=rig.interactionLayers;
            rig.stationTitle=rig.mode==FastFoodStationMode.Fry?"FRYER":rig.mode.ToString().ToUpperInvariant();
            rig.workload.gameObject.SetActive(false);
            if(rig.mode==FastFoodStationMode.Assembler)
            {
                if(rig.workSurface==null)
                {
                    var workPoint=gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="BaristaDrinkPoint");
                    rig.workSurface=gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true))
                        .Where(r=>r.name=="Prep.004").OrderBy(r=>(r.bounds.center-workPoint.position).sqrMagnitude).First();
                }
                var bounds=rig.workSurface.bounds;
                var center=new Vector3(bounds.center.x,bounds.max.y+.045f,bounds.center.z);
                rig.preparation.transform.position=center;
                var position=center+new Vector3(-2.5f,2.6f,0);
                rig.stationCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(center-position));
                rig.stationCamera.fieldOfView=50;
            }
            foreach(var target in new[]{rig.cooking,rig.preparation,rig.discard})
                target.transform.rotation=Quaternion.Euler(0,rig.stationCamera.transform.eulerAngles.y,0);
            if(rig.mode==FastFoodStationMode.Fry)
                rig.preparation.transform.position=rig.cooking.transform.position+rig.stationCamera.transform.right*(rig.visualScale*.40f);
            rig.preparation.transform.localScale=new Vector3(.42f,.018f,.34f)*rig.visualScale;
            if(rig.mode==FastFoodStationMode.Assembler || rig.mode==FastFoodStationMode.Fry)
                Tray(rig.preparation,rig.mode==FastFoodStationMode.Assembler?2.1f:rig.visualScale*.42f);
            foreach(var target in new[]{rig.cooking,rig.preparation,rig.discard})
                foreach(var child in target.GetComponentsInChildren<Transform>(true))child.gameObject.layer=kitchenLayer;
            rig.prepFoodAnchor.position=rig.preparation.transform.position;
            if(rig.mode==FastFoodStationMode.Assembler)
                for(int i=0;i<rig.trayAnchors.Length;i++)
                    rig.trayAnchors[i].position=rig.preparation.transform.position+Vector3.up*.06f+
                        rig.preparation.transform.right*((i%3-1)*.55f)+rig.preparation.transform.forward*((i/3-1)*.32f);
            var point=rig.stationCamera.WorldToViewportPoint(rig.cooking.transform.position);
            Fixed((RectTransform)rig.status.transform.parent,new Vector2(point.x,point.y+.12f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(228,44));
            rig.status.fontSize=24; rig.status.text=""; rig.status.transform.parent.gameObject.SetActive(false);
            foreach(var target in new[]{rig.cooking,rig.preparation,rig.discard})
            {
                var badge=rig.labels.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name==target.name);
                target.hint=badge!=null?badge.gameObject:null;
                if(badge!=null)
                {
                    var screenPoint=rig.stationCamera.WorldToViewportPoint(target.transform.position);
                    badge.anchorMin=badge.anchorMax=new Vector2(screenPoint.x,screenPoint.y);
                    badge.anchoredPosition=new Vector2(0,-40);
                    badge.sizeDelta=new Vector2(target.kind==2?160:180,42);
                    var label=badge.GetComponentInChildren<TMP_Text>(true);
                    label.text=target.kind==0?"Cook here":target.kind==2?"Discard":"Place here";
                    label.fontSize=22; badge.gameObject.SetActive(false);
                }
            }
            // Use the real grill/fryer surface; the old dark proxy covered the appliance.
            rig.cooking.GetComponent<Renderer>().enabled=false;
            rig.discard.transform.localScale=new Vector3(.20f,.018f,.16f)*rig.visualScale;
            rig.discard.gameObject.SetActive(false);
        }
        var characters=gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StaffRole>(true)).Select(s=>s.transform)
            .Concat(gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ManagerPlayer>(true)).Select(p=>p.transform)).Distinct().ToArray();
        staffRenderers=characters.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
        staffNameplates=characters.SelectMany(t=>t.GetComponentsInChildren<Canvas>(true)).Where(c=>c.renderMode==RenderMode.WorldSpace).Distinct().ToArray();
        foreach(var component in GetComponentsInChildren<Component>(true))
        {
            if(component==null)continue;
            UnityEditor.EditorUtility.SetDirty(component);
            if(UnityEditor.PrefabUtility.IsPartOfPrefabInstance(component))UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }

    public static void ApplyCompactSlotInEditor(GameObject root)
    {
        var slot=root.GetComponent<FastFoodCookingDragHandle>();
        var rect=root.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(128,128);
        slot.stockFormat=slot.servingFormat="x{1}";
        var icon=slot.icon.rectTransform; icon.anchorMin=new Vector2(.12f,.26f); icon.anchorMax=new Vector2(.88f,.94f);
        icon.offsetMin=icon.offsetMax=Vector2.zero;
        var count=slot.count.rectTransform; count.anchorMin=new Vector2(.05f,.035f); count.anchorMax=new Vector2(.92f,.27f);
        count.offsetMin=count.offsetMax=Vector2.zero;
        slot.count.text="x0"; slot.count.fontSize=26; slot.count.alignment=TextAlignmentOptions.BottomRight;
    }

    public static void ApplyCompactTicketInEditor(GameObject root)
    {
        void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        var ticket=root.GetComponent<FastFoodCookingTicketView>();
        root.GetComponent<RectTransform>().sizeDelta=new Vector2(360,300);
        var layout=root.GetComponent<UnityEngine.UI.LayoutElement>();
        layout.preferredWidth=360; layout.preferredHeight=300;
        layout.minWidth=360; layout.minHeight=300; layout.flexibleWidth=0; layout.flexibleHeight=0;
        ticket.titleFormat="ORDER #{0}"; ticket.timerFormat="{0}";
        ticket.title.fontSize=26; ticket.timer.fontSize=24;
        Stretch(ticket.title.rectTransform,new Vector2(.04f,.84f),new Vector2(.96f,.98f));
        Stretch(ticket.timer.rectTransform,new Vector2(.04f,.02f),new Vector2(.96f,.14f));
        var viewport=(RectTransform)ticket.products.parent;
        Stretch(viewport,Vector2.zero,Vector2.one);
        viewport.offsetMin=new Vector2(16,48); viewport.offsetMax=new Vector2(-16,-50);
        var content=(RectTransform)ticket.products;
        content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one;
        content.pivot=new Vector2(.5f,1); content.anchoredPosition=Vector2.zero; content.sizeDelta=Vector2.zero;
        var grid=content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize=new Vector2(96,84); grid.spacing=new Vector2(8,8);
        grid.padding=new RectOffset(8,8,8,8); grid.childAlignment=TextAnchor.UpperCenter;
        grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount=3;
        var fit=content.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        fit.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var scroll=viewport.GetComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal=false; scroll.vertical=true; scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.verticalNormalizedPosition=1;
    }

    public void ApplyTicketLayoutInEditor()
    {
        tickets.anchorMin=tickets.anchorMax=tickets.pivot=new Vector2(0,1);
        tickets.anchoredPosition=new Vector2(32,-168); tickets.sizeDelta=new Vector2(380,320);
        var layout=ticketContent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.childForceExpandHeight=false;
    }

    private FastFoodCookingStation BuildEditorStation(FastFoodStationMode mode)
    {
        var anchorName = mode == FastFoodStationMode.Grill ? "GrillWorkPoint" : mode == FastFoodStationMode.Fry ? "FryWorkPoint" : "BaristaDrinkPoint";
        var anchor = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == anchorName);
        if (anchor == null) throw new InvalidOperationException("Station work point is missing: " + anchorName);
        worldRoot = new GameObject(mode + " Station"); worldRoot.transform.SetParent(transform, false);
        var cameraGo = new GameObject("Kitchen First Person Camera", typeof(Camera), typeof(PhysicsRaycaster)); cameraGo.transform.SetParent(worldRoot.transform);
        stationCamera = cameraGo.GetComponent<Camera>();
        if (previousCamera != null) { stationCamera.CopyFrom(previousCamera); previousCamera.enabled = false; }
        stationCamera.enabled = true; stationCamera.orthographic = false; stationCamera.fieldOfView = 60; stationCamera.nearClipPlane = .05f;
        // Work points are staff floor positions, not camera mounts. Imported appliances
        // have different scales; frame their top from outside their footprint.
        var machine = anchor.parent != null ? anchor.parent.GetComponent<Renderer>() : null;
        var bounds = machine != null ? machine.bounds : new Bounds(anchor.position + anchor.forward*.8f + Vector3.up*.5f,new Vector3(2,1,1));
        var forward = bounds.center - anchor.position; forward.y = 0;
        if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up,forward);
        float halfWidth = Mathf.Abs(right.x)*bounds.extents.x + Mathf.Abs(right.z)*bounds.extents.z;
        float halfDepth = Mathf.Abs(forward.x)*bounds.extents.x + Mathf.Abs(forward.z)*bounds.extents.z;
        surfaceScale = Mathf.Max(.75f,halfWidth*1.5f);
        Vector3 top = new Vector3(bounds.center.x,bounds.max.y+.04f,bounds.center.z);
        Vector3 position = top-forward*(halfDepth+surfaceScale*.85f)+Vector3.up*surfaceScale*1.25f;
        stationCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(top-position));
        loadTarget = MakeTarget("COOK", top-right*surfaceScale*.36f, 0);
        prepTarget = MakeTarget(mode == FastFoodStationMode.Assembler ? "ASSEMBLY TRAY" : "PREP / COLLECT", mode == FastFoodStationMode.Assembler ? top : top+right*surfaceScale*.36f, 1);
        discardTarget = MakeTarget("DISCARD BURNT FOOD", top-forward*surfaceScale*.48f, 2);
        loadTarget.gameObject.SetActive(mode != FastFoodStationMode.Assembler); discardTarget.gameObject.SetActive(mode != FastFoodStationMode.Assembler);
        surfaceLabels = Rect(station,"Station surface labels",Vector2.zero,Vector2.one);
        var statusPanel = Panel(surfaceLabels,"Cooking status",new Vector2(.26f,.73f),new Vector2(.68f,.82f));
        worldStatus = Label(statusPanel,"",new Vector2(.02f,0),new Vector2(.98f,1),28);
        statusPanel.gameObject.SetActive(mode != FastFoodStationMode.Assembler);
        worldStatus.gameObject.SetActive(mode != FastFoodStationMode.Assembler);
        foreach (var target in new[] { loadTarget, prepTarget, discardTarget })
        {
            if (!target.gameObject.activeSelf) continue;
            var badge = Panel(surfaceLabels,target.name,Vector2.zero,Vector2.zero);
            badge.sizeDelta = new Vector2(target.kind == 2 ? 280 : 240,42);
            Label(badge,target.name,Vector2.zero,Vector2.one,22);
            targetLabels.Add((target.transform,badge));
        }
        notification.gameObject.SetActive(false);
        var rig = worldRoot.AddComponent<FastFoodCookingStation>();
        rig.mode=mode; rig.stationCamera=stationCamera; rig.cooking=loadTarget; rig.preparation=prepTarget; rig.discard=discardTarget;
        rig.labels=surfaceLabels; rig.status=worldStatus; rig.workload=stationCounts[mode];
        rig.visualScale=surfaceScale; rig.stationTitle=mode.ToString().ToUpperInvariant()+" / KITCHEN HELP";
        rig.rayDistance=Mathf.Max(8,surfaceScale*8); rig.previewDistance=1.6f*surfaceScale; rig.previewLift=.14f*surfaceScale;
        rig.foodAnchor=Anchor(worldRoot.transform,"Cooking food anchor",loadTarget.transform.position);
        rig.prepFoodAnchor=Anchor(worldRoot.transform,"Prepared food anchor",prepTarget.transform.position);
        rig.trayAnchors=new Transform[9];
        for(int i=0;i<rig.trayAnchors.Length;i++)
            rig.trayAnchors[i]=Anchor(worldRoot.transform,"Tray item "+(i+1),prepTarget.transform.position+(prepTarget.transform.right*((i%3-1)*.17f)+prepTarget.transform.forward*((i/3-1)*.16f))*surfaceScale);
        rig.cookingFoodTemplate=VisualTemplate(mode+" Cooking Food",PrimitiveType.Cylinder,new Vector3(.25f,.035f,.25f)*surfaceScale,Vector3.up*.09f*surfaceScale,true);
        rig.dragPreviewTemplate=VisualTemplate(mode+" Drag Preview",PrimitiveType.Cube,Vector3.one*.16f*surfaceScale,Vector3.zero,false);
        rig.servingTemplate=VisualTemplate(mode+" Serving",PrimitiveType.Cube,new Vector3(.12f,.08f,.12f)*surfaceScale,Vector3.up*.12f*surfaceScale,false);
        rig.drinkTemplate=VisualTemplate(mode+" Drink",PrimitiveType.Cylinder,new Vector3(.12f,.08f,.12f)*surfaceScale,Vector3.up*.12f*surfaceScale,false);
        foreach(var entry in targetLabels)
        {
            var point=stationCamera.WorldToViewportPoint(entry.target.position);
            entry.label.anchorMin=entry.label.anchorMax=new Vector2(point.x,point.y);
            entry.label.anchoredPosition=new Vector2(0,-48);
        }
        targetLabels.Clear(); rig.labels.gameObject.SetActive(false); rig.gameObject.SetActive(false);
        return rig;
    }
    private FastFoodCookingDropTarget MakeTarget(string name, Vector3 position, int kind)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(worldRoot.transform);
        go.transform.position = position; go.transform.rotation = Quaternion.Euler(0,stationCamera.transform.eulerAngles.y,0);
        go.transform.localScale = (kind == 2 ? new Vector3(.3f,.035f,.18f) : new Vector3(.64f,.025f,.48f))*surfaceScale;
        var target = go.AddComponent<FastFoodCookingDropTarget>(); target.kind = kind; target.view = this;
        SetColor(go,kind == 2 ? new Color(.35f,.12f,.1f) : kind == 1 ? new Color(.82f,.73f,.54f) : new Color(.12f,.15f,.16f));
        return target;
    }
    private void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>(); if (renderer == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader); mat.color = color; renderer.sharedMaterial = mat; materials.Add(mat);
    }

    private Transform Anchor(Transform parent,string name,Vector3 position)
    { var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.position=position; return go.transform; }
    private GameObject VisualTemplate(string name,PrimitiveType kind,Vector3 scale,Vector3 offset,bool draggable)
    {
        var go=GameObject.CreatePrimitive(kind); go.name=name; go.transform.SetParent(editorTemplates,false);
        go.transform.localScale=scale; go.transform.localPosition=offset; go.GetComponent<Collider>().enabled=draggable;
        SetColor(go,accent);
        if(draggable) go.AddComponent<FastFoodCookingDragHandle>();
        go.SetActive(false); return go;
    }
    private void BuildEditorTemplates()
    {
        editorTemplates=new GameObject("Editable Templates").transform; editorTemplates.SetParent(transform,false);
        var slot=Panel(editorTemplates,"Ingredient Slot",Vector2.zero,Vector2.one);
        slot.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        slot.sizeDelta=new Vector2(340,148);
        ingredientTemplate=slot.gameObject.AddComponent<FastFoodCookingDragHandle>();
        ingredientTemplate.icon=Rect(slot,"Ingredient icon",new Vector2(.02f,.12f),new Vector2(.32f,.88f)).gameObject.AddComponent<UnityEngine.UI.Image>();
        ingredientTemplate.icon.preserveAspect=true; ingredientTemplate.icon.raycastTarget=false;
        ingredientTemplate.count=Label(slot,"Ingredient\n31 available · 0 in use\n2 boxes",new Vector2(.33f,.05f),new Vector2(.98f,.95f),24);
        var slots=hotbar.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        slots.cellSize=new Vector2(340,148); slots.spacing=new Vector2(10,8); slots.childAlignment=TextAnchor.MiddleCenter;
        slots.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; slots.constraintCount=5;
        // The slot grid can expand for larger menus; scroll clipping keeps it clear of station controls.
        var hotbarFrame=Rect(station,"Supplies scroll",hotbar.anchorMin,hotbar.anchorMax);
        var viewport=Panel(hotbarFrame,"Viewport",Vector2.zero,Vector2.one);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        hotbar.SetParent(viewport,false); hotbar.anchorMin=new Vector2(0,1); hotbar.anchorMax=Vector2.one; hotbar.pivot=new Vector2(.5f,1); hotbar.sizeDelta=Vector2.zero;
        var fit=hotbar.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>(); fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var scroll=hotbarFrame.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.viewport=viewport; scroll.content=hotbar; scroll.horizontal=false; scroll.vertical=true;
        scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
        viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var card=Panel(editorTemplates,"Order Ticket",Vector2.zero,Vector2.one);
        card.sizeDelta=new Vector2(308,260);
        var layout=card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredWidth=308; layout.flexibleWidth=0;
        ticketTemplate=card.gameObject.AddComponent<FastFoodCookingTicketView>();
        ticketTemplate.title=Label(card,"#12 · YOUR ORDER",new Vector2(.02f,.75f),new Vector2(.98f,.98f),22);
        ticketTemplate.timer=Label(card,"4:59 · Your tray",new Vector2(.02f,.02f),new Vector2(.98f,.22f),20);
        var productsFrame=Panel(card,"Products viewport",new Vector2(.03f,.24f),new Vector2(.97f,.74f));
        productsFrame.gameObject.AddComponent<UnityEngine.UI.RectMask2D>(); productsFrame.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var products=Rect(productsFrame,"Products",new Vector2(0,1),Vector2.one); products.pivot=new Vector2(.5f,1);
        var productGrid=products.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>(); productGrid.cellSize=new Vector2(88,100); productGrid.spacing=new Vector2(6,6);
        productGrid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; productGrid.constraintCount=3;
        var productFit=products.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>(); productFit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var productScroll=productsFrame.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); productScroll.viewport=productsFrame; productScroll.content=products; productScroll.horizontal=false;
        ticketTemplate.products=products;
        var product=Panel(editorTemplates,"Ticket Product",Vector2.zero,Vector2.one); product.sizeDelta=new Vector2(88,100);
        ticketTemplate.productTemplate=product.gameObject.AddComponent<FastFoodCookingDragHandle>();
        ticketTemplate.productTemplate.icon=Rect(product,"Product image",new Vector2(.08f,.28f),new Vector2(.92f,.97f)).gameObject.AddComponent<UnityEngine.UI.Image>();
        ticketTemplate.productTemplate.icon.preserveAspect=true; ticketTemplate.productTemplate.icon.raycastTarget=false;
        ticketTemplate.productTemplate.count=Label(product,"0/1",new Vector2(0,0),new Vector2(1,.28f),20);
        var ticketsLayout=ticketContent.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        ticketsLayout.spacing=12; ticketsLayout.padding=new RectOffset(6,6,6,6); ticketsLayout.childControlWidth=true; ticketsLayout.childForceExpandWidth=false; ticketsLayout.childControlHeight=true;
        var ticketsFit=ticketContent.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>(); ticketsFit.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        ingredientTemplate.gameObject.SetActive(false); ticketTemplate.gameObject.SetActive(false); product.gameObject.SetActive(false);
    }

    public void SaveEditorTemplateAssets(string folder)
    {
        foreach(var mat in materials)
        {
            if(UnityEditor.AssetDatabase.Contains(mat))continue;
            UnityEditor.AssetDatabase.CreateAsset(mat,UnityEditor.AssetDatabase.GenerateUniqueAssetPath(folder+"/Kitchen Material.mat"));
        }
        // Persist the inner product template first, so ticket prefab references a saved asset.
        var productTemplate = SaveTemplate(ticketTemplate.productTemplate.gameObject,folder);
        ticketTemplate.productTemplate = productTemplate.GetComponent<FastFoodCookingDragHandle>();
        SaveTemplate(ingredientTemplate.gameObject,folder);
        SaveTemplate(ticketTemplate.gameObject,folder);
        foreach(var rig in stations)
            foreach(var template in new[]{rig.cookingFoodTemplate,rig.dragPreviewTemplate,rig.servingTemplate,rig.drinkTemplate}) SaveTemplate(template,folder);
        UnityEditor.EditorUtility.SetDirty(this);
    }
    private static GameObject SaveTemplate(GameObject template,string folder)
    {
        return UnityEditor.PrefabUtility.SaveAsPrefabAssetAndConnect(template,UnityEditor.AssetDatabase.GenerateUniqueAssetPath(folder+"/"+template.name+".prefab"),UnityEditor.InteractionMode.AutomatedAction);
    }
}
#endif
