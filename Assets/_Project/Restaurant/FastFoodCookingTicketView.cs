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
        int columns=grid.constraint==UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount?Mathf.Max(1,grid.constraintCount):3;
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
    void Awake() { if(motion!=null)restingPosition=motion.anchoredPosition; }
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
