using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName="DineIn/Customer Drawer Art")]
public sealed class CustomerDrawerArt : ScriptableObject
{
    [Serializable] public struct Entry { public string name; public Sprite sprite; }
    public Entry[] sprites;
    public TMP_FontAsset headingFont, bodyFont;
    public Vector2 listSize=new(345,345),detailsSize=new(345,380),taskSize=new(276,138);
    [Min(16)] public float headingSize=23,bodySize=19,smallSize=16;
    [Min(6)] public float contentPadding=12;
    [Min(4)] public float taskButtonGap=12;
    [Min(8)] public float patienceHeight=13;
    [Min(.05f)] public float contentFade=.12f,patienceSmooth=.15f,panelSeconds=.20f;
    [Range(0,1)] public float panelSoundGain=.18f;
    public AudioClip panelSound;
    public Color ink=new(.06f,.14f,.27f,1);
    private static CustomerDrawerArt cached;
    public static CustomerDrawerArt Shared => cached!=null?cached:cached=Resources.Load<CustomerDrawerArt>("UI/CustomerDrawerArt");
    public Sprite Find(string name){if(sprites!=null)foreach(var e in sprites)if(e.name==name)return e.sprite;return null;}
    public void PlayPanelSound(){if(GameplaySfx.PresentationSuspended)return;if(panelSound!=null)GameplaySfx.PlayUiClip(panelSound,panelSoundGain);else GameplaySfx.Play(GameplaySfx.Cue.Panel);}
}

// Shared construction for the two compact prefab-backed HUD branches, not a UI manager.
internal static class CustomerDrawerUI
{
    internal static RectTransform Node(string name,Transform parent)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        r.gameObject.layer=parent.gameObject.layer;r.SetParent(parent,false);return r;
    }
    internal static void Place(RectTransform r,float x,float y,float w,float h)
    {r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    internal static void Stretch(RectTransform r)
    {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    internal static Image Image(string name,Transform parent,string sprite,bool sliced=true)
    {
        var image=Node(name,parent).gameObject.AddComponent<Image>();image.sprite=CustomerDrawerArt.Shared.Find(sprite);
        image.type=sliced?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
        image.pixelsPerUnitMultiplier=2;image.preserveAspect=!sliced;image.raycastTarget=false;return image;
    }
    internal static TMP_Text Text(string name,Transform parent,string value,bool heading=false)
    {
        var art=CustomerDrawerArt.Shared;var t=Node(name,parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.text=value;t.font=heading?art.headingFont:art.bodyFont;t.fontSize=heading?art.headingSize:art.bodySize;
        t.color=art.ink;t.raycastTarget=false;t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.Normal;
        t.overflowMode=TextOverflowModes.Overflow;t.margin=Vector4.zero;return t;
    }
    internal static Button Button(string name,Transform parent,string value,Action clicked,string family="NavButton")
    {
        var img=Image(name,parent,family+"Ready");img.raycastTarget=true;var b=img.gameObject.AddComponent<Button>();b.targetGraphic=img;
        var art=CustomerDrawerArt.Shared;b.transition=Selectable.Transition.SpriteSwap;
        b.spriteState=new SpriteState{pressedSprite=art.Find(family+"Pressed"),highlightedSprite=art.Find(family+"Focused"),selectedSprite=art.Find(family+"Focused"),disabledSprite=art.Find(family+"Disabled")};
        b.onClick.AddListener(()=>clicked());GameplayButtonSound.Ensure(b);
        b.gameObject.AddComponent<ButtonAnimator>();
        if(!string.IsNullOrEmpty(value)){var t=Text("Label",b.transform,value);Stretch(t.rectTransform);t.alignment=TextAlignmentOptions.Center;}
        return b;
    }
    internal static ScrollRect Scroll(string name,Transform parent,out RectTransform content)
    {
        var r=Node(name,parent);var s=r.gameObject.AddComponent<ScrollRect>();
        var viewport=Node("Viewport",r);Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
        var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        content=Node("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
        s.viewport=viewport;s.content=content;s.horizontal=false;s.vertical=true;s.movementType=ScrollRect.MovementType.Clamped;s.scrollSensitivity=28;
        var track=Image("Scrollbar",r,"ScrollTrack");track.raycastTarget=true;
        track.rectTransform.anchorMin=new Vector2(1,0);track.rectTransform.anchorMax=Vector2.one;track.rectTransform.pivot=new Vector2(1,.5f);track.rectTransform.offsetMin=new Vector2(-10,0);track.rectTransform.offsetMax=Vector2.zero;
        var handle=Image("Handle",track.transform,"ScrollThumb");Stretch(handle.rectTransform);
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;
        s.verticalScrollbar=bar;s.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        return s;
    }
    internal static void Header(RectTransform parent,string title)
    {
        var tab=Image("Header",parent,"HeaderTab");Place(tab.rectTransform,12,-13,Mathf.Min(parent.rect.width-24,260),39);
        var label=Text("Title",tab.transform,title,true);Stretch(label.rectTransform);label.color=new Color(1,.98f,.9f);label.alignment=TextAlignmentOptions.Center;
    }
}
