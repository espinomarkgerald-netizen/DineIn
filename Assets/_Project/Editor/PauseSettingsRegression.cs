#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Settings=DineIn.NewMenu.SettingsManager;
using Key=PauseSettingsPanel.Setting;

public static class PauseSettingsRegression
{
    const string PausePath="Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab";
    [MenuItem("Dine In/Validation/Validate Tabbed Pause Settings")]
    public static void Run()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode for this isolated check.");
        foreach(var path in new[]{PausePath,"Assets/_Project/Resources/UI/LobbyHUD.prefab"})
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var panel=root.GetComponentInChildren<PauseSettingsPanel>(true);
            Require(panel!=null && panel.rows.Length==18,"Missing settings rows.");
            Require(panel.rows.Select(r=>r.setting).Distinct().Count()==18,"Duplicate row binding.");
            var view=root.GetComponentInChildren<LobbyPauseMenuView>(true);
            Require(!view.Overlay.activeSelf && view.GetComponent<Canvas>().sortingOrder>32761,"Tutorial pause layer or initial state invalid.");
            Require(panel.tabs.Length==5 && panel.pages.Length==5 && panel.playersTabIndex==4,"Missing tabs.");
            foreach(var action in new[]{panel.applyDisplay,panel.revertDisplay,panel.resetControls,panel.resetAccessibility})
            {
                Require(action.transform.parent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>()!=null,"Action is still stretched across the settings list.");
                Require(action.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth<=360,"Action button is too wide.");
            }
            foreach(var row in panel.rows)
            {
                Require(row.root!=null,"Missing row root.");
                Require(row.slider!=null || row.dropdown!=null || row.toggle!=null,"Unbound row.");
                if(row.slider!=null)Require(row.slider.fillRect!=null&&row.slider.handleRect!=null,"Slider incomplete.");
                if(row.dropdown!=null)
                {
                    Require(row.dropdown.template!=null&&row.dropdown.itemText!=null,"Dropdown incomplete.");
                    var arrow=(RectTransform)row.dropdown.transform.Find("Arrow");
                    Require(arrow.anchoredPosition.x+arrow.sizeDelta.x*.5f<=-20,"Arrow overlaps dropdown border.");
                    Require(row.dropdown.captionText.rectTransform.offsetMax.x<=-68,"Caption overlaps dropdown arrow.");
                }
            }
        }
        var developerView=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/UI/DeveloperSettings.prefab").GetComponent<DeveloperSettingsView>();
        Require(developerView.options.Length==26 && developerView.options.All(o=>o.button!=null),"Developer actions missing.");
        string[] commands={"day","approval","money","addmoney","startday","endday","gameover","timescale","wrongorder","burntfood","cardpayment","fillstocks","zerostocks","setcoin","addcoin","resetrun","recover","upgrade","unlockpopup","save","status","help"};
        Require(commands.All(c=>developerView.options.Any(o=>o.command==c)),"Missing debug command mapping.");
        Require(developerView.options.Count(o=>o.command=="upgrade")==3 && developerView.options.Count(o=>o.command=="unlockpopup")==3,"Missing equipment controls.");
        CheckPlayersTab();
        CheckCallbacksAndPersistence();
        Debug.Log("[Pause settings] PASS: prefab references, tabs, callbacks, preferences, resets, non-compounding global scale, computer/pause visibility, tutorial time restoration, developer categories and landscape layout bounds. Device display/F10 input and live multiplayer remain manual checks.");
    }
    [MenuItem("Dine In/Validation/Validate Multiplayer Players Pause Tab")]
    public static void CheckPlayersTab()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode for this isolated check.");
        foreach(string path in new[]{PausePath,"Assets/_Project/Resources/UI/LobbyHUD.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var panel=root.GetComponentInChildren<PauseSettingsPanel>(true);
                var view=root.GetComponentInChildren<LobbyPauseMenuView>(true);
                Require(panel!=null && panel.rows.Length==18,"Existing settings rows were changed.");
                Require(!view.Overlay.activeSelf,"Pause overlay must remain closed.");
                Require(panel.tabs.Length==5 && panel.pages.Length==5 && panel.playersTabIndex==4,"Players tab is not bound.");
                var players=panel.pages[panel.playersTabIndex].GetComponent<PausePlayersPanel>();
                Require(players!=null && players.masterVolume!=null && players.masterValue!=null &&
                    players.voiceToggle!=null && players.voiceToggleLabel!=null && players.microphoneToggle!=null &&
                    players.microphoneLabel!=null && players.voiceStatus!=null && players.emptyRoster!=null,"Local voice controls are incomplete.");
                Require(players.remoteRows!=null && players.remoteRows.Length==3,"Expected three reusable remote player rows.");
                Require(players.remoteRows.Select(r=>r.root).Distinct().Count()==3,"Remote player rows are duplicated.");
                var scroll=players.GetComponent<UnityEngine.UI.ScrollRect>();
                Require(scroll!=null && scroll.content!=null && scroll.viewport!=null && scroll.vertical && !scroll.horizontal,
                    "Players page must scroll vertically.");
                Require(scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>()!=null &&
                    scroll.content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit==ContentSizeFitter.FitMode.PreferredSize,
                    "Players content is not clipped/sized for scrolling.");
                foreach(var row in players.remoteRows)
                {
                    Require(row.root!=null && row.root.transform.parent==scroll.content && row.name!=null && row.status!=null &&
                        row.speaking!=null && row.volume!=null && row.volumeValue!=null && row.mute!=null && row.muteLabel!=null,
                        "Remote player controls are incomplete.");
                    Require(!row.name.richText,"Player names must not accept rich text.");
                    Require(row.volume.minValue==0 && row.volume.maxValue==1 && row.volume.fillRect!=null &&
                        row.volume.handleRect!=null,"Remote volume slider is incomplete.");
                }
                panel.SetMultiplayerTabsVisible(true);panel.SelectTab(panel.playersTabIndex);
                Require(panel.tabs.All(t=>t.gameObject.activeSelf) && panel.pages.Count(p=>p.activeSelf)==1 &&
                    panel.pages[panel.playersTabIndex].activeSelf,"Multiplayer Players tab cannot open exclusively.");
                for(int i=0;i<panel.tabs.Length;i++)
                    Require(Mathf.Approximately(((RectTransform)panel.tabs[i].transform).anchorMax.x,(i+1)/5f),"Multiplayer tabs do not reflow.");
                panel.SetMultiplayerTabsVisible(false);
                Require(!panel.tabs[panel.playersTabIndex].gameObject.activeSelf && !panel.pages[panel.playersTabIndex].activeSelf &&
                    panel.pages[0].activeSelf,"Players tab remains open outside multiplayer.");
                panel.SelectTab(panel.playersTabIndex);
                Require(!panel.pages[panel.playersTabIndex].activeSelf,"Hidden Players tab can still be selected.");
                for(int i=0;i<4;i++)
                    Require(Mathf.Approximately(((RectTransform)panel.tabs[i].transform).anchorMax.x,(i+1)/4f),"Single-player tabs did not recover full width.");
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        CheckLandscapeLayout();
        Debug.Log("[Pause players] PASS: both prefab bindings, three unique roster rows, scroll clipping, multiplayer/SP tab visibility and landscape bounds.");
    }

    private static void Require(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
    [MenuItem("Dine In/Validation/Validate Settings Slider Polish")]
    public static void CheckSliderPolish()
    {
        foreach(string path in new[]{PausePath,"Assets/_Project/Resources/UI/LobbyHUD.prefab"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach(var row in prefab.GetComponentInChildren<PauseSettingsPanel>(true).rows)
            {
                if(row.slider==null)continue;
                var track=row.slider.transform.Find("Track").GetComponent<UnityEngine.UI.Image>();
                Require(track.sprite==null && Mathf.Approximately(track.rectTransform.sizeDelta.y,8),"Slider rail is not a plain line.");
                Require(row.slider.handleRect.sizeDelta==new Vector2(44,44),"Slider thumb is not compact and round.");
            }
        }
        var go=new GameObject("Toggle visual test",typeof(RectTransform),typeof(UnityEngine.UI.Toggle));
        string key="DineIn.ReducedMotion";bool had=PlayerPrefs.HasKey(key);int old=PlayerPrefs.GetInt(key);float time=Time.timeScale;
        try
        {
            PlayerPrefs.SetInt(key,0);Time.timeScale=0;
            var visual=go.AddComponent<SettingsToggleVisual>();
            visual.thumb=(RectTransform)new GameObject("Thumb",typeof(RectTransform)).transform;visual.thumb.SetParent(go.transform,false);
            go.GetComponent<UnityEngine.UI.Toggle>().SetIsOnWithoutNotify(true);visual.Refresh();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(SettingsToggleVisual).GetField("startX",flags).SetValue(visual,.22f);
            typeof(SettingsToggleVisual).GetField("targetX",flags).SetValue(visual,.78f);
            typeof(SettingsToggleVisual).GetField("elapsed",flags).SetValue(visual,0f);
            var advance=typeof(SettingsToggleVisual).GetMethod("Advance",flags);
            advance.Invoke(visual,new object[]{visual.animationDuration*.5f});
            Require(Mathf.Abs(visual.thumb.anchorMin.x-.5f)<.001f,"Toggle easing midpoint failed at timeScale zero.");
            visual.Refresh();Require(Mathf.Abs(visual.thumb.anchorMin.x-.5f)<.001f,"Settings refresh interrupted animation.");
            PlayerPrefs.SetInt(key,1);advance.Invoke(visual,new object[]{0f});
            Require(Mathf.Abs(visual.thumb.anchorMin.x-.78f)<.001f,"Reduced Motion did not snap to target.");
        }
        finally {Time.timeScale=time;if(had)PlayerPrefs.SetInt(key,old);else PlayerPrefs.DeleteKey(key);UnityEngine.Object.DestroyImmediate(go);}
        Debug.Log("[Settings polish] PASS: both prefab slider rails/thumbs; toggle easing at paused time, repeated refresh and Reduced Motion.");
    }
    private static void CheckCallbacksAndPersistence()
    {
        string[] floats={"Settings_MusicVolume","Settings_SfxVolume","Settings_MasterVolume","Settings_PanSpeed","Settings_ZoomSpeed","Settings_UIScale","Settings_DialogueSpeed","Settings_VoiceVolume"};
        string[] ints={"Settings_Quality","Settings_QualityUserSet","Settings_ShowFPS","Settings_MuteAll","Settings_InvertZoom","Settings_EdgePan","Settings_VSync","Settings_FPSLimit","Settings_DisplayWidth","Settings_DisplayHeight","Settings_WindowMode","DineIn.LargeText","DineIn.HighContrast","DineIn.ReducedMotion","Settings_VoiceEnabled","Settings_MicrophoneMuted"};
        var existing=new HashSet<string>(floats.Concat(ints).Where(PlayerPrefs.HasKey));
        var floatValues=floats.ToDictionary(k=>k,k=>PlayerPrefs.GetFloat(k));var intValues=ints.ToDictionary(k=>k,k=>PlayerPrefs.GetInt(k));
        int quality=QualitySettings.GetQualityLevel(),vsync=QualitySettings.vSyncCount,fps=Application.targetFrameRate;
        var qualityAsset=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
        var qualityObject=new SerializedObject(qualityAsset);var tiers=qualityObject.FindProperty("m_QualitySettings");
        var tierVsync=new int[tiers.arraySize];for(int i=0;i<tierVsync.Length;i++)tierVsync[i]=tiers.GetArrayElementAtIndex(i).FindPropertyRelative("vSyncCount").intValue;
        float volume=AudioListener.volume,time=Time.timeScale;
        var instance=typeof(Settings).GetField("<Instance>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);var old=instance.GetValue(null);
        var root=PrefabUtility.LoadPrefabContents(PausePath);
        var helper=new GameObject("Settings validation");helper.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(helper,root.scene);
        var mixerValues=new Dictionary<UnityEngine.Audio.AudioMixer,Dictionary<string,float>>();
        foreach(string path in new[]{"Assets/_Project/Audio/AudioMixer.mixer","Assets/_Project/MainMenu/NewDesign/Audio/Mixer Controller/Music.mixer","Assets/_Project/MainMenu/NewDesign/Audio/Mixer Controller/SFX.mixer"})
        {
            var mixer=AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(path);var values=new Dictionary<string,float>();
            string[] keys=path.EndsWith("/Music.mixer")?new[]{"MusicVolume"}:path.EndsWith("/SFX.mixer")?new[]{"SFXVolume"}:new[]{"MusicVol","SFXVol"};
            foreach(string key in keys)if(mixer.GetFloat(key,out float value))values[key]=value;
            mixerValues[mixer]=values;
        }
        try
        {
            PlayerPrefs.SetInt("Settings_DisplayWidth",0);PlayerPrefs.SetInt("Settings_DisplayHeight",0);
            var settings=helper.AddComponent<Settings>();
            EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/UI/PlayerSettings.prefab").GetComponent<Settings>(),settings);
            instance.SetValue(null,settings);settings.LoadLocal();
            CheckGlobalScale(settings,helper);
            var panel=root.GetComponentInChildren<PauseSettingsPanel>(true);panel.Initialize();
            panel.SetMultiplayerTabsVisible(true);
            for(int i=0;i<panel.tabs.Length;i++){panel.tabs[i].onClick.Invoke();Require(panel.pages.Where(p=>p.activeSelf).Count()==1&&panel.pages[i].activeSelf,"Tab did not switch exclusively.");}
            foreach(var row in panel.rows)
            {
                if(row.slider!=null)row.slider.value=row.slider.minValue+(row.slider.maxValue-row.slider.minValue)*.6f;
                if(row.toggle!=null){float before=panel.Read(row.setting);row.toggle.isOn=!row.toggle.isOn;Require(panel.Read(row.setting)!=before,"Toggle did not apply: "+row.setting);}
                if(row.dropdown!=null && row.setting!=Key.Resolution&&row.setting!=Key.WindowMode)row.dropdown.value=Mathf.Min(1,row.dropdown.options.Count-1);
            }
            float pan=settings.Current.panSpeed,master=settings.Current.masterVolume,music=settings.Current.musicVolume,sfx=settings.Current.sfxVolume,scale=settings.Current.uiScale;
            bool mute=settings.Current.muteAll,invert=settings.Current.invertZoom;
            settings.LoadLocal();
            Require(Mathf.Approximately(settings.Current.panSpeed,pan)&&Mathf.Approximately(settings.Current.masterVolume,master)&&Mathf.Approximately(settings.Current.musicVolume,music)&&Mathf.Approximately(settings.Current.sfxVolume,sfx)&&Mathf.Approximately(settings.Current.uiScale,scale)&&settings.Current.muteAll==mute&&settings.Current.invertZoom==invert,"Preferences did not round trip.");
            Require(Mathf.Approximately(AudioListener.volume,mute?0:master),"Master/mute not applied.");
            var gameMixer=AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/_Project/Audio/AudioMixer.mixer");
            // Native mixer writes return false outside Play Mode; verify routing here,
            // and leave audible gain checks to the short player acceptance test.
            Require(gameMixer.GetFloat("MusicVol",out _)&&gameMixer.GetFloat("SFXVol",out _),"Required mixer parameters are missing.");
            panel.resetControls.onClick.Invoke();Require(settings.Current.panSpeed==1&&settings.Current.zoomSpeed==1&&!settings.Current.invertZoom,"Controls reset failed.");
            panel.resetAccessibility.onClick.Invoke();Require(!LevelOneUIAccessibility.LargeText&&!LevelOneUIAccessibility.HighContrast&&!LevelOneUIAccessibility.ReducedMotion&&settings.Current.dialogueSpeed==1,"Accessibility reset failed.");
            var pause=helper.AddComponent<LobbyPauseMenu>();var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var view=root.GetComponent<LobbyPauseMenuView>();
            typeof(LobbyPauseMenu).GetField("overlay",flags).SetValue(pause,view.Overlay);
            typeof(LobbyPauseMenu).GetField("pauseButton",flags).SetValue(pause,view.PauseButton);
            foreach(float original in new[]{0f,.5f,1f})
            {
                typeof(LobbyPauseMenu).GetField("previousTimeScale",flags).SetValue(pause,original);
                typeof(LobbyPauseMenu).GetField("paused",flags).SetValue(pause,true);Time.timeScale=0;
                typeof(LobbyPauseMenu).GetMethod("Resume",flags).Invoke(pause,null);Require(Time.timeScale==original,"Pause changed a tutorial's time state.");
            }
            var dev=helper.AddComponent<DevSettingsConsole>();typeof(DevSettingsConsole).GetMethod("Awake",flags).Invoke(dev,null);
            dev.OpenPanel();var developer=(GameObject)typeof(DevSettingsConsole).GetField("panelRoot",flags).GetValue(dev);
            Require(developer.activeSelf&&developer.GetComponent<DeveloperSettingsView>()!=null,"Authored developer UI did not open.");
            dev.ClosePanel();Require(!developer.activeSelf,"Developer window did not close.");
            var devView=developer.GetComponent<DeveloperSettingsView>();
            for(int i=0;i<devView.tabs.Length;i++){devView.tabs[i].onClick.Invoke();Require(devView.pages.Count(p=>p.activeSelf)==1&&devView.pages[i].activeSelf,"Developer category did not switch.");}
            var computer=helper.AddComponent<ManagementComputerController>();
            var publish=typeof(ManagementComputerController).GetMethod("PublishOpenState",flags);
            publish.Invoke(computer,new object[]{true});
            typeof(LobbyPauseMenu).GetMethod("RefreshPauseButtonVisibility",flags).Invoke(pause,null);
            Require(!view.PauseButton.gameObject.activeSelf,"Pause button still visible over computer.");
            publish.Invoke(computer,new object[]{false});
            typeof(LobbyPauseMenu).GetMethod("RefreshPauseButtonVisibility",flags).Invoke(pause,null);
            Require(view.PauseButton.gameObject.activeSelf,"Pause button did not return after computer close.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(helper);PrefabUtility.UnloadPrefabContents(root);instance.SetValue(null,old);
            foreach(var key in floats){if(existing.Contains(key))PlayerPrefs.SetFloat(key,floatValues[key]);else PlayerPrefs.DeleteKey(key);}
            foreach(var key in ints){if(existing.Contains(key))PlayerPrefs.SetInt(key,intValues[key]);else PlayerPrefs.DeleteKey(key);}
            PlayerPrefs.Save();qualityObject.Update();tiers=qualityObject.FindProperty("m_QualitySettings");
            for(int i=0;i<tierVsync.Length;i++)tiers.GetArrayElementAtIndex(i).FindPropertyRelative("vSyncCount").intValue=tierVsync[i];
            qualityObject.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.SetQualityLevel(quality,true);QualitySettings.vSyncCount=vsync;Application.targetFrameRate=fps;AudioListener.volume=volume;Time.timeScale=time;
            foreach(var mixer in mixerValues)foreach(var value in mixer.Value)mixer.Key.SetFloat(value.Key,value.Value);
        }
    }

    private static void CheckGlobalScale(Settings settings,GameObject helper)
    {
        var root=new GameObject("Isolated UI scale check",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,helper.scene);
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scaler=root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        Vector2 authored=new Vector2(1920,1080);scaler.referenceResolution=authored;
        float original=settings.Current.uiScale;
        try
        {
            foreach(float scale in new[]{.85f,1f,1.15f,1f})
            {
                settings.Current.uiScale=scale;
                for(int repeat=0;repeat<3;repeat++)settings.ApplyUIScaleToCanvas(scaler);
                Require(Vector2.Distance(scaler.referenceResolution,authored/scale)<.01f,$"Global scale failed: {scaler.referenceResolution}, expected {authored/scale}, root={canvas.isRootCanvas}.");
            }
            scaler.referenceResolution=new Vector2(1600,900);settings.Current.uiScale=1.15f;settings.ApplyUIScaleToCanvas(scaler);
            Require(Vector2.Distance(scaler.referenceResolution,new Vector2(1600,900)/1.15f)<.01f,"Authored/mobile baseline update lost.");
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=2;
            settings.ApplyUIScaleToCanvas(scaler);settings.ApplyUIScaleToCanvas(scaler);
            Require(Mathf.Approximately(scaler.scaleFactor,2.3f),"Pixel canvas scale compounded.");
            var nested=new GameObject("Nested",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));nested.transform.SetParent(root.transform,false);
            var nestedScaler=nested.GetComponent<CanvasScaler>();Vector2 nestedSize=nestedScaler.referenceResolution;settings.ApplyUIScaleToCanvas(nestedScaler);
            Require(nestedScaler.referenceResolution==nestedSize,"Nested canvas was scaled twice.");
        }
        finally {settings.Current.uiScale=original;UnityEngine.Object.DestroyImmediate(root);}
    }

    private static void CheckLandscapeLayout()
    {
        var root=PrefabUtility.LoadPrefabContents(PausePath);
        try
        {
            foreach(var safe in root.GetComponentsInChildren<UIScreenSafeArea>(true))safe.enabled=false;
            var view=root.GetComponent<LobbyPauseMenuView>();var canvas=view.GetComponent<Canvas>();
            view.GetComponent<CanvasScaler>().enabled=false;canvas.renderMode=RenderMode.WorldSpace;
            var canvasRect=(RectTransform)canvas.transform;view.Overlay.SetActive(true);
            var panel=view.GetComponentInChildren<PauseSettingsPanel>(true);
            panel.SetMultiplayerTabsVisible(true);
            foreach(Vector2 pixels in new[]{new Vector2(1280,720),new Vector2(2340,1080),new Vector2(1920,1440)})
            foreach(float scale in new[]{.85f,1f,1.15f})
            {
                float units=Mathf.Min(pixels.x/1920,pixels.y/1080)*scale;
                // Reserve a landscape notch margin, in addition to the authored insets.
                canvasRect.sizeDelta=(pixels-new Vector2(100,0))/units;
                for(int tab=0;tab<panel.pages.Length;tab++)
                {
                    panel.SelectTab(tab);Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
                    foreach(var button in panel.tabs.Concat(new[]{view.ResumeButton,view.GameMenuButton}))
                    {
                        var r=(RectTransform)button.transform;
                        Require(r.rect.width*units>=80 && r.rect.height*units>=44,"Landscape control too small: "+button.name);
                    }
                    var page=(RectTransform)panel.pages[tab].transform;
                    Require(page.rect.width>600 && page.rect.height>180,"Settings viewport clipped at "+pixels+" / "+scale);
                    var corners=new Vector3[4];page.GetWorldCorners(corners);
                    var footer=(RectTransform)view.ResumeButton.transform.parent;var footerCorners=new Vector3[4];footer.GetWorldCorners(footerCorners);
                    Require(corners[0].y>=footerCorners[1].y-1,"Settings overlap footer.");
                }
            }
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif
