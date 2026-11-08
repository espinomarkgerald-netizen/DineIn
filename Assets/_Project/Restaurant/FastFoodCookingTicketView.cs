using UnityEngine;
using TMPro;
public sealed class FastFoodCookingTicketView : MonoBehaviour
{
    public TMP_Text title, timer;
    public Transform products;
    public UnityEngine.UI.Image progressFill;
    public CanvasGroup visibility;
    public RectTransform motion;
    [Min(.05f)] public float slideSeconds=.24f;
    public float slideDistance=70;
    [Min(.05f)] public float exitSeconds=.22f;
    [System.NonSerialized] public float entranceDelay;
    [Min(200)] public float minimumHeight=380;
    [Min(.01f)] public float progressSmoothSeconds=.18f;
    float progressTarget,progressVelocity;
    bool progressInitialized;
    public void SetProgress(float value)
    {
        progressTarget=Mathf.Clamp01(value);
        if(progressFill!=null&&!progressInitialized){progressFill.fillAmount=progressTarget;progressInitialized=true;progressVelocity=0;}
    }
    public void FitProducts(int count)
    {
        var grid=products!=null?products.GetComponent<UnityEngine.UI.GridLayoutGroup>():null;
        if(grid==null)return;
        // Three columns work well for six-item tickets, but force a four-item
        // order into an unbalanced 3+1 layout. Use a compact 2x2 for four items.
        int columns=count==4?2:Mathf.Clamp(count,1,3);
        grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount=columns;
        grid.childAlignment=TextAnchor.UpperCenter;
        int rows=Mathf.CeilToInt((float)count/columns);
        float height=Mathf.Max(minimumHeight,166+rows*grid.cellSize.y+Mathf.Max(0,rows-1)*grid.spacing.y+grid.padding.vertical);
        var rect=(RectTransform)transform;rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
        var layout=GetComponent<UnityEngine.UI.LayoutElement>();if(layout!=null)layout.preferredHeight=height;
    }
    float animationTime;
    bool leaving;
    Vector2 restingPosition, exitStart;
    float exitAlpha;
    float Travel => motion!=null?Mathf.Max(slideDistance,motion.rect.width+16):slideDistance;
    void Awake() { FastFoodTicketAppearance.Apply(this); if(motion!=null)restingPosition=motion.anchoredPosition; }
    void OnEnable()
    {
        animationTime=0; leaving=false; progressInitialized=false; progressVelocity=0;
        if(visibility!=null) { visibility.alpha=0; visibility.blocksRaycasts=false; visibility.interactable=false; }
        if(motion!=null)motion.anchoredPosition=restingPosition+Vector2.left*(LevelOneUIAccessibility.ReducedMotion?0:Travel);
    }
    public void Dismiss()
    {
        if(leaving)return;
        leaving=true;animationTime=0;
        exitStart=motion!=null?motion.anchoredPosition:Vector2.zero;
        exitAlpha=visibility!=null?visibility.alpha:0;
        if(visibility!=null) { visibility.blocksRaycasts=false; visibility.interactable=false; }
        var layout=GetComponent<UnityEngine.UI.LayoutElement>();
        if(layout!=null)layout.ignoreLayout=true;
    }
    void Update()
    {
        if(progressFill!=null&&progressInitialized)
            progressFill.fillAmount=LevelOneUIAccessibility.ReducedMotion?progressTarget:
                Mathf.SmoothDamp(progressFill.fillAmount,progressTarget,ref progressVelocity,progressSmoothSeconds,Mathf.Infinity,Time.unscaledDeltaTime);
        if(visibility==null||motion==null) { if(leaving)Destroy(gameObject);return; }
        animationTime+=Time.unscaledDeltaTime;
        float duration=LevelOneUIAccessibility.ReducedMotion ? .08f : leaving ? exitSeconds : slideSeconds;
        float t=Mathf.Clamp01((animationTime-(leaving?0:entranceDelay))/Mathf.Max(.05f,duration));
        float fade=Mathf.SmoothStep(0,1,t), eased=leaving?fade:1-Mathf.Pow(1-t,3);
        visibility.alpha=leaving?exitAlpha*(1-fade):fade;
        motion.anchoredPosition=LevelOneUIAccessibility.ReducedMotion?restingPosition:leaving?
            Vector2.Lerp(exitStart,restingPosition+Vector2.left*Travel,eased):restingPosition+Vector2.left*(Travel*(1-eased));
        if(!leaving&&t>=1) { visibility.blocksRaycasts=true; visibility.interactable=true; }
        if(leaving&&t>=1)Destroy(gameObject);
    }
    public FastFoodCookingDragHandle productTemplate;
    public string titleFormat = "#{0} · {1}", timerFormat = "{0} · {1}", quantityFormat = "{0}/{1}";
    public string playerText = "YOUR ORDER", staffText = "STAFF", waitingText = "Waiting / cooking", preparingText = "Staff preparing";
}

