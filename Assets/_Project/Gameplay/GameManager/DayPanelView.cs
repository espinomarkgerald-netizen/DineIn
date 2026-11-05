using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Local UI widgets for the day flow. GameDayManager owns data, actions, audio and animation timing.</summary>
[DisallowMultipleComponent]
public sealed class DayPanelView : MonoBehaviour
{
    public static readonly string[] SpriteNames = {
        "PanelBody","TitleTab","RowShell","ReportCard","RibbonCoral","RibbonGold","ValuePill","CoachingStrip",
        "PrimaryButton","SecondaryButton","Badge","ProgressTrack","ProgressFill","StarFilled","StarEmpty",
        "IconClock","IconCash","IconChef","IconGuests","IconArrival","IconHourglass","IconOrder","IconCloche",
        "IconCalendar","IconOnTime","IconUnserved","IconHappy","IconNeutral","IconAngry","IconTips","IconWarning",
        "IconCheck","IconApproval","IconGrade","IconMegaphone","IconSparkle","AccentRays",
        "IconChefOutline","IconClocheOutline","IconCoin" };
    private static readonly Color Ink = new Color(.035f,.085f,.18f,1);
    private static readonly Color Cream = new Color(1,.975f,.89f,1);
    [SerializeField] private TMP_FontAsset headingFont, bodyFont;
    [SerializeField] private Sprite[] artwork;
    [SerializeField] private RectTransform introSurface, resultsSurface, introHost, reportHost;
    [SerializeField] private RectTransform[] introSections = new RectTransform[3], resultsSections = new RectTransform[3];
    [SerializeField] private RectTransform introContent, reportContent, reportViewport, introFooter, reportFooter;
    [SerializeField] private ScrollRect reportScroll;
    [SerializeField] private Scrollbar reportScrollbar;
    [SerializeField] private TMP_Text introTitle, reportTitle, statusText, introInstruction, coachingText, noticeText;
    [SerializeField] private TMP_Text[] introValues = new TMP_Text[3], chipValues = new TMP_Text[3], metrics = new TMP_Text[15];
    [SerializeField] private Image revenueFill, approvalFill, cashStateIcon;
    [SerializeField] private Image[] earnedStars = new Image[3], emptyStars = new Image[3], sparkles = new Image[3];
    [SerializeField] private RectTransform[] metricIcons = new RectTransform[3];
    [SerializeField] private RectTransform starRow, coaching, notice, reservations;
    [SerializeField] private Button playButton, primaryButton, secondaryButton;
    private DayPanelIntroSnapshot introData;
    private DayPanelReportSnapshot reportData;
    private string transientNotice;
    private int stars;
    private bool portrait, largeText, animateMetrics, detailsReady;
    private Vector2 previewViewport;
    private Vector3 introFit = Vector3.one, reportFit = Vector3.one;
    private readonly Vector2[] introRest = new Vector2[3], reportRest = new Vector2[3];
    public RectTransform IntroSurface => introSurface;
    public RectTransform ResultsSurface => resultsSurface;
    public RectTransform[] IntroSections => introSections;
    public RectTransform[] ResultsSections => resultsSections;
    public Image[] EarnedStars => earnedStars;
    public Vector3 FittedScale(bool intro) => intro ? introFit : reportFit;
    // Editor-only preview callers supply their render target dimensions; runtime uses the real safe area.
    public void SetPreviewViewport(int width,int height) { previewViewport = new Vector2(width,height); }

