using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation only. Session recovery and host authorization remain with the session owner.
public sealed class MultiplayerConnectionNotice : MonoBehaviour
{
    public Sprite panelSprite, headerSprite, buttonSprite, stripSprite, disconnectedSprite, removedSprite;
    public TMP_FontAsset headingFont, bodyFont;
    public Vector2 panelSize = new Vector2(620, 540);
    public float enterSeconds = .22f, exitSeconds = .16f, statusFadeSeconds = .12f;
    public float headingSize = 38, bodySize = 25, buttonSize = 28;
    [TextArea] public string disconnectedCopy = "Trying to reconnect to your restaurant…";
    [TextArea] public string removedCopy = "You were removed from this restaurant by the host.";
    private RectTransform safe, frame;
    private CanvasGroup group, statusGroup;
    private TMP_Text title, detail, status, returnLabel;
    private Image icon;
    private Button retry, back;
    private MultiplayerSessionManager session;
    private float visibility, statusAlpha = 1;
    private bool targetVisible, hadRecovery, removed, closingConfirmed, returnRequested;
    private string nextStatus = "";
    public bool IsVisible => targetVisible || visibility > 0;

    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32010;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
        var shade = Picture("Input shield", transform, null); Stretch(shade.rectTransform);
        shade.color = new Color(.015f,.035f,.07f,.65f); shade.raycastTarget = true;
        safe = Rect("Safe area", transform); Stretch(safe);
        frame = Picture("Panel", safe, panelSprite).rectTransform;
        var header = Picture("Title tab", frame, headerSprite); Place(header.rectTransform, .5f, 1, 0,-27,510,100);
        title = Text("Title",header.transform,headingFont,headingSize,Color.white); Stretch(title.rectTransform,14);
        icon = Picture("State icon",frame,disconnectedSprite); icon.preserveAspect = true;
        Place(icon.rectTransform,.5f,1,0,-155,210,130);
        detail = Text("Explanation",frame,bodyFont,bodySize,new Color(.04f,.08f,.17f));
        Place(detail.rectTransform,.5f,1,0,-282,530,94);
        var strip = Picture("Status strip",frame,stripSprite); Place(strip.rectTransform,.5f,0,0,145,510,64);
        status = Text("Status",strip.transform,bodyFont,23,new Color(.04f,.08f,.17f)); Stretch(status.rectTransform,12);
        statusGroup = status.gameObject.AddComponent<CanvasGroup>();
        back = Button("Return to Lobby",frame,()=>{returnRequested=true;targetVisible=false;},out returnLabel);
        retry = Button("Retry",frame,()=>session?.RetryRecovery(),out _);
        Layout(false);
    }

    public void Refresh(MultiplayerSessionManager owner)
    {
        session = owner;
        if (returnRequested)
        {
            Animate();
            if (!IsVisible) owner.LeaveToMenu();
            return;
        }
        bool recovering = owner.HasConnectionInterruption && owner.IsRecovering && !owner.Ended;
        bool kicked = owner.RemovalPending;
        bool hostLost = owner.Ended && owner.Run?.endReason == "HostDisconnected";
        if (recovering || kicked || hostLost)
        {
            bool first = !targetVisible;
            bool changedToRemoval = kicked && !removed;
            targetVisible = true; closingConfirmed = false; removed = kicked;
            if (recovering) hadRecovery = true;
            if (first || changedToRemoval) GameplaySfx.Play(GameplaySfx.Cue.Info);
            title.text = kicked ? "SESSION ENDED" : "CONNECTION LOST";
            detail.text = kicked ? removedCopy : hostLost ? "The host connection was lost. This multiplayer run has ended." : owner.RecoveryNeedsRetry ? "We couldn’t reconnect. Try again or return to the lobby." : disconnectedCopy;
            icon.sprite = kicked ? removedSprite : disconnectedSprite;
            SetStatus(kicked ? "Removed by host" : hostLost ? "Session ended" : owner.RecoveryNeedsRetry ? "Connection interrupted" : owner.IsConnected ? "Synchronizing restaurant…" : owner.Status);
            Layout(!kicked && !hostLost && owner.RecoveryNeedsRetry);
        }
        else if (hadRecovery && owner.CanAct && !closingConfirmed)
        {
            closingConfirmed = true; hadRecovery = false; targetVisible = false;
            GameplaySfx.Play(GameplaySfx.Cue.Success);
        }
        else if (owner.Ended && !kicked) targetVisible = false;
        Animate();
    }

    private void Animate()
    {
        Rect area = Screen.safeArea;
        safe.anchorMin = new Vector2(area.xMin / Mathf.Max(1,Screen.width),area.yMin / Mathf.Max(1,Screen.height));
        safe.anchorMax = new Vector2(area.xMax / Mathf.Max(1,Screen.width),area.yMax / Mathf.Max(1,Screen.height));
        float scale = Mathf.Min(1, Mathf.Min((safe.rect.width-32)/panelSize.x,(safe.rect.height-32)/panelSize.y));
        frame.sizeDelta = panelSize;
        float textScale = LevelOneUIAccessibility.LargeText ? 1.2f : 1;
        title.fontSize = headingSize * textScale; detail.fontSize = bodySize * textScale;
        bool reduced = LevelOneUIAccessibility.ReducedMotion;
        float duration = reduced ? .08f : targetVisible ? enterSeconds : exitSeconds;
        visibility = Mathf.MoveTowards(visibility,targetVisible?1:0,LevelOneUIAccessibility.UnscaledAnimationDeltaTime/Mathf.Max(.01f,duration));
        float eased = Mathf.SmoothStep(0,1,visibility);
        group.alpha = eased; group.blocksRaycasts = IsVisible; group.interactable = targetVisible;
        frame.localScale = Vector3.one * Mathf.Max(.01f,scale) * (reduced?1:Mathf.Lerp(.96f,1,eased));
        frame.anchoredPosition = new Vector2(0,reduced?0:Mathf.Lerp(-16,0,eased));
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject,IsVisible);
        if (status.text != nextStatus)
        {
            statusAlpha = Mathf.MoveTowards(statusAlpha,0,Time.unscaledDeltaTime/Mathf.Max(.01f,statusFadeSeconds*.5f));
            if(statusAlpha<=0) status.text=nextStatus;
        }
        else statusAlpha=Mathf.MoveTowards(statusAlpha,1,Time.unscaledDeltaTime/Mathf.Max(.01f,statusFadeSeconds*.5f));
        statusGroup.alpha=statusAlpha;
    }
    private void SetStatus(string value) { nextStatus=value??""; }
    private void Layout(bool canRetry)
    {
        retry.gameObject.SetActive(canRetry);
        Place(back.GetComponent<RectTransform>(),.5f,0,canRetry?133:0,65,canRetry?255:440,70);
        Place(retry.GetComponent<RectTransform>(),.5f,0,-133,65,255,70);
        returnLabel.text="RETURN TO LOBBY";
    }
    public void HideImmediately()
    {
        targetVisible=false;visibility=0;hadRecovery=false;returnRequested=false;
        if(group!=null){group.alpha=0;group.blocksRaycasts=false;group.interactable=false;}
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject,false);
    }
    private void OnDestroy() { GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject,false); }
    private static RectTransform Rect(string name,Transform parent)
    { var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r; }
    private static Image Picture(string name,Transform parent,Sprite sprite)
    {var r=Rect(name,parent);var i=r.gameObject.AddComponent<Image>();i.sprite=sprite;i.type=sprite!=null&&sprite.border!=Vector4.zero?Image.Type.Sliced:Image.Type.Simple;i.raycastTarget=false;return i;}
    private static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,float size,Color color)
    {var r=Rect(name,parent);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font!=null?font:TMP_Settings.defaultFontAsset;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.richText=false;return t;}
    private Button Button(string name,Transform parent,UnityEngine.Events.UnityAction action,out TMP_Text label)
    {var image=Picture(name,parent,buttonSprite);image.raycastTarget=true;var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.navigation=new Navigation{mode=Navigation.Mode.None};b.onClick.AddListener(action);image.gameObject.AddComponent<ButtonAnimator>();GameplayButtonSound.Ensure(b);label=Text("Label",image.transform,headingFont,buttonSize,Color.white);label.text=name.ToUpperInvariant();Stretch(label.rectTransform,8);return b;}
    private static void Stretch(RectTransform r,float pad=0){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.one*pad;r.offsetMax=-Vector2.one*pad;}
    private static void Place(RectTransform r,float ax,float ay,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(ax,ay);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
}
