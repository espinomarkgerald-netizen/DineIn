#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Settings = DineIn.NewMenu.SettingsManager;
using Key = PauseSettingsPanel.Setting;

/// <summary>Explicit prefab migration. No runtime generation, no scene saves, no external art.</summary>
public static partial class PauseSettingsAuthoring
{
    private static readonly Color Navy=new Color(.12f,.23f,.33f), Paper=new Color(.91f,.96f,.99f), Blue=new Color(.15f,.59f,.77f), Red=new Color(.73f,.21f,.27f), Green=new Color(.20f,.55f,.35f);
    private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Assets/Fonts/Atkinson_Hyperlegible/AtkinsonHyperlegible-Regular SDF.asset");
    private static TMP_FontAsset DisplayFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Assets/Fonts/Anton/Anton-Regular SDF.asset");
    private static Sprite Art(string color,string name="button_rectangle_depth_flat") => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/"+color+"/Double/"+name+".png");
    private static void Remove(Transform parent,string name) { var child=parent!=null?parent.Find(name):null; if(child!=null)UnityEngine.Object.DestroyImmediate(child.gameObject); }
    private static Sprite frame;
    [MenuItem("Dine In/Polish/Author Tabbed Pause Settings")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
        AuthorService();
        AuthorPause("Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab");
        AuthorPause("Assets/_Project/Resources/UI/LobbyHUD.prefab");
        AuthorDeveloper();
        Debug.Log("[Pause settings] Saved editable tabs, player settings and developer prefabs.");
    }
    private static RectTransform Node(Transform parent,string name)
    {
        var existing=parent!=null?parent.Find(name):null;
        if(existing!=null) return (RectTransform)existing;
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;
        go.transform.SetParent(parent,false);return (RectTransform)go.transform;
    }
    private static T Component<T>(RectTransform rect) where T:UnityEngine.Component
    { var component=rect.GetComponent<T>();return component!=null?component:rect.gameObject.AddComponent<T>(); }
    private static void Stretch(RectTransform rect,Vector2 min,Vector2 max,Vector2 insetMin=default,Vector2 insetMax=default)
    {rect.anchorMin=min;rect.anchorMax=max;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=insetMin;rect.offsetMax=insetMax;rect.localScale=Vector3.one;}
    private static Image Paint(RectTransform rect,Color color,bool raycast=false)
    {var image=Component<Image>(rect);image.sprite=Art("Grey");image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=raycast;return image;}
    private static TMP_Text Text(Transform parent,string name,string text,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
    {
        var rect=Node(parent,name);var label=Component<TextMeshProUGUI>(rect);label.text=text;label.font=Font;label.color=color;
        label.fontSize=label.fontSizeMax=size;label.fontSizeMin=size*.85f;label.enableAutoSizing=true;label.raycastTarget=false;label.alignment=alignment;
        Stretch(rect,Vector2.zero,Vector2.one,new Vector2(16,6),new Vector2(-16,-6));return label;
    }
    private static Button Button(Transform parent,string name,string text,Color color)
    {
        var rect=Node(parent,name);var button=Component<Button>(rect);button.targetGraphic=Paint(rect,color,true);
        var image=(Image)button.targetGraphic;image.sprite=Art(color==Red?"Red":color==Green?"Green":"Blue");image.color=Color.white;
        button.transition=Selectable.Transition.ColorTint;button.navigation=new Navigation{mode=Navigation.Mode.None};
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.9f,.96f,1);colors.selectedColor=Color.white;colors.pressedColor=new Color(.75f,.83f,.9f);button.colors=colors;
        var animation=rect.GetComponent<ButtonAnimator>();if(animation!=null) animation.enabled=false;
        var label=Text(rect,"Label",text,36,Color.white,TextAlignmentOptions.Center);label.font=DisplayFont;
        return button;
    }
    private static RectTransform Content(Transform page)
    {
        var viewport=Node(page,"Viewport");Stretch(viewport,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-44,0));
        var oldMask=viewport.GetComponent<Mask>();if(oldMask!=null)UnityEngine.Object.DestroyImmediate(oldMask);
        Component<RectMask2D>(viewport);Paint(viewport,Paper,true).sprite=null;
        var content=Node(viewport,"Content");Stretch(content,new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);
        var layout=Component<VerticalLayoutGroup>(content);layout.padding=new RectOffset(20,20,12,12);layout.spacing=4;
        layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        var fitter=Component<ContentSizeFitter>(content);fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=Component<ScrollRect>((RectTransform)page);scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
        var rail=Node(page,"Scrollbar");Stretch(rail,new Vector2(1,0),Vector2.one,new Vector2(-38,8),new Vector2(-4,-8));Paint(rail,Color.white).sprite=Art("Grey");
        var area=Node(rail,"SlidingArea");Stretch(area,Vector2.zero,Vector2.one,new Vector2(4,4),new Vector2(-4,-4));
        var handle=Node(area,"Handle");Stretch(handle,Vector2.zero,Vector2.one);var handleImage=Paint(handle,Color.white,true);handleImage.sprite=Art("Blue");
        var scrollbar=Component<Scrollbar>(rail);scrollbar.handleRect=handle;scrollbar.targetGraphic=handleImage;scrollbar.direction=Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        return content;
    }
    private static TMP_Dropdown Dropdown(Transform parent,string name,string[] options)
    {
        RectTransform rect=parent.Find(name) as RectTransform;
        if(rect==null)
        {
            var go=TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources{standard=Art("Grey"),background=Art("Grey"),checkmark=Art("Blue","icon_checkmark"),dropdown=Art("Blue","arrow_basic_s"),mask=Art("Grey")});
            go.name=name;go.transform.SetParent(parent,false);rect=(RectTransform)go.transform;
        }
        var dropdown=rect.GetComponent<TMP_Dropdown>();dropdown.ClearOptions();dropdown.AddOptions(options.ToList());
        dropdown.GetComponent<Image>().sprite=Art("Grey");dropdown.GetComponent<Image>().color=Color.white;
        dropdown.template.GetComponent<Image>().sprite=Art("Grey");
        foreach(var text in dropdown.GetComponentsInChildren<TMP_Text>(true))
        {text.font=Font;text.color=Navy;text.fontSize=text.fontSizeMax=34;text.fontSizeMin=28;text.enableAutoSizing=true;}
        dropdown.template.sizeDelta=new Vector2(dropdown.template.sizeDelta.x,360);
        var item=dropdown.template.GetComponentInChildren<Toggle>(true);
        if(item.targetGraphic is Image itemBackground) { itemBackground.sprite=Art("Grey");itemBackground.color=Color.white; }
        if(item.graphic is Image itemCheck) { itemCheck.sprite=Art("Blue","icon_checkmark");itemCheck.color=Color.white;itemCheck.preserveAspect=true; }
        ((RectTransform)item.transform).sizeDelta=new Vector2(0,82);
        var content=(RectTransform)item.transform.parent;content.sizeDelta=new Vector2(content.sizeDelta.x,90);
        var arrow=rect.Find("Arrow");if(arrow!=null){Remove(arrow,"Chevron");var image=arrow.GetComponent<Image>();image.enabled=true;image.sprite=Art("Blue","arrow_basic_s");image.preserveAspect=true;((RectTransform)arrow).sizeDelta=new Vector2(32,32);}
        return dropdown;
    }
    private static PauseSettingsPanel.Row Row(Transform parent,Key key,string title,string help,int kind,float min=0,float max=1,string objectName=null)
    {
        var rect=Node(parent,objectName??key.ToString());Paint(rect,Color.white);
        var layout=Component<LayoutElement>(rect);layout.preferredHeight=124;layout.minHeight=110;
        var titleText=Text(rect,"Label",title,36,Navy);Stretch(titleText.rectTransform,new Vector2(.015f,.1f),new Vector2(.50f,.9f));
        Remove(rect,"Hint");Remove(rect,"Divider");
        var row=new PauseSettingsPanel.Row{setting=key,root=rect};
        if(kind==0)
        {
            var sliderRect=Node(rect,"Slider");Stretch(sliderRect,new Vector2(.56f,.18f),new Vector2(.87f,.82f));
            var slider=Component<Slider>(sliderRect);Paint(sliderRect,new Color(1,1,1,0),true);
            var track=Node(sliderRect,"Track");Stretch(track,new Vector2(0,.32f),new Vector2(1,.68f));Paint(track,Color.white).sprite=Art("Grey","slide_horizontal_grey");
            var fill=Node(track,"Fill");Stretch(fill,Vector2.zero,Vector2.one);Paint(fill,Color.white).sprite=Art("Blue","slide_horizontal_color");
            var handleArea=Node(sliderRect,"HandleArea");Stretch(handleArea,Vector2.zero,Vector2.one,new Vector2(15,0),new Vector2(-15,0));
            var handle=Node(handleArea,"Handle");handle.sizeDelta=new Vector2(48,0);Paint(handle,Color.white).sprite=Art("Grey","slide_hangle");
            slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.minValue=min;slider.maxValue=max;
            row.slider=slider;row.value=Text(rect,"Value","100%",32,Navy,TextAlignmentOptions.Right);
            StyleSliderLine(slider);
            Stretch(row.value.rectTransform,new Vector2(.88f,.2f),new Vector2(.99f,.8f));
        }
        else if(kind==1)
        {
            var toggleRect=Node(rect,"Toggle");var old=toggleRect.GetComponent<Button>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);Remove(toggleRect,"Label");
            Stretch(toggleRect,new Vector2(.86f,.15f),new Vector2(.98f,.85f));
            row.toggle=Component<Toggle>(toggleRect);var track=Paint(toggleRect,Color.white,true);row.toggle.targetGraphic=track;row.toggle.graphic=null;
            var thumb=Node(toggleRect,"Thumb");thumb.sizeDelta=new Vector2(58,58);Paint(thumb,Color.white).sprite=Art("Grey","check_round_color");
            var visual=Component<SettingsToggleVisual>(toggleRect);visual.track=track;visual.thumb=thumb;visual.enabledSprite=Art("Green");visual.disabledSprite=Art("Grey");visual.Refresh();
        }
        else
        {row.dropdown=Dropdown(rect,"Choice",new[]{"Default"});Stretch((RectTransform)row.dropdown.transform,new Vector2(.56f,.18f),new Vector2(.99f,.82f));}
        return row;
    }
    public static void AuthorPause(string path)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view=root.GetComponentInChildren<LobbyPauseMenuView>(true);frame=Art("Grey");
            var pauseImage=view.PauseButton.GetComponent<Image>();pauseImage.sprite=Art("Blue","button_square_depth_flat");pauseImage.color=Color.white;
            view.GetComponent<Canvas>().sortingOrder=32766;
            var window=(RectTransform)view.Overlay.transform.Find("PauseWindow");
            Stretch(window,new Vector2(.045f,.04f),new Vector2(.955f,.96f));Paint(window,Paper,true);
            var title=window.Find("Title");if(title!=null) title.gameObject.SetActive(false);
            var content=Node(window,"SettingsContent");Stretch(content,Vector2.zero,Vector2.one);
            if(content.Find("Tabs")==null)
                foreach(Transform legacy in content) legacy.gameObject.SetActive(false); // Retain old references for recovery.
            var panel=Component<PauseSettingsPanel>(content);
            var header=Node(content,"Header");Stretch(header,new Vector2(0,1),Vector2.one,new Vector2(0,-130),Vector2.zero);Paint(header,Navy);
            Text(header,"Heading","PAUSED",58,Color.white).font=DisplayFont;
            var tabBar=Node(content,"Tabs");Stretch(tabBar,new Vector2(0,1),Vector2.one,new Vector2(26,-236),new Vector2(-26,-144));
            panel.tabs=new Button[4];panel.pages=new GameObject[4];var contents=new RectTransform[4];
            string[] names={"AUDIO","CONTROLS","GRAPHICS","ACCESSIBILITY"};
            for(int i=0;i<4;i++)
            {
                var button=Button(tabBar,names[i],names[i],i==0?Blue:Navy);Stretch((RectTransform)button.transform,new Vector2(i*.25f,0),new Vector2((i+1)*.25f,1),new Vector2(6,0),new Vector2(-6,0));panel.tabs[i]=button;
                var page=Node(content,names[i]+" Page");Stretch(page,Vector2.zero,Vector2.one,new Vector2(26,146),new Vector2(-26,-254));contents[i]=Content(page);panel.pages[i]=page.gameObject;
            }
            var rows=new List<PauseSettingsPanel.Row>();
            // Reuse actions already moved into compact rows by the targeted migration.
            if(panel.resetControls!=null)panel.resetControls.transform.SetParent(contents[1],false);
            if(panel.resetAccessibility!=null)panel.resetAccessibility.transform.SetParent(contents[3],false);
            if(panel.applyDisplay!=null)panel.applyDisplay.transform.SetParent(contents[2],false);
            if(panel.revertDisplay!=null)panel.revertDisplay.transform.SetParent(contents[2],false);
            rows.Add(Row(contents[0],Key.Master,"Master volume","All game audio",0));
            rows.Add(Row(contents[0],Key.Music,"Music volume","Background music",0));
            rows.Add(Row(contents[0],Key.Sfx,"Sound effects","Kitchen, interaction and UI sounds",0));
            rows.Add(Row(contents[0],Key.Mute,"Mute all","Keep your saved channel volumes",1));
            rows.Add(Row(contents[1],Key.Pan,"Camera pan speed","Drag camera movement",0,.25f,2));
            rows.Add(Row(contents[1],Key.Zoom,"Camera zoom speed","Pinch and scroll sensitivity",0,.25f,2));
            rows.Add(Row(contents[1],Key.InvertZoom,"Invert zoom","Reverse pinch and scroll direction",1));
            rows.Add(Row(contents[1],Key.EdgePan,"Edge pan","",1));
            panel.resetControls=Button(contents[1],"ResetControls","RESET CONTROLS",Blue);Component<LayoutElement>((RectTransform)panel.resetControls.transform).preferredHeight=100;
            rows.Add(Row(contents[2],Key.Quality,"Graphics quality","Uses the project's quality presets",2));
            rows.Add(Row(contents[2],Key.Resolution,"Resolution","Desktop only · confirm after Apply",2));
            rows.Add(Row(contents[2],Key.WindowMode,"Window mode","Fullscreen or windowed",2));
            rows.Add(Row(contents[2],Key.VSync,"VSync","Desktop · overrides the FPS cap",1));
            rows.Add(Row(contents[2],Key.Fps,"FPS limit","Actual rate depends on your device",2));
            rows.Add(Row(contents[2],Key.UIScale,"UI Scale","",0,.85f,1.15f));
            panel.displayStatus=Text(contents[2],"DisplayStatus","Display changes need Apply.",28,Navy);Component<LayoutElement>(panel.displayStatus.rectTransform).preferredHeight=90;
            panel.applyDisplay=Button(contents[2],"ApplyDisplay","APPLY DISPLAY",Green);Component<LayoutElement>((RectTransform)panel.applyDisplay.transform).preferredHeight=100;
            panel.revertDisplay=Button(contents[2],"RevertDisplay","REVERT DISPLAY",Red);Component<LayoutElement>((RectTransform)panel.revertDisplay.transform).preferredHeight=100;panel.revertDisplay.gameObject.SetActive(false);
            rows.Add(Row(contents[3],Key.LargeText,"Large text","Larger supported menu and dialogue labels",1));
            rows.Add(Row(contents[3],Key.HighContrast,"High contrast UI","Stronger supported label contrast",1));
            rows.Add(Row(contents[3],Key.ReducedMotion,"Reduced motion","Reduce non-essential presentation effects",1));
            Remove(contents[3].Find("DialogueSpeed"),"Choice");
            rows.Add(Row(contents[3],Key.DialogueSpeed,"Dialogue speed","",0,.5f,2));
            panel.resetAccessibility=Button(contents[3],"ResetAccessibility","RESET ACCESSIBILITY",Blue);Component<LayoutElement>((RectTransform)panel.resetAccessibility.transform).preferredHeight=100;
            panel.rows=rows.ToArray();panel.selectedTabSprite=Art("Blue");panel.idleTabSprite=Art("Grey");panel.contentColor=Color.white;
            foreach(var tab in panel.tabs){tab.GetComponent<Image>().sprite=panel.idleTabSprite;tab.GetComponentInChildren<TMP_Text>().color=Navy;}panel.tabs[0].GetComponent<Image>().sprite=panel.selectedTabSprite;
            var footer=Node(content,"Footer");Stretch(footer,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,132));Paint(footer,new Color(.83f,.9f,.95f));
            Remove(footer,"SavedHint");
            view.GameMenuButton.transform.SetParent(footer,false);Button(footer,view.GameMenuButton.name,"RETURN TO MENU",Red);Stretch((RectTransform)view.GameMenuButton.transform,new Vector2(.03f,.16f),new Vector2(.40f,.84f));
            view.ResumeButton.transform.SetParent(footer,false);Button(footer,view.ResumeButton.name,"RESUME",Green);Stretch((RectTransform)view.ResumeButton.transform,new Vector2(.70f,.16f),new Vector2(.97f,.84f));
            for(int i=0;i<4;i++) panel.pages[i].SetActive(i==0);
            AuthorPlayers(panel);
            PolishSettingsLayout(panel);
            view.Overlay.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    private static void StyleSliderLine(UnityEngine.UI.Slider slider)
    {
        var track=(RectTransform)slider.transform.Find("Track");
        track.anchorMin=new Vector2(0,.5f);track.anchorMax=new Vector2(1,.5f);
        track.anchoredPosition=Vector2.zero;track.sizeDelta=new Vector2(0,8);
        foreach(var image in new[]{track.GetComponent<UnityEngine.UI.Image>(),slider.fillRect.GetComponent<UnityEngine.UI.Image>()})
        {image.sprite=null;image.type=UnityEngine.UI.Image.Type.Simple;image.color=Blue;image.raycastTarget=false;}
        var handle=slider.handleRect;handle.anchorMin=handle.anchorMax=new Vector2(slider.normalizedValue,.5f);
        handle.anchoredPosition=Vector2.zero;handle.sizeDelta=new Vector2(44,44);
        var thumb=handle.GetComponent<UnityEngine.UI.Image>();thumb.sprite=Art("Grey","check_round_color");thumb.type=UnityEngine.UI.Image.Type.Simple;thumb.preserveAspect=true;thumb.color=Color.white;
    }

    [MenuItem("Dine In/Polish/Update Settings Slider Lines")]
    public static void UpdateSliderLines()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before saving prefab changes.");
        foreach(string path in new[]{"Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab","Assets/_Project/Resources/UI/LobbyHUD.prefab"})
        {
            var stage=UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if(stage!=null && stage.assetPath==path && stage.scene.isDirty)
                throw new InvalidOperationException("Save your open pause prefab edits first; they have not been overwritten.");
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var panel=root.GetComponentInChildren<PauseSettingsPanel>(true);
                int count=0;
                foreach(var row in panel.rows)if(row.slider!=null){StyleSliderLine(row.slider);count++;}
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Debug.Log("[Settings polish] Saved "+count+" plain blue slider lines in "+path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
    private static void AuthorService()
    {
        const string path="Assets/_Project/Resources/UI/PlayerSettings.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null) return;
        var root=new GameObject("PlayerSettings");
        try
        {
            var manager=root.AddComponent<Settings>();var so=new SerializedObject(manager);
            var game=AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/_Project/Audio/AudioMixer.mixer");
            so.FindProperty("gameplayMixer").objectReferenceValue=game;
            so.FindProperty("defaultSfxGroup").objectReferenceValue=game.FindMatchingGroups("SFX").First();
            so.FindProperty("menuMusicMixer").objectReferenceValue=AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/_Project/MainMenu/NewDesign/Audio/Mixer Controller/Music.mixer");
            so.FindProperty("menuSfxMixer").objectReferenceValue=AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/_Project/MainMenu/NewDesign/Audio/Mixer Controller/SFX.mixer");
            so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }

    private static void PolishSettingsLayout(PauseSettingsPanel panel)
    {
        panel.rowHeight=108;
        foreach(var row in panel.rows)
        {
            var layout=row.root.GetComponent<UnityEngine.UI.LayoutElement>();layout.preferredHeight=108;layout.minHeight=100;
            if(row.dropdown==null)continue;
            var arrow=(RectTransform)row.dropdown.transform.Find("Arrow");
            arrow.anchorMin=arrow.anchorMax=new Vector2(1,.5f);arrow.pivot=new Vector2(.5f,.5f);
            arrow.anchoredPosition=new Vector2(-36,0);arrow.sizeDelta=new Vector2(28,28);
            arrow.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var caption=row.dropdown.captionText.rectTransform;
            Stretch(caption,Vector2.zero,Vector2.one,new Vector2(20,8),new Vector2(-68,-8));
            row.dropdown.captionText.margin=Vector4.zero;
        }
        foreach(var page in panel.pages)
        {
            var content=page.GetComponent<UnityEngine.UI.ScrollRect>().content;
            var layout=content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=10;layout.padding=new RectOffset(20,20,14,14);
        }
        CompactActions(panel.pages[1],"ControlActions",panel.resetControls);
        CompactActions(panel.pages[3],"AccessibilityActions",panel.resetAccessibility);
        CompactActions(panel.pages[2],"DisplayActions",panel.revertDisplay,panel.applyDisplay);
        panel.displayStatus.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=60;
    }

    private static void CompactActions(GameObject page,string name,params UnityEngine.UI.Button[] buttons)
    {
        var content=page.GetComponent<UnityEngine.UI.ScrollRect>().content;
        var row=Node(content,name);row.SetAsLastSibling();
        Component<UnityEngine.UI.LayoutElement>(row).preferredHeight=96;
        var layout=Component<UnityEngine.UI.HorizontalLayoutGroup>(row);
        layout.childAlignment=TextAnchor.MiddleRight;layout.spacing=16;layout.padding=new RectOffset(0,0,6,6);
        layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
        foreach(var button in buttons)
        {
            button.transform.SetParent(row,false);
            var size=Component<UnityEngine.UI.LayoutElement>((RectTransform)button.transform);
            size.minWidth=280;size.preferredWidth=360;size.flexibleWidth=0;size.minHeight=84;size.preferredHeight=84;size.flexibleHeight=0;
            var label=button.GetComponentInChildren<TMP_Text>(true);label.fontSize=label.fontSizeMax=32;label.fontSizeMin=28;
        }
    }

    [MenuItem("Dine In/Polish/Refine Settings Layout")]
    public static void RefineSettingsLayout()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before saving layout.");
        foreach(string path in new[]{"Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab","Assets/_Project/Resources/UI/LobbyHUD.prefab"})
        {
            var stage=UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if(stage!=null && stage.assetPath==path && stage.scene.isDirty)throw new InvalidOperationException("Save the open prefab first; your unsaved edits were preserved.");
            var root=PrefabUtility.LoadPrefabContents(path);
            try {PolishSettingsLayout(root.GetComponentInChildren<PauseSettingsPanel>(true));PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        Debug.Log("[Settings layout] Saved inset arrows, compact actions and consistent spacing in both pause prefabs.");
    }

    [MenuItem("Dine In/Polish/Enlarge Switch Station Label")]
    public static void EnlargeStationLabel()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before saving kitchen text.");
        const string path="Assets/_Project/Scenes/RoleBased/Lobby2.unity";
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid() || !scene.isLoaded;
        if(!opened && scene.isDirty)throw new InvalidOperationException("Save Lobby2's current edits first; no scene changes were overwritten.");
        if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            var label=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMP_Text>(true)).Single(t=>t.name=="Station Navigation Label");
            Undo.RecordObjects(new UnityEngine.Object[]{label,label.rectTransform},"Enlarge station label");
            label.fontSize=label.fontSizeMax=42;label.fontSizeMin=38;label.enableAutoSizing=true;
            label.rectTransform.sizeDelta=new Vector2(248,72);label.alignment=TextAlignmentOptions.Center;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[Settings layout] Saved larger Switch Station text in Lobby2.");
        }
        finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
    }
    public static void AuthorDeveloper()
    {
        const string path="Assets/_Project/Resources/UI/DeveloperSettings.prefab";
        bool exists=AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null;
        var root=exists?(RectTransform)PrefabUtility.LoadPrefabContents(path).transform:Node(null,"Developer Settings");
        try
        {
            frame=Art("Grey");
            var canvas=Component<Canvas>(root);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32767;
            var scaler=Component<CanvasScaler>(root);scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;Component<GraphicRaycaster>(root);
            // Keep the existing root/Canvas and prefab GUID; replace only its rejected front end.
            var window=root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="Window");
            if(window==null)window=Node(root,"Window");
            var backdrop=root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="Backdrop");
            if(backdrop==null)backdrop=Node(root,"Backdrop");
            Stretch(backdrop,Vector2.zero,Vector2.one);Paint(backdrop,new Color(0,0,0,.65f),true).sprite=null;backdrop.SetAsFirstSibling();
            for(int i=window.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(window.GetChild(i).gameObject);
            Stretch(window,new Vector2(.06f,.04f),new Vector2(.94f,.96f));Paint(window,Color.white,true);
            var view=Component<DeveloperSettingsView>(root);
            var header=Node(window,"Header");Stretch(header,new Vector2(0,1),Vector2.one,new Vector2(0,-110),Vector2.zero);Paint(header,Navy);
            var heading=Text(header,"Title","DEVELOPER SETTINGS",48,Color.white);heading.font=DisplayFont;Stretch(heading.rectTransform,Vector2.zero,new Vector2(.88f,1),new Vector2(28,8),new Vector2(-20,-8));
            view.close=Button(header,"Close","",Red);Stretch((RectTransform)view.close.transform,new Vector2(.93f,.14f),new Vector2(.985f,.86f));
            var cross=Paint(Node(view.close.transform,"Icon"),Color.white);cross.sprite=Art("Red","icon_cross");cross.preserveAspect=true;Stretch(cross.rectTransform,Vector2.zero,Vector2.one,new Vector2(14,14),new Vector2(-14,-14));
            string[] categories={"SESSION","GAMEPLAY","INVENTORY","ACCOUNT","PROGRESSION","UTILITY"};
            view.tabs=new Button[categories.Length];view.pages=new GameObject[categories.Length];var contents=new RectTransform[categories.Length];
            var tabs=Node(window,"Tabs");Stretch(tabs,new Vector2(0,1),Vector2.one,new Vector2(24,-204),new Vector2(-24,-122));
            for(int i=0;i<categories.Length;i++)
            {
                var button=Button(tabs,categories[i],categories[i],Blue);Stretch((RectTransform)button.transform,new Vector2(i/6f,0),new Vector2((i+1)/6f,1),new Vector2(4,0),new Vector2(-4,0));
                button.GetComponentInChildren<TMP_Text>().fontSizeMax=30;button.GetComponentInChildren<TMP_Text>().color=Navy;view.tabs[i]=button;
                var page=Node(window,categories[i]+" Page");Stretch(page,Vector2.zero,Vector2.one,new Vector2(24,170),new Vector2(-24,-224));view.pages[i]=page.gameObject;contents[i]=Content(page);
            }
            var actions=new List<DeveloperSettingsView.Option>();
            Action<int,string,string,bool,bool,int,string> add=(category,label,command,numeric,destructive,fixedValue,warning)=>
            {
                var row=Node(contents[category],label);Paint(row,Color.white);Component<LayoutElement>(row).preferredHeight=108;
                var title=Text(row,"Label",label,34,Navy);Stretch(title.rectTransform,Vector2.zero,new Vector2(.49f,1));
                TMP_InputField input=null;
                if(numeric && fixedValue<0)
                {
                    var go=TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources{standard=frame,inputField=frame});go.name="Amount";go.transform.SetParent(row,false);
                    input=go.GetComponent<TMP_InputField>();input.contentType=TMP_InputField.ContentType.IntegerNumber;input.characterLimit=9;input.text=command=="day"?"1":command=="timescale"?"1":"100";
                    foreach(var text in go.GetComponentsInChildren<TMP_Text>(true)){text.font=Font;text.fontSize=34;text.color=Navy;}
                    Stretch((RectTransform)go.transform,new Vector2(.50f,.15f),new Vector2(.70f,.85f));
                }
                var button=Button(row,"Execute",numeric&&fixedValue<0?command.StartsWith("add")?"ADD":"SET":label,destructive?Red:Blue);
                Stretch((RectTransform)button.transform,new Vector2(numeric&&fixedValue<0?.73f:.52f,.15f),new Vector2(.98f,.85f));
                actions.Add(new DeveloperSettingsView.Option{label=label,command=command,needsValue=numeric,changesSave=destructive,fixedValue=Mathf.Max(0,fixedValue),input=input,button=button,description=warning});
            };
            add(0,"Change day (1–30)","day",true,true,-1,"Start a fresh day? Purchases, staff and unlock guidance will reset.");
            add(0,"Set approval (0–100)","approval",true,false,-1,"");
            add(0,"Set money","money",true,false,-1,"");
            add(0,"Add money","addmoney",true,false,-1,"");
            add(0,"Start service","startday",false,false,0,"");
            add(0,"End day","endday",false,true,0,"End the current day?");
            add(0,"Game over","gameover",false,true,0,"Set approval to zero and end the run?");
            add(1,"Time scale (0–10)","timescale",true,false,-1,"");
            add(1,"Wrong-order complaint","wrongorder",false,false,0,"");
            add(1,"Burnt-food complaint","burntfood",false,false,0,"");
            add(1,"Next card payment","cardpayment",false,false,0,"");
            add(2,"Fill all stocks","fillstocks",true,false,-1,"");
            add(2,"Empty all stocks","zerostocks",false,true,0,"Empty all restaurant stock?");
            add(3,"Set account coins","setcoin",true,true,-1,"Replace the account coin balance?");
            add(3,"Add account coins","addcoin",true,false,-1,"");
            add(4,"Reset campaign","resetrun",false,true,0,"Reset campaign progress to Day 1?");
            add(4,"Recover restaurant","recover",false,false,0,"");
            string[] equipment={"Busser trolley","Waiter trolley","Card payment"};
            for(int i=0;i<3;i++) {add(4,"Unlock "+equipment[i],"upgrade",true,false,i+1,"");add(4,"Preview "+equipment[i],"unlockpopup",true,false,i+1,"");}
            add(5,"Save game","save",false,false,0,"");
            add(5,"Refresh status","status",false,false,0,"");
            add(5,"Command reference","help",false,false,0,"");
            view.options=actions.ToArray();view.selectedSprite=Art("Blue");view.idleSprite=Art("Grey");
            var output=Node(window,"Output");Stretch(output,Vector2.zero,new Vector2(1,0),new Vector2(24,18),new Vector2(-24,152));Paint(output,Navy);
            var outputContent=Content(output);outputContent.parent.GetComponent<Image>().color=Navy;
            view.result=Text(outputContent,"Result","Select a developer action.",28,Color.white);
            var resultLayout=Component<LayoutElement>(view.result.rectTransform);resultLayout.minHeight=100;
            var confirmation=Node(window,"Confirmation");Stretch(confirmation,Vector2.zero,Vector2.one);Paint(confirmation,new Color(.08f,.12f,.18f,.96f),true);view.confirmation=confirmation.gameObject;
            view.warning=Text(confirmation,"Warning","",40,Color.white,TextAlignmentOptions.Center);Stretch(view.warning.rectTransform,new Vector2(.10f,.45f),new Vector2(.90f,.70f));
            view.confirm=Button(confirmation,"Confirm","CONFIRM",Red);Stretch((RectTransform)view.confirm.transform,new Vector2(.53f,.25f),new Vector2(.86f,.38f));
            view.cancel=Button(confirmation,"Cancel","CANCEL",Blue);Stretch((RectTransform)view.cancel.transform,new Vector2(.14f,.25f),new Vector2(.47f,.38f));confirmation.gameObject.SetActive(false);
            view.SelectTab(0);
            BuildParityAuthoring.AddSafeArea(canvas);canvas.GetComponent<UIScreenSafeArea>().Capture(new[]{window});root.gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root.gameObject,path);
        }
        finally{if(exists)PrefabUtility.UnloadPrefabContents(root.gameObject);else UnityEngine.Object.DestroyImmediate(root.gameObject);}
    }
    private static DeveloperSettingsView.Option Option(string label,string command,string description,bool value,bool save) => new DeveloperSettingsView.Option{label=label,command=command,description=description,needsValue=value,changesSave=save};
}
#endif
