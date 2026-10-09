#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-time authoring of the editable Reservations layout and static model previews.</summary>
public static class FineDiningReservationUIAuthoring
{
    private const string Root = "Assets/_Project/ManagementComputer/Prefabs";
    private const string Art = "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/";
    private static TMP_FontAsset regular, bold;
    private static readonly Color Ink = new Color(.045f,.24f,.32f);
    private static Sprite Sprite(string color, string name)
    {
        var original=AssetDatabase.LoadAssetAtPath<Sprite>(Art+color+"/Double/"+name+".png");
        if(original==null || original.border!=Vector4.zero || !name.StartsWith("button_"))return original;
        var counterpart=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Blue/Double/"+name+".png");
        Vector4 border=counterpart!=null && counterpart.border!=Vector4.zero?counterpart.border:new Vector4(16,24,16,16);
        string folder=Root+"/ReservationStyle";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(Root,"ReservationStyle");
        string path=folder+"/"+color+"_"+name+".asset";
        var sliced=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sliced!=null)return sliced;
        sliced=UnityEngine.Sprite.Create(original.texture,original.rect,new Vector2(.5f,.5f),original.pixelsPerUnit,0,SpriteMeshType.FullRect,border);
        sliced.name=color+" "+name+" Sliced";AssetDatabase.CreateAsset(sliced,path);return sliced;
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); return r;
    }
    private static void Place(RectTransform r, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi)
    { r.anchorMin=min; r.anchorMax=max; r.pivot=new Vector2(.5f,.5f); r.offsetMin=lo; r.offsetMax=hi; }
    private static void Fill(RectTransform r, float pad=0) => Place(r,Vector2.zero,Vector2.one,Vector2.one*pad,Vector2.one*-pad);
    private static void Box(RectTransform r,float x,float y,float w,float h)
    { r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h); }
    private static UnityEngine.UI.Image Image(string name, Transform parent, Sprite sprite)
    { var r=Rect(name,parent); var i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.sprite=sprite;i.type=UnityEngine.UI.Image.Type.Sliced;i.raycastTarget=false;return i; }
    private static TMP_Text Text(string name,Transform parent,string value,float size,bool heavy=false)
    {
        var r=Rect(name,parent);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=heavy?bold:regular;t.text=value;t.fontSize=size;t.enableAutoSizing=false;t.color=Ink;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;t.overflowMode=TextOverflowModes.Ellipsis;return t;
    }
    private static UnityEngine.UI.Button Button(string name,Transform parent,string label,string color)
    {
        var i=Image(name,parent,Sprite(color,"button_rectangle_depth_flat"));i.raycastTarget=true;
        var b=i.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=i;
        b.transition=Selectable.Transition.ColorTint;var colors=b.colors;colors.disabledColor=Color.white;colors.pressedColor=new Color(.86f,.9f,.92f);b.colors=colors;
        var t=Text("Label",i.transform,label,24,true);Fill(t.rectTransform,8);t.alignment=TextAlignmentOptions.Center;t.color=color=="Grey"?Ink:Color.white;
        i.gameObject.AddComponent<UISubtlePressFeedback>();return b;
    }
    private static ScrollRect Scroll(string name,Transform parent,bool grid)
    {
        var root=Rect(name,parent);var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
        var vp=Image("Viewport",root,null);Fill(vp.rectTransform);vp.color=new Color(1,1,1,.001f);vp.raycastTarget=true;vp.gameObject.AddComponent<RectMask2D>();
        var content=Rect("Content",vp.transform);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        if(grid){var g=content.gameObject.AddComponent<GridLayoutGroup>();g.constraint=GridLayoutGroup.Constraint.FixedColumnCount;g.constraintCount=2;g.spacing=new Vector2(12,12);g.padding=new RectOffset(8,8,8,8);g.cellSize=new Vector2(270,220);}
        else {var g=content.gameObject.AddComponent<VerticalLayoutGroup>();g.spacing=10;g.padding=new RectOffset(8,8,8,8);g.childControlHeight=g.childControlWidth=true;g.childForceExpandHeight=false;}
        scroll.viewport=vp.rectTransform;scroll.content=content;
        var track=Image("ScrollTrack",root,Sprite("Grey","button_square_flat"));Place(track.rectTransform,new Vector2(1,0),Vector2.one,new Vector2(-8,3),new Vector2(-1,-3));track.color=new Color(1,1,1,.3f);
        var handle=Image("Handle",track.transform,Sprite("Blue","button_square_flat"));Fill(handle.rectTransform);handle.raycastTarget=true;
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=bar;
        return scroll;
    }
    private static ManagementReservationCard Card(string name,Transform parent,bool table,bool detail=false)
    {
        var i=Image(name,parent,Sprite("Grey","button_square_depth_border"));var card=i.gameObject.AddComponent<ManagementReservationCard>();card.frame=i;
        card.visual=i.gameObject.AddComponent<CanvasGroup>();card.feedback=i.gameObject.AddComponent<ManagementItemCardFeedback>();
        if(!detail){var le=i.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=146;}
        if(table)
        {
            card.preview=Image("TablePreview",i.transform,null);card.preview.type=UnityEngine.UI.Image.Type.Simple;card.preview.preserveAspect=true;
            Place(card.preview.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(14,-106),new Vector2(-14,-10));
            card.title=Text("TableName",i.transform,"Table",24,true);Place(card.title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(14,-134),new Vector2(-82,-105));
            card.details=Text("Capacity",i.transform,"Seats 4",21);Place(card.details.rectTransform,Vector2.one,Vector2.one,new Vector2(-82,-134),new Vector2(-10,-105));
            card.status=Text("Status",i.transform,"AVAILABLE",19,true);Place(card.status.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(14,-161),new Vector2(-12,-134));
            card.button=Button("SelectTable",i.transform,"Select","Green");Place((RectTransform)card.button.transform,Vector2.zero,new Vector2(1,0),new Vector2(14,12),new Vector2(-14,57));card.buttonLabel=card.button.GetComponentInChildren<TMP_Text>();
        }
        else
        {
            var circle=Image("PortraitBacking",i.transform,Sprite("Blue","button_round_depth_flat"));Box(circle.rectTransform,14,14,92,92);
            card.portraits=new UnityEngine.UI.Image[4];
            for(int n=3;n>=0;n--){var pic=Image("GuestPortrait"+n,i.transform,null);pic.type=UnityEngine.UI.Image.Type.Simple;pic.preserveAspect=true;Box(pic.rectTransform,n==0?20:18+(n-1)*27,n==0?15:82,n==0?80:41,n==0?80:47);card.portraits[n]=pic;}
            card.title=Text("PartyName",i.transform,"Taylor party",detail?29:25,true);Place(card.title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(122,-48),new Vector2(-16,-14));
            card.details=Text("Details",i.transform,"5:00 PM \u2022 4 guests",22);Place(card.details.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(122,-102),new Vector2(-14,-48));
            card.status=Text("Status",i.transform,"AWAITING ASSIGNMENT",18,true);Place(card.status.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(122,-132),new Vector2(-14,-104));
            if(!detail){card.button=i.gameObject.AddComponent<UnityEngine.UI.Button>();card.button.targetGraphic=i;i.raycastTarget=true;}
        }
        card.checkmark=Image("SelectedCheck",i.transform,Sprite("Green","icon_checkmark"));Place(card.checkmark.rectTransform,new Vector2(1,1),Vector2.one,new Vector2(-33,-33),new Vector2(-9,-9));card.checkmark.gameObject.SetActive(false);
        i.gameObject.AddComponent<UIRevealAnimation>();
        return card;
    }

    [MenuItem("Dine In/Fine Dining/Author Reservations UI")]
    public static void Author()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/ManagementReservationsPanel.prefab")!=null)throw new InvalidOperationException("Prefab already authored; edit it directly to preserve your layout.");
        regular=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Assets/Fonts/Atkinson_Hyperlegible/AtkinsonHyperlegible-Regular SDF.asset");
        bold=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Assets/Fonts/Atkinson_Hyperlegible/AtkinsonHyperlegible-Bold SDF.asset");
        var root=Rect("Reservations",null);root.sizeDelta=new Vector2(1400,720);var p=root.gameObject.AddComponent<ManagementReservationPanel>();
        try
        {
            p.blueButton=Sprite("Blue","button_rectangle_depth_flat");p.greenButton=Sprite("Green","button_rectangle_depth_flat");p.redButton=Sprite("Red","button_rectangle_depth_flat");p.greyButton=Sprite("Grey","button_rectangle_depth_flat");
            p.cardNormal=Sprite("Grey","button_square_depth_border");p.cardSelected=Sprite("Blue","button_square_depth_border");p.cardDisabled=p.cardNormal;p.checkmarkSprite=Sprite("Green","icon_checkmark");
            p.customerProfiles=AssetDatabase.FindAssets("t:CustomerTypeProfile").Select(g=>AssetDatabase.LoadAssetAtPath<CustomerTypeProfile>(AssetDatabase.GUIDToAssetPath(g))).Where(x=>x!=null).ToArray();
            var tabs=Rect("Tabs",root);Place(tabs,new Vector2(0,1),Vector2.one,new Vector2(8,-62),new Vector2(-8,-4));
            p.requestsTab=Button("Requests",tabs,"Requests  3","Blue");Place((RectTransform)p.requestsTab.transform,Vector2.zero,new Vector2(.5f,1),Vector2.zero,new Vector2(-6,0));
            p.tonightTab=Button("Tonight",tabs,"Tonight  0","Grey");Place((RectTransform)p.tonightTab.transform,new Vector2(.5f,0),Vector2.one,new Vector2(6,0),Vector2.zero);
            p.requestsLabel=p.requestsTab.GetComponentInChildren<TMP_Text>();p.tonightLabel=p.tonightTab.GetComponentInChildren<TMP_Text>();
            var left=Image("PartyListPanel",root,p.cardNormal);Place(left.rectTransform,Vector2.zero,new Vector2(.36f,1),new Vector2(8,70),new Vector2(-8,-76));
            var heading=Text("Heading",left.transform,"YOUR GUESTS",22,true);Place(heading.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(18,-42),new Vector2(-18,-10));
            p.requestsScroll=Scroll("Parties",left.transform,false);Place((RectTransform)p.requestsScroll.transform,Vector2.zero,Vector2.one,new Vector2(8,10),new Vector2(-12,-45));p.requestsScroll.gameObject.AddComponent<UIRevealAnimation>();
            p.requestTemplate=Card("RequestCardTemplate",p.requestsScroll.content,false);p.requestTemplate.gameObject.SetActive(false);
            p.emptyList=Text("EmptySchedule",left.transform,"All caught up!",25,true);Place(p.emptyList.rectTransform,Vector2.zero,Vector2.one,new Vector2(28,20),new Vector2(-28,-50));p.emptyList.alignment=TextAlignmentOptions.Center;p.emptyList.gameObject.SetActive(false);
            var right=Image("ReservationDetailPanel",root,p.cardNormal);Place(right.rectTransform,new Vector2(.36f,0),Vector2.one,new Vector2(8,70),new Vector2(-8,-76));
            p.selectionHint=Text("SelectionHint",right.transform,"Choose a party to review its seating.",26,true);Fill(p.selectionHint.rectTransform,28);p.selectionHint.alignment=TextAlignmentOptions.Center;p.selectionHint.gameObject.SetActive(false);
            p.detailsRoot=Rect("SelectedReservation",right.transform);Fill(p.detailsRoot,10);
            p.selectedDetails=Card("SelectedParty",p.detailsRoot,false,true);Place((RectTransform)p.selectedDetails.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-144),Vector2.zero);
            p.declineButton=Button("DeclineRequest",p.detailsRoot,"Decline Request","Red");Place((RectTransform)p.declineButton.transform,new Vector2(1,1),Vector2.one,new Vector2(-214,-190),new Vector2(-4,-146));
            var choose=Text("ChooseTable",p.detailsRoot,"CHOOSE A TABLE",24,true);Place(choose.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(10,-194),new Vector2(-224,-148));
            p.tablesScroll=Scroll("Tables",p.detailsRoot,true);Place((RectTransform)p.tablesScroll.transform,Vector2.zero,Vector2.one,new Vector2(0,69),new Vector2(-2,-240));p.tableGrid=p.tablesScroll.content.GetComponent<GridLayoutGroup>();
            p.tableTemplate=Card("TableCardTemplate",p.tablesScroll.content,true);p.tableTemplate.gameObject.SetActive(false);
            var banner=Image("StatusBanner",p.detailsRoot,Sprite("Grey","button_rectangle_flat"));Place(banner.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(6,-236),new Vector2(-6,-196));p.bannerRoot=banner.gameObject;
            p.banner=Text("Message",banner.transform,"Choose a table, then confirm below.",19,true);Fill(p.banner.rectTransform,5);p.banner.alignment=TextAlignmentOptions.Center;p.bannerRoot.SetActive(false);
            p.acceptButton=Button("AcceptReservation",p.detailsRoot,"Accept Reservation","Grey");Place((RectTransform)p.acceptButton.transform,Vector2.zero,new Vector2(1,0),new Vector2(12,4),new Vector2(-12,59));p.acceptButton.interactable=false;p.acceptLabel=p.acceptButton.GetComponentInChildren<TMP_Text>();
            p.backButton=Button("Back",root,"Back","Blue");Place((RectTransform)p.backButton.transform,Vector2.zero,Vector2.zero,new Vector2(12,5),new Vector2(185,59));
            p.summary=Text("DemandSummary",root,"Booked guests \u2022 Expected walk-ins",22);Place(p.summary.rectTransform,Vector2.zero,new Vector2(1,0),new Vector2(208,8),new Vector2(-12,54));
            var toast=Image("Feedback",root,Sprite("Blue","button_rectangle_depth_flat"));Place(toast.rectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-280,8),new Vector2(280,84));p.feedbackRoot=toast.gameObject;toast.gameObject.AddComponent<UIRevealAnimation>();
            p.feedbackIcon=Image("Check",toast.transform,p.checkmarkSprite);Box(p.feedbackIcon.rectTransform,21,23,30,30);
            p.feedbackText=Text("Result",toast.transform,"Reservation Confirmed",25,true);Fill(p.feedbackText.rectTransform,12);p.feedbackText.rectTransform.offsetMin=new Vector2(64,14);p.feedbackText.rectTransform.offsetMax=new Vector2(-22,-14);p.feedbackText.color=Color.white;p.feedbackText.fontStyle=FontStyles.Bold;p.feedbackRoot.SetActive(false);
            var confirm=Image("DeclineConfirmation",p.detailsRoot,Sprite("Red","button_square_depth_border"));Place(confirm.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-230,-88),new Vector2(230,88));confirm.raycastTarget=true;p.declineConfirmation=confirm.gameObject;
            var question=Text("Question",confirm.transform,"Decline this reservation?",26,true);Place(question.rectTransform,new Vector2(0,.5f),Vector2.one,new Vector2(14,2),new Vector2(-14,-14));question.alignment=TextAlignmentOptions.Center;
            p.cancelDecline=Button("Cancel",confirm.transform,"Cancel","Blue");Place((RectTransform)p.cancelDecline.transform,Vector2.zero,new Vector2(.5f,0),new Vector2(16,18),new Vector2(-7,76));
            p.confirmDecline=Button("ConfirmDecline",confirm.transform,"Decline","Red");Place((RectTransform)p.confirmDecline.transform,new Vector2(.5f,0),new Vector2(1,0),new Vector2(7,18),new Vector2(-16,76));confirm.gameObject.SetActive(false);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,Root+"/ManagementReservationsPanel.prefab").GetComponent<ManagementReservationPanel>();
            var controller=Resources.FindObjectsOfTypeAll<ManagementComputerController>().Single(c=>c.gameObject.scene.name=="Lobby3");var so=new SerializedObject(controller);so.FindProperty("reservationPanelPrefab").objectReferenceValue=prefab;so.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);EditorSceneManager.SaveScene(controller.gameObject.scene);
        }
        finally{UnityEngine.Object.DestroyImmediate(root.gameObject);}
    }


    public static void PolishLayout()
    {
        string path=Root+"/ManagementReservationsPanel.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var p=root.GetComponent<ManagementReservationPanel>();
            p.blueButton=Sprite("Blue","button_rectangle_depth_flat");p.greenButton=Sprite("Green","button_rectangle_depth_flat");p.redButton=Sprite("Red","button_rectangle_depth_flat");p.greyButton=Sprite("Grey","button_rectangle_depth_flat");
            p.cardNormal=Sprite("Grey","button_square_depth_border");p.cardSelected=Sprite("Blue","button_square_depth_border");p.cardDisabled=p.cardNormal;p.tableCardHeight=222;p.tableTopPadding=184;p.bannerTableTopPadding=250;
            foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if(image.sprite==null)continue;
                string source=AssetDatabase.GetAssetPath(image.sprite);
                if(!source.StartsWith(Art))continue;
                string color=source.Substring(Art.Length).Split('/')[0];string file=Path.GetFileNameWithoutExtension(source);
                image.sprite=Sprite(color,file);
            }
            foreach(string name in new[]{"PartyListPanel","ReservationDetailPanel","PartyListPanel/Parties/Viewport/Content/RequestCardTemplate","ReservationDetailPanel/SelectedReservation/SelectedParty","ReservationDetailPanel/SelectedReservation/Tables/Viewport/Content/TableCardTemplate"})
            {
                var item=root.transform.Find(name);var image=item.GetComponent<UnityEngine.UI.Image>();image.fillCenter=false;
                if(name=="PartyListPanel"||name=="ReservationDetailPanel")image.sprite=p.cardSelected;
                var face=item.Find("ReadingSurface");if(face==null){var bg=Image("ReadingSurface",item,null);bg.color=new Color(.94f,.96f,.97f);Fill(bg.rectTransform,14);bg.transform.SetAsFirstSibling();}
            }
            var detail=p.selectedDetails;Place((RectTransform)detail.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-142),Vector2.zero);
            foreach(var card in new[]{p.requestTemplate,p.selectedDetails})
            {
                Place(card.title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(122,-43),new Vector2(-18,-12));
                Place(card.details.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(122,-94),new Vector2(-18,-44));
                Place(card.status.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(122,-120),new Vector2(-18,-96));card.status.fontSize=17;
                for(int n=1;n<4;n++)Box(card.portraits[n].rectTransform,18+(n-1)*25,80,36,39);
            }
            detail.title.fontSize=27;detail.title.rectTransform.offsetMin=new Vector2(122,-49);detail.title.rectTransform.offsetMax=new Vector2(-18,-9);detail.details.rectTransform.offsetMin=new Vector2(122,-102);
            detail.status.rectTransform.offsetMax=new Vector2(-230,-102);detail.status.rectTransform.offsetMin=new Vector2(122,-125);
            detail.details.rectTransform.offsetMax=new Vector2(-230,-49);
            Place((RectTransform)p.declineButton.transform,new Vector2(1,1),Vector2.one,new Vector2(-217,-130),new Vector2(-16,-77));
            Place((RectTransform)p.detailsRoot.Find("ChooseTable"),new Vector2(0,1),Vector2.one,new Vector2(10,-181),new Vector2(-10,-143));
            Place((RectTransform)p.bannerRoot.transform,new Vector2(0,1),Vector2.one,new Vector2(6,-246),new Vector2(-6,-184));
            ((RectTransform)p.tablesScroll.transform).offsetMax=new Vector2(-2,-184);
            var table=p.tableTemplate;
            Place(table.preview.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(14,-95),new Vector2(-14,-8));
            Place(table.title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(14,-129),new Vector2(-88,-96));
            Place(table.details.rectTransform,Vector2.one,Vector2.one,new Vector2(-88,-129),new Vector2(-12,-96));
            Place(table.status.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(14,-154),new Vector2(-12,-129));table.status.fontSize=17;table.title.fontSize=22;
            Place((RectTransform)table.button.transform,Vector2.zero,new Vector2(1,0),new Vector2(14,10),new Vector2(-14,65));
            foreach(var scroll in new[]{p.requestsScroll,p.tablesScroll})
            {
                scroll.viewport.offsetMax=new Vector2(-20,0);
                var track=(RectTransform)scroll.verticalScrollbar.transform;
                track.offsetMin=new Vector2(-14,3);track.offsetMax=new Vector2(-2,-3);
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    // Updates only the shared prefab. Never saves the currently open gameplay scene.
    [MenuItem("Dine In/Fine Dining/Apply Reservations Staff Window Style")]
    public static void ApplyStaffWindowStyle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        string path = Root + "/ManagementReservationsPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var p = root.GetComponent<ManagementReservationPanel>();
            var employee = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/ManagementEmployeeCard.prefab");
            var panelSprite = employee.GetComponent<UnityEngine.UI.Image>().sprite;
            var displayFont = employee.transform.Find("EmployeeName").GetComponent<TMP_Text>().font;
            p.useComputerAppStyle = true;
            p.computerPanelSprite = panelSprite;
            p.blueButton = Sprite("Blue", "button_rectangle_depth_gloss");
            p.greenButton = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Green/Double/button_rectangle_gloss.png");
            p.redButton = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Red/Double/button_rectangle_gloss.png");
            p.greyButton = Sprite("Grey", "button_rectangle_depth_gloss");
            p.tableCardHeight = 214f;
            p.wideTableThreshold = 850f;
            p.wideTableColumns = 3;
            p.tableTopPadding = 148f;
            p.bannerTableTopPadding = 214f;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font != null && text.font.name.Contains("Anton")) text.font = displayFont;
                text.raycastTarget = false;
            }
            var tabs = (RectTransform)root.transform.Find("Tabs");
            Place(tabs, new Vector2(0, 1), Vector2.one, new Vector2(12, -68), new Vector2(-12, -8));
            Box((RectTransform)p.requestsTab.transform, 0, 0, 216, 60);
            Box((RectTransform)p.tonightTab.transform, 230, 0, 216, 60);
            p.requestsLabel.fontSize = p.tonightLabel.fontSize = 21;
            p.requestsLabel.text = "REQUESTS (0)"; p.tonightLabel.text = "TONIGHT (0)";
            var left = (RectTransform)root.transform.Find("PartyListPanel");
            var right = (RectTransform)root.transform.Find("ReservationDetailPanel");
            Place(left, Vector2.zero, new Vector2(.35f, 1), new Vector2(12, 66), new Vector2(-8, -88));
            Place(right, new Vector2(.35f, 0), Vector2.one, new Vector2(8, 66), new Vector2(-12, -88));
            foreach (var section in new[] { left, right })
            {
                var bg = section.GetComponent<UnityEngine.UI.Image>();
                bg.sprite = panelSprite; bg.fillCenter = true; bg.color = new Color(.84f, .89f, .94f);
                section.Find("ReadingSurface").gameObject.SetActive(false);
            }
            var heading = left.Find("Heading").GetComponent<TMP_Text>();
            heading.text = "1  SELECT A PARTY"; heading.fontSize = 22;
            Place((RectTransform)p.requestsScroll.transform, Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-8, -46));
            p.requestsScroll.content.GetComponent<VerticalLayoutGroup>().spacing = 10;
            var requestHeight = p.requestTemplate.GetComponent<LayoutElement>();
            requestHeight.minHeight = requestHeight.preferredHeight = 150;
            foreach (var card in new[] { p.requestTemplate, p.selectedDetails, p.tableTemplate })
            {
                card.useComputerAppStyle = true;
                card.frame.sprite = panelSprite; card.frame.fillCenter = true; card.frame.color = Color.white;
                card.transform.Find("ReadingSurface").gameObject.SetActive(false);
                var accent = card.transform.Find("RoleAccent")?.GetComponent<UnityEngine.UI.Image>();
                if (accent == null) accent = Image("RoleAccent", card.transform, panelSprite);
                accent.color = new Color(.08f, .55f, .88f);
                Place(accent.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -6), Vector2.zero);
                card.checkmark.sprite = Sprite("Blue", "icon_checkmark");
                foreach (var text in new[] { card.title, card.details, card.status }) text.enableAutoSizing = false;
            }
            foreach (var card in new[] { p.requestTemplate, p.selectedDetails })
            {
                bool detail = card == p.selectedDetails;
                float size = detail ? 76 : 104;
                var backing = card.transform.Find("PortraitBacking").GetComponent<UnityEngine.UI.Image>();
                backing.gameObject.SetActive(true); backing.sprite = panelSprite; backing.fillCenter = true; backing.enabled = true;
                backing.color = new Color(.60f, .84f, .96f);
                Box(backing.rectTransform, 14, 18, size, size);
                Box(card.portraits[0].rectTransform, 20, 24, size - 12, size - 12);
                for (int i = 1; i < card.portraits.Length; i++) card.portraits[i].gameObject.SetActive(false);
                float x = detail ? 104 : 132;
                float end = detail ? -166 : -38;
                Place(card.title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(x, -54), new Vector2(end, -14));
                card.title.fontSize = detail ? 27 : 26;
                card.title.overflowMode = TextOverflowModes.Ellipsis; card.title.textWrappingMode = TextWrappingModes.NoWrap;
                Place(card.details.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(x, detail ? -78 : -109), new Vector2(-14, -55));
                card.details.fontSize = 20;
                Place(card.status.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(x, detail ? -103 : -139), new Vector2(-14, detail ? -79 : -111));
                card.status.fontSize = 17;
            }
            Place((RectTransform)p.selectedDetails.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, -110), Vector2.zero);
            p.selectedDetails.frame.color = new Color(.94f, .97f, 1f);
            p.declineButton.transform.SetParent(p.selectedDetails.transform, false);
            Place((RectTransform)p.declineButton.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-152, -21), new Vector2(-12, 21));
            p.selectedDetails.details.rectTransform.offsetMax = new Vector2(-166, -55);
            p.declineButton.GetComponentInChildren<TMP_Text>().text = "Decline";
            p.tableHeading = p.detailsRoot.Find("ChooseTable").GetComponent<TMP_Text>();
            Place(p.tableHeading.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(8, -145), new Vector2(-8, -112));
            p.tableHeading.fontSize = 22;
            Place((RectTransform)p.bannerRoot.transform, new Vector2(0, 1), Vector2.one, new Vector2(6, -210), new Vector2(-6, -148));
            p.banner.fontSize = 19;
            Place((RectTransform)p.tablesScroll.transform, Vector2.zero, Vector2.one, new Vector2(0, 12), new Vector2(-2, -148));
            p.tableGrid.padding = new RectOffset(6, 6, 6, 6); p.tableGrid.spacing = new Vector2(12, 12);
            var table = p.tableTemplate;
            Place(table.preview.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(12, -90), new Vector2(-12, -12));
            Place(table.title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(12, -122), new Vector2(-92, -92));
            table.title.fontSize = 23;
            Place(table.details.rectTransform, Vector2.one, Vector2.one, new Vector2(-90, -122), new Vector2(-12, -92));
            table.details.fontSize = 19; table.details.alignment = TextAlignmentOptions.MidlineRight;
            Place(table.status.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(12, -160), new Vector2(-12, -123));
            table.status.fontSize = 17;
            Place((RectTransform)table.button.transform, Vector2.zero, new Vector2(1, 0), new Vector2(12, 10), new Vector2(-12, 52));
            table.buttonLabel.fontSize = 20;
            Place((RectTransform)p.feedbackRoot.transform, new Vector2(0, 1), Vector2.one, new Vector2(480, -68), new Vector2(-12, -8));
            p.feedbackText.fontSize = 22;
            p.feedbackRoot.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            p.feedbackRoot.GetComponent<UnityEngine.UI.Image>().enabled = false;
            p.feedbackIcon.gameObject.SetActive(false);
            Place((RectTransform)p.backButton.transform, Vector2.zero, Vector2.zero, new Vector2(12, 6), new Vector2(168, 56));
            Place(p.summary.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(186, 8), new Vector2(-12, 54));
            p.summary.fontSize = 20;
            foreach (var button in new[] { p.acceptButton, p.declineButton, p.cancelDecline, p.confirmDecline, p.backButton, table.button })
            {
                button.image.sprite = button == p.acceptButton ? p.greenButton : button == p.declineButton || button == p.confirmDecline ? p.redButton : p.blueButton;
                button.image.color = Color.white;
                var label = button.GetComponentInChildren<TMP_Text>(); label.color = Color.white; label.font = displayFont;
                Fill(label.rectTransform); label.rectTransform.offsetMin = new Vector2(8, 2); label.rectTransform.offsetMax = new Vector2(-8, -2);
                label.fontSize = button == p.acceptButton ? 24 : 20;
            }
            table.checkmark.sprite = Sprite("Green", "icon_checkmark");
            table.selectionAccent = table.transform.Find("RoleAccent").GetComponent<UnityEngine.UI.Image>();
            table.selectionOutline = table.GetComponent<UnityEngine.UI.Outline>();
            if (table.selectionOutline == null) table.selectionOutline = table.gameObject.AddComponent<UnityEngine.UI.Outline>();
            table.selectionOutline.effectColor = new Color(.10f, .58f, .32f);
            table.selectionOutline.effectDistance = new Vector2(2, -2);
            table.selectionOutline.enabled = false;
            p.cancelDecline.image.sprite = p.greenButton;
            p.cancelDecline.image.type = p.confirmDecline.image.type = UnityEngine.UI.Image.Type.Sliced;
            p.cancelDecline.GetComponentInChildren<TMP_Text>().text = "Back";
            if (p.declineConfirmation.GetComponent<UIRevealAnimation>() == null)
                p.declineConfirmation.AddComponent<UIRevealAnimation>();
            p.acceptLabel.text = "Confirm Reservation";
            p.acceptButton.gameObject.SetActive(false);
            p.feedbackRoot.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Dine In/Fine Dining/Capture Reservation Table Previews")]
    public static void CaptureTables()
    {
        var binding=Resources.FindObjectsOfTypeAll<FineDiningSceneBindings>().Single(x=>x.gameObject.scene.name=="Lobby3");
        string folder=Root+"/ReservationPreviews";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        foreach(var table in binding.tables)
        {
            string path=folder+"/"+table.binding.tableId+".png";
            var previewScene=EditorSceneManager.NewPreviewScene();RenderTexture rt=null;Texture2D pixels=null;var previous=RenderTexture.active;
            try
            {
                var clone=UnityEngine.Object.Instantiate(binding.FindFurniture(table.furniture).gameObject);SceneManager.MoveGameObjectToScene(clone,previewScene);clone.transform.position=Vector3.zero;clone.SetActive(true);
                var renderers=clone.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                var cameraObject=new GameObject("Preview Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,previewScene);var camera=cameraObject.GetComponent<Camera>();camera.scene=previewScene;camera.orthographic=true;camera.aspect=2f;camera.transform.rotation=Quaternion.Euler(28,-35,0);camera.transform.position=bounds.center-camera.transform.forward*bounds.size.magnitude*3f;camera.orthographicSize=bounds.size.magnitude*.38f;camera.nearClipPlane=.01f;camera.farClipPlane=bounds.size.magnitude*8;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.89f,.94f,.96f,1);
                var lightObject=new GameObject("Preview Light",typeof(Light));SceneManager.MoveGameObjectToScene(lightObject,previewScene);var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-30,0);
                rt=new RenderTexture(512,256,24);rt.Create();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(512,256,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,512,256),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);EditorSceneManager.ClosePreviewScene(previewScene);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            table.binding.reservationPreview=AssetDatabase.LoadAssetAtPath<Sprite>(path);EditorUtility.SetDirty(table.binding);
        }
        EditorSceneManager.MarkSceneDirty(binding.gameObject.scene);EditorSceneManager.SaveScene(binding.gameObject.scene);
    }
}
#endif

