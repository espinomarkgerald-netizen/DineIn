using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Menu-only newspaper reader. Content is authored independently of navigation and presentation.</summary>
public sealed class AlmanacMenu : MonoBehaviour
{
    [Header("Content and existing menu")]
    [SerializeField] private AlmanacCatalog catalog;
    [SerializeField] private CanvasGroup menuGroup;
    [SerializeField] private UnityEngine.UI.Button openButton;
    [SerializeField] private AlmanacPreviewStage previewStage;
    [SerializeField] private TMP_FontAsset headlineFont, bodyFont;
    [SerializeField] private AudioClip pageSound;
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup soundMixer;
    [Header("Motion")]
    [SerializeField, Min(.05f)] private float openingSeconds = .38f;
    [SerializeField, Min(.05f)] private float pageSeconds = .22f;
    [Header("Authored layout")]
    [SerializeField] private GameObject overlay;
    [SerializeField] private RectTransform paper;
    [SerializeField] private CanvasGroup paperGroup, detailGroup;
    [SerializeField] private UnityEngine.UI.Button closeButton, backButton, previousButton, nextButton;
    [SerializeField] private TMP_InputField search;
    [SerializeField] private RectTransform categoryContent, indexContent, variantContent, photoFrame;
    [SerializeField] private ScrollRect indexScroll, bodyScroll, variantScroll;
    [SerializeField] private TMP_Text title, subtitle, article, pageLabel, categoryLabel, emptyLabel, inspectLabel, variantLabel;
    [SerializeField] private RawImage liveImage;
    [SerializeField] private UnityEngine.UI.Image stillImage;
    private readonly List<AlmanacEntryData> visible = new List<AlmanacEntryData>();
    private readonly List<string> history = new List<string>();
    private readonly List<UnityEngine.UI.Button> cards = new List<UnityEngine.UI.Button>();
    private readonly Dictionary<AlmanacCategory, UnityEngine.UI.Button> tabs = new Dictionary<AlmanacCategory, UnityEngine.UI.Button>();
    private List<AlmanacEntryData> entries;
    private AlmanacEntryData selected;
    private AlmanacCategory category;
    private Coroutine openingRoutine, pageRoutine;
    [NonSerialized] private bool initialized;
    private bool opened, seenOpening, menuCaptured;
    private float menuAlpha, lastSound;
    private bool menuInteractable, menuBlocks;
    private AudioSource audioSource;
    private GameObject oldSelection;
    private static readonly Color Ink = new Color(.09f,.20f,.24f);
    private static readonly Color Paper = new Color(.98f,.95f,.85f);
    private static readonly Color Teal = new Color(.08f,.43f,.48f);
    private static readonly Color Coral = new Color(.79f,.28f,.23f);
    public bool IsOpen => opened;