    private Sprite Art(string name) { int i=Array.IndexOf(SpriteNames,name);return artwork!=null&&i>=0&&i<artwork.Length?artwork[i]:null; }
    private static void Remove(GameObject go) { if(go==null)return;if(Application.isPlaying)Destroy(go);else DestroyImmediate(go); }
    private static RectTransform Node(Transform parent,string name)
    { var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return (RectTransform)go.transform; }
    private Image Picture(Transform parent,string name,string art)
    {
        var r=Node(parent,name);var i=r.gameObject.AddComponent<Image>();i.sprite=Art(art);i.color=Color.white;i.raycastTarget=false;
        i.type=i.sprite!=null&&i.sprite.border.sqrMagnitude>0?Image.Type.Sliced:Image.Type.Simple;i.preserveAspect=i.type==Image.Type.Simple;return i;
    }
    private TMP_Text Label(Transform parent,string name,string value,float size,bool heading=false,bool light=false)
    {
        var r=Node(parent,name);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=heading?headingFont:bodyFont;
        if(t.font!=null)t.fontSharedMaterial=t.font.material;t.text=value;t.fontSize=size;t.fontStyle=heading?FontStyles.Normal:FontStyles.Bold;
        t.color=light?Cream:Ink;t.raycastTarget=false;t.alignment=TextAlignmentOptions.Center;t.enableAutoSizing=false;
        t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.margin=Vector4.zero;return t;
    }
    private static void At(RectTransform r,float x,float y,float w,float h)
    { if(r==null)return;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(x,-y);r.localScale=Vector3.one;r.localRotation=Quaternion.identity; }
    private static void Stretch(RectTransform r)
    { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one; }
    private static RectTransform Find(Transform root,string name) => root!=null?root.Find(name) as RectTransform:null;
    private static CanvasGroup Group(Component c)
    { var group=c.GetComponent<CanvasGroup>();if(group==null)group=c.gameObject.AddComponent<CanvasGroup>();return group; }
    private void Header(RectTransform surface,out TMP_Text title)
    { Picture(surface,"Title Plate","TitleTab");title=Label(surface,"Title","DAY",56,true,true); }
    private RectTransform DetailLayer(RectTransform parent)
    {
        var layer=Find(parent,"Detail Artwork");if(layer!=null)return layer;
        layer=Node(parent,"Detail Artwork");Stretch(layer);layer.SetSiblingIndex(Find(parent,"Body")!=null?1:0);return layer;
    }
    private void Detail(RectTransform layer,string name,string art)
    { if(Find(layer,name)==null&&Art(art)!=null)Picture(layer,name,art); }
    private void Divider(RectTransform layer,string name)
    {
        if(Find(layer,name)!=null)return;var image=Node(layer,name).gameObject.AddComponent<Image>();
        image.color=new Color(.78f,.66f,.52f,.72f);image.raycastTarget=false;
    }
    private void EnsureDetailArtwork()
    {
        if(detailsReady)return;
        // Add only missing decorative artwork once; retain the saved widgets and their bindings.
        foreach(var surface in new[]{introSurface,resultsSurface})
        {
            var layer=DetailLayer(surface);Detail(layer,"Chef Doodle","IconChefOutline");Detail(layer,"Cloche Doodle","IconClocheOutline");
        }
        var report=DetailLayer(resultsSurface);
        Detail(report,"Rating Left Rays","AccentRays");Detail(report,"Rating Right Rays","AccentRays");Detail(report,"Status Left Line","ProgressFill");Detail(report,"Status Right Line","ProgressFill");
        var finance=DetailLayer(resultsSections[0]);
        Detail(finance,"Target Row","RowShell");Detail(finance,"Target Badge","ValuePill");Detail(finance,"Tips Row","RowShell");Detail(finance,"Accuracy Row","RowShell");
        Detail(finance,"Cash Coins","IconTips");Detail(finance,"Cash Rays","AccentRays");Divider(finance,"Divider Finance");
        var service=DetailLayer(resultsSections[1]);Detail(service,"Orders Row","RowShell");Detail(service,"Served Row","RowShell");
        var booking=DetailLayer(reservations);Divider(booking,"Divider Header");Detail(booking,"Booking Well","RowShell");Detail(booking,"Heading Badge","RowShell");Divider(booking,"Divider Column 1");Divider(booking,"Divider Column 2");
        var guests=DetailLayer(resultsSections[2]);Divider(guests,"Divider Mood 1");Divider(guests,"Divider Mood 2");Divider(guests,"Divider Approval");Divider(guests,"Divider Grade");
        foreach(string name in new[]{"Target Row","Tips Row","Accuracy Row"})Find(finance,name).GetComponent<Image>().color=new Color(1,1,1,.65f);
        foreach(string name in new[]{"Orders Row","Served Row"})Find(service,name).GetComponent<Image>().color=new Color(1,1,1,.65f);
        Find(booking,"Booking Well").GetComponent<Image>().color=new Color(1,1,1,.72f);
        detailsReady=true;
    }
    private void Skin(Button button,string art)
    {
        if(button==null)return;var i=button.targetGraphic as Image;if(i!=null){i.sprite=Art(art);i.type=Image.Type.Sliced;i.color=Color.white;i.raycastTarget=true;}
        var t=button.GetComponentInChildren<TMP_Text>(true);if(t!=null){t.text=t.text.ToUpperInvariant();t.font=headingFont;t.fontSharedMaterial=headingFont.material;t.color=Cream;t.fontStyle=FontStyles.Normal;t.enableAutoSizing=true;t.fontSizeMin=24;t.fontSizeMax=44;t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;Stretch(t.rectTransform);t.margin=new Vector4(18,8,18,8);}
    }

