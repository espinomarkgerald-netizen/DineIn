using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Settings = DineIn.NewMenu.SettingsManager;

/// <summary>Binds saved controls only; the complete tab/page hierarchy is prefab-authored.</summary>
public sealed class PauseSettingsPanel : MonoBehaviour
{
    public enum Setting { Master, Music, Sfx, Mute, Pan, Zoom, InvertZoom, Quality, Resolution, WindowMode, VSync, Fps, UIScale, LargeText, HighContrast, ReducedMotion, DialogueSpeed, EdgePan }
    [Serializable] public sealed class Row
    {
        public Setting setting;
        public RectTransform root;
        public Slider slider;
        public Toggle toggle;
        public TMP_Dropdown dropdown;
        public TMP_Text value;
    }
    public Button[] tabs;
    public GameObject[] pages;
    public Row[] rows;
    public Button resetControls, resetAccessibility, applyDisplay, revertDisplay;
    public TMP_Text displayStatus;
    public Sprite selectedTabSprite, idleTabSprite;
    public Color selectedTab = new Color(.16f,.60f,.77f), idleTab = new Color(.16f,.29f,.41f);
    public Color contentColor = new Color(.91f,.96f,.99f), inkColor = new Color(.1f,.23f,.33f);
    [Min(80)] public float rowHeight = 124;
    [Min(10)] public float displayConfirmationSeconds = 15;
    private Settings settings;
    private bool bound, refreshing, previewing;
    private Vector2Int[] resolutions;
    private Vector2Int previousResolution;
    private FullScreenMode previousMode;
    private float previewDeadline;
    private readonly Dictionary<TMP_Text,float> textSizes = new Dictionary<TMP_Text,float>();
    private int activeTab;
    private static readonly int[] FrameCaps = {30,60,120,-1};

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        Initialize(); Refresh();
    }
    public void Initialize()
    {
        if (bound) return;
        settings = Settings.EnsureInstance();
        if (settings == null) { Debug.LogError("PlayerSettings prefab is missing.",this); return; }
        bound = true;
        for(int i=0;i<tabs.Length;i++) { int index=i; tabs[i].onClick.AddListener(()=>SelectTab(index)); }
        foreach(var row in rows)
        {
            Row captured = row;
            if(row.slider != null) row.slider.onValueChanged.AddListener(v=>Change(captured.setting,v));
            if(row.toggle != null) row.toggle.onValueChanged.AddListener(v=>Change(captured.setting,v?1:0));
            if(row.dropdown != null) row.dropdown.onValueChanged.AddListener(v=>Change(captured.setting,v));
            foreach(var text in row.root.GetComponentsInChildren<TMP_Text>(true)) textSizes[text]=text.fontSizeMax;
        }
        resetControls.onClick.AddListener(()=>settings.ResetControls());
        resetAccessibility.onClick.AddListener(ResetAccessibility);
        applyDisplay.onClick.AddListener(ApplyDisplay);
        revertDisplay.onClick.AddListener(RevertDisplay);
        settings.OnSettingsLoaded += SettingsChanged;
        LevelOneUIAccessibility.SettingsChanged += Refresh;
        SetOptions(Setting.Quality, QualitySettings.names);
        SetOptions(Setting.Fps, new[]{"30 FPS","60 FPS","120 FPS",Settings.SupportsDesktopDisplay?"Uncapped":"Device default"});
        SetOptions(Setting.WindowMode,new[]{"Windowed","Borderless Fullscreen","Exclusive Fullscreen"});
        resolutions=Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height))
            .Append(new Vector2Int(Screen.width,Screen.height)).Where(r=>r.x>=640 && r.y>=360).Distinct().OrderBy(r=>r.x).ThenBy(r=>r.y).ToArray();
        if(resolutions.Length==0) resolutions=new[]{new Vector2Int(1280,720)};
        SetOptions(Setting.Resolution,resolutions.Select(r=>$"{r.x} × {r.y}").ToArray());
        foreach(var row in rows)
            if(row.setting==Setting.Resolution || row.setting==Setting.WindowMode || row.setting==Setting.VSync || row.setting==Setting.EdgePan)
                row.root.gameObject.SetActive(Settings.SupportsDesktopDisplay);
        applyDisplay.gameObject.SetActive(Settings.SupportsDesktopDisplay);
        revertDisplay.gameObject.SetActive(false);
        SyncDisplay(); SelectTab(0); Refresh();
    }
    private void SettingsChanged(DineIn.NewMenu.UserSettings _) => Refresh();
    private void OnDestroy()
    {
        if(settings!=null) settings.OnSettingsLoaded -= SettingsChanged;
        LevelOneUIAccessibility.SettingsChanged -= Refresh;
    }
    private void OnDisable() { if(previewing) RevertDisplay(); }
    public void SelectTab(int index)
    {
        activeTab=Mathf.Clamp(index,0,pages.Length-1);
        for(int i=0;i<pages.Length;i++)
        {
            pages[i].SetActive(i==activeTab);
            var image=tabs[i].GetComponent<Image>();
            image.sprite=i==activeTab?selectedTabSprite:idleTabSprite;
            image.color=Color.white;
        }
    }
    private Row Find(Setting key) => Array.Find(rows,r=>r.setting==key);
    private void SetOptions(Setting key,string[] names)
    {
        var control=Find(key).dropdown; control.ClearOptions(); control.AddOptions(new List<string>(names));
    }
    public float Read(Setting key)
    {
        var s=settings.Current;
        switch(key)
        {
            case Setting.Master:return s.masterVolume;
            case Setting.Music:return s.musicVolume;
            case Setting.Sfx:return s.sfxVolume;
            case Setting.Mute:return s.muteAll?1:0;
            case Setting.Pan:return s.panSpeed;
            case Setting.Zoom:return s.zoomSpeed;
            case Setting.InvertZoom:return s.invertZoom?1:0;
            case Setting.EdgePan:return s.edgePan?1:0;
            case Setting.Quality:return s.graphicsQualityIndex;
            case Setting.VSync:return s.vSync;
            case Setting.Fps:return Mathf.Max(0,Array.IndexOf(FrameCaps,s.fpsLimit));
            case Setting.UIScale:return s.uiScale;
            case Setting.LargeText:return LevelOneUIAccessibility.LargeText?1:0;
            case Setting.HighContrast:return LevelOneUIAccessibility.HighContrast?1:0;
            case Setting.ReducedMotion:return LevelOneUIAccessibility.ReducedMotion?1:0;
            case Setting.DialogueSpeed:return s.dialogueSpeed;
            default:return 0;
        }
    }
    private void Change(Setting key,float value)
    {
        if(refreshing || settings==null) return;
        var s=settings.Current;
        switch(key)
        {
            case Setting.Master:settings.SetMasterVolume(value);break;
            case Setting.Music:settings.SetMusicVolume(value);break;
            case Setting.Sfx:settings.SetSfxVolume(value);break;
            case Setting.Mute:settings.SetMuteAll(value>.5f);break;
            case Setting.Pan:settings.SetControls(value,s.zoomSpeed,s.invertZoom);break;
            case Setting.Zoom:settings.SetControls(s.panSpeed,value,s.invertZoom);break;
            case Setting.InvertZoom:settings.SetControls(s.panSpeed,s.zoomSpeed,value>.5f);break;
            case Setting.EdgePan:settings.SetEdgePan(value>.5f);break;
            case Setting.Quality:settings.SetGraphicsQuality((int)value);break;
            case Setting.VSync:settings.SetFrameTiming((int)value,s.fpsLimit);break;
            case Setting.Fps:settings.SetFrameTiming(s.vSync,FrameCaps[Mathf.Clamp((int)value,0,3)]);break;
            case Setting.UIScale:settings.SetPresentation(value,s.dialogueSpeed);break;
            case Setting.LargeText:LevelOneUIAccessibility.SetLargeTextEnabled(value>.5f);break;
            case Setting.HighContrast:LevelOneUIAccessibility.SetHighContrastEnabled(value>.5f);break;
            case Setting.ReducedMotion:LevelOneUIAccessibility.SetReducedMotionEnabled(value>.5f);break;
            case Setting.DialogueSpeed:settings.SetPresentation(s.uiScale,value);break;
        }
    }
    private void Refresh()
    {
        if(!bound || settings==null) return;
        refreshing=true;
        foreach(var row in rows)
        {
            if(row.setting!=Setting.Resolution && row.setting!=Setting.WindowMode)
            {
                float value=Read(row.setting);
                if(row.slider!=null) row.slider.SetValueWithoutNotify(value);
                if(row.dropdown!=null) row.dropdown.SetValueWithoutNotify((int)value);
                if(row.value!=null) row.value.text=
                    row.setting==Setting.Pan||row.setting==Setting.Zoom||row.setting==Setting.DialogueSpeed ? $"{value:0.0}×" : $"{value*100:0}%";
                if(row.toggle!=null) { row.toggle.SetIsOnWithoutNotify(value>.5f); row.toggle.GetComponent<SettingsToggleVisual>()?.Refresh(); }
            }
            var layout=row.root.GetComponent<LayoutElement>();
            if(layout!=null) layout.preferredHeight=rowHeight;
            foreach(var text in row.root.GetComponentsInChildren<TMP_Text>(true))
                if(textSizes.TryGetValue(text,out float size))
                {
                    text.fontSize=text.fontSizeMax=size*(LevelOneUIAccessibility.LargeText?1.15f:1);
                    if(text.transform.parent==row.root) text.color=LevelOneUIAccessibility.HighContrast?Color.black:inkColor;
                }
            var background=row.root.GetComponent<Image>();
            if(background!=null) background.color=LevelOneUIAccessibility.HighContrast?Color.white:contentColor;
        }
        if(!previewing) displayStatus.text=Settings.SupportsDesktopDisplay?
            "VSync overrides FPS limit.":"";
        refreshing=false;
    }
    private void ResetAccessibility()
    {
        LevelOneUIAccessibility.SetLargeTextEnabled(false);
        LevelOneUIAccessibility.SetHighContrastEnabled(false);
        LevelOneUIAccessibility.SetReducedMotionEnabled(false);
        settings.SetPresentation(1,1);
    }
    private void SyncDisplay()
    {
        int index=Array.IndexOf(resolutions,new Vector2Int(Screen.width,Screen.height));
        Find(Setting.Resolution).dropdown.SetValueWithoutNotify(Mathf.Max(0,index));
        Find(Setting.WindowMode).dropdown.SetValueWithoutNotify(Screen.fullScreenMode==FullScreenMode.Windowed?0:Screen.fullScreenMode==FullScreenMode.ExclusiveFullScreen?2:1);
    }
    private void ApplyDisplay()
    {
        if(!Settings.SupportsDesktopDisplay) return;
        if(previewing)
        {
            settings.CommitDisplay(Screen.width,Screen.height,Screen.fullScreenMode);
            previewing=false; revertDisplay.gameObject.SetActive(false);
            applyDisplay.GetComponentInChildren<TMP_Text>().text="APPLY DISPLAY"; Refresh(); return;
        }
        previousResolution=new Vector2Int(Screen.width,Screen.height);previousMode=Screen.fullScreenMode;
        Vector2Int target=resolutions[Find(Setting.Resolution).dropdown.value];
        int mode=Find(Setting.WindowMode).dropdown.value;
        Screen.SetResolution(target.x,target.y,mode==0?FullScreenMode.Windowed:mode==2?FullScreenMode.ExclusiveFullScreen:FullScreenMode.FullScreenWindow);
        previewing=true;previewDeadline=Time.unscaledTime+displayConfirmationSeconds;
        revertDisplay.gameObject.SetActive(true);
        applyDisplay.GetComponentInChildren<TMP_Text>().text="KEEP DISPLAY";
    }
    private void Update()
    {
        if(!previewing) return;
        float remaining=previewDeadline-Time.unscaledTime;
        if(remaining<=0) {RevertDisplay();return;}
        displayStatus.text=$"Keep these display settings? Reverting in {Mathf.CeilToInt(remaining)}s.";
    }
    private void RevertDisplay()
    {
        if(previewing) Screen.SetResolution(previousResolution.x,previousResolution.y,previousMode);
        previewing=false;revertDisplay.gameObject.SetActive(false);
        applyDisplay.GetComponentInChildren<TMP_Text>().text="APPLY DISPLAY";
        if(bound) {SyncDisplay();Refresh();}
    }
}
