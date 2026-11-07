using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Layout of the saved issue only; independent viewports keep editorial headings stationary.
public sealed class NewspaperLandscapeView : MonoBehaviour
{
    private sealed class Metric { public Image icon,extra; public TMP_Text text; }
    private sealed class MarketRow { public TMP_Text item,cost,change,note; public Image rule; }
    private sealed class Section {
        public string title,body; public int column,order; public RectTransform root;
        public Text heading; public TMP_Text text; public Image icon; public Image[] stars;
        public List<Metric> metrics; public List<MarketRow> market; public TMP_Text[] tableHeaders;
    }
    private readonly List<Section> sections=new();
    private readonly ScrollRect[] columns=new ScrollRect[3];
    private readonly Text[] columnTitles=new Text[3];
    private readonly RectTransform[] panels=new RectTransform[3];
    private readonly Image[] dividers=new Image[2];
    private RectTransform paper,area; private CasualDiningPolishSettings settings; private Font serif;
    private Vector2 lastSize; private bool lastLarge;
    private TMP_FontAsset bodyFont;
    private int lastTypography;
    private bool layoutDirty = true;
    private void OnEnable() { layoutDirty = true; }
    private void OnDisable() { layoutDirty = true; }
    private void LateUpdate() => RefreshLayout();
    private int TypographySignature()
    {
        unchecked
        {
            int result = bodyFont != null ? bodyFont.GetInstanceID() ^ bodyFont.atlasTextureCount ^ bodyFont.faceInfo.lineHeight.GetHashCode() : 0;
            if (settings != null)
                result = result * 397 ^ settings.newspaperBodySize.GetHashCode() ^ settings.newspaperSectionSize.GetHashCode()
                    ^ settings.newspaperColumnGap.GetHashCode() ^ settings.newspaperColumnWeights.GetHashCode();
            return result;
        }
    }
    private static readonly Color Ink=new(.06f,.13f,.23f), Rule=new(.24f,.26f,.28f,.65f);
    public void Initialize(RectTransform frame,ScrollRect existing,Text legacy,CasualDiningPolishSettings config,Font font)
    {
        paper=frame;area=(RectTransform)existing.transform;settings=config;serif=font;
        var source=settings!=null&&settings.newspaperBodyFont!=null?settings.newspaperBodyFont:CustomerDrawerArt.Shared?.bodyFont;
        bodyFont=source;
        legacy.gameObject.SetActive(false);existing.enabled=false;existing.viewport.gameObject.SetActive(false);
        string[] names={"LAST SHIFT","NEXT SHIFT","MARKET WATCH"};
        for(int i=0;i<3;i++){
            panels[i]=Node(names[i],area);columnTitles[i]=Heading(names[i],panels[i]);
            columns[i]=CustomerDrawerUI.Scroll(names[i]+" content",panels[i],out _);
            columns[i].movementType=ScrollRect.MovementType.Clamped;
        }
        for(int i=0;i<2;i++)dividers[i]=Image("Column rule",area,null,Rule);
        RefreshLayout(true);
    }
    private static RectTransform Node(string name,Transform parent){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.gameObject.layer=parent.gameObject.layer;return r;}
    private static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    private static void Dispose(GameObject go){go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
    private static string Plain(string value)=>Regex.Replace(value??"","<[^>]+>","");
    private Text Heading(string value,Transform parent){var t=Node(value,parent).gameObject.AddComponent<Text>();t.font=serif;t.fontStyle=FontStyle.Bold;t.text=value;t.color=Ink;t.raycastTarget=false;t.verticalOverflow=VerticalWrapMode.Overflow;return t;}
    private TMP_Text Copy(string name,string value,Transform parent,bool bold=false){
        var t=Node(name,parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.font=bodyFont!=null?bodyFont:TMP_Settings.defaultFontAsset;
        t.text=value;t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;t.color=Ink;t.raycastTarget=false;t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.Normal;return t;
    }
    private Image Image(string name,Transform parent,string icon,Color? color=null){var im=Node(name,parent).gameObject.AddComponent<Image>();im.sprite=icon!=null?settings?.NewspaperIcon(icon):null;im.preserveAspect=icon!=null;im.color=color??Color.white;im.raycastTarget=false;return im;}
    public void Populate(string article)
    {
        layoutDirty = true;
        foreach(var s in sections)Dispose(s.root.gameObject);sections.Clear();
        string clean=Regex.Replace(article??"",@"</?size(?:=[^>]+)?>","");var headings=new List<Match>();
        foreach(Match m in Regex.Matches(clean,@"<b>(?<h>[^<]+)</b>"))if(Identify(m.Groups["h"].Value,out _,out _,out _))headings.Add(m);
        if(headings.Count==0)Add("RESTAURANT NEWS",clean,"IconOrder",1,2);
        else {
            if(!string.IsNullOrWhiteSpace(clean.Substring(0,headings[0].Index)))Add("RESTAURANT NEWS",clean.Substring(0,headings[0].Index),"IconOrder",1,2);
            for(int i=0;i<headings.Count;i++){
                var m=headings[i];string title=m.Groups["h"].Value;Identify(title,out int col,out int order,out string icon);
                string body=clean.Substring(m.Index+m.Length,(i+1<headings.Count?headings[i+1].Index:clean.Length)-m.Index-m.Length).Trim();
                if(title.Contains("BOSS ORDER:")){body="<b>"+title.Substring(title.IndexOf(':')+1).Trim()+"</b>\n"+body;title="BOSS ORDER";}
                Add(Regex.Replace(title,@"^[^A-Za-z]+",""),body,icon,col,order);
            }
        }
        sections.Sort((a,b)=>a.column!=b.column?a.column.CompareTo(b.column):a.order.CompareTo(b.order));RefreshLayout(true);
        Canvas.ForceUpdateCanvases();foreach(var col in columns)col.verticalNormalizedPosition=1;
    }
    private static bool Identify(string title,out int col,out int order,out string icon){
        col=0;order=0;icon="IconApproval";
        if(title.Contains("APPROVAL WATCH")||title.Contains("EARTH RESTAURANT WATCH"))return true;
        if(title.Contains("RESTAURANT RATING")){order=1;icon="StarFilled";return true;}
        if(title.Contains("SHIFT SNAPSHOT")||title.Contains("YESTERDAY AT THE DINER")){order=2;icon="IconGuests";return true;}
        col=1;icon="IconMegaphone";if(title.Contains("BOSS ORDER")||title.Contains("ALIEN BOSS ADVISORY"))return true;
        col=2;icon="IconRestock";if(title.Contains("MARKET WATCH"))return true;
        col=1;order=4;icon="IconQuote";if(title.Contains("VOICE FROM THE QUEUE"))return true;
        order=3;icon="IconChef";return title.Contains("STAFF SPOTLIGHT");
    }
    private void Add(string title,string body,string icon,int col,int order)
    {
        if(icon=="IconApproval"){
            int end=body.IndexOf('\n');if(end>0&&!string.IsNullOrWhiteSpace(body.Substring(end+1))){Add("RESTAURANT NEWS",body.Substring(end+1).Trim(),"IconOrder",1,2);body=body.Substring(0,end);}
        }
        var s=new Section{title=title,body=body,column=col,order=order,root=Node(title,columns[col].content)};
        s.heading=Heading(title,s.root);s.icon=Image("Icon",s.root,icon);s.text=Copy("Saved article",body,s.root);
        if(icon=="IconMegaphone"||title=="SERVICE ALERT"){
            var bg=s.root.gameObject.AddComponent<Image>();bg.sprite=settings?.newspaperInset;bg.type=UnityEngine.UI.Image.Type.Sliced;bg.pixelsPerUnitMultiplier=2;bg.raycastTarget=false;
            if(title=="SERVICE ALERT"){
                bg.sprite=null;bg.color=new Color(1,.84f,.80f);
                int end=body.IndexOf('\n');if(end>0){Add("SERVICE NOTES",body.Substring(end+1).Trim(),"IconOrder",1,5);s.text.text="<b>"+body.Substring(0,end).TrimStart('!',' ')+"</b>";}
                else s.text.text="<b>"+body.TrimStart('!',' ')+"</b>";
            }
        }
        Image("Section rule",s.root,null,Rule);
        if(icon=="StarFilled"){
            var rating=Regex.Match(Plain(body),@"(?<v>\d+(?:[.,]\d+)?)\s*/\s*5");
            if(rating.Success&&float.TryParse(rating.Groups["v"].Value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out float value)){
                value=Mathf.Clamp(value,0,5);s.stars=new Image[5];for(int i=0;i<5;i++)s.stars[i]=Image("Rating star "+i,s.root,value>=i+1?"StarFilled":value>=i+.5f?"StarHalf":"StarEmpty");
                s.text.text=Regex.Replace(body,@"[★☆½]+","").Trim();
            }
        }
        if(icon=="IconGuests")BuildMetrics(s);
        if(icon=="IconRestock")BuildMarket(s);
        sections.Add(s);
    }
    private void BuildMetrics(Section s)
    {
        string plain=Plain(s.body);string[] patterns={@"\b(\d+)\s+SERVED\b",@"\b(\d+)\s+ORDERS DONE\b",@"\b(\d+)\s+GROUPS SEATED\b",@"\b(\d+)\s+HAPPY\b",@"\b(\d+)\s+ANGRY\b"};
        var values=new string[5];for(int i=0;i<5;i++){var m=Regex.Match(plain,patterns[i],RegexOptions.IgnoreCase);if(!m.Success)return;values[i]=m.Groups[1].Value;}
        s.metrics=new List<Metric>();string[] icons={"IconCloche","IconOrder","IconGuests","IconHappy"};
        string[] labels={"<b>"+values[0]+"</b> served","<b>"+values[1]+"</b> orders done","<b>"+values[2]+"</b> groups seated","<b>"+values[3]+"</b> happy · <b>"+values[4]+"</b> angry"};
        for(int i=0;i<4;i++)s.metrics.Add(new Metric{icon=Image("Metric icon "+i,s.root,icons[i]),extra=i==3?Image("Angry reaction",s.root,"IconAngry"):null,text=Copy("Metric value "+i,labels[i],s.root)});
        foreach(var pattern in patterns)plain=Regex.Replace(plain,pattern,"",RegexOptions.IgnoreCase);
        string remainder=Regex.Replace(Regex.Replace(plain,@"[ \t·]+"," "),@"(?m)^\s*$\n?","").Trim();s.text.text="";
        if(!string.IsNullOrWhiteSpace(remainder))Add(remainder.Contains("CLEAN SHIFT")?"SHIFT NOTE":"SERVICE ALERT",remainder,remainder.Contains("CLEAN SHIFT")?"IconCheck":"IconWarning",1,1);
    }
    private void BuildMarket(Section s)
    {
        string[] lines=Plain(s.body).Replace("\r","").Split('\n');var data=new List<(string item,string cost,string change,string note)>();var other=new List<string>();
        for(int i=0;i<lines.Length;i++){
            if(lines[i].Contains("PRICE INCREASE")||lines[i].Contains("PRICE DECREASE")){
                if(i+1>=lines.Length)return;
                var m=Regex.Match(lines[i+1],@"^(?<item>.+?)\s+₱\s*(?<cost>[\d,]+)\s*/\s*BOX\s+(?<change>[+-]\s*\d+(?:[.,]\d+)?)%",RegexOptions.IgnoreCase);
                if(!m.Success)return;
                string cost=m.Groups["cost"].Value;if(long.TryParse(cost.Replace(",",""),out long amount))cost=amount.ToString("N0",CultureInfo.InvariantCulture);
                i+=2;var notes=new List<string>();while(i<lines.Length&&!lines[i].Contains("PRICE INCREASE")&&!lines[i].Contains("PRICE DECREASE")&&!lines[i].Contains("LOCKED AT CHECKOUT")){if(!string.IsNullOrWhiteSpace(lines[i]))notes.Add(lines[i]);i++;}i--;
                data.Add((m.Groups["item"].Value.Trim(),"₱"+cost,m.Groups["change"].Value.Replace(" ",""),string.Join("\n",notes)));
            }else if(!string.IsNullOrWhiteSpace(lines[i]))other.Add(lines[i]);
        }
        if(data.Count==0)return; // Stable prices and unfamiliar archived formats stay readable verbatim.
        s.market=new List<MarketRow>();s.tableHeaders=new[]{Copy("Item header","ITEM",s.root,true),Copy("Cost header","COST / BOX",s.root,true),Copy("Change header","CHANGE",s.root,true)};
        foreach(var item in data){
            bool up=item.change.StartsWith("+");var change=Copy("Price change",(up?"▲ ":"▼ ")+item.change.TrimStart('+','-')+"%",s.root,true);change.color=up?new Color(.64f,.09f,.08f):new Color(.08f,.40f,.18f);
            s.market.Add(new MarketRow{item=Copy("Supplier item",item.item,s.root,true),cost=Copy("Cost per box",item.cost,s.root,true),change=change,note=Copy("Supplier note",item.note,s.root),rule=Image("Market rule",s.root,null,Rule)});
        }
        s.text.text=string.Join("\n",other).Replace("LOCKED AT CHECKOUT","").Trim();s.text.fontStyle=FontStyles.Italic;
    }
    public void RefreshLayout(bool force=false)
    {
        if (!gameObject.activeInHierarchy) { layoutDirty = true; return; }
        if(paper==null||paper.parent is not RectTransform parent||parent.rect.width<=0||parent.rect.height<=0)return;
        Vector2 wanted=settings!=null?settings.newspaperSize:new Vector2(1560,930),size=new(Mathf.Min(wanted.x,parent.rect.width*.90f),Mathf.Min(wanted.y,parent.rect.height*.88f));
        int typography=TypographySignature();
        bool large=LevelOneUIAccessibility.LargeText;if(!force&&!layoutDirty&&size==lastSize&&large==lastLarge&&typography==lastTypography)return;layoutDirty=false;lastSize=size;lastLarge=large;lastTypography=typography;paper.sizeDelta=size;Canvas.ForceUpdateCanvases();
        float width=area.rect.width,height=area.rect.height,gap=settings!=null?settings.newspaperColumnGap:28,f=large?1.2f:1;
        float fs=(settings!=null?settings.newspaperBodySize:24)*f,hs=(settings!=null?settings.newspaperSectionSize:32)*f;
        bool narrow=width<850;float available=width-gap*2;Vector3 weights=settings!=null?settings.newspaperColumnWeights:new Vector3(.29f,.30f,.41f);
        weights=new Vector3(Mathf.Max(.1f,weights.x),Mathf.Max(.1f,weights.y),Mathf.Max(.1f,weights.z));weights/=weights.x+weights.y+weights.z;
        float[] widths=narrow?new[]{width,width,width}:new[]{available*weights.x,available*weights.y,available*weights.z};
        float x=0;for(int i=0;i<3;i++){
            float h=narrow?(height-gap*2)/3:height,head=narrow?Mathf.Min(h*.4f,hs*1.7f):hs*1.7f;
            Place(panels[i],narrow?0:x,narrow?i*(h+gap):0,widths[i],h);
            columnTitles[i].fontSize=Mathf.RoundToInt(narrow?Mathf.Min(hs,head*.7f):hs);Place(columnTitles[i].rectTransform,0,0,widths[i]-8,head);
            Place((RectTransform)columns[i].transform,0,head,widths[i],h-head);
            float y=0;foreach(var s in sections)if(s.column==i){float ch=LayoutSection(s,widths[i]-18,fs,hs);Place(s.root,0,y,widths[i]-18,ch);y+=ch+18;}
            columns[i].content.sizeDelta=new Vector2(0,y);
            if(i<2){dividers[i].enabled=!narrow;Place(dividers[i].rectTransform,x+widths[i]+gap*.5f,0,1,height);}x+=widths[i]+gap;
        }
    }
    private float LayoutSection(Section s,float w,float fs,float hs)
    {
        bool approval=s.order==0&&s.column==0,rating=s.stars!=null,market=s.column==2,advice=s.title=="BOSS ORDER"||s.title.Contains("ADVISORY"),alert=s.title=="SERVICE ALERT";
        bool title=!(approval||rating||market||advice||alert);s.heading.gameObject.SetActive(title);s.heading.fontSize=Mathf.RoundToInt(hs-2);
        float y=0;var rule=(RectTransform)s.root.Find("Section rule");rule.gameObject.SetActive(!approval&&!rating&&!advice&&!alert);Place(rule,0,0,w,1);
        float iconSize=approval?46:advice||alert?46:30;s.icon.enabled=!rating&&!market;Place(s.icon.rectTransform,advice||alert?10:0,advice||alert?12:8,iconSize,iconSize);
        if(title){float th=Mathf.Max(42,s.heading.cachedTextGenerator.GetPreferredHeight(s.title,s.heading.GetGenerationSettings(new Vector2(w-40,1000)))/s.heading.pixelsPerUnit);Place(s.heading.rectTransform,40,6,w-42,th);y=th+18;}
        s.text.fontSize=fs;
        if(approval){s.text.fontSize=fs+6;string plain=Plain(s.body);var value=Regex.Match(plain,@"\d+%");s.text.text=value.Success?"<b>"+value.Value+" Approval</b>\n<size=75%>"+plain.Remove(value.Index,value.Length).Trim()+"</size>":s.body;s.text.fontStyle=FontStyles.Normal;float h=MeasuredHeight(s.text,w-58);Place(s.text.rectTransform,58,8,w-58,h+4);return Mathf.Max(60,h+16);}
        if(rating){for(int i=0;i<5;i++)Place(s.stars[i].rectTransform,i*50,0,46,46);y=54;s.text.fontSize=fs-2;}
        if(s.metrics!=null){foreach(var metric in s.metrics){metric.text.fontSize=fs;float tx=metric.extra!=null?86:50,mh=Mathf.Max(36,MeasuredHeight(metric.text,w-tx-2));Place(metric.icon.rectTransform,4,y,36,36);if(metric.extra!=null)Place(metric.extra.rectTransform,42,y,36,36);Place(metric.text.rectTransform,tx,y,w-tx-2,mh+4);y+=mh+12;}}
        if(market){s.heading.text="Today’s supplier changes";s.heading.gameObject.SetActive(true);s.heading.fontStyle=FontStyle.Italic;s.heading.fontSize=Mathf.RoundToInt(fs);Place(s.heading.rectTransform,0,0,w,40);y=48;if(s.market!=null)y=LayoutMarket(s,w,y,fs);}
        float inset=advice||alert?66:0,tw=w-inset-(advice||alert?12:0),bh=MeasuredHeight(s.text,tw);
        Place(s.text.rectTransform,inset,y+(advice||alert?12:0),tw,bh+6);return Mathf.Max(y+bh+(advice||alert?28:10),advice||alert?76:0);
    }
    private static float MeasuredHeight(TMP_Text text, float width)
    {
        width = Mathf.Max(1, width);
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32767);
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.ForceMeshUpdate(true, true);
        return Mathf.Ceil(Mathf.Max(text.GetPreferredValues(text.text,width,32767).y,
            text.GetRenderedValues(false).y));
    }
    private float LayoutMarket(Section s,float w,float y,float fs)
    {
        float costWidth=120,changeWidth=105;
        foreach(var row in s.market){row.cost.fontSize=fs;row.change.fontSize=fs;costWidth=Mathf.Max(costWidth,row.cost.GetPreferredValues(row.cost.text).x+8);changeWidth=Mathf.Max(changeWidth,row.change.GetPreferredValues(row.change.text).x+8);}
        for(int i=0;i<3;i++)s.tableHeaders[i].fontSize=fs-4;
        costWidth=Mathf.Max(costWidth,s.tableHeaders[1].GetPreferredValues(s.tableHeaders[1].text).x+8);changeWidth=Mathf.Max(changeWidth,s.tableHeaders[2].GetPreferredValues(s.tableHeaders[2].text).x+8);
        bool stacked=costWidth+changeWidth+146>w;float itemWidth=stacked?w:w-costWidth-changeWidth-32;
        for(int i=0;i<3;i++)s.tableHeaders[i].gameObject.SetActive(!stacked);
        if(!stacked){float hh=Mathf.Max(32,Mathf.Max(s.tableHeaders[0].GetPreferredValues(s.tableHeaders[0].text,itemWidth,0).y,Mathf.Max(s.tableHeaders[1].GetPreferredValues(s.tableHeaders[1].text,costWidth,0).y,s.tableHeaders[2].GetPreferredValues(s.tableHeaders[2].text,changeWidth,0).y)));Place(s.tableHeaders[0].rectTransform,0,y,itemWidth,hh);Place(s.tableHeaders[1].rectTransform,itemWidth+8,y,costWidth,hh);Place(s.tableHeaders[2].rectTransform,w-changeWidth,y,changeWidth,hh);y+=hh+10;}
        foreach(var row in s.market){
            row.item.fontSize=fs+2;row.note.fontSize=fs-2;Place(row.rule.rectTransform,0,y,w,1);y+=14;
            float ih=Mathf.Max(32,MeasuredHeight(row.item,itemWidth));Place(row.item.rectTransform,0,y,itemWidth,ih+4);
            if(stacked){y+=ih+6;float ch=Mathf.Max(MeasuredHeight(row.cost,w),MeasuredHeight(row.change,w));Place(row.cost.rectTransform,0,y,w,ch+4);y+=ch+4;Place(row.change.rectTransform,0,y,w,ch+4);y+=ch+4;}
            else {float ch=Mathf.Max(ih,Mathf.Max(MeasuredHeight(row.cost,costWidth),MeasuredHeight(row.change,changeWidth)));Place(row.cost.rectTransform,itemWidth+8,y,costWidth,ch+4);Place(row.change.rectTransform,w-changeWidth,y,changeWidth,ch+4);y+=ch+8;}
            float nh=MeasuredHeight(row.note,w-4);Place(row.note.rectTransform,0,y,w-4,nh+4);y+=nh+22;
        }
        return y+12;
    }
}