    /// <summary>Explicit authoring only; no scene objects or artwork are rebuilt during Awake.</summary>
    public void Author(RectTransform legacyIntroHost,RectTransform legacyResultsHost,Button play,Button action,TMP_FontAsset heading,TMP_FontAsset body,Sprite[] artSprites)
    {
        headingFont=heading;bodyFont=body;artwork=artSprites;introHost=legacyIntroHost;reportHost=legacyResultsHost;playButton=play;primaryButton=action;detailsReady=false;
        // Retain original Button instances/listeners before removing a previously authored visual tree.
        if(play!=null)play.transform.SetParent(introHost,false);if(action!=null)action.transform.SetParent(reportHost,false);
        foreach(var host in new[]{introHost,reportHost})
        {
            for(int n=host.childCount-1;n>=0;n--){var child=host.GetChild(n);if(child.name.StartsWith("DayPanel V2 ",StringComparison.Ordinal))Remove(child.gameObject);else if(child!=play?.transform&&child!=action?.transform)child.gameObject.SetActive(false);}
            var old=host.GetComponent<Image>();if(old!=null)old.enabled=false;
        }
        introSurface=Node(introHost,"DayPanel V2 Intro");resultsSurface=Node(reportHost,"DayPanel V2 Report");
        foreach(var surface in new[]{introSurface,resultsSurface}){var bg=Picture(surface,"Body","PanelBody");Stretch(bg.rectTransform);}
        Header(introSurface,out introTitle);Header(resultsSurface,out reportTitle);
        foreach(var surface in new[]{introSurface,resultsSurface}){Picture(surface,"Left Accent","AccentRays");Picture(surface,"Right Accent","AccentRays");}
        introContent=Node(introSurface,"Intro Content");introFooter=Node(introSurface,"Actions");reportFooter=Node(resultsSurface,"Actions");
        string[] labels={"HOURS","TARGET","PREP"},icons={"IconClock","IconCash","IconChef"};
        for(int n=0;n<3;n++)
        {
            var row=Picture(introContent,"Row "+n,"RowShell");introSections[n]=row.rectTransform;Group(row);
            Picture(row.transform,"Badge","Badge");Picture(row.transform,"Icon",icons[n]);Picture(row.transform,"Ribbon","RibbonCoral");
            Label(row.transform,"Label",labels[n],30,true,true);Picture(row.transform,"Pill","ValuePill");introValues[n]=Label(row.transform,"Value","",44);
        }
        string[] chips={"IconGuests","IconArrival","IconHourglass"};
        for(int n=0;n<3;n++){var c=Picture(introContent,"Chip "+n,"CoachingStrip");Picture(c.transform,"Icon",chips[n]);chipValues[n]=Label(c.transform,"Value","",22);}
        introInstruction=Label(introContent,"Instruction","Check your stock and staff before opening.",24);
        var view=Node(resultsSurface,"Report Viewport");reportViewport=view;var mask=view.gameObject.AddComponent<RectMask2D>();
        var shield=view.gameObject.AddComponent<Image>();shield.color=new Color(1,1,1,.001f);shield.raycastTarget=true;
        reportScroll=view.gameObject.AddComponent<ScrollRect>();reportScroll.horizontal=false;reportScroll.vertical=true;reportScroll.movementType=ScrollRect.MovementType.Clamped;reportScroll.scrollSensitivity=34;reportScroll.viewport=view;
        reportContent=Node(view,"Cards");reportScroll.content=reportContent;
        var scrollTrack=Picture(resultsSurface,"Report Scrollbar","ProgressTrack");scrollTrack.raycastTarget=true;reportScrollbar=scrollTrack.gameObject.AddComponent<Scrollbar>();
        var scrollHandle=Picture(scrollTrack.transform,"Handle","ProgressFill");scrollHandle.raycastTarget=true;Stretch(scrollHandle.rectTransform);reportScrollbar.handleRect=scrollHandle.rectTransform;reportScrollbar.targetGraphic=scrollHandle;reportScrollbar.direction=Scrollbar.Direction.BottomToTop;
        reportScroll.verticalScrollbar=reportScrollbar;reportScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        for(int n=0;n<3;n++){var card=Picture(reportContent,"Card "+n,"ReportCard");resultsSections[n]=card.rectTransform;Group(card);var ribbon=Picture(card.transform,"Ribbon",n==1?"RibbonGold":"RibbonCoral");Label(ribbon.transform,"Heading",new[]{"FINANCES","SERVICE","GUESTS & APPROVAL"}[n],27,true,n!=1);}
        var finance=resultsSections[0];Picture(finance,"Cash Icon","IconCash");metrics[0]=Label(finance,"Revenue","0",44);
        Label(finance,"Target Label","Target",24);metrics[1]=Label(finance,"Target","0",24);
        Picture(finance,"Target Track","ProgressTrack");revenueFill=Picture(finance,"Target Fill","ProgressFill");revenueFill.type=Image.Type.Filled;revenueFill.fillMethod=Image.FillMethod.Horizontal;
        metrics[2]=Label(finance,"Target Status","",22);Picture(finance,"Tips Icon","IconTips");Label(finance,"Tips Label","Tips",24);metrics[3]=Label(finance,"Tips","0",24);
        cashStateIcon=Picture(finance,"Accuracy Icon","IconCheck");Label(finance,"Accuracy Label","Cash errors",24);metrics[4]=Label(finance,"Accuracy","0",24);
        var service=resultsSections[1];Picture(service,"Orders Icon","IconOrder");Label(service,"Orders Label","Orders",24);metrics[5]=Label(service,"Orders","0",32);
        Picture(service,"Served Icon","IconCloche");Label(service,"Served Label","Served",24);metrics[6]=Label(service,"Served","0",32);
        reservations=Node(service,"Reservations");Label(reservations,"Heading","RESERVATIONS",22,true);
        string[] reservationIcons={"IconCalendar","IconOnTime","IconUnserved"},reservationLabels={"Booked","On time","Unserved"};
        for(int n=0;n<3;n++){var cell=Node(reservations,"Item "+n);Picture(cell,"Icon",reservationIcons[n]);metrics[7+n]=Label(cell,"Value","0",32);Label(cell,"Label",reservationLabels[n],20);}
        Label(service,"Service Note","Complete each order through serving and payment.",22).textWrappingMode=TextWrappingModes.Normal;
        var guests=resultsSections[2];string[] moods={"Happy","Neutral","Angry"};
        for(int n=0;n<3;n++){var cell=Node(guests,"Mood "+n);Picture(cell,"Icon","Icon"+moods[n]);metrics[10+n]=Label(cell,"Value","0",34);Label(cell,"Label",moods[n],21);}
        Picture(guests,"Approval Icon","IconApproval");Label(guests,"Approval Label","Approval",24);metrics[13]=Label(guests,"Approval","0%",28);
        Picture(guests,"Approval Track","ProgressTrack");approvalFill=Picture(guests,"Approval Fill","ProgressFill");approvalFill.type=Image.Type.Filled;approvalFill.fillMethod=Image.FillMethod.Horizontal;
        Picture(guests,"Grade Badge","IconGrade");metrics[14]=Label(guests,"Grade","–",32);Label(guests,"Grade Label","Objective grade",22);
        metricIcons[0]=Find(finance,"Cash Icon");metricIcons[1]=Find(service,"Orders Icon");metricIcons[2]=Find(Find(guests,"Mood 0"),"Icon");
        starRow=Node(resultsSurface,"Rating");for(int n=0;n<3;n++){var cell=Node(starRow,"Star "+n);emptyStars[n]=Picture(cell,"Empty","StarEmpty");earnedStars[n]=Picture(cell,"Earned","StarFilled");sparkles[n]=Picture(cell,"Sparkle","IconSparkle");}
        statusText=Label(resultsSurface,"Status","",28,true);var strip=Picture(resultsSurface,"Coaching","CoachingStrip");coaching=strip.rectTransform;Picture(coaching,"Icon","IconMegaphone");coachingText=Label(coaching,"Text","",23);coachingText.textWrappingMode=TextWrappingModes.Normal;
        notice=Picture(resultsSurface,"Notice","RowShell").rectTransform;noticeText=Label(notice,"Text","",21);noticeText.textWrappingMode=TextWrappingModes.Normal;noticeText.alignment=TextAlignmentOptions.TopLeft;
        LayoutActions(action,null);if(play!=null){play.transform.SetParent(introFooter,false);Skin(play,"PrimaryButton");}
        RefreshLayout(false,false);Finish(true);Finish(false);
    }

