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
    float animationTime;
    bool leaving;
    void OnEnable() { animationTime=0; leaving=false; }
    public void Dismiss()
    {
        leaving=true;animationTime=0;
        if(visibility!=null)visibility.blocksRaycasts=false;
        var layout=GetComponent<UnityEngine.UI.LayoutElement>();
        if(layout!=null)layout.ignoreLayout=true;
    }
    void Update()
    {
        if(visibility==null||motion==null) { if(leaving)Destroy(gameObject);return; }
        animationTime+=Time.unscaledDeltaTime;
        float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(animationTime/Mathf.Max(.05f,slideSeconds)));
        visibility.alpha=leaving?1-t:t;
        motion.anchoredPosition=Vector2.left*(LevelOneUIAccessibility.ReducedMotion?0:slideDistance*(leaving?t:1-t));
        if(leaving&&t>=1)Destroy(gameObject);
    }
    public FastFoodCookingDragHandle productTemplate;
    public string titleFormat = "#{0} · {1}", timerFormat = "{0} · {1}", quantityFormat = "{0}/{1}";
    public string playerText = "YOUR ORDER", staffText = "STAFF", waitingText = "Waiting / cooking", preparingText = "Staff preparing";
}
