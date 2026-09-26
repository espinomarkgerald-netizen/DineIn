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
    [Header("All kitchen station popups")]
    [Tooltip("Normalized position within the gameplay UI's safe area.")]
    public Vector2 popupScreenPosition=new Vector2(.5f,.5f);
    [Min(.01f)] public float popupPopDuration=.18f;
    [Min(0)] public float popupHoldDuration=.85f;
    [Min(.01f)] public float popupFadeDuration=.6f;
    [Min(0)] public float popupUpwardTravel=32;
    readonly Queue<(string text,Sprite icon,int kind)> pending=new();
    string current="";
    Vector2 origin;
    float elapsed;
    int currentPriority;
    bool showing, initialized;
    Vector2 savedAnchorMin,savedAnchorMax,savedPivot,savedSize;

    void Initialize()
    {
        if(initialized)return;
        initialized=true; origin=motion.anchoredPosition;
        savedAnchorMin=motion.anchorMin;savedAnchorMax=motion.anchorMax;
        savedPivot=motion.pivot;savedSize=motion.sizeDelta;
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
        // Every station uses the same centered feedback presentation.
        // Restore first so dimensions follow the current screen, not the previous popup.
        RestoreLayout();
        Vector2 size=motion.rect.size;
        motion.anchorMin=motion.anchorMax=popupScreenPosition;
        motion.pivot=new Vector2(.5f,.5f);motion.sizeDelta=size;
        motion.anchoredPosition=Vector2.zero;
        motion.SetAsLastSibling();
        currentPriority=entry.kind==1?3:entry.kind==2?2:entry.kind==3?1:0;
        label.color=entry.kind==1?error:entry.kind==2?warning:entry.kind==3?success:normal;
        icon.sprite=entry.icon;icon.enabled=entry.icon!=null;
        if(icon.enabled)
        {
            float width=Mathf.Min(label.rectTransform.rect.width,label.GetPreferredValues(current).x);
            icon.rectTransform.anchoredPosition=new Vector2(Mathf.Max(0,(motion.rect.width-width)*.5f-icon.rectTransform.rect.width-iconGap),0);
        }
        elapsed=0;showing=true;motion.anchoredPosition=Vector2.zero;
        group.alpha=1;
        motion.localScale=Vector3.one*(LevelOneUIAccessibility.ReducedMotion?1:.92f);
    }
    void Update()=>Advance(Time.unscaledDeltaTime);
    void Advance(float deltaTime)
    {
        if(!showing)return;
        elapsed+=deltaTime;
        float pop=Mathf.Clamp01(elapsed/Mathf.Max(.01f,popupPopDuration));
        float fade=Mathf.Clamp01((elapsed-popupPopDuration-popupHoldDuration)/Mathf.Max(.01f,popupFadeDuration));
        bool reduced=LevelOneUIAccessibility.ReducedMotion;
        motion.anchorMin=motion.anchorMax=popupScreenPosition;
        motion.anchoredPosition=Vector2.up*(reduced?0:popupUpwardTravel*Mathf.SmoothStep(0,1,fade));
        motion.localScale=Vector3.one*(reduced?1:Mathf.Lerp(.92f,1,Mathf.SmoothStep(0,1,pop)));
        group.alpha=1-fade;
        if(fade<1)return;
        showing=false;current="";RestoreLayout();motion.localScale=Vector3.one;
        if(pending.Count>0)Next();
    }
    public void Clear()
    {
        pending.Clear();showing=false;current="";
        if(group!=null)group.alpha=0;
        if(initialized&&motion!=null){RestoreLayout();motion.localScale=Vector3.one;}
    }
    void RestoreLayout()
    {
        motion.anchorMin=savedAnchorMin;motion.anchorMax=savedAnchorMax;
        motion.pivot=savedPivot;motion.sizeDelta=savedSize;motion.anchoredPosition=origin;
    }
    void OnDisable()=>Clear();
}