    public void BindIntro(DayPanelIntroSnapshot snapshot)
    {
        introData=snapshot;if(introTitle==null)return;introTitle.text=snapshot.Title.ToUpperInvariant();
        introValues[0].text=snapshot.Hours;introValues[1].text=Money(snapshot.TargetRevenue);introValues[2].text="STOCK & STAFF";
        chipValues[0].text=$"UP TO {snapshot.Capacity}\nGROUPS";chipValues[1].text=$"{snapshot.ArrivalRate} GROUPS/MIN";
        float seconds=Mathf.Max(1,snapshot.RealSecondsPerGameHour);chipValues[2].text=seconds%60==0?$"{seconds/60:0} MIN =\n1 GAME HOUR":$"{seconds:0} SEC =\n1 GAME HOUR";
    }
    public void BindReport(DayPanelReportSnapshot snapshot)
    {
        reportData=snapshot;transientNotice=null;if(reportTitle==null)return;reportTitle.text=snapshot.Title.ToUpperInvariant();statusText.text=snapshot.Status;
        stars=Mathf.Clamp(snapshot.EarnedStars,0,3);coachingText.text="NEXT SHIFT: "+snapshot.Coaching;
        reservations.gameObject.SetActive(snapshot.HasReservations);
        Find(resultsSections[1],"Service Note").gameObject.SetActive(!snapshot.HasReservations);
        cashStateIcon.sprite=Art(snapshot.CashErrors==0?"IconCheck":"IconWarning");
        Find(resultsSections[0],"Accuracy Label").GetComponent<TMP_Text>().text=snapshot.CashErrors==0?"Cash accuracy":"Cash errors";
        Find(resultsSections[2],"Grade Label").GetComponent<TMP_Text>().text=snapshot.HasObjectiveGrade?"Objective grade":"Not scored";
        ResetNotice();SetMetricProgress(1);RefreshLayout(portrait,largeText);
    }
    private static string Money(int value) => "₱"+value.ToString("N0",CultureInfo.InvariantCulture);
    private static int Revealed(int value,float progress) => progress>=1?value:(int)Math.Round(value*(double)progress,MidpointRounding.AwayFromZero);
    public void SetMetricProgress(float value)
    {
        if(reportData==null||metrics[0]==null)return;float t=Mathf.Clamp01(value);var s=reportData;
        metrics[0].text=Money(Revealed(s.Revenue,t));metrics[1].text=Money(s.TargetRevenue);
        metrics[2].text=s.Revenue>=s.TargetRevenue?"TARGET MET":"Need "+Money(Mathf.Max(0,s.TargetRevenue-s.Revenue))+" more";
        metrics[3].text=Money(Revealed(s.Tips,t));metrics[4].text=s.CashErrors==0?"Perfect":s.CashErrors.ToString();
        int[] nums={s.OrdersProcessed,s.FoodServed,s.ReservationsBooked,s.ReservationsOnTime,s.ReservationsUnserved,s.Happy,s.Neutral,s.Angry};
        for(int n=0;n<nums.Length;n++)metrics[5+n].text=Revealed(nums[n],t).ToString();
        metrics[13].text=Mathf.RoundToInt(s.Approval*t)+"%";metrics[14].text=s.HasObjectiveGrade?s.ObjectiveGrade:"–";
        revenueFill.fillAmount=(s.TargetRevenue>0?Mathf.Clamp01((float)s.Revenue/s.TargetRevenue):s.Revenue>=s.TargetRevenue?1:0)*t;
        approvalFill.fillAmount=Mathf.Clamp01(s.Approval/100f)*t;
        bool liveBeat=animateMetrics&&!LevelOneUIAccessibility.ReducedMotion&&(!s.IsOutcome||s.IsVictory);
        int[] positives={s.Revenue,s.OrdersProcessed,s.Happy};
        for(int n=0;n<metricIcons.Length;n++)if(metricIcons[n]!=null)metricIcons[n].localScale=Vector3.one*(1+(liveBeat&&positives[n]>0?Mathf.Sin(t*Mathf.PI)*.055f:0));
    }
    public void SetNotice(string message) { transientNotice=message;UpdateNotice();RefreshLayout(portrait,largeText); }
    public void ResetNotice() { transientNotice=null;UpdateNotice(); }
    private void UpdateNotice()
    {
        if(noticeText==null)return;string text=transientNotice;
        if(string.IsNullOrEmpty(text)&&reportData!=null&&reportData.IsOutcome)
            text=reportData.OutcomeReason+"\n"+reportData.Notice+(reportData.IsVictory?"\n<b>Next service:</b> ":"\n<b>Next run:</b> ")+reportData.Coaching;
        noticeText.text=text??"";notice.gameObject.SetActive(!string.IsNullOrEmpty(text));coaching.gameObject.SetActive(string.IsNullOrEmpty(text));
    }
    public void LayoutActions(Button action,Button paidContinue)
    {
        primaryButton=action;secondaryButton=paidContinue;if(reportFooter==null)return;
        if(action!=null){action.transform.SetParent(reportFooter,false);Skin(action,"PrimaryButton");}
        if(paidContinue!=null){paidContinue.transform.SetParent(reportFooter,false);Skin(paidContinue,"SecondaryButton");}
        LayoutButtons();
    }
    private void LayoutButtons()
    {
        if(reportFooter==null)return;bool two=secondaryButton!=null&&secondaryButton.gameObject.activeSelf;float w=portrait?720:1280;
        float bw=two?(portrait?292:410):(portrait?420:500),gap=20,total=two?bw*2+gap:bw;
        if(primaryButton!=null)At(primaryButton.transform as RectTransform,(w-total)/2,0,bw,portrait?68:76);
        if(two)At(secondaryButton.transform as RectTransform,(w-total)/2+bw+gap,0,bw,portrait?68:76);
    }
    public void RefreshLayout(bool isPortrait,bool isLargeText)
    {
        portrait=isPortrait;largeText=isLargeText;if(introSurface==null||resultsSurface==null)return;
        EnsureDetailArtwork();
        float w=portrait?720:1280,h=portrait?1100:850,bodyScale=largeText?1.15f:1;
        // Legacy scenes wrap the modal in an extra 100x100 RectTransform below the HUD canvas.
        // Fit against the complete modal frame, without changing the canvas or its scaler.
        foreach(var surface in new[]{introSurface,resultsSurface})
            for(var frame=surface.parent as RectTransform;frame!=null&&frame.GetComponent<Canvas>()==null;frame=frame.parent as RectTransform)
                Stretch(frame);
        Canvas.ForceUpdateCanvases();
        foreach(var surface in new[]{introSurface,resultsSurface})
        {
            var host=surface.parent as RectTransform;
            float screenW=previewViewport.x>0?previewViewport.x:Screen.width,screenH=previewViewport.y>0?previewViewport.y:Screen.height;
            Rect safe=previewViewport.x>0?new Rect(0,0,screenW,screenH):Screen.safeArea;
            float sw=screenW>0?safe.width/screenW:1,sh=screenH>0?safe.height/screenH:1;
            surface.anchorMin=surface.anchorMax=new Vector2(.5f,.5f);surface.pivot=new Vector2(.5f,.5f);surface.sizeDelta=new Vector2(w,h);
            float scale=Mathf.Min(host.rect.width*sw*.95f/w,host.rect.height*sh*.95f/h);surface.localScale=Vector3.one*Mathf.Max(.01f,scale);
            if(surface==introSurface)introFit=surface.localScale;else reportFit=surface.localScale;
            surface.anchoredPosition=screenW>0?new Vector2((safe.center.x/screenW-.5f)*host.rect.width,(safe.center.y/screenH-.5f)*host.rect.height):Vector2.zero;
            // Match the sprite aspect exactly: a top-left Image pivot otherwise left-aligns the preserved artwork.
            float titleHeight=108,titleWidth=titleHeight*Art("TitleTab").rect.width/Art("TitleTab").rect.height;
            At(Find(surface,"Title Plate"),(w-titleWidth)/2,12,titleWidth,titleHeight);
            var title=surface==introSurface?introTitle:reportTitle;At(title.rectTransform,(w-470)/2,43,470,64);title.alignment=TextAlignmentOptions.Midline;title.enableAutoSizing=true;title.fontSizeMin=30;title.fontSizeMax=portrait?44:56;
            At(Find(surface,"Left Accent"),portrait?38:78,portrait?152:68,portrait?70:100,portrait?70:100);
            At(Find(surface,"Right Accent"),w-(portrait?38:78),portrait?152:68,portrait?70:100,portrait?70:100);Find(surface,"Right Accent").localScale=new Vector3(-1,1,1);
            var detail=Find(surface,"Detail Artwork");
            At(Find(detail,"Chef Doodle"),portrait?46:60,portrait?983:704,portrait?64:82,portrait?64:82);
            At(Find(detail,"Cloche Doodle"),w-(portrait?110:142),portrait?983:704,portrait?64:82,portrait?64:82);
            Stretch(Find(surface,"Body"));
        }
        Stretch(introContent);float rx=portrait?40:110,rw=portrait?640:1060,rh=portrait?130:116;
        for(int n=0;n<3;n++)
        {
            var row=introSections[n];At(row,rx,(portrait?210:180)+n*(portrait?145:130),rw,rh);
            float icon=portrait?100:116,labelX=portrait?122:158,labelW=portrait?176:250,valueX=portrait?312:445,valueW=rw-valueX-24;
            At(Find(row,"Badge"),10,(rh-icon)/2,icon,icon);At(Find(row,"Icon"),18,(rh-icon+16)/2,icon-16,icon-16);
            At(Find(row,"Ribbon"),labelX,(rh-64)/2,labelW,64);At(Find(row,"Label"),labelX+8,(rh-48)/2,labelW-16,48);
            At(Find(row,"Pill"),valueX,(rh-84)/2,valueW,84);At(introValues[n].rectTransform,valueX+14,(rh-64)/2,valueW-28,64);
            introValues[n].fontSize=(portrait?30:44)*bodyScale;introValues[n].enableAutoSizing=true;introValues[n].fontSizeMin=portrait?25:32;introValues[n].fontSizeMax=(portrait?34:44)*bodyScale;
            Find(row,"Label").GetComponent<TMP_Text>().fontSize=portrait?26:30;introRest[n]=row.anchoredPosition;
        }
        float chipWidth=(rw-24)/3;
        for(int n=0;n<3;n++){var chip=Find(introContent,"Chip "+n);At(chip,rx+n*(chipWidth+12),portrait?692:580,chipWidth,portrait?92:82);At(Find(chip,"Icon"),12,12,portrait?40:50,50);At(chipValues[n].rectTransform,portrait?58:72,3,chipWidth-(portrait?66:80),portrait?86:76);chipValues[n].fontSize=(portrait?19:22)*bodyScale;chipValues[n].textWrappingMode=TextWrappingModes.Normal;}
        At(introInstruction.rectTransform,rx,portrait?810:665,rw,portrait?62:44);introInstruction.fontSize=24*bodyScale;introInstruction.textWrappingMode=TextWrappingModes.Normal;
        At(introFooter,0,portrait?975:714,w,portrait?68:76);if(playButton!=null)At(playButton.transform as RectTransform,(w-(portrait?420:500))/2,0,portrait?420:500,portrait?68:76);
        const float starSize=112,starGap=28,ratingWidth=starSize*3+starGap*2;
        At(starRow,(w-ratingWidth)/2,124,ratingWidth,starSize);for(int n=0;n<3;n++){var cell=starRow.GetChild(n) as RectTransform;At(cell,n*(starSize+starGap),0,starSize,starSize);Stretch(emptyStars[n].rectTransform);Stretch(earnedStars[n].rectTransform);At(sparkles[n].rectTransform,82,-5,32,32);}
        At(statusText.rectTransform,40,232,w-80,38);statusText.fontSize=(portrait?24:28)*bodyScale;
        var ratingDetail=Find(resultsSurface,"Detail Artwork");
        At(Find(ratingDetail,"Rating Left Rays"),(w-ratingWidth)/2-80,164,68,68);
        At(Find(ratingDetail,"Rating Right Rays"),(w+ratingWidth)/2+80,164,68,68);Find(ratingDetail,"Rating Right Rays").localScale=new Vector3(-1,1,1);
        Find(ratingDetail,"Rating Left Rays").gameObject.SetActive(!portrait);Find(ratingDetail,"Rating Right Rays").gameObject.SetActive(!portrait);
        float flankWidth=portrait?104:250;
        At(Find(ratingDetail,"Status Left Line"),portrait?60:170,250,flankWidth,5);At(Find(ratingDetail,"Status Right Line"),w-(portrait?164:420),250,flankWidth,5);
        bool hasNotice=notice!=null&&notice.gameObject.activeSelf;
        float noticeHeight=portrait?(largeText?320:244):(largeText?214:184),noticeTop=(portrait?962:702)-noticeHeight;
        float viewportY=portrait?286:274,viewportHeight=hasNotice?noticeTop-viewportY-14:portrait?544:365,contentW=portrait?640:1100;
        At(reportViewport,portrait?40:90,viewportY,contentW,viewportHeight);
        At(reportScrollbar.transform as RectTransform,portrait?687:1198,viewportY+8,9,viewportHeight-16);
        float cardHeight=largeText?440:365,cardWidth=portrait?contentW:(contentW-32)/3;
        At(reportContent,0,0,contentW,portrait?cardHeight*3+40:cardHeight);
        reportContent.pivot=new Vector2(.5f,1);reportContent.anchorMin=new Vector2(.5f,1);reportContent.anchorMax=new Vector2(.5f,1);reportContent.anchoredPosition=Vector2.zero;
        for(int n=0;n<3;n++){At(resultsSections[n],portrait?0:n*(cardWidth+16),portrait?n*(cardHeight+20):0,cardWidth,cardHeight);LayoutCard(n,cardWidth,cardHeight,bodyScale);reportRest[n]=resultsSections[n].anchoredPosition;}
        float[] metricSizes={44,24,22,24,24,32,32,32,32,32,34,34,34,28,32};
        for(int n=0;n<metrics.Length;n++)if(metrics[n]!=null){metrics[n].fontSize=metricSizes[n]*bodyScale;metrics[n].enableAutoSizing=true;metrics[n].fontSizeMax=metricSizes[n]*bodyScale;metrics[n].fontSizeMin=Mathf.Max(18,metricSizes[n]*.7f)*bodyScale;}
        reportScroll.vertical=portrait||cardHeight>viewportHeight;reportScroll.horizontal=false;
        reportScrollbar.gameObject.SetActive(reportScroll.vertical);
        At(coaching,portrait?40:90,portrait?842:647,contentW,portrait?120:54);At(Find(coaching,"Icon"),10,3,48,48);At(coachingText.rectTransform,72,4,contentW-88,portrait?112:46);coachingText.fontSize=23*bodyScale;
        At(notice,portrait?40:90,noticeTop,contentW,noticeHeight);At(noticeText.rectTransform,18,10,contentW-36,noticeHeight-20);noticeText.fontSize=20*bodyScale;
        At(reportFooter,0,portrait?975:714,w,portrait?68:76);LayoutButtons();
        Canvas.ForceUpdateCanvases();RefreshDensity(introSurface);RefreshDensity(resultsSurface);
    }
    private void LayoutCard(int n,float w,float h,float scale)
    {
        var c=resultsSections[n];At(Find(c,"Ribbon"),8,8,w-16,50);Stretch(Find(Find(c,"Ribbon"),"Heading"));
        float extra=h-365;var detail=Find(c,"Detail Artwork");
        if(n==0)
        {
            At(Find(c,"Cash Icon"),14,64,70,70);At(metrics[0].rectTransform,88,64,w-104,76);metrics[0].fontSize=44*scale;metrics[0].enableAutoSizing=true;metrics[0].fontSizeMin=30;metrics[0].fontSizeMax=44*scale;
            At(Find(detail,"Cash Coins"),50,109,34,34);At(Find(detail,"Cash Rays"),64,58,28,28);
            At(Find(detail,"Target Row"),14,140,w-28,44);At(Find(c,"Target Label"),22,140,w*.40f,44);At(metrics[1].rectTransform,w*.43f,140,w*.50f,44);
            At(Find(c,"Target Track"),20,190,w-40,22);At(revenueFill.rectTransform,23,193,w-46,16);
            var badge=Find(detail,"Target Badge");At(badge,24,216,w-48,40);badge.GetComponent<Image>().sprite=Art(reportData!=null&&reportData.Revenue>=reportData.TargetRevenue?"ValuePill":"RowShell");
            At(metrics[2].rectTransform,30,214,w-60,42);At(Find(detail,"Divider Finance"),20,261+extra,w-40,2);
            At(Find(detail,"Tips Row"),14,267+extra,w-28,40);At(Find(c,"Tips Icon"),18,270+extra,32,32);At(Find(c,"Tips Label"),58,264+extra,w*.43f,42);At(metrics[3].rectTransform,w*.58f,264+extra,w*.36f,42);
            At(Find(detail,"Accuracy Row"),14,311+extra,w-28,40);At(Find(c,"Accuracy Icon"),18,314+extra,30,30);At(Find(c,"Accuracy Label"),58,308+extra,w*.46f,42);At(metrics[4].rectTransform,w*.66f,308+extra,w*.28f,42);
        }
        else if(n==1)
        {
            string[] names={"Orders","Served"};for(int i=0;i<2;i++){float y=68+i*58;At(Find(c,names[i]+" Icon"),20,y+2,44,44);At(Find(c,names[i]+" Label"),75,y,w-198,48);At(metrics[5+i].rectTransform,w-114,y,94,48);}
            At(Find(detail,"Orders Row"),14,66,w-28,50);At(Find(detail,"Served Row"),14,124,w-28,50);
            At(reservations,16,h-188,w-32,176);float bookingWidth=w-32,badgeWidth=Mathf.Min(216,bookingWidth-36);var bookingDetail=Find(reservations,"Detail Artwork");
            At(Find(bookingDetail,"Divider Header"),0,17,bookingWidth,2);At(Find(bookingDetail,"Heading Badge"),(bookingWidth-badgeWidth)/2,0,badgeWidth,34);Find(bookingDetail,"Heading Badge").GetComponent<Image>().color=new Color(1,.93f,.80f,1);
            At(Find(bookingDetail,"Booking Well"),0,32,bookingWidth,144);At(Find(reservations,"Heading"),(bookingWidth-badgeWidth)/2+4,0,badgeWidth-8,34);
            for(int i=1;i<3;i++)At(Find(bookingDetail,"Divider Column "+i),i*bookingWidth/3-1,46,2,114);
            for(int i=0;i<3;i++){float cw=(w-32)/3;var item=Find(reservations,"Item "+i);At(item,i*cw,36,cw,136);At(Find(item,"Icon"),(cw-40)/2,0,40,40);At(metrics[7+i].rectTransform,2,41,cw-4,53);At(Find(item,"Label"),0,95,cw,39);}
            At(Find(c,"Service Note"),24,230,w-48,90+extra);
        }
        else
        {
            for(int i=0;i<3;i++){float cw=(w-32)/3;var item=Find(c,"Mood "+i);At(item,16+i*cw,72,cw,140);At(Find(item,"Icon"),(cw-52)/2,0,52,52);At(metrics[10+i].rectTransform,0,89,cw,50);At(Find(item,"Label"),0,53,cw,36);}
            for(int i=1;i<3;i++)At(Find(detail,"Divider Mood "+i),16+i*(w-32)/3-1,80,2,126);
            At(Find(detail,"Divider Approval"),20,216+extra*.5f,w-40,2);At(Find(c,"Approval Icon"),20,228+extra*.5f,28,28);At(Find(c,"Approval Label"),55,220+extra*.5f,w-145,44);At(metrics[13].rectTransform,w-86,220+extra*.5f,65,44);
            At(Find(c,"Approval Track"),20,264+extra*.5f,w-40,18);At(approvalFill.rectTransform,23,267+extra*.5f,w-46,12);
            At(Find(detail,"Divider Grade"),20,290+extra,w-40,2);At(Find(c,"Grade Badge"),24,296+extra,54,54);At(metrics[14].rectTransform,29,297+extra,44,52);At(Find(c,"Grade Label"),88,300+extra,w-108,46);
        }
        foreach(var text in c.GetComponentsInChildren<TMP_Text>(true))
        {
            if(Array.IndexOf(metrics,text)>=0)continue;float baseSize=text.name=="Heading"?(text.transform.parent==reservations?22:n==2?25:27):text.name=="Label"?20:text.name=="Service Note"?22:24;
            text.fontSize=baseSize*scale;if(text.name=="Heading") {text.enableAutoSizing=true;text.fontSizeMin=20;text.fontSizeMax=baseSize*scale;}
        }
    }
    private static void RefreshDensity(RectTransform root)
    { foreach(var i in root.GetComponentsInChildren<Image>(true))if(i.type==Image.Type.Sliced&&i.sprite!=null){var r=i.rectTransform.rect;i.pixelsPerUnitMultiplier=Mathf.Max(.1f,Mathf.Max(i.sprite.rect.width/Mathf.Max(1,r.width),i.sprite.rect.height/Mathf.Max(1,r.height)));} }

