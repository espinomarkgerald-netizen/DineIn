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
    private readonly Color ink=new Color(.15f,.20f,.26f,1), accent=new Color(1,.68f,.22f);
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
            card.GetComponent<UnityEngine.UI.Image>().color = new Color(.3f,.4f,.48f,1);
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
        progressFill = Panel(bar, "Fill", Vector2.zero, Vector2.one).GetComponent<UnityEngine.UI.Image>(); progressFill.color = accent;
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
        var rt = Panel(parent, text, min, max); var image = rt.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.16f,.30f,.37f,1); image.raycastTarget = true;
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(1,.88f,.6f); colors.pressedColor = accent; colors.disabledColor = new Color(.5f,.5f,.5f,.5f); button.colors = colors;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick,action); Label(rt,text,new Vector2(.03f,.08f),new Vector2(.97f,.92f),28); return button;
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