public static class FastFoodTicketAppearance
{
    public static void Apply(FastFoodCookingTicketView ticket)
    {
        var art=CustomerDrawerArt.Shared;if(art==null)return;
        var root=ticket.motion!=null?ticket.motion:(RectTransform)ticket.transform;
        if(root.Find("Order header")!=null)return;
        var image=root.GetComponent<UnityEngine.UI.Image>();
        if(image!=null){image.sprite=art.Find("PanelFrame");image.type=UnityEngine.UI.Image.Type.Sliced;image.color=Color.white;image.pixelsPerUnitMultiplier=2;}
        var tab=CustomerDrawerUI.Image("Order header",root,"HeaderTab");
        tab.rectTransform.anchorMin=new Vector2(.04f,1);tab.rectTransform.anchorMax=new Vector2(.96f,1);
        tab.rectTransform.pivot=new Vector2(.5f,1);tab.rectTransform.anchoredPosition=new Vector2(0,-8);tab.rectTransform.sizeDelta=new Vector2(0,40);tab.transform.SetAsFirstSibling();
        foreach(var label in new[]{ticket.title,ticket.timer}){label.font=art.headingFont;label.color=Color.white;label.fontSize=24;label.textWrappingMode=TextWrappingModes.NoWrap;}
        Place(ticket.title.rectTransform,.07f,.65f);Place(ticket.timer.rectTransform,.68f,.94f);
        ticket.title.alignment=TextAlignmentOptions.MidlineLeft;ticket.timer.alignment=TextAlignmentOptions.MidlineRight;
        var item=ticket.productTemplate;
        if(item!=null)
        {
            var surface=item.GetComponent<UnityEngine.UI.Image>();
            if(surface!=null){surface.sprite=art.Find("HoverRow");surface.type=UnityEngine.UI.Image.Type.Sliced;surface.color=new Color(1,.96f,.86f);}
            item.count.font=art.headingFont;item.count.fontSize=24;item.count.color=art.ink;
        }
        var footer=CustomerDrawerUI.Text("Ticket hint",root,"Match the ticket to finish");
        footer.fontSize=18;footer.alignment=TextAlignmentOptions.Center;
        footer.rectTransform.anchorMin=new Vector2(.05f,0);footer.rectTransform.anchorMax=new Vector2(.95f,0);
        footer.rectTransform.pivot=new Vector2(.5f,0);footer.rectTransform.anchoredPosition=new Vector2(0,12);footer.rectTransform.sizeDelta=new Vector2(0,28);
        if(ticket.progressFill!=null)ticket.progressFill.transform.parent.gameObject.SetActive(false);
    }
    private static void Place(RectTransform rect,float left,float right)
    {
        rect.anchorMin=new Vector2(left,1);rect.anchorMax=new Vector2(right,1);rect.pivot=new Vector2(.5f,1);
        rect.anchoredPosition=new Vector2(0,-10);rect.sizeDelta=new Vector2(0,36);
    }
}
