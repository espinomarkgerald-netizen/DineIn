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
    public Bounds ClipboardBounds(RectTransform button, Image artwork, RectTransform space)
    {
        if (artwork == null || !artwork.isActiveAndEnabled)
            return BoundsIn(button, space);

        Rect rect = artwork.GetPixelAdjustedRect();
        Sprite sprite = artwork.overrideSprite;
        if (sprite != null && artwork.type == UnityEngine.UI.Image.Type.Simple)
        {
            Vector2 size = sprite.rect.size;
            if (artwork.preserveAspect && size.x > 0 && size.y > 0 && rect.width > 0 && rect.height > 0)
            {
                float ratio = size.x / size.y;
                Vector2 fitted = rect.width / rect.height > ratio
                    ? new Vector2(rect.height * ratio, rect.height)
                    : new Vector2(rect.width, rect.width / ratio);
                rect.position += Vector2.Scale(rect.size - fitted, artwork.rectTransform.pivot);
                rect.size = fitted;
            }
            if (sprite == art.taskClipboardSprite)
            {
                Rect ink = art.TaskClipboardInk;
                rect = new Rect(rect.x + rect.width * ink.x, rect.y + rect.height * ink.y,
                    rect.width * ink.width, rect.height * ink.height);
            }
            else if (size.x > 0 && size.y > 0)
            {
                Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
                rect = Rect.MinMaxRect(
                    rect.xMin + rect.width * padding.x / size.x,
                    rect.yMin + rect.height * padding.y / size.y,
                    rect.xMax - rect.width * padding.z / size.x,
                    rect.yMax - rect.height * padding.w / size.y);
            }
        }
        return BoundsIn(artwork.rectTransform, space, rect);
    }

    internal Bounds PanelBounds(RectTransform space)
    {
        if (panelArtwork == null) return BoundsIn(frame, space);
        Rect rect = panelArtwork.GetPixelAdjustedRect();
        // The transparent border is inside the preserved region of this sliced sprite.
        float pixelsPerUnit = panelArtwork.pixelsPerUnit * panelArtwork.pixelsPerUnitMultiplier;
        Vector4 inset = art.taskPanelInkInset / pixelsPerUnit;
        rect = Rect.MinMaxRect(rect.xMin + inset.x, rect.yMin + inset.y,
            rect.xMax - inset.z, rect.yMax - inset.w);
        return BoundsIn(frame, space, rect);
    }

    private Bounds BoundsIn(RectTransform root, RectTransform space, Rect? localRect = null)
    {
        if (localRect is Rect rect)
        {
            dockCorners[0] = root.TransformPoint(new Vector3(rect.xMin, rect.yMin));
            dockCorners[1] = root.TransformPoint(new Vector3(rect.xMin, rect.yMax));
            dockCorners[2] = root.TransformPoint(new Vector3(rect.xMax, rect.yMax));
            dockCorners[3] = root.TransformPoint(new Vector3(rect.xMax, rect.yMin));
        }
        else root.GetWorldCorners(dockCorners);

        var bounds = new Bounds(space.InverseTransformPoint(dockCorners[0]), Vector3.zero);
        for (int i = 1; i < dockCorners.Length; i++)
            bounds.Encapsulate(space.InverseTransformPoint(dockCorners[i]));
        return bounds;
    }

    public Vector2 DockedPosition(RectTransform button, Image artwork)
    {
        if (button == null || frame.parent is not RectTransform safe) return frame.anchoredPosition;
        float pad=art.contentPadding,gap=art.taskButtonGap;
        Vector2 size = new Vector2(Mathf.Min(art.taskSize.x, Mathf.Max(1f, safe.rect.width - pad * 2)), art.taskSize.y);
        if (frame.sizeDelta != size)
        {
            frame.sizeDelta = size;
            Layout();
        }
        Bounds target = ClipboardBounds(button, artwork, safe);
        Bounds panel = PanelBounds(safe);
        float left = target.min.x - gap - panel.size.x;
        float centerY = target.center.y;
        if (left < safe.rect.xMin + pad)
        {
            left = Mathf.Clamp(target.min.x, safe.rect.xMin + pad, safe.rect.xMax - pad - panel.size.x);
            centerY = target.max.y + gap + panel.extents.y;
            if (centerY + panel.extents.y > safe.rect.yMax - pad)
                centerY = target.min.y - gap - panel.extents.y;
        }
        centerY = Mathf.Clamp(centerY, safe.rect.yMin + pad + panel.extents.y, safe.rect.yMax - pad - panel.extents.y);
        return frame.anchoredPosition + new Vector2(left - panel.min.x, centerY - panel.center.y);
    }
    public static CompactTaskPanelView Attach(RectTransform root,TMP_Text oldAction,TMP_Text oldDetail)
    {
        if(root==null||CustomerDrawerArt.Shared==null)return null;
        var view=root.GetComponent<CompactTaskPanelView>();if(view==null)view=root.gameObject.AddComponent<CompactTaskPanelView>();
        view.Initialize(root,oldAction,oldDetail);return view;
    }
    // Measure final rendered transforms, including inherited scale and clipboard artwork.
    public void KeepClearOf(RectTransform button, Image artwork)
    {
        if(frame==null || !frame.gameObject.activeInHierarchy || button==null || frame.parent is not RectTransform safe)return;
        var target=ClipboardBounds(button,artwork,safe);var panel=PanelBounds(safe);float gap=art.taskButtonGap,pad=art.contentPadding;
        if(panel.max.x<=target.min.x-gap || panel.min.x>=target.max.x+gap || panel.max.y<=target.min.y-gap || panel.min.y>=target.max.y+gap)return;
        float dx=target.min.x-gap-panel.max.x;
        Vector3 move=panel.min.x+dx>=safe.rect.xMin+pad ? new Vector3(dx,0,0) : new Vector3(0,target.max.y+gap-panel.min.y,0);
        if(move.y>0 && panel.max.y+move.y>safe.rect.yMax-pad && target.min.y-gap-panel.size.y>=safe.rect.yMin+pad)
            move=new Vector3(0,target.min.y-gap-panel.max.y,0);
        frame.localPosition+=move;
    }
    private Image panelArtwork;
    private void Initialize(RectTransform root,TMP_Text oldAction,TMP_Text oldDetail)
    {
        if(frame!=null)return;frame=root;art=CustomerDrawerArt.Shared;legacyAction=oldAction;legacyDetail=oldDetail;
        frame.localScale=Vector3.one;frame.sizeDelta=art.taskSize;
        panelArtwork=GetComponent<Image>();if(panelArtwork!=null){panelArtwork.sprite=art.Find("PanelFrame");panelArtwork.type=UnityEngine.UI.Image.Type.Sliced;panelArtwork.pixelsPerUnitMultiplier=2;panelArtwork.color=Color.white;panelArtwork.raycastTarget=true;}
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
        action.fontSize=art.headingSize*f;support.fontSize=art.smallSize*f;
        FitSingleLine(destination,art.bodySize*f);FitSingleLine(table,art.smallSize*f);
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