    public void Prepare(bool intro,bool immediate,int earned)
    {
        stars=Mathf.Clamp(earned,0,3);animateMetrics=!immediate;
        for(int n=0;n<3;n++)SetSectionProgress(intro,n,immediate?1:0);
        if(!intro){SetMetricProgress(immediate?1:0);for(int n=0;n<3;n++){if(earnedStars[n]!=null)earnedStars[n].gameObject.SetActive(n<stars);SetStarProgress(n,immediate?1:0);}}
    }
    public void SetSectionProgress(bool intro,int index,float t)
    {
        var roots=intro?introSections:resultsSections;if(index<0||index>=roots.Length||roots[index]==null)return;var r=roots[index];t=Mathf.Clamp01(t);
        Group(r).alpha=t;r.localScale=Vector3.one*Mathf.Lerp(.98f,1,t);r.anchoredPosition=(intro?introRest[index]:reportRest[index])+Vector2.down*(1-t)*10;
    }
    public void SetStarProgress(int index,float t)
    {
        if(index<0||index>=earnedStars.Length||earnedStars[index]==null)return;t=Mathf.Clamp01(t);var star=earnedStars[index];star.gameObject.SetActive(index<stars);
        float u=t-1,pop=1+2.4f*u*u*u+1.4f*u*u,beat=Mathf.Sin(t*Mathf.PI);
        bool motion=animateMetrics&&!LevelOneUIAccessibility.ReducedMotion&&(reportData==null||!reportData.IsOutcome||reportData.IsVictory);
        star.rectTransform.localScale=motion?new Vector3(Mathf.Max(0,pop)*(1+beat*.06f),Mathf.Max(0,pop)*(1-beat*.04f),1):Vector3.one;
        star.rectTransform.localRotation=Quaternion.Euler(0,0,motion?-6*beat*(1-t):0);
        if(sparkles[index]!=null){var color=Color.white;color.a=motion&&index<stars?beat:0;sparkles[index].color=color;sparkles[index].rectTransform.localScale=Vector3.one*(.7f+beat*.4f);}
    }
    public void Finish(bool intro)
    {
        for(int n=0;n<3;n++)SetSectionProgress(intro,n,1);
        if(!intro){SetMetricProgress(1);for(int n=0;n<3;n++)SetStarProgress(n,1);}
    }
}
