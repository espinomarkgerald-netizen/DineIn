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
    private Outline previewOutline;
    private FastFoodCookingDragHandle drag;
    private bool opened, previousCameraEnabled, previousCursorVisible;
    private CursorLockMode previousCursor;
    private RestockStorageType alertStorage;
    private string lastSignature;
    private float refreshAt, feedbackUntil;
    private readonly List<GameObject> transientVisuals = new();
    private readonly List<(FastFoodCookingState.Ticket ticket, FastFoodCookingTicketView view)> ticketLabels = new();
    private readonly List<(CanvasGroup group,float alpha,bool interactable,bool blocks,bool added)> hidden = new();
    public bool IsOpen => opened;
    public bool IsAuthored => canvas != null && ingredientTemplate != null && ticketTemplate != null && stations.Length == 3 && stations.All(s=>s!=null && s.stationCamera!=null && s.preparation!=null);

    private void Awake() { owner=GetComponent<FastFoodCookingController>(); }
    private void Start()
    {
        if(owner==null || !owner.Active) { if(canvas!=null)canvas.gameObject.SetActive(false); enabled=false; return; }
        if (!IsAuthored) { Debug.LogError("Kitchen presentation is missing. Run Dine In > Fast Food > Create Editable Kitchen in Lobby2.",this); enabled=false; return; }
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
        CancelDrag(); State.Enter(FastFoodStationMode.None); PlayerTaskGuidance.SetKitchenFocus(true);
        DestroyWorld(); selection.gameObject.SetActive(true); station.gameObject.SetActive(false); help.gameObject.SetActive(false);
        if(previousCamera!=null)previousCamera.enabled=previousCameraEnabled;
        Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
    }
    private void Enter(FastFoodStationMode mode)
    {
        CancelDrag(); DestroyWorld();
        activeStation=stations.First(s=>s.mode==mode);
        activeStation.gameObject.SetActive(true); activeStation.labels.gameObject.SetActive(true);
        stationCamera=activeStation.stationCamera;
        if(previousCamera!=null)previousCamera.enabled=false;
        loadTarget=activeStation.cooking; prepTarget=activeStation.preparation; discardTarget=activeStation.discard;
        foreach(var target in new[]{loadTarget,prepTarget,discardTarget}) if(target!=null)target.view=this;
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
        var batch=p?.batch??State.Batches.FirstOrDefault(b=>FastFoodCookingState.Station(b.recipe)==State.Mode&&b.completed<b.target);
        progress.text=State.Mode==FastFoodStationMode.Assembler ? t==null?waitingAssignment:string.Format(trayFormat,t.number,t.portions.Count(x=>x.placed),t.portions.Count) :
            batch==null?idleProduction:string.Format(batchFormat,batch.recipe.DisplayName,batch.completed,batch.target,Mathf.Max(0,batch.target-batch.completed),TimeLabel(State.deadlineSeconds-batch.elapsed));
        progressFill.rectTransform.anchorMax=new Vector2(batch!=null?(float)batch.completed/Mathf.Max(1,batch.target):t!=null?(float)t.portions.Count(x=>x.placed)/Mathf.Max(1,t.portions.Count):0,1);
        serve.gameObject.SetActive(State.Mode==FastFoodStationMode.Assembler); serve.interactable=t!=null&&t.Ready;
        tickets.gameObject.SetActive(State.Mode==FastFoodStationMode.Assembler);
        if(Time.unscaledTime>feedbackUntil) { feedback.text=pointerHint; feedback.color=hintColor; }
        if(activeStation.status!=null)
            activeStation.status.text=p==null?idleStatus:string.Format(foodStatusFormat,p.recipe.DisplayName,stageNames[(int)p.stage],
                p.stage==FastFoodCookingStage.Cooking?TimeLabel(State.cookSeconds-p.elapsed):p.stage==FastFoodCookingStage.Ready?TimeLabel(State.overcookSeconds-p.elapsed):"");
        var low=MenuCatalog.Default.Ingredients.Where(i=>InventoryManager.Instance.GetStock(i.itemType)<=Mathf.Max(1,i.unitsPerBox*lowStockFraction)).Take(maxStockNotices).ToList();
        alertStorage=low.Count>0?low[0].requiredStorage:RestockStorageType.Dry;
        alerts.text=low.Count>0?string.Join("\n",low.Select(i=>string.Format(stockNoticeFormat,i.displayName,InventoryManager.Instance.GetStock(i.itemType)))):PlayerTaskGuidance.RestockNotification.IsValid?PlayerTaskGuidance.RestockNotification.Action:supplyReady;
        noticeButtonLabel.text=low.Count>0?string.Format(noticeCountFormat,low.Count):noticeTitle;
        string signature=State.Mode+":"+(p==null?"idle":p.recipe.ProductId+":"+p.stage+":"+p.ingredientStep)+":"+
            (State.Mode==FastFoodStationMode.Assembler?string.Join(";",State.Tickets.Select(x=>x.number+":"+x.player+":"+x.submitted+":"+string.Join(",",x.portions.Select(y=>y.stage+"/"+y.placed)))):"");
        if(drag==null && signature!=lastSignature) { lastSignature=signature; RebuildControls(p,t); }
        foreach(var entry in ticketLabels)
            entry.view.timer.text=string.Format(entry.view.timerFormat,TimeLabel(State.deadlineSeconds-entry.ticket.elapsed),entry.ticket.player?entry.view.playerText:entry.ticket.portions.Any(x=>x.stage!=FastFoodCookingStage.Complete)?entry.view.waitingText:entry.view.preparingText);
        foreach(var slot in hotbar.GetComponentsInChildren<FastFoodCookingDragHandle>())
            if(slot.item!=null)
            {
                int owned=InventoryManager.Instance.GetStock(slot.item.itemType), available=State.Available(slot.item.itemType);
                slot.count.text=string.Format(slot.stockFormat,slot.item.displayName,available,owned-available,owned/Mathf.Max(1,slot.item.unitsPerBox));
            }
    }
    private static string TimeLabel(float seconds) { int s=Mathf.CeilToInt(Mathf.Max(0,seconds)); return (s/60)+":"+(s%60).ToString("00"); }
    private void Clear(Transform parent) { foreach(Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
    private GameObject SpawnVisual(GameObject template, Transform anchor, bool draggable=false)
    {
        var go=Instantiate(template,anchor); go.SetActive(true);
        transientVisuals.Add(go);
        if(draggable)
        {
            var handle=go.GetComponent<FastFoodCookingDragHandle>();
            if(handle==null)throw new InvalidOperationException("Cooking food prefab needs a FastFoodCookingDragHandle: "+template.name);
            handle.view=this; handle.portion=State.PlayerPortion;
        }
        return go;
    }
    private void RebuildControls(FastFoodCookingState.Portion p,FastFoodCookingState.Ticket ticket)
    {
        Clear(hotbar); Clear(ticketContent); ticketLabels.Clear();
        foreach(var go in transientVisuals) if(go!=null)Destroy(go); transientVisuals.Clear(); foodVisual=null;
        if(State.Mode==FastFoodStationMode.Assembler)
        {
            foreach(var t in State.Tickets.Where(t=>t.active&&!t.submitted).OrderByDescending(t=>t.player))
            {
                var card=Instantiate(ticketTemplate,ticketContent); card.gameObject.SetActive(true);
                card.title.text=string.Format(card.titleFormat,t.number,t.player?card.playerText:card.staffText);
                foreach(var group in t.portions.GroupBy(x=>x.recipe))
                {
                    var product=Instantiate(card.productTemplate,card.products); product.gameObject.SetActive(true);
                    product.enabled=false; product.icon.sprite=group.Key.sprite; product.icon.color=group.All(x=>x.placed)?card.placedColor:card.pendingColor;
                    product.count.text=string.Format(card.quantityFormat,group.Count(x=>x.placed),group.Count());
                }
                ticketLabels.Add((t,card));
            }
            if(ticket!=null)
            {
                foreach(var group in ticket.portions.Where(x=>!x.placed).GroupBy(x=>x.recipe))
                    AddSlot(group.Key.sprite,group.Key.DisplayName,null,group.First(),group.Count());
                int index=0;
                foreach(var portion in ticket.portions.Where(x=>x.placed))
                {
                    var anchor=activeStation.trayAnchors[index++%activeStation.trayAnchors.Length];
                    SpawnVisual(portion.recipe.kitchenServingPrefab!=null?portion.recipe.kitchenServingPrefab:
                        portion.recipe.category==MenuProductCategory.Drink?activeStation.drinkTemplate:activeStation.servingTemplate,anchor);
                }
            }
            return;
        }
        foreach(var item in MenuCatalog.Default.Products.Where(r=>r!=null&&MenuAvailabilityManager.IsProductAvailable(r)&&FastFoodCookingState.Station(r)==State.Mode).SelectMany(FastFoodCookingState.Steps).Select(s=>s.item).Distinct())
            AddSlot(item.sprite,item.displayName,item,p,1);
        if(p==null || p.stage==FastFoodCookingStage.Waiting)return;
        foodVisual=SpawnVisual(p.recipe.kitchenCookingPrefab!=null?p.recipe.kitchenCookingPrefab:activeStation.cookingFoodTemplate,p.stage==FastFoodCookingStage.Preparing?activeStation.prepFoodAnchor:activeStation.foodAnchor,true);
        Tint(foodVisual,p.stage==FastFoodCookingStage.Burnt?activeStation.burntColor:p.stage==FastFoodCookingStage.Cooking?activeStation.cookingColor:activeStation.readyColor);
    }
    private void AddSlot(Sprite sprite,string title,ItemData item,FastFoodCookingState.Portion portion,int count)
    {
        var slot=Instantiate(ingredientTemplate,hotbar); slot.gameObject.SetActive(true);
        slot.view=this; slot.item=item; slot.portion=portion; slot.icon.sprite=sprite;
        slot.count.text=item!=null?title:string.Format(slot.servingFormat,title,count,portion.stage==FastFoodCookingStage.Complete||portion.recipe.category==MenuProductCategory.Drink?slot.readyText:slot.waitingText);
    }
    private void Tint(GameObject go,Color color)
    {
        var props=new MaterialPropertyBlock(); props.SetColor("_BaseColor",color); props.SetColor("_Color",color);
        foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(props);
    }
    public void BeginDrag(FastFoodCookingDragHandle handle,Vector2 position)
    {
        CancelDrag();
        if(HygieneManager.KitchenPaused) { Message(pausedMessage,true); return; }
        if(HygieneManager.HoldNewCooking && handle.portion?.stage==FastFoodCookingStage.Waiting) { Message(closedMessage,true); return; }
        if(State.Mode!=FastFoodStationMode.Assembler && (handle.portion==null||handle.portion!=State.PlayerPortion)) { Message(waitMessage,false); return; }
        if(State.Mode==FastFoodStationMode.Assembler&&!State.BeginServingDrag(handle.portion)) { Message(waitingFood,true); return; }
        var previewTemplate=handle.item!=null && handle.item.kitchenPreviewPrefab!=null ? handle.item.kitchenPreviewPrefab :
            handle.item==null && handle.portion?.recipe.kitchenPreviewPrefab!=null ? handle.portion.recipe.kitchenPreviewPrefab : activeStation.dragPreviewTemplate;
        drag=handle; preview=Instantiate(previewTemplate,activeStation.transform); preview.SetActive(true);
        previewOutline=RestockRoomController.PrepareWorldDragPreview(preview); MoveDrag(position);
    }
    public void MoveDrag(Vector2 position)
    {
        if(drag==null||preview==null||stationCamera==null)return;
        var ray=stationCamera.ScreenPointToRay(position); hovered=null;
        foreach(var hit in Physics.RaycastAll(ray,activeStation.rayDistance,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance))
        { var target=hit.collider.GetComponent<FastFoodCookingDropTarget>(); if(target!=null&&target.view==this) { hovered=target; break; } }
        preview.transform.position=hovered!=null?hovered.transform.position+Vector3.up*activeStation.previewLift:ray.GetPoint(activeStation.previewDistance);
        Color tint=CanDrop(hovered)?activeStation.validDropColor:activeStation.invalidDropColor; Tint(preview,tint);
        if(previewOutline!=null)previewOutline.OutlineColor=tint;
    }
    private bool CanDrop(FastFoodCookingDropTarget target)
    {
        if(target==null || drag==null) return false;
        var p=drag.portion;
        if(State.Mode==FastFoodStationMode.Assembler) return target.kind==1 && p.dragging;
        if(p!=State.PlayerPortion) return false;
        if(drag.item==null) return target.kind==1 && p.stage==FastFoodCookingStage.Ready || target.kind==2 && p.stage==FastFoodCookingStage.Burnt;
        var steps=FastFoodCookingState.Steps(p.recipe);
        return target.kind==0 && p.stage==FastFoodCookingStage.Waiting && steps[0].item==drag.item ||
            target.kind==1 && p.stage==FastFoodCookingStage.Preparing && p.ingredientStep<steps.Count && steps[p.ingredientStep].item==drag.item;
    }
    public void EndDrag(Vector2 position)
    {
        MoveDrag(position); bool success=false;
        if(CanDrop(hovered) && !HygieneManager.KitchenPaused)
        {
            if(State.Mode==FastFoodStationMode.Assembler) success=State.Place(drag.portion);
            else if(drag.item!=null) success=State.Load(drag.portion,drag.item);
            else success=hovered.kind==2?State.Discard(drag.portion):State.Collect(drag.portion);
        }
        Message(success ? placedMessage : returnedMessage,!success);
        if(!success && preview!=null && drag!=null)
        {
            Vector3 target=drag.item!=null?stationCamera.transform.TransformPoint(returnPreviewOffset):drag.transform.position;
            var returning=preview; preview=null; StartCoroutine(ReturnPreview(returning,target));
        }
        CancelDrag(); lastSignature=null;
    }
    private IEnumerator ReturnPreview(GameObject returning,Vector3 target)
    {
        Vector3 start=returning.transform.position; float elapsed=0;
        while(returning!=null && elapsed<returnSeconds) { elapsed+=Time.unscaledDeltaTime; returning.transform.position=Vector3.Lerp(start,target,Mathf.Clamp01(elapsed/Mathf.Max(.01f,returnSeconds))); yield return null; }
        if(returning!=null)Destroy(returning);
    }
    private void CancelDrag() { if(drag!=null && drag.portion!=null) drag.portion.dragging=false; drag=null; hovered=null; if(preview!=null) Destroy(preview); }
    private void Message(string text,bool error) { if(feedback==null)return; feedback.text=text; feedback.color=error?errorColor:successColor; feedbackUntil=Time.unscaledTime+feedbackSeconds; }
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
        foreach (var go in transientVisuals) if (go != null) Destroy(go);
        transientVisuals.Clear(); foodVisual=null;
        if (activeStation != null) { activeStation.gameObject.SetActive(false); activeStation.labels.gameObject.SetActive(false); }
        activeStation=null; stationCamera=null;
    }
    public void Exit()
    {
        if(!opened)return;
        CancelDrag(); owner.State.Enter(FastFoodStationMode.None); PlayerTaskGuidance.SetKitchenFocus(false); PlayerTaskGuidance.ClearTask("FastFoodCooking");
        DestroyWorld(); if(previousCamera!=null)previousCamera.enabled=previousCameraEnabled;
        foreach(var h in hidden) if(h.group!=null) { h.group.alpha=h.alpha; h.group.interactable=h.interactable; h.group.blocksRaycasts=h.blocks; if(h.added)Destroy(h.group); }
        hidden.Clear(); ManagerPlayer.Active?.SetExternalInputSuppressed(false);
        Cursor.lockState=previousCursor; Cursor.visible=previousCursorVisible; opened=false;
        selection.gameObject.SetActive(false); station.gameObject.SetActive(false); help.gameObject.SetActive(true);
    }
    private void OnDisable() { Exit(); }
}
