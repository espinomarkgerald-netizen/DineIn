using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>Runtime binding only. All authored UI, cameras, surfaces and visual templates are saved in Lobby2.</summary>
public sealed partial class FastFoodCookingView : MonoBehaviour
{
    [Header("Saved UI references")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform selection, station, hotbar, tickets, notification, ticketContent;
    [SerializeField] private TMP_Text heading, progress, feedback, alerts, noticeButtonLabel;
    [SerializeField] private TMP_Text restockHeading;
    [SerializeField] private UnityEngine.UI.Image progressFill;
    [SerializeField] private UnityEngine.UI.Button help, serve;
    [SerializeField] private FastFoodCookingDragHandle ingredientTemplate;
    [SerializeField] private FastFoodCookingTicketView ticketTemplate;
    [SerializeField] private FastFoodCookingStation[] stations = Array.Empty<FastFoodCookingStation>();
    [SerializeField] private RectTransform hotbarContainer;
    [SerializeField, Min(128)] private float hotbarMaximumWidth=760;
    [SerializeField, Range(.3f,.8f)] private float hotbarScreenFraction=.46f;
    [SerializeField] private Vector2 hotbarCellRange=new Vector2(96,128);
    [SerializeField,Min(1)] private float hotbarScrollSensitivity=40;
    private float hotbarParentWidth=-1;
    [Header("Hide while using a station (visuals only)")]
    [SerializeField] private Renderer[] staffRenderers = Array.Empty<Renderer>();
    [SerializeField] private Canvas[] staffNameplates = Array.Empty<Canvas>();
    [Header("Interaction")]
    [SerializeField, Min(.01f)] private float refreshSeconds=.1f, feedbackSeconds=3, returnSeconds=.18f;
    [SerializeField] private Vector3 returnPreviewOffset = new Vector3(0,-.55f,1);
    [SerializeField] private Color errorColor=new Color(1,.65f,.5f), successColor=new Color(.5f,1,.7f), hintColor=Color.white;
    [Header("Dynamic text (numbered placeholders are live values)")]
    [SerializeField] private string idleWorkload="Staff ready", workloadFormat="{0} in progress", idleProduction="Staff are keeping the kitchen stocked";
    [SerializeField] private string batchFormat="{0}  {1}/{2} ready · {3} left · {4}", trayFormat="Your tray: Order #{0} · {1}/{2}", waitingAssignment="Waiting for an assignment";
    [SerializeField] private string pointerHint="Drag from your supplies onto the matching surface", idleStatus="Staff working", foodStatusFormat="{0}\n{1} {2}";
    [SerializeField] private string[] stageNames={"Waiting","Cooking","Ready","Preparing","Burnt","Complete"};
    [SerializeField] private string supplyReady="Supplies are ready", noticeTitle="Restaurant notices", noticeCountFormat="Restock needed ({0})", stockNoticeFormat="{0}: {1} left";
    [SerializeField] private string placedMessage="Placed successfully", returnedMessage="Returned · drop onto the matching surface", deliveredMessage="Order ready for delivery";
    [SerializeField] private string pausedMessage="Kitchen temporarily paused", closedMessage="Wait until the kitchen reopens", waitMessage="Staff are working · wait for your next assignment", waitingFood="Waiting for this food";
    [SerializeField, Min(1)] private int maxStockNotices=3;
    [SerializeField, Range(.01f,1)] private float lowStockFraction=.2f;
    private FastFoodCookingController owner;
    private FastFoodCookingState State => owner.State;
    private FastFoodCookingStation activeStation;
    private Camera stationCamera, previousCamera;
    private FastFoodCookingDropTarget loadTarget, prepTarget, discardTarget, hovered;
    private GameObject foodVisual, preview;
    private Vector3 foodVisualPosition, foodVisualScale;
    private FastFoodCookingDragHandle drag;
    private bool opened, previousCameraEnabled, previousCursorVisible;
    private CursorLockMode previousCursor;
    private RestockStorageType alertStorage;
    private string lastSignature;
    private float refreshAt, feedbackUntil;
    private readonly List<GameObject> transientVisuals = new();
    private readonly List<(FastFoodCookingState.Ticket ticket, FastFoodCookingTicketView view)> ticketLabels = new();
    private FastFoodCookingState.Ticket boundTicket;
    private readonly List<(Recipe recipe, FastFoodCookingDragHandle view)> ticketProducts = new();
    private readonly List<(CanvasGroup group,float alpha,bool interactable,bool blocks,bool added)> hidden = new();
    private readonly List<(Renderer renderer,bool hidden)> hiddenStaff = new();
    private readonly List<(Canvas canvas,bool enabled)> hiddenNameplates = new();
    public bool IsOpen => opened;
    public bool OwnsDrag(FastFoodCookingDragHandle handle)=>drag==handle;
    public bool HasActiveDrag=>drag!=null;
    public bool CanOpen => owner != null && owner.Active && IsAuthored && !opened &&
        ManagerPlayer.Active != null && !GameplayUIBlocker.IsBlocked() && Time.timeScale > 0 &&
        RestockFlowCoordinator.Instance?.IsRestockRoomOpen != true;
    public bool IsAuthored => canvas != null && ingredientTemplate != null && ticketTemplate != null && stations.Length == 3 && stations.All(s=>s!=null && s.stationCamera!=null && s.preparation!=null);

    private void Awake() { owner=GetComponent<FastFoodCookingController>(); }
    private void Start()
    {
        if(owner==null || !owner.Active) { if(canvas!=null)canvas.gameObject.SetActive(false); enabled=false; return; }
        if (!IsAuthored) { Debug.LogError("Kitchen presentation is missing. Run Dine In > Fast Food > Create Editable Kitchen in Lobby2.",this); enabled=false; return; }
        canvas.gameObject.SetActive(true);
        InitializeUIFeedback();
        selection.gameObject.SetActive(false); station.gameObject.SetActive(false); help.gameObject.SetActive(false);
        foreach(var rig in stations) { rig.gameObject.SetActive(false); rig.labels.gameObject.SetActive(false); }
    }
    public void EnterGrill() { Enter(FastFoodStationMode.Grill); }
    public void EnterFry() { Enter(FastFoodStationMode.Fry); }
    public void EnterAssembler() { Enter(FastFoodStationMode.Assembler); }
    public void ToggleNotices() => ShowNotice(!notification.gameObject.activeSelf || !noticeVisibility.blocksRaycasts);
    public void GoToRestock() { Exit(); RestockFlowCoordinator.EnsureInstance().EnterRestockRoom(alertStorage); }
    public void ServeOrder() { if(State.PlayerTicket!=null && State.Serve(State.PlayerTicket.number)) { PlayKitchenCue(finishedSound); Message(deliveredMessage,false); lastSignature=null; Refresh(); } }
    public void Open()
    {
        if (!CanOpen) return;
        opened=true; previousCamera=Camera.main; previousCameraEnabled=previousCamera!=null && previousCamera.enabled;
        State.PrepTransferred+=OnPrepTransferred;
        previousCursor=Cursor.lockState; previousCursorVisible=Cursor.visible;
        ManagerPlayer.Active.SetExternalInputSuppressed(true); HideLobby(); PlayerTaskHUD.Instance?.SetKitchenLayout(true); EnableEquipmentOutlines(); Back();
    }
    public void Back()
    {
        StopStationTransition(); CancelDrag(); State.SelectStations(); PlayerTaskGuidance.SetKitchenFocus(true);
        DestroyWorld(); selection.gameObject.SetActive(true); station.gameObject.SetActive(false); help.gameObject.SetActive(false);
        PulseUI(selection);
        if(previousCamera!=null)previousCamera.enabled=previousCameraEnabled;
        Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
    }
    private void Enter(FastFoodStationMode mode)
    {
        CancelDrag(); DestroyWorld(); ResetPolish();
        activeStation=stations.First(s=>s.mode==mode);
        activeStation.gameObject.SetActive(true); activeStation.labels.gameObject.SetActive(true);
        stationCamera=activeStation.stationCamera;
        HideStaffVisuals();
        if(previousCamera!=null)previousCamera.enabled=false;
        loadTarget=activeStation.cooking; prepTarget=activeStation.preparation; discardTarget=activeStation.discard;
        foreach(var target in activeStation.Slots.Concat(activeStation.PrepTargets).Concat(activeStation.collectionSurfaces).Concat(new[]{prepTarget,discardTarget})) if(target!=null)target.view=this;
        if(mode==FastFoodStationMode.Grill)State.grillPrepSlots=activeStation.PrepTargets.Length;
        ResetProductionCamera();
        State.Enter(mode); PlayerTaskGuidance.SetKitchenFocus(true);
        selection.gameObject.SetActive(false); station.gameObject.SetActive(true); HideNoticeImmediate();
        lastSignature=null; Refresh();
        PulseUI(hotbarContainer);
    }
    private void Update()
    {
        if(owner==null || !IsAuthored)return;
        UpdateBaskets();
        if(opened && Time.timeScale>0) { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
        if(Time.unscaledTime>=refreshAt) { refreshAt=Time.unscaledTime+refreshSeconds; Refresh(); }
        UpdateCookingFeedback();
        UpdateProductionCamera();
        AnimateBatchProgress();
    }
    private void UpdateCookingFeedback()
    {
        UpdateBayFeedback();
    }
    private void Refresh()
    {
        if(!opened)return;
        foreach(var rig in stations)
        {
            int n=rig.mode==FastFoodStationMode.Assembler?State.Tickets.Count(t=>t.active&&!t.submitted):State.Portions.Count(p=>FastFoodCookingState.Station(p.recipe)==rig.mode&&p.stage!=FastFoodCookingStage.Complete);
            rig.workload.text=n==0?idleWorkload:string.Format(workloadFormat,n);
        }
        if(activeStation==null)return;
        RefreshCompletedPrep();
        var p=State.PlayerPortion; var t=State.PlayerTicket;
        heading.text=activeStation.stationTitle;
        var batch=p?.batch;
        progress.text=State.Mode==FastFoodStationMode.Assembler ? t==null?waitingAssignment:string.Format(trayFormat,t.number,t.portions.Count(x=>x.placed),t.portions.Count) :
            batch==null?idleProduction:string.Format(batchFormat,batch.recipe.DisplayName,batch.completed,batch.target,Mathf.Max(0,batch.target-batch.completed),TimeLabel(State.deadlineSeconds-batch.elapsed));
        if(State.Mode==FastFoodStationMode.Assembler)
            SetBatchProgress(t,t!=null?(float)t.portions.Count(x=>x.placed)/Mathf.Max(1,t.portions.Count):0);
        bool showServe=State.Mode==FastFoodStationMode.Assembler && t!=null && t.Ready;
        bool revealServe=showServe&&!serve.gameObject.activeSelf;
        serve.gameObject.SetActive(showServe); serve.interactable=t!=null&&t.Ready;
        if(revealServe)PulseUI((RectTransform)serve.transform);
        tickets.gameObject.SetActive(State.Mode==FastFoodStationMode.Assembler);
        progressFill.transform.parent.gameObject.SetActive(batch!=null || t!=null);
        if(feedback!=null)feedback.transform.parent.gameObject.SetActive(false);
        if(activeStation.status!=null)
        {
            activeStation.status.transform.parent.gameObject.SetActive(p!=null && p.stage!=FastFoodCookingStage.Waiting && p.stage!=FastFoodCookingStage.Preparing);
            activeStation.status.text=p==null?idleStatus:string.Format(foodStatusFormat,p.recipe.DisplayName,stageNames[(int)p.stage],
                p.stage==FastFoodCookingStage.Cooking?TimeLabel(State.cookSeconds-p.elapsed):p.stage==FastFoodCookingStage.Ready?TimeLabel(State.overcookSeconds-p.elapsed):"");
        }
        if(discardTarget!=null)discardTarget.gameObject.SetActive(false);
        if(discardBox!=null)discardBox.gameObject.SetActive(State.PlayerWork.Any(x=>x.player&&x.stage==FastFoodCookingStage.Burnt));
        UpdateDropHints();
        var low=MenuCatalog.Default.Products.Where(r=>r!=null && r.IsUnlocked && MenuAvailabilityManager.IsProductAvailable(r))
            .SelectMany(FastFoodCookingState.Steps).Select(s=>s.item).Distinct()
            .Where(i=>InventoryManager.Instance.GetStock(i.itemType)<=Mathf.Max(1,i.unitsPerBox*lowStockFraction)).Take(maxStockNotices).ToList();
        alertStorage=low.Count>0?low[0].requiredStorage:RestockStorageType.Dry;
        alerts.text=low.Count>0?string.Join("\n",low.Select(i=>string.Format(stockNoticeFormat,i.displayName,InventoryManager.Instance.GetStock(i.itemType)))):PlayerTaskGuidance.RestockNotification.IsValid?PlayerTaskGuidance.RestockNotification.Action:supplyReady;
        noticeButtonLabel.text=low.Count>0?string.Format(noticeCountFormat,low.Count):noticeTitle;
        if(restockHeading!=null)restockHeading.text=low.Count>0?"RESTOCK ("+low.Count+")":"RESTOCK";
        bool needsNotice=low.Count>0 || PlayerTaskGuidance.RestockNotification.IsValid;
        noticeButtonLabel.transform.parent.gameObject.SetActive(needsNotice);
        if(!needsNotice && notification.gameObject.activeSelf)HideNoticeImmediate();
        if(State.Mode!=FastFoodStationMode.Assembler) RefreshProductionHeader();
        if(hotbarContainer!=null && Mathf.Abs(((RectTransform)hotbarContainer.parent).rect.width-hotbarParentWidth)>1)FitHotbar();
        UpdateKitchenAudio();
        string signature=ProductionSignature()+":"+State.Mode+":"+(p==null?"idle":p.recipe.ProductId+":"+p.stage+":"+p.ingredientStep)+":"+
            (State.Mode==FastFoodStationMode.Assembler && t!=null?t.number+":"+string.Join(",",t.portions.Select(y=>y.stage+"/"+y.placed)):"");
        if(drag==null && signature!=lastSignature) { lastSignature=signature; RebuildControls(p,t); }
        foreach(var entry in ticketLabels)
        {
            entry.view.timer.text=string.Format(entry.view.timerFormat,TimeLabel(State.deadlineSeconds-entry.ticket.elapsed),entry.ticket.player?entry.view.playerText:entry.ticket.portions.Any(x=>x.stage!=FastFoodCookingStage.Complete)?entry.view.waitingText:entry.view.preparingText);
            entry.view.SetProgress((float)entry.ticket.portions.Count(x=>x.placed)/Mathf.Max(1,entry.ticket.portions.Count));
        }
        foreach(var entry in ticketProducts)
            if(boundTicket!=null && entry.view!=null)
            {
                string text=string.Format(ticketTemplate.quantityFormat,boundTicket.portions.Count(x=>x.recipe==entry.recipe&&x.placed),boundTicket.portions.Count(x=>x.recipe==entry.recipe));
                if(entry.view.count.text!=text) { entry.view.count.text=text; PulseUI(entry.view.icon.rectTransform); }
            }
        heading.transform.parent.gameObject.SetActive(State.Mode==FastFoodStationMode.Assembler?t!=null:State.PlayerWork.Any(x=>x.player)||State.ShowingCompletedBatch);
        if(State.Mode!=FastFoodStationMode.Assembler)
        {
            if(drag==null)UpdateProductionHotbar();
            return;
        }
        foreach(var slot in hotbar.GetComponentsInChildren<FastFoodCookingDragHandle>())
            if(slot.storedProtein && slot.portion!=null)
                slot.count.text=string.Format(storedProteinFormat,State.StoredProteinCount(slot.portion.recipe));
            else if(slot.item!=null)
            {
                int units=InventoryManager.Instance.GetStock(slot.item.itemType);
                slot.count.text=string.Format(slot.stockFormat,slot.item.displayName,units);
            }
    }
    private static string TimeLabel(float seconds) { int s=Mathf.CeilToInt(Mathf.Max(0,seconds)); return (s/60)+":"+(s%60).ToString("00"); }
    private void Clear(Transform parent) { foreach(Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
    private GameObject SpawnVisual(GameObject template, Transform anchor, bool draggable=false, FastFoodCookingState.Portion portion=null)
    {
        var go=Instantiate(template,anchor); go.SetActive(true);
        foreach(var child in go.GetComponentsInChildren<Transform>(true))child.gameObject.layer=activeStation.preparation.gameObject.layer;
        transientVisuals.Add(go);
        foreach(var component in go.GetComponentsInChildren<FastFoodCookingDragHandle>(true))component.enabled=draggable;
        if(draggable)
        {
            var handle=go.GetComponent<FastFoodCookingDragHandle>();
            if(handle==null)throw new InvalidOperationException("Cooking food prefab needs a FastFoodCookingDragHandle: "+template.name);
            handle.view=this; handle.portion=portion ?? State.PlayerPortion;
        }
        return go;
    }
    private void RebuildControls(FastFoodCookingState.Portion p,FastFoodCookingState.Ticket ticket)
    {
        cookingVisuals.Clear();
        proteinSlots.Clear();
        if(State.Mode==FastFoodStationMode.Assembler)Clear(hotbar);
        RefreshTicket(State.Mode==FastFoodStationMode.Assembler?ticket:null);
        if(hotbarContainer!=null)hotbarContainer.gameObject.SetActive(State.Mode!=FastFoodStationMode.Assembler || ticket!=null && ticket.portions.Any(x=>!x.placed));
        foreach(var go in transientVisuals) if(go!=null)Destroy(go); transientVisuals.Clear(); foodVisual=null;
        if(State.Mode==FastFoodStationMode.Assembler)
        {
            if(ticket!=null)
            {
                foreach(var group in ticket.portions.Where(x=>!x.placed).GroupBy(x=>x.recipe))
                    AddSlot(group.Key.sprite,group.Key.DisplayName,null,group.First(),group.Count());
                foreach(var portion in ticket.portions.Where(x=>x.placed))
                {
                    var anchor=AssemblySurface;
                    var placed=SpawnVisual(portion.recipe.kitchenServingPrefab!=null?portion.recipe.kitchenServingPrefab:
                        portion.recipe.category==MenuProductCategory.Drink?activeStation.drinkTemplate:activeStation.servingTemplate,anchor);
                    AnimateAssemblyPlacement(placed,portion);
                }
            }
            FitHotbar(); acceptedPortion=null; assemblyHasRelease=false; return;
        }
        RebuildProductionControls();
        acceptedPortion=null;
    }
    private void RefreshTicket(FastFoodCookingState.Ticket ticket)
    {
        if(boundTicket==ticket && (ticket==null || ticketLabels.Count>0 && ticketLabels.All(x=>x.view!=null)))return;
        boundTicket=ticket;
        float enterAfter=ticketLabels.Count>0?ticketLabels.Where(x=>x.view!=null).Select(x=>x.view.exitSeconds).DefaultIfEmpty(0).Max():0;
        foreach(var entry in ticketLabels)if(entry.view!=null)entry.view.Dismiss();
        ticketLabels.Clear(); ticketProducts.Clear();
        if(ticket==null)return;
        var card=Instantiate(ticketTemplate,ticketContent);
        card.entranceDelay=LevelOneUIAccessibility.ReducedMotion?Mathf.Min(.08f,enterAfter):enterAfter;
        card.gameObject.SetActive(true);
        card.title.text=string.Format(card.titleFormat,ticket.number,card.playerText);
        foreach(var group in ticket.portions.GroupBy(x=>x.recipe))
        {
            var product=Instantiate(card.productTemplate,card.products);product.gameObject.SetActive(true);
            product.enabled=false;product.icon.sprite=group.Key.sprite;
            product.count.text=string.Format(card.quantityFormat,group.Count(x=>x.placed),group.Count());
            ticketProducts.Add((group.Key,product));
        }
        ticketLabels.Add((ticket,card));
        card.FitProducts(ticketProducts.Count);
    }
    private void FitHotbar()
    {
        if(hotbarContainer==null)return;
        int count=0; foreach(Transform child in hotbar)if(child.gameObject.activeSelf)count++;
        hotbarContainer.gameObject.SetActive(count>0);
        var scroll=hotbarContainer.GetComponent<UnityEngine.UI.ScrollRect>();
        var grid=hotbar.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        var offset=hotbar.anchoredPosition;
        hotbarParentWidth=((RectTransform)hotbarContainer.parent).rect.width;
        bool production=State.Mode!=FastFoodStationMode.Assembler;
        if(grid!=null && count>0)
        {
            const float inset=16;
            float maximumWidth=Mathf.Max(inset+grid.padding.horizontal+1,Mathf.Min(hotbarMaximumWidth,hotbarParentWidth*hotbarScreenFraction));
            float available=maximumWidth-inset-grid.padding.horizontal;
            int columns=Mathf.Clamp(Mathf.FloorToInt((available+grid.spacing.x)/(hotbarCellRange.x+grid.spacing.x)),1,count);
            float cell=Mathf.Min(hotbarCellRange.y,(available-(columns-1)*grid.spacing.x)/columns);
            int rows=Mathf.CeilToInt((float)count/columns);
            float width=inset+grid.padding.horizontal+columns*cell+(columns-1)*grid.spacing.x;
            grid.startCorner=UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment=TextAnchor.UpperLeft;
            if(production)
            {
                if(productionCellSize<=0)
                {
                    productionCellSize=Mathf.Clamp((available-(count-1)*grid.spacing.x)/count,hotbarCellRange.x,hotbarCellRange.y);
                    productionViewportWidth=Mathf.Min(maximumWidth,inset+grid.padding.horizontal+count*productionCellSize+(count-1)*grid.spacing.x);
                }
                // Freeze the visible frame and cell size for the batch. New cells extend to the
                // right inside the existing scroll view without moving earlier drag targets.
                cell=productionCellSize;width=Mathf.Min(maximumWidth,productionViewportWidth);rows=1;
                grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount;grid.constraintCount=1;
            }
            else
            {
                grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=columns;
            }
            grid.cellSize=new Vector2(cell,cell);
            hotbarContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
            hotbarContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,inset+grid.padding.vertical+rows*cell+(rows-1)*grid.spacing.y);
        }
        if(scroll!=null)
        {
            scroll.horizontal=production;scroll.vertical=false;
            if(production)scroll.scrollSensitivity=hotbarScrollSensitivity;
        }
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(hotbar);
        if(resetHotbarScroll || !production)
        {
            if(scroll!=null){scroll.StopMovement();scroll.horizontalNormalizedPosition=0;scroll.verticalNormalizedPosition=1;}
            hotbar.anchoredPosition=Vector2.zero;resetHotbarScroll=false;
        }
        else
        {
            float viewportWidth=scroll!=null && scroll.viewport!=null?scroll.viewport.rect.width:hotbarContainer.rect.width;
            hotbar.anchoredPosition=new Vector2(Mathf.Clamp(offset.x,Mathf.Min(0,viewportWidth-hotbar.rect.width),0),0);
        }
    }
    private void AddSlot(Sprite sprite,string title,ItemData item,FastFoodCookingState.Portion portion,int count)
    {
        var slot=Instantiate(ingredientTemplate,hotbar); slot.gameObject.SetActive(true);
        slot.view=this; slot.item=item; slot.portion=portion; slot.icon.sprite=sprite;
        slot.count.text=item!=null?string.Format(slot.stockFormat,title,InventoryManager.Instance.GetStock(item.itemType)):string.Format(slot.servingFormat,title,count,portion.stage==FastFoodCookingStage.Complete||portion.recipe.category==MenuProductCategory.Drink?slot.readyText:slot.waitingText);
    }
    private void Tint(GameObject go,Color color)
    {
        if(previewProperties==null)previewProperties=new MaterialPropertyBlock();
        previewProperties.Clear();previewProperties.SetColor("_BaseColor",color);previewProperties.SetColor("_Color",color);
        foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(previewProperties);
    }
    public void BeginDrag(FastFoodCookingDragHandle handle,Vector2 position,bool touch=false)
    {
        if(Time.timeScale<=0 || drag!=null || !handle.enabled)return;
        CancelDrag();
        if(handle.storedProtein && (!State.PrepReady || handle.item==null)) { Warn("Finish cooking this batch to start assembly"); return; }
        if(cameraMoving||stationTransition!=null)return;
        if(State.Mode!=FastFoodStationMode.Assembler && handle.item==null && handle.portion?.stage==FastFoodCookingStage.Ready) return;
        if(State.Mode!=FastFoodStationMode.Assembler && handle.item!=null)
            handle.portion=State.PrepReady?Enumerable.Range(0,State.grillPrepSlots).Select(i=>State.PrepPortion(handle.item,i)).FirstOrDefault(p=>p!=null):State.NextLoad(handle.item);
        if(HygieneManager.KitchenPaused) { Warn(pausedMessage); return; }
        if(HygieneManager.HoldNewCooking && handle.portion?.stage==FastFoodCookingStage.Waiting) { Warn(closedMessage); return; }
        if(State.Mode!=FastFoodStationMode.Assembler && (handle.portion==null||!handle.portion.player||FastFoodCookingState.WorkStation(handle.portion)!=State.Mode)) { Warn(waitMessage); return; }
        if(State.Mode==FastFoodStationMode.Assembler&&!State.BeginServingDrag(handle.portion)) { Warn(waitingFood,handle.portion?.recipe.sprite); return; }
        var previewTemplate=handle.previewTemplate!=null?handle.previewTemplate:handle.item!=null && handle.item.kitchenPreviewPrefab!=null ? handle.item.kitchenPreviewPrefab :
            handle.item==null && handle.portion?.recipe.kitchenPreviewPrefab!=null ? handle.portion.recipe.kitchenPreviewPrefab : activeStation.dragPreviewTemplate;
        if(IsAssembler && handle.portion!=null)
            previewTemplate=handle.portion.recipe.kitchenServingPrefab!=null?handle.portion.recipe.kitchenServingPrefab:
                handle.portion.recipe.category==MenuProductCategory.Drink?activeStation.drinkTemplate:activeStation.servingTemplate;
        if(State.Mode==FastFoodStationMode.Grill && State.PrepReady)
        {
            var prepVisual=PrepIngredientVisual(handle.portion);
            if(prepVisual!=null)previewTemplate=prepVisual;
        }
        prepDragTemplate=previewTemplate;
        drag=handle;
        // World pickups retain their actual geometry, scale and current food pose.
        if(!(handle.transform is RectTransform))previewTemplate=handle.gameObject;
        preview=CreateFoodGhost(previewTemplate,ghostMaterial,out ghostCenterOffset);
        preview.transform.SetParent(activeStation.transform,true);
        if(State.Mode==FastFoodStationMode.Grill && State.PrepReady && activeStation.prepSlots.Length>0)
            preview.transform.localScale=activeStation.prepSlots[0].foodAnchor.localScale;
        pointerPosition=position;
        var depthAnchor=State.Mode==FastFoodStationMode.Assembler||State.PrepReady?activeStation.prepFoodAnchor:
            activeStation.Slots.FirstOrDefault(s=>s!=null&&s.foodAnchor!=null)?.foodAnchor;
        float surfaceDepth=depthAnchor!=null?Vector3.Dot(depthAnchor.position-stationCamera.transform.position,stationCamera.transform.forward):0;
        dragDepth=surfaceDepth>stationCamera.nearClipPlane?surfaceDepth:activeStation.previewDistance;
        stagingDrag=State.Mode==FastFoodStationMode.Fry && handle.item==null && handle.portion.stage==FastFoodCookingStage.Ready;
        if(State.Mode==FastFoodStationMode.Fry)fryerPrepUntil=stagingDrag?float.PositiveInfinity:0;
        previewPositionInitialized=false;
        if(IsAssembler)BeginAssemblyMotion(position,touch);
        CreateDropGhosts();
        MoveDrag(position);
    }
    public void MoveDrag(Vector2 position)
    {
        if(drag==null||preview==null||stationCamera==null||Time.timeScale<=0)return;
        pointerPosition=position;
        if(IsAssembler){MoveAssemblyDrag(position);return;}
        discardHovered=discardBox!=null && discardBox.gameObject.activeInHierarchy && drag.item==null && drag.portion?.stage==FastFoodCookingStage.Burnt &&
            RectTransformUtility.RectangleContainsScreenPoint(discardBox,position,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera);
        var ray=stationCamera.ScreenPointToRay(position); hovered=null;
        if(!discardHovered && !cameraMoving)hovered=ResolveDropTarget(position,ray);
        MatchPrepPreview(hovered);
        var anchor=DropAnchor(hovered);
        if(State.Mode==FastFoodStationMode.Grill && hovered!=null && hovered.kind==1 && anchor!=null)
            preview.transform.localScale=anchor.localScale;
        float depth=anchor!=null?Vector3.Dot(anchor.position-stationCamera.transform.position,stationCamera.transform.forward):dragDepth;
        var plane=new Plane(stationCamera.transform.forward,stationCamera.transform.position+stationCamera.transform.forward*Mathf.Max(.2f,depth));
        bool valid=CanDrop(hovered);
        Vector3 destination=valid?DropGhostPosition(hovered):plane.Raycast(ray,out float distance)?ray.GetPoint(distance):ray.GetPoint(dragDepth);
        preview.transform.position=!previewPositionInitialized||!valid||LevelOneUIAccessibility.ReducedMotion?destination:
            Vector3.Lerp(preview.transform.position,destination,1-Mathf.Exp(-Time.unscaledDeltaTime/Mathf.Max(.01f,magnetEaseSeconds)));
        previewPositionInitialized=true;
        if(anchor!=null)preview.transform.rotation=anchor.rotation;
        Color tint=discardHovered||CanDrop(hovered)?activeStation.validDropColor:activeStation.invalidDropColor;
        tint.a=ghostOpacity;
        Tint(preview,tint);
        UpdateDropHints();
    }
    private bool CanDrop(FastFoodCookingDropTarget target)
    {
        if(target==null || !target.isActiveAndEnabled || target.view!=this || drag==null) return false;
        var p=drag.portion;
        if(State.Mode==FastFoodStationMode.Assembler) return target.kind==1 && p!=null && p.dragging && !p.placed &&
            State.PlayerTicket!=null && State.PlayerTicket.portions.Contains(p);
        if(State.Mode==FastFoodStationMode.Grill && target.kind==1 && drag.item!=null)
            return State.PrepPortion(drag.item,target.slotIndex)!=null;
        if(p==null || !p.player || FastFoodCookingState.WorkStation(p)!=State.Mode) return false;
        if(drag.item==null) return target.kind==1 && p.stage==FastFoodCookingStage.Ready || target.kind==2 && p.stage==FastFoodCookingStage.Burnt;
        return (target.kind==0 && p.stage==FastFoodCookingStage.Waiting || target.kind==1 && p.stage==FastFoodCookingStage.Preparing) && State.CanLoad(p,drag.item,target.slotIndex);
    }
    public void EndDrag(Vector2 position)
    {
        if(drag==null)return;
        if(Time.timeScale<=0){CancelDrag();return;}
        MoveDrag(position); bool success=false;
        if(State.Mode==FastFoodStationMode.Grill && hovered!=null && hovered.kind==1 && drag.item!=null)
            drag.portion=State.PrepPortion(drag.item,hovered.slotIndex);
        if(IsAssembler && preview!=null){assemblyReleasePosition=preview.transform.position;assemblyHasRelease=true;}
        var attempted=drag!=null?drag.portion:null;
        bool discarding=discardHovered || hovered!=null&&hovered.kind==2;
        int previousStep=attempted!=null?attempted.ingredientStep:0;
        if(discardHovered && !HygieneManager.KitchenPaused)success=State.Discard(attempted);
        else if(CanDrop(hovered) && !HygieneManager.KitchenPaused)
        {
            if(State.Mode==FastFoodStationMode.Assembler) success=State.Place(drag.portion);
            else if(drag.item!=null) success=State.Load(drag.portion,drag.item,hovered.slotIndex);
            else success=hovered.kind==2?State.Discard(drag.portion):State.Collect(drag.portion);
        }
        Message(success ? discarding ? "Burnt food discarded" : State.Mode==FastFoodStationMode.Fry && attempted!=null && FastFoodCookingState.PreparationStation(attempted.recipe)==FastFoodStationMode.Grill && attempted.proteinReady ?
            "Ready for Grill: "+attempted.recipe.DisplayName : placedMessage : returnedMessage,!success);
        if(success) { acceptedPortion=attempted;acceptedStep=previousStep;PlayKitchenCue(attempted?.stage==FastFoodCookingStage.Complete?finishedSound:placementSound); }
        if(!success && preview!=null && drag!=null)
        {
            Vector3 target=IsAssembler?assemblySourcePosition:drag.item!=null?stationCamera.transform.TransformPoint(returnPreviewOffset):drag.transform.position;
            var returning=preview; preview=null;
            if(IsAssembler)StartCoroutine(MoveAssemblyFood(returning,returning.transform.position,target,assemblyReturnDuration,true));
            else StartCoroutine(ReturnPreview(returning,target));
        }
        if(State.Mode==FastFoodStationMode.Fry)fryerPrepUntil=stagingDrag?Time.unscaledTime+prepLookSeconds:0;
        CancelDrag(); lastSignature=null;
        if(IsAssembler)Refresh();
    }
    private IEnumerator ReturnPreview(GameObject returning,Vector3 target)
    {
        if(LevelOneUIAccessibility.ReducedMotion) { if(returning!=null)Destroy(returning);yield break; }
        Vector3 start=returning.transform.position; float elapsed=0;
        while(returning!=null && elapsed<returnSeconds) { elapsed+=Time.unscaledDeltaTime; returning.transform.position=Vector3.Lerp(start,target,Mathf.Clamp01(elapsed/Mathf.Max(.01f,returnSeconds))); yield return null; }
        if(returning!=null)Destroy(returning);
    }
    private void CancelDrag() { if(drag!=null && drag.portion!=null) drag.portion.dragging=false; drag=null; hovered=null; discardHovered=false; if(preview!=null){preview.SetActive(false);Destroy(preview);preview=null;} ClearDropGhosts(); UpdateDropHints(); }
    private void UpdateDropHints()
    {
        foreach(var target in (activeStation!=null?activeStation.Slots:Array.Empty<FastFoodCookingDropTarget>()).Concat(new[]{prepTarget,discardTarget}))
            if(target!=null && target.hint!=null)
                target.hint.SetActive(false);
        UpdateDropGhosts();
    }
    private void HideStaffVisuals()
    {
        foreach(var renderer in staffRenderers)
            if(renderer!=null) { hiddenStaff.Add((renderer,renderer.forceRenderingOff)); renderer.forceRenderingOff=true; }
        foreach(var nameplate in staffNameplates)
            if(nameplate!=null) { hiddenNameplates.Add((nameplate,nameplate.enabled)); nameplate.enabled=false; }
    }
    private void RestoreStaffVisuals()
    {
        foreach(var entry in hiddenStaff)if(entry.renderer!=null)entry.renderer.forceRenderingOff=entry.hidden;
        foreach(var entry in hiddenNameplates)if(entry.canvas!=null)entry.canvas.enabled=entry.enabled;
        hiddenStaff.Clear(); hiddenNameplates.Clear();
    }
    private void Message(string text,bool error) { if(floatingCue!=null)floatingCue.Show(text,drag!=null?drag.portion?.recipe.sprite:null,error?1:3); feedbackUntil=Time.unscaledTime+feedbackSeconds; }
    private void HideLobby()
    {
        foreach(var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if(c!=canvas && c.isRootCanvas && c.renderMode!=RenderMode.WorldSpace && c.gameObject.activeInHierarchy &&
                (c.name=="CanvasMainHUD" || c.GetComponentInParent<LobbyHUDRoot>()!=null || c.GetComponentInChildren<LobbyHUDRoot>(true)!=null || c.GetComponentInChildren<PlayerTaskHUD>(true)!=null)) HideBranch(c.transform);
    }
    private void HideBranch(Transform branch)
    {
        if(branch.name=="TaskButton" || branch.name=="TaskMessage" || branch.GetComponent<LobbyPauseMenuView>()!=null) return;
        bool containsTask=branch.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="TaskButton"||t.name=="TaskMessage"||t.GetComponent<LobbyPauseMenuView>()!=null);
        if(containsTask) { foreach(Transform child in branch) HideBranch(child); return; }
        var group=branch.GetComponent<CanvasGroup>(); bool added=group==null; if(added)group=branch.gameObject.AddComponent<CanvasGroup>();
        hidden.Add((group,group.alpha,group.interactable,group.blocksRaycasts,added)); group.alpha=0; group.interactable=false; group.blocksRaycasts=false;
    }

    private void DestroyWorld()
    {
        ResetProductionHotbar();
        ClearPrepVisuals();
        ClearAssemblyMotion();
        HideNoticeImmediate();
        ClearCollections();
        if(owner?.State!=null)owner.State.FryerPrepPaused=false;
        if(ticketContent!=null)Clear(ticketContent);
        ticketLabels.Clear();ticketProducts.Clear();boundTicket=null;lastSignature=null;
        cookingVisuals.Clear();
        acceptedPortion=null; observedStages.Clear();
        RestoreStaffVisuals();
        foreach (var go in transientVisuals) if (go != null) Destroy(go);
        transientVisuals.Clear(); foodVisual=null;
        if (activeStation != null) {
            if(activeStation.cookingAudio!=null)activeStation.cookingAudio.Stop();
            if(stationCamera!=null)
            {
                stationCamera.orthographicSize=cookingViewSize;stationCamera.fieldOfView=cookingFieldOfView;
                if(activeStation.cookingViewAnchor!=null)stationCamera.transform.SetPositionAndRotation(activeStation.cookingViewAnchor.position,activeStation.cookingViewAnchor.rotation);
            }
            if (activeStation.cookingFeedback != null) activeStation.cookingFeedback.SetActive(false);
            activeStation.gameObject.SetActive(false);
            if(activeStation.labels!=null)activeStation.labels.gameObject.SetActive(false);
        }
        activeStation=null; stationCamera=null;
    }
    public void Exit()
    {
        if(!opened)return;
        State.PrepTransferred-=OnPrepTransferred;
        StopStationTransition(); CancelDrag(); owner.State.Enter(FastFoodStationMode.None); PlayerTaskGuidance.SetKitchenFocus(false); PlayerTaskGuidance.ClearTask("FastFoodCooking");
        DestroyWorld(); if(previousCamera!=null)previousCamera.enabled=previousCameraEnabled;
        foreach(var h in hidden) if(h.group!=null) { h.group.alpha=h.alpha; h.group.interactable=h.interactable; h.group.blocksRaycasts=h.blocks; if(h.added)Destroy(h.group); }
        hidden.Clear(); PlayerTaskHUD.Instance?.SetKitchenLayout(false); RestoreEquipmentOutlines(); ManagerPlayer.Active?.SetExternalInputSuppressed(false);
        Cursor.lockState=previousCursor; Cursor.visible=previousCursorVisible; opened=false;
        selection.gameObject.SetActive(false); station.gameObject.SetActive(false); help.gameObject.SetActive(false);
    }
    private void OnDisable() { Exit(); }
}