    private void Start() => Initialize();
    public void Initialize()
    {
        if (initialized || catalog == null || overlay == null) return;
        initialized = true;
        entries = catalog.LoadEntries();
        openButton.onClick.AddListener(Toggle);
        closeButton.onClick.AddListener(Close);
        backButton.onClick.AddListener(GoBack);
        previousButton.onClick.AddListener(() => Step(-1));
        nextButton.onClick.AddListener(() => Step(1));
        search.onValueChanged.AddListener(_ => RebuildIndex());
        Clear(categoryContent); tabs.Clear();
        foreach (var group in entries.GroupBy(e => e.category))
        {
            var key = group.Key;
            var button = MakeButton(CategoryName(key), categoryContent, CategoryName(key), 19, Paper, 148, 46);
            button.onClick.AddListener(() => SelectCategory(key)); tabs.Add(key, button);
        }
        category = selected != null ? selected.category : entries.Count > 0 ? entries[0].category : AlmanacCategory.Customers;
        categoryLabel.text = CategoryName(category).ToUpperInvariant();
        foreach (var tab in tabs) tab.Value.GetComponent<UnityEngine.UI.Image>().color = tab.Key == category ? new Color(.77f,.89f,.85f) : Paper;
        overlay.SetActive(false);
        if (Application.isPlaying)
        {
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
            audioSource.outputAudioMixerGroup = soundMixer; audioSource.spatialBlend = 0; audioSource.volume = .22f;
        }
    }
    private void Update()
    {
        if (!opened) return;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
#else
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
#endif
    }
    public void Toggle() { if (opened) Close(); else Open(); }
    public void Open()
    {
        Initialize(); if (!initialized || opened) return;
        bool wasVisible = overlay.activeSelf;
        if (openingRoutine != null) StopCoroutine(openingRoutine);
        openingRoutine = null;
        if (!wasVisible)
        {
            paperGroup.alpha = 0;
            paper.localScale = LevelOneUIAccessibility.ReducedMotion ? Vector3.one : new Vector3(.70f,.90f,1);
            paper.localEulerAngles = LevelOneUIAccessibility.ReducedMotion ? Vector3.zero : new Vector3(0,0,-3);
        }
        opened = true; overlay.SetActive(true);
        if (!menuCaptured && EventSystem.current != null) oldSelection = EventSystem.current.currentSelectedGameObject;
        if (!menuCaptured && menuGroup != null)
        {
            menuAlpha = menuGroup.alpha; menuInteractable = menuGroup.interactable; menuBlocks = menuGroup.blocksRaycasts;
            menuCaptured = true; menuGroup.interactable = false; menuGroup.blocksRaycasts = false;
        }
        if (selected == null) SelectCategory(category); else { RebuildIndex(); Populate(selected, 0); }
        if (Application.isPlaying) openingRoutine = StartCoroutine(AnimatePaper(true));
        else { paperGroup.alpha = 1; paper.localScale = Vector3.one; paper.localEulerAngles = Vector3.zero; paperGroup.interactable = true; }
        closeButton.Select(); PlaySound();
    }
    public void Close()
    {
        if (!opened) return; opened = false;
        if (pageRoutine != null) StopCoroutine(pageRoutine);
        if (openingRoutine != null) StopCoroutine(openingRoutine);
        pageRoutine = openingRoutine = null;
        detailGroup.interactable = false; detailGroup.blocksRaycasts = false;
        if (Application.isPlaying && isActiveAndEnabled) openingRoutine = StartCoroutine(AnimatePaper(false));
        else FinishClose();
        PlaySound();
    }
    private IEnumerator AnimatePaper(bool opening)
    {
        float duration = opening ? openingSeconds * (seenOpening ? .7f : 1) : openingSeconds * .7f;
        bool reduced = LevelOneUIAccessibility.ReducedMotion;
        var fromScale = paper.localScale;
        var fromRotation = paper.localRotation;
        var toRotation = Quaternion.Euler(0,0,opening ? 0 : 2);
        float fromAlpha = paperGroup.alpha;
        float fromMenuAlpha = menuGroup != null ? menuGroup.alpha : 0;
        paperGroup.interactable = false;
        for (float elapsed = 0; elapsed < duration; elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
        {
            float t = Mathf.SmoothStep(0,1,elapsed/duration);
            paperGroup.alpha = Mathf.Lerp(fromAlpha, opening ? 1 : 0, t);
            paper.localScale = reduced ? Vector3.one : Vector3.Lerp(fromScale, opening ? Vector3.one : new Vector3(.9f,.94f,1), t);
            paper.localRotation = reduced ? Quaternion.identity : Quaternion.Slerp(fromRotation, toRotation, t);
            if (menuGroup != null && menuCaptured) menuGroup.alpha = Mathf.Lerp(fromMenuAlpha, opening ? menuAlpha*.35f : menuAlpha,t);
            yield return null;
        }
        openingRoutine = null;
        if (opening) { seenOpening = true; paperGroup.alpha=1; paper.localScale=Vector3.one; paper.localEulerAngles=Vector3.zero; paperGroup.interactable=true; if(menuGroup!=null&&menuCaptured)menuGroup.alpha=menuAlpha*.35f; }
        else FinishClose();
    }
    private void FinishClose()
    {
        bool restoreSelection = menuCaptured || (overlay != null && overlay.activeSelf);
        if (previewStage != null) previewStage.Hide();
        if (overlay != null) overlay.SetActive(false);
        if (menuCaptured && menuGroup != null)
        { menuGroup.alpha=menuAlpha;menuGroup.interactable=menuInteractable;menuGroup.blocksRaycasts=menuBlocks; }
        menuCaptured=false;
        if (restoreSelection && EventSystem.current != null)
        {
            var fallback = openButton != null && openButton.gameObject.activeInHierarchy ? openButton.gameObject : null;
            EventSystem.current.SetSelectedGameObject(oldSelection != null && oldSelection.activeInHierarchy ? oldSelection : fallback);
        }
    }
    private void OnDisable()
    {
        if (openingRoutine != null) StopCoroutine(openingRoutine);
        if (pageRoutine != null) StopCoroutine(pageRoutine);
        openingRoutine=pageRoutine=null; opened=false;
        if (initialized) FinishClose();
    }
    private void SelectCategory(AlmanacCategory value)
    {
        category=value; search.SetTextWithoutNotify(string.Empty);
        foreach(var tab in tabs) tab.Value.GetComponent<UnityEngine.UI.Image>().color=tab.Key==category ? new Color(.77f,.89f,.85f) : Paper;
        categoryLabel.text=CategoryName(value).ToUpperInvariant();
        RebuildIndex();
        if(visible.Count>0) Navigate(visible[0],true);
        PlaySound();
    }
    private void RebuildIndex()
    {
        if(entries==null)return;
        Clear(indexContent);cards.Clear();visible.Clear();
        string query=search.text.Trim();
        visible.AddRange(entries.Where(e=>e.category==category && (query.Length==0 || (e.entryName+" "+e.searchAliases).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)));
        string lastSection = null;
        foreach(var entry in visible)
        {
            if (!string.IsNullOrWhiteSpace(entry.listSection) && entry.listSection != lastSection)
            {
                var heading = Text("Section heading", indexContent, entry.listSection.ToUpperInvariant(), 19, true);
                heading.color = Teal;
                var sizing = heading.gameObject.AddComponent<LayoutElement>();
                sizing.minHeight = sizing.preferredHeight = 30;
                lastSection = entry.listSection;
            }
            var card=MakeButton(entry.entryName,indexContent,string.Empty,21,Paper,0,76);
            var image=Graphic("Photo",card.transform,new Color(.88f,.86f,.77f));
            Place(image.rectTransform,new Vector2(0,0),new Vector2(0,1),new Vector2(6,7),new Vector2(70,-7));
            image.sprite=entry.icon;image.preserveAspect=true;image.color=entry.icon!=null?Color.white:new Color(.78f,.83f,.75f);
            var label=Text("Name",card.transform,entry.entryName,21,true);
            Place(label.rectTransform,new Vector2(0,0),Vector2.one,new Vector2(82,9),new Vector2(-10,-9));
            label.alignment=TextAlignmentOptions.MidlineLeft;label.textWrappingMode=TextWrappingModes.Normal;
            card.onClick.AddListener(()=>Navigate(entry,true));cards.Add(card);
        }
        emptyLabel.gameObject.SetActive(visible.Count==0);
        indexScroll.verticalNormalizedPosition=1;RefreshNavigation();
    }
    private void Navigate(AlmanacEntryData entry,bool remember)
    {
        if(entry==null)return;
        if(remember && selected!=null && selected!=entry){history.Add(selected.entryId);if(history.Count>32)history.RemoveAt(0);}
        selected=entry;
        if(category!=entry.category){category=entry.category;search.SetTextWithoutNotify("");categoryLabel.text=CategoryName(category).ToUpperInvariant();RebuildIndex();foreach(var tab in tabs)tab.Value.GetComponent<UnityEngine.UI.Image>().color=tab.Key==category?new Color(.77f,.89f,.85f):Paper;}
        if(pageRoutine!=null)StopCoroutine(pageRoutine);
        if(Application.isPlaying)pageRoutine=StartCoroutine(ChangePage(entry,0));else Populate(entry,0);
        RefreshNavigation();PlaySound();
    }
    private IEnumerator ChangePage(AlmanacEntryData entry,int variant)
    {
        float half=pageSeconds*.5f;bool reduced=LevelOneUIAccessibility.ReducedMotion;
        float fromAlpha=detailGroup.alpha;var fromScale=detailGroup.transform.localScale;
        detailGroup.interactable=false;detailGroup.blocksRaycasts=false;
        for(float t=0;t<half;t+=LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
        {float f=Mathf.SmoothStep(0,1,t/half);detailGroup.alpha=Mathf.Lerp(fromAlpha,0,f);detailGroup.transform.localScale=reduced?Vector3.one:Vector3.Lerp(fromScale,Vector3.one*.98f,f);yield return null;}
        detailGroup.alpha=0;Populate(entry,variant);
        detailGroup.interactable=false;detailGroup.blocksRaycasts=false;
        for(float t=0;t<half;t+=LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
        {float f=Mathf.SmoothStep(0,1,t/half);detailGroup.alpha=f;detailGroup.transform.localScale=reduced?Vector3.one:Vector3.one*Mathf.Lerp(.98f,1,f);yield return null;}
        detailGroup.alpha=1;detailGroup.transform.localScale=Vector3.one;detailGroup.interactable=true;detailGroup.blocksRaycasts=true;pageRoutine=null;
    }
    private void Populate(AlmanacEntryData entry,int variant)
    {
        title.text=entry.entryName.ToUpperInvariant();subtitle.text=entry.subTitle;
        article.text=entry.description+(!string.IsNullOrWhiteSpace(entry.gameplayNotes)?"\n\n<b>IN DINE IN</b>\n"+entry.gameplayNotes:"")+(!string.IsNullOrWhiteSpace(entry.bossNote)?"\n\n<b>BIG BOSS' NOTE</b>\n<i>"+entry.bossNote+"</i>":"");
        bool live=entry.previewKind!=AlmanacPreviewKind.Image && previewStage!=null && previewStage.Show(entry,variant);
        if(!live && previewStage!=null)previewStage.Hide();
        liveImage.gameObject.SetActive(live);liveImage.texture=live?previewStage.Texture:null;
        stillImage.gameObject.SetActive(!live);stillImage.sprite=entry.icon;stillImage.enabled=entry.icon!=null;

        Clear(variantContent);
        if(live && entry.variants!=null && entry.variants.Length>1)
            for(int i=0;i<entry.variants.Length;i++){int pick=i;var b=MakeButton(entry.variants[i].label,variantContent,entry.variants[i].label,18,i==variant?new Color(.77f,.89f,.85f):Paper,152,38);b.onClick.AddListener(()=>SelectVariant(entry,pick));}
        ConfigureDetailLayout(entry, live && entry.variants != null && entry.variants.Length > 1);
        if (variantScroll.gameObject.activeSelf)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(variantContent);
            variantScroll.horizontalNormalizedPosition = variant / (float)(entry.variants.Length - 1);
        }
        bodyScroll.verticalNormalizedPosition=1;detailGroup.alpha=1;detailGroup.transform.localScale=Vector3.one;detailGroup.interactable=true;detailGroup.blocksRaycasts=true;RefreshNavigation();
    }
    private void ConfigureDetailLayout(AlmanacEntryData entry, bool hasVariants)
    {
        bool wide = entry.category == AlmanacCategory.Restaurants || entry.widePreview;
        variantScroll.gameObject.SetActive(hasVariants);
        variantLabel.gameObject.SetActive(hasVariants);
        variantLabel.text = entry.category == AlmanacCategory.Staff ? "UNIFORM / RESTAURANT" : "PREVIEW VARIANT";
        if (wide)
        {
            Place(photoFrame, new Vector2(0,.23f), Vector2.one, new Vector2(0,10), new Vector2(0,-75));
            Place(bodyScroll.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1,.23f), Vector2.zero, new Vector2(0,-4));
        }
        else
        {
            Place(photoFrame, Vector2.zero, new Vector2(.46f,1), new Vector2(0,hasVariants?80:0), new Vector2(0,-75));
            Place(bodyScroll.GetComponent<RectTransform>(), new Vector2(.50f,0), Vector2.one, Vector2.zero, new Vector2(0,-75));
        }
        ConfigureInspectionHint(liveImage.gameObject.activeSelf);
        if (!liveImage.gameObject.activeSelf) inspectLabel.text = wide ? "FROM THE GAME ARCHIVE" : "";
    }
    private void SelectVariant(AlmanacEntryData entry,int variant)
    {
        if(!opened || selected!=entry)return;
        if(pageRoutine!=null)StopCoroutine(pageRoutine);
        pageRoutine=null;
        if(Application.isPlaying)pageRoutine=StartCoroutine(ChangePage(entry,variant));else Populate(entry,variant);
        PlaySound();
    }
    private void ConfigureInspectionHint(bool live)
    {
        // The hint follows the aspect-fitted viewport, not the screen or the menu's character.
        inspectLabel.rectTransform.SetParent(live ? liveImage.transform : photoFrame, false);
        inspectLabel.text = live ? "DRAG TO ROTATE" : "FROM THE RESTAURANT ARCHIVE";
        inspectLabel.color = Ink;
        inspectLabel.raycastTarget = false;
        inspectLabel.alignment = live ? TextAlignmentOptions.Center : TextAlignmentOptions.BottomRight;
        if (live)
            Place(inspectLabel.rectTransform, new Vector2(.03f,.01f), new Vector2(.97f,.09f), Vector2.zero, Vector2.zero);
        else
            Place(inspectLabel.rectTransform, Vector2.zero, Vector2.right, new Vector2(10,4), new Vector2(-10,24));
        inspectLabel.enableAutoSizing = true; inspectLabel.fontSizeMin = 10; inspectLabel.fontSizeMax = 14;
        inspectLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void RefreshNavigation()
    {
        int i=selected!=null?visible.IndexOf(selected):-1;
        previousButton.interactable=i>0;nextButton.interactable=i>=0&&i<visible.Count-1;backButton.interactable=history.Count>0;backButton.gameObject.SetActive(history.Count>0);
        pageLabel.text=selected!=null?"FIELD GUIDE  /  "+(entries.IndexOf(selected)+1).ToString("00")+" OF "+entries.Count.ToString("00"):"FIELD GUIDE";
        for(int n=0;n<cards.Count;n++)cards[n].GetComponent<UnityEngine.UI.Image>().color=visible[n]==selected?new Color(.77f,.89f,.85f):Paper;
    }
    private void Step(int direction){int i=visible.IndexOf(selected)+direction;if(i>=0&&i<visible.Count)Navigate(visible[i],true);}
    private void GoBack()
    {
        if (history.Count == 0) return;
        string id = history[history.Count - 1]; history.RemoveAt(history.Count - 1);
        search.SetTextWithoutNotify(""); RebuildIndex();
        Navigate(entries.Find(e => e.entryId == id), false);
    }
    private void PlaySound(){if(audioSource==null||pageSound==null||Time.unscaledTime-lastSound<.12f)return;lastSound=Time.unscaledTime;audioSource.PlayOneShot(pageSound);}
    public void RotatePreview(float pixels){if(opened&&previewStage!=null)previewStage.Rotate(pixels);}
    private static string CategoryName(AlmanacCategory c)=>c==AlmanacCategory.CleaningStorage?"Storage & Cleaning":c.ToString();
    private static void Clear(Transform parent){for(int i=parent.childCount-1;i>=0;i--){var go=parent.GetChild(i).gameObject;go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}}

    // Author once in the Editor; these references and all static RectTransforms remain editable in the scene.
    public void BuildLayout()
    {
        if(overlay!=null)throw new InvalidOperationException("Almanac layout already exists.");
        overlay=new GameObject("Almanac Overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));overlay.transform.SetParent(transform,false);
        var canvas=overlay.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=100;
        var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var dim=Graphic("Backdrop",overlay.transform,new Color(.035f,.09f,.11f,.72f));Stretch(dim.rectTransform);dim.raycastTarget=true;
        var shadow=Graphic("Paper shadow",overlay.transform,new Color(.03f,.07f,.08f,.65f));Place(shadow.rectTransform,new Vector2(.03f,.04f),new Vector2(.97f,.96f),new Vector2(8,-8),new Vector2(8,-8));
        var bg=Graphic("Newspaper Spread",overlay.transform,Paper);paper=bg.rectTransform;Place(paper,new Vector2(.03f,.04f),new Vector2(.97f,.96f),Vector2.zero,Vector2.zero);bg.raycastTarget=true;paperGroup=paper.gameObject.AddComponent<CanvasGroup>();
        var line=Graphic("Masthead rule",paper,Ink);Place(line.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(24,-91),new Vector2(-24,-88));
        var heading=Text("Masthead",paper,"DINE IN  /  ALMANAC",43,true);Place(heading.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(24,-66),new Vector2(-100,-12));
        var strap=Text("Strapline",paper,"GOOD FOOD. STRANGE GUESTS. EVERYTHING YOU NEED TO KNOW.",15,false);Place(strap.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(26,-87),new Vector2(-80,-63));
        closeButton=MakeButton("Close",paper,"X",26,Coral,52,52);Place((RectTransform)closeButton.transform,Vector2.one,Vector2.one,new Vector2(-78,-71),new Vector2(-24,-17));closeButton.GetComponentInChildren<TMP_Text>().color=Color.white;
        var categoryScroll=Scroll("Categories",paper,true,out categoryContent);Place(categoryScroll.GetComponent<RectTransform>(),new Vector2(0,1),Vector2.one,new Vector2(24,-147),new Vector2(-24,-98));
        var content=new GameObject("Pages",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(paper,false);Place(content,Vector2.zero,Vector2.one,new Vector2(24,66),new Vector2(-24,-162));
        var fold=Graphic("Center fold",content,new Color(.68f,.65f,.53f,.5f));Place(fold.rectTransform,new Vector2(.34f,0),new Vector2(.34f,1),new Vector2(-1,0),new Vector2(2,0));
        var left=new GameObject("Index page",typeof(RectTransform)).GetComponent<RectTransform>();left.SetParent(content,false);Place(left,Vector2.zero,new Vector2(.32f,1),Vector2.zero,Vector2.zero);
        categoryLabel=Text("Category headline",left,"CUSTOMERS",25,true);Place(categoryLabel.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(0,-33),Vector2.zero);
        var inputBG=Graphic("Search",left,new Color(1,1,1,.72f));Place(inputBG.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(0,-84),new Vector2(0,-38));inputBG.raycastTarget=true;
        inputBG.gameObject.AddComponent<RectMask2D>();search=inputBG.gameObject.AddComponent<TMP_InputField>();
        var inputText=Text("Query",inputBG.transform,"",21,false);Stretch(inputText.rectTransform,10,5);inputText.textWrappingMode=TextWrappingModes.NoWrap;
        var hint=Text("Placeholder",inputBG.transform,"Search this section...",20,false);Stretch(hint.rectTransform,10,5);hint.color=new Color(.3f,.37f,.36f,.8f);
        search.textViewport=inputBG.rectTransform;search.textComponent=inputText;search.placeholder=hint;search.fontAsset=bodyFont;search.lineType=TMP_InputField.LineType.SingleLine;
        indexScroll=Scroll("Entry index",left,false,out indexContent);Place(indexScroll.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,-94));
        emptyLabel=Text("No matches",left,"No matching entries.\nTry another search.",21,false);Place(emptyLabel.rectTransform,new Vector2(0,.3f),new Vector2(1,.6f),Vector2.zero,Vector2.zero);emptyLabel.gameObject.SetActive(false);
        var right=new GameObject("Feature page",typeof(RectTransform),typeof(CanvasGroup)).GetComponent<RectTransform>();right.SetParent(content,false);Place(right,new Vector2(.37f,0),Vector2.one,Vector2.zero,Vector2.zero);detailGroup=right.GetComponent<CanvasGroup>();
        title=Text("Entry headline",right,"",30,true);Place(title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(0,-40),Vector2.zero);title.enableAutoSizing=true;title.fontSizeMin=22;title.fontSizeMax=30;
        subtitle=Text("Entry subtitle",right,"",17,false);Place(subtitle.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(0,-65),new Vector2(0,-39));
        var photo=Graphic("Animated photograph",right,new Color(.76f,.84f,.81f));photoFrame=photo.rectTransform;Place(photoFrame,Vector2.zero,new Vector2(.46f,1),Vector2.zero,new Vector2(0,-75));
        var raw=new GameObject("Live preview",typeof(RectTransform),typeof(RawImage));raw.transform.SetParent(photo.transform,false);liveImage=raw.GetComponent<RawImage>();Stretch(liveImage.rectTransform,5,5);liveImage.raycastTarget=true;var aspect=raw.AddComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=1;
        var drag=raw.AddComponent<AlmanacPreviewDrag>();drag.Stage=previewStage;
        stillImage=Graphic("Archive photograph",photo.transform,Color.white);Stretch(stillImage.rectTransform,8,8);stillImage.preserveAspect=true;
        inspectLabel=Text("Inspection hint",liveImage.transform,"DRAG TO ROTATE",14,true);ConfigureInspectionHint(true);
        variantLabel=Text("Variant caption",right,"UNIFORM / RESTAURANT",15,true);Place(variantLabel.rectTransform,Vector2.zero,new Vector2(.46f,0),new Vector2(0,52),new Vector2(0,74));
        variantScroll=Scroll("Uniform editions",right,true,out variantContent);Place(variantScroll.GetComponent<RectTransform>(),Vector2.zero,new Vector2(.46f,0),Vector2.zero,new Vector2(0,48));
        bodyScroll=Scroll("Article",right,false,out var bodyContent);Place(bodyScroll.GetComponent<RectTransform>(),new Vector2(.50f,0),Vector2.one,Vector2.zero,new Vector2(0,-75));
        article=Text("Article text",bodyContent,"",21,false);article.richText=true;article.textWrappingMode=TextWrappingModes.Normal;article.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
        var footer=Graphic("Footer rule",paper,Ink);Place(footer.rectTransform,Vector2.zero,Vector2.right,new Vector2(24,57),new Vector2(-24,59));
        backButton=MakeButton("History",paper,"BACK",20,new Color(.88f,.88f,.78f),105,44);Place((RectTransform)backButton.transform,Vector2.zero,Vector2.zero,new Vector2(24,10),new Vector2(129,54));
        pageLabel=Text("Page number",paper,"FIELD GUIDE",16,true);Place(pageLabel.rectTransform,Vector2.zero,new Vector2(.65f,0),new Vector2(143,10),new Vector2(0,54));pageLabel.alignment=TextAlignmentOptions.MidlineLeft;
        previousButton=MakeButton("Previous entry",paper,"PREVIOUS",20,new Color(.77f,.89f,.85f),124,44);Place((RectTransform)previousButton.transform,Vector2.right,Vector2.right,new Vector2(-285,10),new Vector2(-157,54));
        nextButton=MakeButton("Next entry",paper,"NEXT",20,new Color(.77f,.89f,.85f),124,44);Place((RectTransform)nextButton.transform,Vector2.right,Vector2.right,new Vector2(-150,10),new Vector2(-24,54));
        overlay.SetActive(false);
    }
    private TMP_Text Text(string name,Transform parent,string value,float size,bool heading)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
        var text=go.GetComponent<TextMeshProUGUI>();text.text=value;text.font=heading?headlineFont:bodyFont;text.fontSize=size;text.color=Ink;text.raycastTarget=false;text.alignment=TextAlignmentOptions.TopLeft;text.textWrappingMode=TextWrappingModes.Normal;return text;
    }
    private static UnityEngine.UI.Image Graphic(string name,Transform parent,Color color)
    {var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return image;}
    private UnityEngine.UI.Button MakeButton(string name,Transform parent,string label,float fontSize,Color color,float width,float height)
    {
        var image=Graphic(name,parent,color);image.raycastTarget=true;var button=image.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
        var colors=button.colors;colors.highlightedColor=new Color(.94f,.98f,.90f);colors.pressedColor=new Color(.68f,.85f,.78f);colors.disabledColor=new Color(.7f,.7f,.7f,.45f);button.colors=colors;
        var layout=image.gameObject.AddComponent<LayoutElement>();layout.preferredWidth=width;layout.preferredHeight=height;layout.minHeight=height;
        image.rectTransform.sizeDelta=new Vector2(width,height);
        var text=Text("Label",image.transform,label,fontSize,true);Stretch(text.rectTransform,6,4);text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;
        if(width>0)layout.preferredWidth=Mathf.Max(width,text.GetPreferredValues(label).x+22);
        return button;
    }
    private static ScrollRect Scroll(string name,Transform parent,bool horizontal,out RectTransform content)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(ScrollRect));go.transform.SetParent(parent,false);
        var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(Mask)).GetComponent<RectTransform>();viewport.SetParent(go.transform,false);Stretch(viewport);viewport.GetComponent<Mask>().showMaskGraphic=false;
        content=new GameObject("Content",typeof(RectTransform),typeof(ContentSizeFitter)).GetComponent<RectTransform>();content.SetParent(viewport,false);
        content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(horizontal?0:1,1);content.pivot=new Vector2(0,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
        var fit=content.GetComponent<ContentSizeFitter>();fit.horizontalFit=horizontal?ContentSizeFitter.FitMode.PreferredSize:ContentSizeFitter.FitMode.Unconstrained;fit.verticalFit=horizontal?ContentSizeFitter.FitMode.Unconstrained:ContentSizeFitter.FitMode.PreferredSize;
        if(horizontal){content.anchorMin=Vector2.zero;content.anchorMax=Vector2.up;var layout=content.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=7;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=true;}
        else{var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;}
        var scroll=go.GetComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=horizontal;scroll.vertical=!horizontal;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;return scroll;
    }
    private static void Stretch(RectTransform r,float x=0,float y=0)=>Place(r,Vector2.zero,Vector2.one,new Vector2(x,y),new Vector2(-x,-y));
    private static void Place(RectTransform r,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax){r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(.5f,.5f);r.offsetMin=offsetMin;r.offsetMax=offsetMax;}
}
