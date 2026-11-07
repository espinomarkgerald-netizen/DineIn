using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CustomerDrawerUI;

[DisallowMultipleComponent]
public sealed class CompactTaskPanelView : MonoBehaviour
{
    private CustomerDrawerArt art;
    private RectTransform frame,content;
    private ScrollRect scroll;
    private Image icon,destinationIcon,badge;
    private TMP_Text action,destination,table,support;
    private TMP_Text legacyAction,legacyDetail;
    private CanvasGroup contentGroup;
    private Coroutine fade;
    private string key;
    private bool lastLarge;
    private readonly Vector3[] dockCorners=new Vector3[4];
    private readonly System.Collections.Generic.List<Graphic> clipboardGraphics=new();
    public void DockBeside(RectTransform button)
    {
        if(frame==null || button==null || frame.parent is not RectTransform safe)return;
        float pad=art.contentPadding,gap=art.taskButtonGap;
        frame.sizeDelta=new Vector2(Mathf.Min(art.taskSize.x,Mathf.Max(160,safe.rect.width-pad*2)),art.taskSize.y);
        button.GetWorldCorners(dockCorners);Vector3 low=safe.InverseTransformPoint(dockCorners[0]),high=safe.InverseTransformPoint(dockCorners[2]);
        float left=low.x-gap-frame.rect.width,centerY=(low.y+high.y)*.5f;
        if(left<safe.rect.xMin+pad){left=Mathf.Clamp(low.x,safe.rect.xMin+pad,safe.rect.xMax-pad-frame.rect.width);centerY=high.y+gap+frame.rect.height*.5f;}
        centerY=Mathf.Clamp(centerY,safe.rect.yMin+pad+frame.rect.height*.5f,safe.rect.yMax-pad-frame.rect.height*.5f);
        frame.localPosition=new Vector3(left+frame.pivot.x*frame.rect.width,centerY+(frame.pivot.y-.5f)*frame.rect.height,frame.localPosition.z);Layout();
    }
    public static CompactTaskPanelView Attach(RectTransform root,TMP_Text oldAction,TMP_Text oldDetail)
    {
        if(root==null||CustomerDrawerArt.Shared==null)return null;
        var view=root.GetComponent<CompactTaskPanelView>();if(view==null)view=root.gameObject.AddComponent<CompactTaskPanelView>();
        view.Initialize(root,oldAction,oldDetail);return view;
    }
    // Measure final rendered transforms, including inherited scale and clipboard artwork.
    public void KeepClearOf(RectTransform button)
    {
        if(frame==null || !frame.gameObject.activeInHierarchy || button==null || frame.parent is not RectTransform safe)return;
        Bounds BoundsOf(RectTransform root,bool graphics)
        {
            root.GetWorldCorners(dockCorners);var b=new Bounds(safe.InverseTransformPoint(dockCorners[0]),Vector3.zero);
            foreach(var c in dockCorners)b.Encapsulate(safe.InverseTransformPoint(c));
            if(graphics){root.GetComponentsInChildren(false,clipboardGraphics);foreach(var g in clipboardGraphics)if(g.enabled){g.rectTransform.GetWorldCorners(dockCorners);foreach(var c in dockCorners)b.Encapsulate(safe.InverseTransformPoint(c));}}
            return b;
        }
        var target=BoundsOf(button,true);var panel=BoundsOf(frame,false);float gap=art.taskButtonGap,pad=art.contentPadding;
        if(panel.max.x<=target.min.x-gap || panel.min.x>=target.max.x+gap || panel.max.y<=target.min.y-gap || panel.min.y>=target.max.y+gap)return;
        float dx=target.min.x-gap-panel.max.x;
        Vector3 move=panel.min.x+dx>=safe.rect.xMin+pad ? new Vector3(dx,0,0) : new Vector3(0,target.max.y+gap-panel.min.y,0);
        if(move.y>0 && panel.max.y+move.y>safe.rect.yMax-pad && target.min.y-gap-panel.size.y>=safe.rect.yMin+pad)
            move=new Vector3(0,target.min.y-gap-panel.max.y,0);
        frame.localPosition+=move;
    }
    private void Initialize(RectTransform root,TMP_Text oldAction,TMP_Text oldDetail)
    {
        if(frame!=null)return;frame=root;art=CustomerDrawerArt.Shared;legacyAction=oldAction;legacyDetail=oldDetail;
        frame.localScale=Vector3.one;frame.sizeDelta=art.taskSize;
        var image=GetComponent<Image>();if(image!=null){image.sprite=art.Find("PanelFrame");image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=2;image.color=Color.white;image.raycastTarget=true;}
        if(legacyAction!=null)legacyAction.enabled=false;if(legacyDetail!=null)legacyDetail.enabled=false;
        Header(frame,"CURRENT TASK");scroll=Scroll("Task content",frame,out content);contentGroup=content.gameObject.AddComponent<CanvasGroup>();
        icon=Image("Action icon",content,"IconReceipt",false);action=Text("Action text",content,"",true);
        destinationIcon=Image("Destination icon",content,"IconCashier",false);destination=Text("Destination text",content,"");
        badge=Image("Table badge",content,"TableBadge");table=Text("Table text",badge.transform,"");Stretch(table.rectTransform);table.alignment=TextAlignmentOptions.Center;table.color=Color.white;
        support=Text("Supporting instructions",content,"");Render(default,"Choose a customer or work item.",false);
    }
    private void OnEnable(){if(frame!=null)Layout();}
    private void OnDisable(){if(fade!=null)StopCoroutine(fade);fade=null;if(contentGroup!=null)contentGroup.alpha=1;}
    private void Update(){if(frame!=null && lastLarge!=LevelOneUIAccessibility.LargeText)Layout();}
    public void Render(PlayerTaskView task,string idle=null,bool animate=true)
    {
        if(frame==null)return;
        bool changed=key!=task.Source+":"+task.Key;key=task.Source+":"+task.Key;
        action.text=task.IsValid?(string.IsNullOrEmpty(task.ShortAction)?task.Action:task.ShortAction):"No active task";
        destination.text=task.Destination??"";table.text=task.Table??"";support.text=task.IsValid?task.Detail:(idle??"Choose a customer or work item.");
        var sprite=art.Find(task.Icon??"IconReceipt");if(sprite==null && GameplayActionButtonArt.Shared!=null)sprite=GameplayActionButtonArt.Shared.Find(task.Icon??"");
        icon.sprite=sprite!=null?sprite:art.Find("IconReceipt");
        Layout();if(changed)scroll.verticalNormalizedPosition=1;
        if(changed && animate && isActiveAndEnabled && !LevelOneUIAccessibility.ReducedMotion){if(fade!=null)StopCoroutine(fade);fade=StartCoroutine(Reveal());}
    }
    public void ShowConfirmedCompletion()
    {
        if(frame==null)return;Render(default,"Ready for the next job",false);action.text="Task complete";icon.sprite=art.Find("IconCheck");Layout();
    }
    private IEnumerator Reveal(){for(float t=0;t<art.contentFade;t+=Time.unscaledDeltaTime){contentGroup.alpha=Mathf.Lerp(.35f,1,t/art.contentFade);yield return null;}contentGroup.alpha=1;fade=null;}
    private void Layout()
    {
        if(frame==null)return;lastLarge=LevelOneUIAccessibility.LargeText;
        float f=lastLarge?1.2f:1,pad=art.contentPadding,w=frame.rect.width-pad*2-10;
        Place((RectTransform)scroll.transform,pad,30,frame.rect.width-pad*2,frame.rect.height-41);
        action.fontSize=art.headingSize*f;destination.fontSize=art.bodySize*f;support.fontSize=art.smallSize*f;table.fontSize=art.smallSize*f;
        float titleHeight=Mathf.Max(38,action.GetPreferredValues(action.text,w-51,0).y);
        Place(icon.rectTransform,2,1,40,40);Place(action.rectTransform,48,0,w-51,titleHeight);
        float y=titleHeight+8;bool hasDestination=!string.IsNullOrEmpty(destination.text),hasTable=!string.IsNullOrEmpty(table.text);
        destination.gameObject.SetActive(hasDestination);destinationIcon.enabled=hasDestination;badge.gameObject.SetActive(hasTable);
        if(hasDestination||hasTable){
            float dw=w-32-(hasTable?94:0),height=Mathf.Max(30,destination.GetPreferredValues(destination.text,dw,0).y);
            Place(destinationIcon.rectTransform,0,y,26,26);Place(destination.rectTransform,33,y,dw,height);
            Place(badge.rectTransform,w-91,y,86,29);y+=height+7;
        }
        float sh=string.IsNullOrEmpty(support.text)?0:support.GetPreferredValues(support.text,w-9,0).y;
        // Start a supporting paragraph below the fold rather than showing a cut first line.
        if(sh>0 && y+support.GetPreferredValues("Ag",w-9,0).y>((RectTransform)scroll.transform).rect.height)y=Mathf.Max(y,((RectTransform)scroll.transform).rect.height+3);
        Place(support.rectTransform,3,y,w-9,sh+3);content.sizeDelta=new Vector2(0,y+sh+5);
        if(legacyAction!=null)legacyAction.enabled=false;if(legacyDetail!=null)legacyDetail.enabled=false;
    }
}
