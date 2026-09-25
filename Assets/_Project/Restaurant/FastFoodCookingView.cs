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
    [SerializeField] private UnityEngine.UI.Image progressFill;
    [SerializeField] private UnityEngine.UI.Button help, serve;
    [SerializeField] private FastFoodCookingDragHandle ingredientTemplate;
    [SerializeField] private FastFoodCookingTicketView ticketTemplate;
    [SerializeField] private FastFoodCookingStation[] stations = Array.Empty<FastFoodCookingStation>();
    [SerializeField] private RectTransform hotbarContainer;
    [SerializeField, Min(128)] private float hotbarMaximumWidth=760;
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
    private Outline previewOutline;
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
    public bool IsAuthored => canvas != null && ingredientTemplate != null && ticketTemplate != null && stations.Length == 3 && stations.All(s=>s!=null && s.stationCamera!=null && s.preparation!=null);

    private void Awake() { owner=GetComponent<FastFoodCookingController>(); }
    private void Start()
    {
        if(owner==null || !owner.Active) { if(canvas!=null)canvas.gameObject.SetActive(false); enabled=false; return; }
        if (!IsAuthored) { Debug.LogError("Kitchen presentation is missing. Run Dine In > Fast Food > Create Editable Kitchen in Lobby2.",this); enabled=false; return; }
        canvas.gameObject.SetActive(true);
        selection.gameObject.SetActive(false); station.gameObject.SetActive(false); help.gameObject.SetActive(true);
        foreach(var rig in stations) { rig.gameObject.SetActive(false); rig.labels.gameObject.SetActive(false); }
    }
    public void EnterGrill() => Enter(FastFoodStationMode.Grill);
    public void EnterFry() => Enter(FastFoodStationMode.Fry);
    public void EnterAssembler() => Enter(FastFoodStationMode.Assembler);
    public void ToggleNotices() => notification.gameObject.SetActive(!notification.gameObject.activeSelf);
    public void GoToRestock() { Exit(); RestockFlowCoordinator.EnsureInstance().EnterRestockRoom(alertStorage); }
    public void ServeOrder() { if(State.PlayerTicket!=null && State.Serve(State.PlayerTicket.number)) Message(deliveredMessage,false); }
    public void Open()
    {
        if (opened || ManagerPlayer.Active == null || GameplayUIBlocker.IsBlocked() || Time.timeScale<=0 || RestockFlowCoordinator.Instance?.IsRestockRoomOpen==true) return;
        opened=true; previousCamera=Camera.main; previousCameraEnabled=previousCamera!=null && previousCamera.enabled;
        previousCursor=Cursor.lockState; previousCursorVisible=Cursor.visible;
        ManagerPlayer.Active.SetExternalInputSuppressed(true); HideLobby(); Back();
    }
    public void Back()
    {
        StopStationTransition(); CancelDrag(); State.Enter(FastFoodStationMode.None); PlayerTaskGuidance.SetKitchenFocus(true);
        DestroyWorld(); selection.gameObject.SetActive(true); station.gameObject.SetActive(false); help.gameObject.SetActive(false);
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
        foreach(var target in activeStation.Slots.Concat(new[]{prepTarget,discardTarget})) if(target!=null)target.view=this;
        ResetProductionCamera();
        State.Enter(mode); PlayerTaskGuidance.SetKitchenFocus(true);
        selection.gameObject.SetActive(false); station.gameObject.SetActive(true); notification.gameObject.SetActive(false);
        lastSignature=null; Refresh();
    }
    private void Update()
    {
        if(owner==null || !IsAuthored)return;
        if(!opened)help.interactable=!GameplayUIBlocker.IsBlocked() && Time.timeScale>0 && RestockFlowCoordinator.Instance?.IsRestockRoomOpen!=true;
        if(opened && Time.timeScale>0) { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
        if(Time.unscaledTime>=refreshAt) { refreshAt=Time.unscaledTime+refreshSeconds; Refresh(); }
        UpdateCookingFeedback();
        UpdateProductionCamera();
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
        var p=State.PlayerPortion; var t=State.PlayerTicket;
        heading.text=activeStation.stationTitle;
        var batch=p?.batch;
        progress.text=State.Mode==FastFoodStationMode.Assembler ? t==null?waitingAssignment:string.Format(trayFormat,t.number,t.portions.Count(x=>x.placed),t.portions.Count) :
            batch==null?idleProduction:string.Format(batchFormat,batch.recipe.DisplayName,batch.completed,batch.target,Mathf.Max(0,batch.target-batch.completed),TimeLabel(State.deadlineSeconds-batch.elapsed));
        progressFill.rectTransform.anchorMax=new Vector2(batch!=null?(float)batch.completed/Mathf.Max(1,batch.target):t!=null?(float)t.portions.Count(x=>x.placed)/Mathf.Max(1,t.portions.Count):0,1);
        serve.gameObject.SetActive(State.Mode==FastFoodStationMode.Assembler && t!=null && t.Ready); serve.interactable=t!=null&&t.Ready;
        tickets.gameObject.SetActive(State.Mode==FastFoodStationMode.Assembler);
        progressFill.transform.parent.gameObject.SetActive(batch!=null || t!=null);
        if(feedback!=null)feedback.transform.parent.gameObject.SetActive(false);
        if(activeStation.status!=null)
        {
            activeStation.status.transform.parent.gameObject.SetActive(p!=null && p.stage!=FastFoodCookingStage.Waiting && p.stage!=FastFoodCookingStage.Preparing);
            activeStation.status.text=p==null?idleStatus:string.Format(foodStatusFormat,p.recipe.DisplayName,stageNames[(int)p.stage],
                p.stage==FastFoodCookingStage.Cooking?TimeLabel(State.cookSeconds-p.elapsed):p.stage==FastFoodCookingStage.Ready?TimeLabel(State.overcookSeconds-p.elapsed):"");
        }
        if(discardTarget!=null)discardTarget.gameObject.SetActive(State.PlayerWork.Any(x=>x.player&&x.stage==FastFoodCookingStage.Burnt));
        UpdateDropHints();
        var low=MenuCatalog.Default.Products.Where(r=>r!=null && r.IsUnlocked && MenuAvailabilityManager.IsProductAvailable(r))
            .SelectMany(FastFoodCookingState.Steps).Select(s=>s.item).Distinct()
            .Where(i=>InventoryManager.Instance.GetStock(i.itemType)<=Mathf.Max(1,i.unitsPerBox*lowStockFraction)).Take(maxStockNotices).ToList();
        alertStorage=low.Count>0?low[0].requiredStorage:RestockStorageType.Dry;
        alerts.text=low.Count>0?string.Join("\n",low.Select(i=>string.Format(stockNoticeFormat,i.displayName,InventoryManager.Instance.GetStock(i.itemType)))):PlayerTaskGuidance.RestockNotification.IsValid?PlayerTaskGuidance.RestockNotification.Action:supplyReady;
        noticeButtonLabel.text=low.Count>0?string.Format(noticeCountFormat,low.Count):noticeTitle;
        bool needsNotice=low.Count>0 || PlayerTaskGuidance.RestockNotification.IsValid;
        noticeButtonLabel.transform.parent.gameObject.SetActive(needsNotice);
        if(!needsNotice)notification.gameObject.SetActive(false);
        if(State.Mode!=FastFoodStationMode.Assembler) RefreshProductionHeader();
        RefreshStaffActivity();
        UpdateKitchenAudio();
        string signature=ProductionSignature()+":"+State.Mode+":"+(p==null?"idle":p.recipe.ProductId+":"+p.stage+":"+p.ingredientStep)+":"+
            (State.Mode==FastFoodStationMode.Assembler && t!=null?t.number+":"+string.Join(",",t.portions.Select(y=>y.stage+"/"+y.placed)):"");
        if(drag==null && signature!=lastSignature) { lastSignature=signature; RebuildControls(p,t); }
        foreach(var entry in ticketLabels)
        {
            entry.view.timer.text=string.Format(entry.view.timerFormat,TimeLabel(State.deadlineSeconds-entry.ticket.elapsed),entry.ticket.player?entry.view.playerText:entry.ticket.portions.Any(x=>x.stage!=FastFoodCookingStage.Complete)?entry.view.waitingText:entry.view.preparingText);
            if(entry.view.progressFill!=null)entry.view.progressFill.fillAmount=(float)entry.ticket.portions.Count(x=>x.placed)/Mathf.Max(1,entry.ticket.portions.Count);
        }
        foreach(var entry in ticketProducts)
            if(boundTicket!=null && entry.view!=null)
                entry.view.count.text=string.Format(ticketTemplate.quantityFormat,boundTicket.portions.Count(x=>x.recipe==entry.recipe&&x.placed),boundTicket.portions.Count(x=>x.recipe==entry.recipe));
        heading.transform.parent.gameObject.SetActive(State.Mode==FastFoodStationMode.Assembler?t!=null:State.PlayerWork.Any(x=>x.player)||State.ShowingCompletedBatch);
        foreach(var slot in hotbar.GetComponentsInChildren<FastFoodCookingDragHandle>())
            if(slot.item!=null)
            {
                int units=InventoryManager.Instance.GetStock(slot.item.itemType);
                if(State.PrepReady && slot.portion!=null && FastFoodCookingState.Steps(slot.portion.recipe).FirstOrDefault()?.item==slot.item)
                    units=State.PlayerWork.Count(x=>x.recipe==slot.portion.recipe && x.proteinReady && x.stage!=FastFoodCookingStage.Complete);
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
        Clear(hotbar); RefreshTicket(State.Mode==FastFoodStationMode.Assembler?ticket:null);
        if(hotbarContainer!=null)hotbarContainer.gameObject.SetActive(State.Mode!=FastFoodStationMode.Assembler || ticket!=null && ticket.portions.Any(x=>!x.placed));
        foreach(var go in transientVisuals) if(go!=null)Destroy(go); transientVisuals.Clear(); foodVisual=null;
        if(State.Mode==FastFoodStationMode.Assembler)
        {
            if(ticket!=null)
            {
                foreach(var group in ticket.portions.Where(x=>!x.placed).GroupBy(x=>x.recipe))
                    AddSlot(group.Key.sprite,group.Key.DisplayName,null,group.First(),group.Count());
                int index=0;
                foreach(var portion in ticket.portions.Where(x=>x.placed))
                {
                    var anchor=activeStation.trayAnchors[index++%activeStation.trayAnchors.Length];
                    var placed=SpawnVisual(portion.recipe.kitchenServingPrefab!=null?portion.recipe.kitchenServingPrefab:
                        portion.recipe.category==MenuProductCategory.Drink?activeStation.drinkTemplate:activeStation.servingTemplate,anchor);
                    AnimateAccepted(placed,portion);
                }
            }
            FitHotbar(); acceptedPortion=null; return;
        }
        RebuildProductionControls();
        acceptedPortion=null;
    }
    private void RefreshTicket(FastFoodCookingState.Ticket ticket)
    {
        if(boundTicket==ticket)return;
        boundTicket=ticket;
        foreach(var entry in ticketLabels)if(entry.view!=null)entry.view.Dismiss();
        ticketLabels.Clear(); ticketProducts.Clear();
        if(ticket==null)return;
        var card=Instantiate(ticketTemplate,ticketContent);card.gameObject.SetActive(true);
        card.title.text=string.Format(card.titleFormat,ticket.number,card.playerText);
        foreach(var group in ticket.portions.GroupBy(x=>x.recipe))
        {
            var product=Instantiate(card.productTemplate,card.products);product.gameObject.SetActive(true);
            product.enabled=false;product.icon.sprite=group.Key.sprite;
            product.count.text=string.Format(card.quantityFormat,group.Count(x=>x.placed),group.Count());
            ticketProducts.Add((group.Key,product));
        }
        ticketLabels.Add((ticket,card));
    }
    private void FitHotbar()
    {
        if(hotbarContainer==null)return;
        int count=0; foreach(Transform child in hotbar)if(child.gameObject.activeSelf)count++;
        hotbarContainer.gameObject.SetActive(count>0);
        var scroll=hotbarContainer.GetComponent<UnityEngine.UI.ScrollRect>();
        float scrollPosition=scroll!=null?scroll.horizontalNormalizedPosition:0;
        var grid=hotbar.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if(grid!=null)hotbarContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            Mathf.Min(hotbarMaximumWidth,grid.padding.horizontal+count*grid.cellSize.x+Mathf.Max(0,count-1)*grid.spacing.x));
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(hotbar);
        if(scroll!=null)scroll.horizontalNormalizedPosition=scrollPosition;
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
    public void BeginDrag(FastFoodCookingDragHandle handle,Vector2 position)
    {
        CancelDrag();
        if(cameraMoving||stationTransition!=null)return;
        if(State.Mode!=FastFoodStationMode.Assembler && handle.item!=null)
            handle.portion=State.PrepReady?State.PlayerPortion:State.NextLoad(handle.item);
        if(HygieneManager.KitchenPaused) { Warn(pausedMessage); return; }
        if(HygieneManager.HoldNewCooking && handle.portion?.stage==FastFoodCookingStage.Waiting) { Warn(closedMessage); return; }
        if(State.Mode!=FastFoodStationMode.Assembler && (handle.portion==null||!handle.portion.player||FastFoodCookingState.WorkStation(handle.portion)!=State.Mode)) { Warn(waitMessage); return; }
        if(State.Mode==FastFoodStationMode.Assembler&&!State.BeginServingDrag(handle.portion)) { Warn(waitingFood,handle.portion?.recipe.sprite); return; }
        var previewTemplate=handle.previewTemplate!=null?handle.previewTemplate:handle.item!=null && handle.item.kitchenPreviewPrefab!=null ? handle.item.kitchenPreviewPrefab :
            handle.item==null && handle.portion?.recipe.kitchenPreviewPrefab!=null ? handle.portion.recipe.kitchenPreviewPrefab : activeStation.dragPreviewTemplate;
        drag=handle; preview=Instantiate(previewTemplate,activeStation.transform); preview.SetActive(true);
        PrepareCenteredGhost();
        previewOutline=RestockRoomController.PrepareWorldDragPreview(preview);
        if(State.Mode==FastFoodStationMode.Fry)fryerPrepUntil=handle.item==null?float.PositiveInfinity:0;
        MoveDrag(position);
    }
    public void MoveDrag(Vector2 position)
    {
        if(drag==null||preview==null||stationCamera==null)return;
        var ray=stationCamera.ScreenPointToRay(position); hovered=null;
        foreach(var hit in Physics.RaycastAll(ray,activeStation.rayDistance,activeStation.interactionLayers,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance))
        { var target=hit.collider.GetComponent<FastFoodCookingDropTarget>(); if(target!=null&&target.view==this) { hovered=target; break; } }
        var anchor=hovered==null?null:hovered.kind==0?hovered.foodAnchor:hovered.kind==1?activeStation.prepFoodAnchor:hovered.transform;
        preview.transform.position=anchor!=null?anchor.position+Vector3.up*activeStation.previewLift:ray.GetPoint(activeStation.previewDistance);
        Color tint=CanDrop(hovered)?activeStation.validDropColor:activeStation.invalidDropColor;
        tint.a=ghostOpacity;
        if(previewOutline!=null)previewOutline.OutlineColor=tint;
        Tint(preview,tint);
        UpdateDropHints();
    }
    private bool CanDrop(FastFoodCookingDropTarget target)
    {
        if(target==null || drag==null) return false;
        var p=drag.portion;
        if(State.Mode==FastFoodStationMode.Assembler) return target.kind==1 && p.dragging;
        if(p==null || !p.player || FastFoodCookingState.WorkStation(p)!=State.Mode) return false;
        if(drag.item==null) return target.kind==1 && p.stage==FastFoodCookingStage.Ready || target.kind==2 && p.stage==FastFoodCookingStage.Burnt;
        return (target.kind==0 && p.stage==FastFoodCookingStage.Waiting || target.kind==1 && p.stage==FastFoodCookingStage.Preparing) && State.CanLoad(p,drag.item,target.slotIndex);
    }
    public void EndDrag(Vector2 position)
    {
        MoveDrag(position); bool success=false;
        var attempted=drag!=null?drag.portion:null;
        int previousStep=attempted!=null?attempted.ingredientStep:0;
        if(CanDrop(hovered) && !HygieneManager.KitchenPaused)
        {
            if(State.Mode==FastFoodStationMode.Assembler) success=State.Place(drag.portion);
            else if(drag.item!=null) success=State.Load(drag.portion,drag.item,hovered.slotIndex);
            else success=hovered.kind==2?State.Discard(drag.portion):State.Collect(drag.portion);
        }
        Message(success ? State.Mode==FastFoodStationMode.Fry && attempted!=null && FastFoodCookingState.PreparationStation(attempted.recipe)==FastFoodStationMode.Grill && attempted.proteinReady ?
            "Ready for Grill: "+attempted.recipe.DisplayName : placedMessage : returnedMessage,!success);
        if(success) { acceptedPortion=attempted;acceptedStep=previousStep;PlayKitchenCue(attempted?.stage==FastFoodCookingStage.Complete?finishedSound:placementSound); }
        if(!success && preview!=null && drag!=null)
        {
            Vector3 target=drag.item!=null?stationCamera.transform.TransformPoint(returnPreviewOffset):drag.transform.position;
            var returning=preview; preview=null; StartCoroutine(ReturnPreview(returning,target));
        }
        if(State.Mode==FastFoodStationMode.Fry)fryerPrepUntil=drag!=null&&drag.item==null?Time.unscaledTime+prepLookSeconds:0;
        CancelDrag(); lastSignature=null;
    }
    private IEnumerator ReturnPreview(GameObject returning,Vector3 target)
    {
        if(LevelOneUIAccessibility.ReducedMotion) { if(returning!=null)Destroy(returning);yield break; }
        Vector3 start=returning.transform.position; float elapsed=0;
        while(returning!=null && elapsed<returnSeconds) { elapsed+=Time.unscaledDeltaTime; returning.transform.position=Vector3.Lerp(start,target,Mathf.Clamp01(elapsed/Mathf.Max(.01f,returnSeconds))); yield return null; }
        if(returning!=null)Destroy(returning);
    }
    private void CancelDrag() { if(drag!=null && drag.portion!=null) drag.portion.dragging=false; drag=null; hovered=null; if(preview!=null) Destroy(preview); UpdateDropHints(); }
    private void UpdateDropHints()
    {
        foreach(var target in (activeStation!=null?activeStation.Slots:Array.Empty<FastFoodCookingDropTarget>()).Concat(new[]{prepTarget,discardTarget}))
            if(target!=null && target.hint!=null)
            {
                target.hint.SetActive(activeStation!=null && target.gameObject.activeInHierarchy && drag!=null && CanDrop(target));
                if(target.hint.activeSelf && target.hint.transform is RectTransform rect && stationCamera!=null)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent,stationCamera.WorldToScreenPoint(target.transform.position),null,out var point);
                    rect.anchoredPosition=point;
                }
            }
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
        StopStationTransition(); CancelDrag(); owner.State.Enter(FastFoodStationMode.None); PlayerTaskGuidance.SetKitchenFocus(false); PlayerTaskGuidance.ClearTask("FastFoodCooking");
        DestroyWorld(); if(previousCamera!=null)previousCamera.enabled=previousCameraEnabled;
        foreach(var h in hidden) if(h.group!=null) { h.group.alpha=h.alpha; h.group.interactable=h.interactable; h.group.blocksRaycasts=h.blocks; if(h.added)Destroy(h.group); }
        hidden.Clear(); ManagerPlayer.Active?.SetExternalInputSuppressed(false);
        Cursor.lockState=previousCursor; Cursor.visible=previousCursorVisible; opened=false;
        selection.gameObject.SetActive(false); station.gameObject.SetActive(false); help.gameObject.SetActive(true);
    }
    private void OnDisable() { Exit(); }
}
