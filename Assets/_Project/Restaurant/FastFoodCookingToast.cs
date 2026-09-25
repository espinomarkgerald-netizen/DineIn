using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>One authored, background-free lane for brief kitchen cues.</summary>
public sealed class FastFoodCookingToast : MonoBehaviour
{
    public TMP_Text label;
    public UnityEngine.UI.Image icon;
    public CanvasGroup group;
    public RectTransform motion;
    [Min(.2f)] public float duration=2.2f;
    [Min(0)] public float rise=48;
    [Min(0)] public float iconGap=14;
    [Min(.05f)] public float popSeconds=.28f;
    [Range(.1f,1)] public float startScale=.65f;
    [Range(1,1.5f)] public float overshootScale=1.18f;
    [Range(1,5)] public int queueLimit=3;
    public Color normal=Color.white, error=new Color(1,.25f,.25f),
        warning=new Color(1,.84f,.2f), success=new Color(.35f,1,.5f);
    readonly Queue<(string text,Sprite icon,int kind)> pending=new();
    string current="";
    Vector2 origin;
    float elapsed;
    int currentPriority;
    bool showing, initialized;

    void Initialize()
    {
        if(initialized)return;
        initialized=true; origin=motion.anchoredPosition;
        group.alpha=0;group.blocksRaycasts=false;group.interactable=false;
    }
    public void Show(string text, Sprite sprite=null, int kind=0)
    {
        if(string.IsNullOrEmpty(text)||label==null||group==null||motion==null)return;
        Initialize();
        if(showing&&current==text)return;
        foreach(var entry in pending)if(entry.text==text)return;
        int priority=kind==1?3:kind==2?2:kind==3?1:0;
        if(showing&&priority>currentPriority) { pending.Clear();showing=false; }
        while(pending.Count>=queueLimit)pending.Dequeue();
        pending.Enqueue((text,sprite,kind));
        if(!showing)Next();
    }
    void Next()
    {
        var entry=pending.Dequeue();current=entry.text;label.text=current;
        currentPriority=entry.kind==1?3:entry.kind==2?2:entry.kind==3?1:0;
        label.color=entry.kind==1?error:entry.kind==2?warning:entry.kind==3?success:normal;
        icon.sprite=entry.icon;icon.enabled=entry.icon!=null;
        if(icon.enabled)
        {
            float width=Mathf.Min(label.rectTransform.rect.width,label.GetPreferredValues(current).x);
            icon.rectTransform.anchoredPosition=new Vector2(Mathf.Max(0,(motion.rect.width-width)*.5f-icon.rectTransform.rect.width-iconGap),0);
        }
        elapsed=0;showing=true;motion.anchoredPosition=origin;
        group.alpha=1;
        motion.localScale=Vector3.one*(LevelOneUIAccessibility.ReducedMotion?1:startScale);
    }
    void Update()
    {
        if(!showing)return;
        elapsed+=Time.unscaledDeltaTime;
        float t=Mathf.Clamp01(elapsed/Mathf.Max(.2f,duration));
        group.alpha=Mathf.Clamp01((1-t)/.25f);
        bool reduced=LevelOneUIAccessibility.ReducedMotion;
        float pop=Mathf.Clamp01(elapsed/Mathf.Max(.05f,popSeconds));
        float scale=pop<.5f?Mathf.Lerp(startScale,overshootScale,Mathf.Sin(pop*Mathf.PI)):Mathf.Lerp(overshootScale,1,Mathf.SmoothStep(0,1,(pop-.5f)*2));
        motion.anchoredPosition=origin+Vector2.up*(reduced?0:rise*Mathf.Clamp01((t-.3f)/.7f));
        motion.localScale=Vector3.one*(reduced?1:scale);
        if(t<1)return;
        showing=false;current="";
        if(pending.Count>0)Next();
    }
    public void Clear()
    {
        pending.Clear();showing=false;current="";
        if(group!=null)group.alpha=0;
        if(initialized&&motion!=null){motion.anchoredPosition=origin;motion.localScale=Vector3.one;}
    }
    void OnDisable()=>Clear();
}
