using UnityEngine;
using UnityEngine.UI;

/// <summary>Sprite switch presentation; the Toggle remains the actual input control.</summary>
[ExecuteAlways, RequireComponent(typeof(Toggle))]
public sealed class SettingsToggleVisual : MonoBehaviour
{
    public Image track;
    public RectTransform thumb;
    public Sprite enabledSprite, disabledSprite;
    [Range(0, .5f)] public float inset = .22f;
    [Min(0)] public float animationDuration = .18f;
    private Toggle control;
    private bool initialized, displayedState, animating;
    private float startX, targetX, elapsed;
    private void OnEnable() { control=GetComponent<Toggle>(); control.onValueChanged.AddListener(Changed); initialized=false; Refresh(); }
    private void OnDisable() { if(control!=null)control.onValueChanged.RemoveListener(Changed); animating=false; initialized=false; }
    private void OnValidate() { initialized=false; Refresh(); }
    private void Changed(bool _) { Refresh(); }
    public void Refresh()
    {
        if(control==null)control=GetComponent<Toggle>();
        if(track!=null) { track.sprite=control.isOn?enabledSprite:disabledSprite; track.color=Color.white; }
        if(thumb==null)return;
        bool changed=!initialized || displayedState!=control.isOn;
        if(!changed)return; // Settings callbacks can refresh the same state more than once.
        targetX=control.isOn?1-inset:inset;
        animating=initialized && Application.isPlaying && isActiveAndEnabled && !LevelOneUIAccessibility.ReducedMotion && animationDuration>0;
        startX=thumb.anchorMin.x;elapsed=0;
        displayedState=control.isOn;initialized=true;
        if(!animating)SetThumb(targetX);
    }
    private void Update()
    {
        if(animating)Advance(Time.unscaledDeltaTime);
    }
    private void Advance(float delta)
    {
        if(thumb==null){animating=false;return;}
        elapsed+=Mathf.Max(0,delta);
        float t=LevelOneUIAccessibility.ReducedMotion || animationDuration<=0?1:Mathf.Clamp01(elapsed/animationDuration);
        SetThumb(Mathf.Lerp(startX,targetX,t*t*(3-2*t)));
        if(t>=1)animating=false;
    }
    private void SetThumb(float x)
    {
        thumb.anchorMin=thumb.anchorMax=new Vector2(x,.5f);
        thumb.anchoredPosition=Vector2.zero;
    }
}
