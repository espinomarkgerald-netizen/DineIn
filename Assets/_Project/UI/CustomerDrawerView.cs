using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static CustomerDrawerUI;

[DefaultExecutionOrder(-9000)]
public sealed class CustomerDrawerView : MonoBehaviour
{
    private RectTransform frame, listContent, detailContent;
    private CanvasGroup listGroup, detailGroup, ownerGroup;
    private ScrollRect listScroll, detailScroll;
    private TMP_Text count, empty;
    private CustomerDrawerArt art;
    private CustomerGroup selected;
    private readonly List<CustomerGroup> groups=new();
    private readonly Dictionary<CustomerGroup,Row> rows=new();
    private readonly Stack<Row> freeRows=new();
    private readonly List<CustomerGroup> removed=new();
    private Row detail;
    private Button back,find;
    private readonly TMP_Text[] factText=new TMP_Text[3];
    private readonly Image[] factIcons=new Image[3];
    private bool expanded,details,wasUsable;
    private bool lastLarge;
    private Coroutine contentRoutine;
    private float nextRefresh;
    private MainCameraController focusedCamera;
    private bool originalSuspended;
    private Outline focusedOutline;
    private bool outlineEnabled;
    private Color outlineColor;
    private float highlightUntil;
    private bool captured,uiPress,dragged,multiTouchGesture;
    private int consumedFrame=-1;
    private Vector2 press;
    private CustomerGroup pressedGroup;
    private bool Usable => expanded && gameObject.activeInHierarchy && ownerGroup!=null && ownerGroup.alpha>.9f &&
        !GameplayUIBlocker.IsBlocked() && !GameplaySfx.PresentationSuspended;
    public bool IsOpen => expanded;
    internal RectTransform GroupCountRect => count != null ? count.rectTransform : null;
    private static CustomerDrawerView Current => LobbyHUDRedesign.Instance!=null?LobbyHUDRedesign.Instance.CustomerDrawer:null;
    private static bool WorldInspectionAllowed => Current!=null && Current.gameObject.activeInHierarchy &&
        Current.ownerGroup!=null && Current.ownerGroup.alpha>.9f && !GameplayUIBlocker.IsBlocked() &&
        !GameplaySfx.PresentationSuspended && !TutorialOwnsWorldTarget;
    private static bool TutorialOwnsWorldTarget {
        get {
            var t=TutorialSystem.Instance;
            if(t!=null && !t.IsComplete && (t.AreUnrelatedInteractionsRestricted || t.IsWaitingForGameplayAction))return true;
            return FineDiningTutorialBridge.Active && FineDiningTutorialBridge.Instance.CurrentLesson!=null &&
                FineDiningTutorialBridge.Instance.CurrentLesson.control!=null && FineDiningTutorialBridge.Instance.CurrentLesson.control.StartsWith("World:",System.StringComparison.Ordinal);
        }
    }
    // Direct OnMouseDown dispatch runs outside Update ordering; never allow it to issue service while inspecting.
    public static bool BlocksCustomerClick => WorldInspectionAllowed;
    // A plain table-number bubble is an inspection target; its explicit Serve action stays authoritative.
    public static bool TryInspectGroupFromUI(CustomerGroup group)
    {if(!WorldInspectionAllowed || group==null || !group.CaptureInspection().Visible)return false;Current.SelectGroup(group);return true;}
    public static bool BlocksWorldPointer => WorldInspectionAllowed && (Current.captured || Current.multiTouchGesture || Current.consumedFrame==Time.frameCount);
    public static bool TryCaptureWorldPress(Vector2 position)
    {
        if(!WorldInspectionAllowed)return false;
        if(Current.captured)return true;
        if(!Input.GetMouseButtonDown(0) && !(Input.touchCount>0 && Input.GetTouch(0).phase==TouchPhase.Began))return false;
        if(OverUI(position))return false;
        var group=ResolveHit(position);
        if(group==null)return false;
        Current.captured=true;Current.dragged=false;Current.press=position;Current.pressedGroup=group;return true;
    }
    private static CustomerGroup ResolveHit(Vector2 position)
    {
        var cam=UIRoot.GameplayCameraOrNull();if(cam==null)cam=Camera.main;if(cam==null)return null;
        var hits=Physics.RaycastAll(cam.ScreenPointToRay(position),500f,~0,QueryTriggerInteraction.Collide);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var h in hits) {
            var group=h.collider.GetComponentInParent<CustomerGroup>();
            if(group==null){var booth=h.collider.GetComponentInParent<Booth>();if(booth!=null)group=booth.CurrentGroup;}
            if(group!=null && group.CaptureInspection().Visible)return group;
            // Do not inspect through other selectable props or interactable surfaces.
            if(h.collider.GetComponentInParent<IInteractable>()!=null)return null;
        }
        return null;
    }
    public void Initialize(CanvasGroup host)
    {
        if(frame!=null)return;art=CustomerDrawerArt.Shared;if(art==null)return;
        ownerGroup=host;frame=(RectTransform)transform;frame.sizeDelta=art.listSize;
        foreach(var g in GetComponentsInChildren<Graphic>(true))if(g.gameObject!=gameObject)g.gameObject.SetActive(false);
        var background=GetComponent<Image>();if(background!=null){background.enabled=true;background.sprite=art.Find("PanelFrame");background.type=UnityEngine.UI.Image.Type.Sliced;background.pixelsPerUnitMultiplier=2;background.color=Color.white;background.raycastTarget=true;}
        Header(frame,"CUSTOMERS");
        var list=Node("Customer list",frame);Stretch(list);listGroup=list.gameObject.AddComponent<CanvasGroup>();
        count=Text("Count",list,"0 groups");count.fontSize=art.smallSize;
        listScroll=Scroll("Groups",list,out listContent);
        empty=Text("Empty",list,"No customers inside yet.");empty.alignment=TextAlignmentOptions.Center;
        var content=Node("Group details",frame);Stretch(content);detailGroup=content.gameObject.AddComponent<CanvasGroup>();
        back=Button("Back",content,"All groups",()=>ShowList(true));
        detailScroll=Scroll("Stats",content,out detailContent);
        detail=new Row(detailContent,null);
        for(int i=0;i<3;i++){factIcons[i]=Image("Service icon "+i,detailContent,"IconWaiting",false);factText[i]=Text("Service fact "+i,detailContent,"");}
        find=Button("Find",detailContent,"Find group",FindSelected,"SecondaryButton");
        listContent.sizeDelta=new Vector2(0,0);ShowList(false);Layout();
        SceneManager.activeSceneChanged+=SceneChanged;
    }
    public void SetExpanded(bool value,bool reset=false)
    {
        expanded=value;
        if(!value||reset)ResetInspection();
        if(value)RefreshGroups(FindObjectsByType<CustomerGroup>(FindObjectsSortMode.InstanceID));
    }
    public void ResetInspection(){selected=null;pressedGroup=null;captured=multiTouchGesture=uiPress=false;ShowList(false);ClearFocus();}
    private void SceneChanged(Scene a,Scene b){ResetInspection();RefreshGroups(System.Array.Empty<CustomerGroup>());}
    private void OnDestroy(){SceneManager.activeSceneChanged-=SceneChanged;ClearFocus();}
    private void OnDisable(){ResetInspection();wasUsable=false;}
    public void RefreshGroups(CustomerGroup[] live)
    {
        if(frame==null)return;
        var scene=SceneManager.GetActiveScene();
        System.Array.Sort(live,(a,b)=>a==null?1:b==null?-1:a.InspectionArrivalTime.CompareTo(b.InspectionArrivalTime));
        foreach(var group in live)if(group!=null && group.gameObject.scene==scene && group.CaptureInspection().Visible && !groups.Contains(group))groups.Add(group);
        removed.Clear();foreach(var group in groups)if(group==null || group.gameObject.scene!=scene || !group.CaptureInspection().Visible)removed.Add(group);
        foreach(var group in removed){groups.Remove(group);if(rows.TryGetValue(group,out var row)){row.Root.gameObject.SetActive(false);freeRows.Push(row);rows.Remove(group);}}
        if(selected==null || !groups.Contains(selected)){selected=null;if(details)ShowList(false);ClearFocus();}
        foreach(var group in groups)if(!rows.ContainsKey(group)) {
            var row=freeRows.Count>0?freeRows.Pop():new Row(listContent,SelectGroup);row.Group=group;row.Root.gameObject.SetActive(true);rows.Add(group,row);
        }
        Set(count,groups.Count+(groups.Count==1?" group":" groups"));empty.gameObject.SetActive(groups.Count==0);RefreshData();
    }
    private void Update()
    {
        if(frame==null)return;
        bool usable=Usable;
        if(!usable && wasUsable)ResetInspection();wasUsable=usable;
        if(highlightUntil>0 && Time.unscaledTime>=highlightUntil)ClearOutline();
        if(lastLarge!=LevelOneUIAccessibility.LargeText){Layout();RefreshData();}
        if(usable && Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.15f;RefreshData();}
        if(usable){foreach(var row in rows.Values)row.Tick(art);detail.Tick(art);}
        if(!WorldInspectionAllowed){captured=false;return;}
        if(multiTouchGesture && Input.touchCount==0){multiTouchGesture=false;consumedFrame=Time.frameCount;}
        if(Input.touchCount>0) {
            var t=Input.GetTouch(0);
            if(t.phase==TouchPhase.Began){uiPress=OverUI(t.position);if(!uiPress)TryCaptureWorldPress(t.position);}
            if(captured && Input.touchCount>1)multiTouchGesture=true;
            if(captured && (multiTouchGesture || Vector2.Distance(t.position,press)>=12))dragged=true;
            if(t.phase==TouchPhase.Ended||t.phase==TouchPhase.Canceled)Release(t.position,t.phase==TouchPhase.Canceled);
        } else {
            if(Input.GetMouseButtonDown(0)){uiPress=OverUI(Input.mousePosition);if(!uiPress)TryCaptureWorldPress(Input.mousePosition);}
            if(captured && Vector2.Distance((Vector2)Input.mousePosition,press)>=12)dragged=true;
            if(Input.GetMouseButtonUp(0))Release(Input.mousePosition,false);
        }
    }
    private static readonly List<RaycastResult> uiHits=new();
    private static bool OverUI(Vector2 position)
    {
        if(EventSystem.current==null)return false;
        uiHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},uiHits);
        foreach(var hit in uiHits)if(hit.module is GraphicRaycaster)return true;
        return false;
    }
    private void Release(Vector2 position,bool cancel)
    {
        if(uiPress){consumedFrame=Time.frameCount;uiPress=false;}
        if(!captured)return;captured=false;consumedFrame=Time.frameCount;
        if(!cancel && !dragged && Vector2.Distance(position,press)<12 && !OverUI(position) && pressedGroup!=null && pressedGroup.CaptureInspection().Visible) {
            SelectGroup(pressedGroup);GameplaySfx.SelectWorld();
        }
        pressedGroup=null;
    }
    private void SelectGroup(CustomerGroup group)
    {
        if(group==null || !group.CaptureInspection().Visible)return;
        LobbyHUDRedesign.Instance?.OpenCustomerDrawer();
        if(!groups.Contains(group))RefreshGroups(FindObjectsByType<CustomerGroup>(FindObjectsSortMode.InstanceID));
        ClearFocus();selected=group;details=true;Layout();RefreshData();SwitchContent(true,true);
    }
    private void ShowList(bool animate)
    {details=false;selected=null;ClearFocus();if(frame!=null){Layout();SwitchContent(false,animate);}}
    private void SwitchContent(bool detailMode,bool animate)
    {
        if(listGroup==null||detailGroup==null)return;
        if(contentRoutine!=null)StopCoroutine(contentRoutine);
        listGroup.gameObject.SetActive(true);detailGroup.gameObject.SetActive(true);
        listGroup.blocksRaycasts=listGroup.interactable=!detailMode;detailGroup.blocksRaycasts=detailGroup.interactable=detailMode;
        if(!animate||LevelOneUIAccessibility.ReducedMotion){FinishContent(detailMode);return;}
        contentRoutine=StartCoroutine(FadeContent(detailMode));
    }
    private IEnumerator FadeContent(bool show)
    {
        float a=listGroup.alpha,b=detailGroup.alpha;
        for(float t=0;t<art.contentFade;t+=Time.unscaledDeltaTime){float k=Mathf.SmoothStep(0,1,t/art.contentFade);listGroup.alpha=Mathf.Lerp(a,show?0:1,k);detailGroup.alpha=Mathf.Lerp(b,show?1:0,k);yield return null;}
        FinishContent(show);
    }
    private void FinishContent(bool show){listGroup.alpha=show?0:1;detailGroup.alpha=show?1:0;listGroup.gameObject.SetActive(!show);detailGroup.gameObject.SetActive(show);contentRoutine=null;}
    private void Layout()
    {
        lastLarge=LevelOneUIAccessibility.LargeText;frame.sizeDelta=details?art.detailsSize:art.listSize;
        // Bound the drawer to its existing safe area; content scrolls when Large Text needs more room.
        var parent=frame.parent as RectTransform;
        if(parent!=null && parent.rect.height>0){frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Min(frame.rect.height,Mathf.Max(180,parent.rect.height-200)));frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(frame.rect.width,Mathf.Max(220,parent.rect.width-48)));}
        float w=frame.rect.width,h=frame.rect.height;
        var header=transform.Find("Header") as RectTransform;
        if(header!=null){header.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(w-24,260));Set(header.Find("Title")?.GetComponent<TMP_Text>(),details?(w<300?"DETAILS":"CUSTOMER DETAILS"):"CUSTOMERS");}
        count.fontSize=art.smallSize*(lastLarge?1.2f:1);
        float pad=art.contentPadding;
        float countHeight=Mathf.Max(22,count.GetPreferredValues(count.text,w-pad*2-10,0).y),listY=27+countHeight;
        Place(count.rectTransform,pad+5,27,w-pad*2-10,countHeight);Place((RectTransform)listScroll.transform,pad,listY,w-pad*2,h-listY-8);Place(empty.rectTransform,pad*2,92,w-pad*4,100);
        Place((RectTransform)back.transform,pad+4,33,lastLarge?160:125,lastLarge?38:32);Place((RectTransform)detailScroll.transform,pad,lastLarge?80:73,w-pad*2,h-(lastLarge?93:86));
    }
    private void RefreshData()
    {
        if(frame==null)return;float width=frame.rect.width-art.contentPadding*2-14,y=0;
        foreach(var group in groups)if(group!=null && rows.TryGetValue(group,out var row)) {
            float height=row.Bind(group.CaptureInspection(),width,art,selected==group);Place(row.Root,0,y,width,height);y+=height;
        }
        listContent.sizeDelta=new Vector2(0,y);
        if(selected==null)return;
        var state=selected.CaptureInspection();if(!state.Visible){ShowList(false);return;}
        float rh=detail.Bind(state,width,art,true);Place(detail.Root,0,0,width,rh);
        float fy=rh+8;
        for(int i=0;i<3;i++) {
            bool show=i<2||(!selected.IsTakeout && selected.FastFood==null);
            factText[i].gameObject.SetActive(show);factIcons[i].enabled=show;if(!show)continue;
            bool done=i==0?state.OrderTaken:i==1?state.FoodServed:state.BillDelivered;
            string text=!state.FactsKnown?(i==0?"Order: syncing":i==1?"Food: syncing":"Bill: syncing"):i==0?(done?"Order taken":"Order not taken"):i==1?(done?"Food served":"Food not served"):(done?"Bill delivered":"Bill not delivered");
            Set(factText[i],text);factText[i].fontSize=art.bodySize*(lastLarge?1.2f:1);float height=Mathf.Max(28,factText[i].GetPreferredValues(text,width-49,0).y+5);
            factIcons[i].sprite=art.Find(done?"IconCheck":"IconWaiting");Place(factIcons[i].rectTransform,9,fy,24,24);Place(factText[i].rectTransform,42,fy,width-49,height);fy+=height;
        }
        find.interactable=selected.TryGetInspectionPosition(out _,out var findTarget);
        var label=find.GetComponentInChildren<TMP_Text>(true);Set(label,state.Table!=null && findTarget==state.Table.transform?"Find table":"Find group");
        if(!find.interactable)Set(label,"Position unavailable");
        float findHeight=Mathf.Max(44,label.GetPreferredValues(label.text,Mathf.Max(1,width-90),0).y+12);
        Place((RectTransform)find.transform,35,fy+8,width-70,findHeight);detailContent.sizeDelta=new Vector2(0,fy+findHeight+16);
    }
    private void FindSelected()
    {
        if(selected==null)return;var state=selected.CaptureInspection();if(!state.Visible)return;
        if(!selected.TryGetInspectionPosition(out var position,out var target))return;
        ClearFocus();var camera=UIRoot.GameplayCameraOrNull();if(camera==null)camera=Camera.main;
        focusedCamera=camera!=null?camera.GetComponentInParent<MainCameraController>():null;
        if(focusedCamera==null)focusedCamera=FindFirstObjectByType<MainCameraController>();
        if(focusedCamera==null)return;
        originalSuspended=focusedCamera.FollowSuspended;
        focusedCamera.StopFollowing();
        focusedCamera.SetFollowSuspended(true);
        focusedCamera.CenterOnWorldPoint(position,LevelOneUIAccessibility.ReducedMotion);
        if(!LevelOneUIAccessibility.ReducedMotion) {
            focusedOutline=target.GetComponentInChildren<Outline>();
            if(focusedOutline!=null){outlineEnabled=focusedOutline.enabled;outlineColor=focusedOutline.OutlineColor;focusedOutline.enabled=true;focusedOutline.OutlineColor=Color.white;highlightUntil=Time.unscaledTime+.7f;}
        }
    }
    private void ClearOutline(){if(focusedOutline!=null && focusedOutline.OutlineColor==Color.white){focusedOutline.OutlineColor=outlineColor;focusedOutline.enabled=outlineEnabled;}focusedOutline=null;highlightUntil=0;}
    private void ClearFocus(){ClearOutline();if(focusedCamera!=null)focusedCamera.SetFollowSuspended(originalSuspended);focusedCamera=null;}
    public static void ReleaseCameraFocus() => Current?.ClearFocus();
    private static void Set(TMP_Text text,string value){if(text!=null && text.text!=value)text.text=value;}

    private sealed class Row
    {
        internal CustomerGroup Group;
        internal readonly RectTransform Root;
        readonly Image portrait,mood,track,fill,highlight,badge,divider;
        readonly TMP_Text type,members,table,status,patience;
        readonly bool detail;
        float shown=-1,targetValue;
        CustomerGroup boundGroup;
        public Row(Transform parent,System.Action<CustomerGroup> select)
        {
            detail=select==null;Root=Node("Customer row",parent);
            if(select!=null){var hit=Root.gameObject.AddComponent<Image>();hit.color=Color.clear;var b=Root.gameObject.AddComponent<Button>();b.targetGraphic=hit;b.transition=Selectable.Transition.None;b.onClick.AddListener(()=>select(Group));Root.gameObject.AddComponent<ButtonAnimator>();GameplayButtonSound.Ensure(b);}
            highlight=Image("Selected",Root,"SelectedRow");Stretch(highlight.rectTransform);
            portrait=Image("Portrait",Root,"IconGuests",false);type=Text("Type",Root,"",true);members=Text("Members",Root,"");
            badge=Image("Table badge",Root,"TableBadge");table=Text("Table",badge.transform,"");Stretch(table.rectTransform);table.alignment=TextAlignmentOptions.Center;table.color=Color.white;
            status=Text("Status",Root,"");mood=Image("Reaction",Root,"IconNeutral",false);patience=Text("Patience",Root,"");
            track=Image("Patience track",Root,"PatienceTrack");fill=Image("Patience fill",track.transform,"PatienceFillTintable");
            divider=Image("Row divider",Root,"Divider");
        }
        public float Bind(CustomerInspectionSnapshot s,float width,CustomerDrawerArt art,bool selected)
        {
            if(boundGroup!=s.Group){boundGroup=s.Group;shown=-1;}
            float f=LevelOneUIAccessibility.LargeText?1.2f:1;
            type.fontSize=art.headingSize*f;members.fontSize=art.smallSize*f;status.fontSize=art.smallSize*f;table.fontSize=art.smallSize*f;patience.fontSize=art.smallSize*f;
            Set(type,s.Type);Set(members,s.Members+(s.Members==1?" guest":" guests"));Set(table,s.TableLabel);Set(status,s.Status);
            portrait.sprite=s.Portrait!=null?s.Portrait:art.Find("IconGuests");highlight.enabled=selected&&!detail;
            mood.sprite=art.Find(s.ReactionMood==0?"IconHappy":s.ReactionMood==1?"IconNeutral":s.ReactionMood==2?"IconAngry":"IconWaiting");
            float nameHeight=Mathf.Max(24*f,type.GetPreferredValues(type.text,width-72,0).y);
            bool narrow=width<290;
            float statusY=7+nameHeight+22*f+(narrow?24*f:0),statusHeight=Mathf.Max(20*f,status.GetPreferredValues(status.text,width-72,0).y);
            float bottom=statusY+statusHeight+1,height=bottom+(detail?60:23)*f;
            Place(portrait.rectTransform,7,10,45*f,45*f);Place(type.rectTransform,63,7,width-72,nameHeight);
            Place(members.rectTransform,63,7+nameHeight,narrow?width-73:90,22*f);Place(badge.rectTransform,narrow?63:width-140,7+nameHeight+(narrow?24*f:0),Mathf.Min(130,width-73),22*f);
            Place(status.rectTransform,63,statusY,width-72,statusHeight);Place(mood.rectTransform,12,bottom,25*f,25*f);
            if(detail) {
                string reaction=s.Reaction;
                Set(patience,reaction+"\n"+(!s.PatienceKnown?"Patience: syncing":!s.PatienceActive?"Patience: not timed":"Patience: "+Mathf.RoundToInt(s.Patience*100)+"%"));
                Place(patience.rectTransform,49,bottom,width-60,46*f);
                Place(track.rectTransform,63,bottom+49*f,width-73,art.patienceHeight);
            } else {
                Set(patience,!s.PatienceKnown?"Syncing":!s.PatienceActive?"Not timed":"");Place(patience.rectTransform,63,bottom,width-73,24*f);
                Place(track.rectTransform,63,bottom+7,width-73,art.patienceHeight);
            }
            track.enabled=s.PatienceKnown&&s.PatienceActive;fill.enabled=track.enabled;
            targetValue=s.Patience;if(shown<0||LevelOneUIAccessibility.ReducedMotion)shown=s.Patience;
            fill.color=s.Patience>.6f?new Color(.26f,.74f,.31f):s.Patience>.3f?new Color(.95f,.74f,.20f):new Color(.93f,.47f,.41f);
            Place(fill.rectTransform,2,2,Mathf.Max(0,(track.rectTransform.rect.width-4)*shown),art.patienceHeight-4);
            divider.enabled=!detail;Place(divider.rectTransform,7,height-1,width-14,2);
            return height;
        }
        public void Tick(CustomerDrawerArt art)
        {
            if(!track.enabled)return;shown=LevelOneUIAccessibility.ReducedMotion?targetValue:Mathf.Lerp(shown,targetValue,1-Mathf.Exp(-Time.unscaledDeltaTime/Mathf.Max(.01f,art.patienceSmooth)));
            Place(fill.rectTransform,2,2,Mathf.Max(0,(track.rectTransform.rect.width-4)*shown),art.patienceHeight-4);
        }
    }
}
