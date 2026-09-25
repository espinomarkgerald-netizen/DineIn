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
    [Range(1,5)] public int queueLimit=3;
    public Color normal=Color.white, error=new Color(1,.25f,.25f),
        warning=new Color(1,.84f,.2f), success=new Color(.35f,1,.5f);
    readonly Queue<(string text,Sprite icon,int kind)> pending=new();
    string current="";
    Vector2 origin;
    float elapsed;
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
        if(kind==1)pending.Clear();
        while(pending.Count>=queueLimit)pending.Dequeue();
        pending.Enqueue((text,sprite,kind));
        if(!showing)Next();
    }
    void Next()
    {
        var entry=pending.Dequeue();current=entry.text;label.text=current;
        label.color=entry.kind==1?error:entry.kind==2?warning:entry.kind==3?success:normal;
        icon.sprite=entry.icon;icon.enabled=entry.icon!=null;
        elapsed=0;showing=true;motion.anchoredPosition=origin;
    }
    void Update()
    {
        if(!showing)return;
        elapsed+=Time.unscaledDeltaTime;
        float t=Mathf.Clamp01(elapsed/Mathf.Max(.2f,duration));
        group.alpha=Mathf.Min(Mathf.Clamp01(t/.1f),Mathf.Clamp01((1-t)/.25f));
        bool reduced=LevelOneUIAccessibility.ReducedMotion;
        motion.anchoredPosition=origin+Vector2.up*(reduced?0:rise*t);
        motion.localScale=Vector3.one*(reduced?1:Mathf.Lerp(.94f,1,Mathf.Clamp01(t/.12f)));
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

