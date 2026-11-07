using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

// Artwork only. All production pieces are blank beneath editable UI labels.
public static class BuildCustomerDrawerArt
{
    static readonly Color Ink = C("102445"), Cream = C("FFF5DF"), Cyan = C("14BCE5"), Pale = C("DEF5F5");
    static string root;
    static Color C(string value) => ColorTranslator.FromHtml("#" + value);
    static GraphicsPath Round(float x,float y,float w,float h,float radius)
    {
        var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(w,h));
        p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);
        p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;
    }
    static Graphics G(Bitmap b)
    {
        var g=Graphics.FromImage(b);g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;return g;
    }
    static void Box(Graphics g,float x,float y,float w,float h,float r,Color color)
    {using(var p=Round(x,y,w,h,r))using(var b=new SolidBrush(color))g.FillPath(b,p);}
    static void Line(Graphics g,Color color,float width,params PointF[] points)
    {using(var p=new Pen(color,width){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})g.DrawLines(p,points);}
    static void Save(string name,int w,int h,Action<Graphics> draw)
    {using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){using(var g=G(b))draw(g);b.Save(Path.Combine(root,"Components",name+".png"),ImageFormat.Png);}}
    static void Pill(string name,int w,int h,Color edge,Color fill)
    {Save(name,w,h,g=>{Box(g,4,6,w-8,h-10,(h-10)/2,edge);Box(g,8,10,w-16,h-18,(h-18)/2,fill);});}
    public static void Run(string project,string output)
    {
        root=output;Directory.CreateDirectory(Path.Combine(root,"Components"));
        Save("PanelFrame",512,512,g=>{
            Box(g,8,12,496,492,42,C("CABFA9"));Box(g,6,6,500,496,42,Ink);
            Box(g,10,10,492,488,38,Cyan);Box(g,16,16,480,476,32,Color.White);Box(g,19,19,474,470,29,Cream);
        });
        Pill("HeaderTab",384,80,Color.White,Ink);
        Pill("TableBadge",192,64,Ink,Ink);
        Save("PortraitTile",128,128,g=>Box(g,4,4,120,120,20,C("F7E7D1")));
        Save("SelectedRow",256,96,g=>Box(g,2,2,252,92,12,C("CFF2EF")));
        Save("HoverRow",256,96,g=>Box(g,2,2,252,92,12,C("E9F5EF")));
        Save("StatusStrip",384,72,g=>{Box(g,3,3,378,66,20,C("8BBFC5"));Box(g,5,5,374,62,18,Pale);});
        Save("Divider",256,8,g=>Box(g,2,2,252,4,2,C("C7AD8B")));
        string[] states={"Ready","Pressed","Disabled","Focused"};
        Color[] nav={Pale,C("ABDCDD"),C("D9D9D2"),C("BDEFF0")};
        Color[] secondary={C("E3EAF0"),C("C0CFDB"),C("D5D7D6"),C("D7F3F5")};
        for(int i=0;i<states.Length;i++) {
            int n=i;Pill("NavButton"+states[i],256,64,i==3?Cyan:nav[i],nav[i]);
            Save("SecondaryButton"+states[i],384,96,g=>{
                Box(g,4,8,376,84,36,n==3?Cyan:Ink);Box(g,8,12,368,76,32,Color.White);
                Box(g,12,16,360,68,28,secondary[n]);
                Box(g,18,55,348,23,11,n==1?C("B1C0CD"):C("CCD6DF"));
            });
        }
        Save("ScrollTrack",24,128,g=>{Box(g,4,4,16,120,8,C("AEBABD"));Box(g,7,7,10,114,5,C("E8ECE6"));});
        Save("ScrollThumb",24,64,g=>{Box(g,4,4,16,56,8,C("567480"));Box(g,7,7,7,50,3,C("829CA3"));});
        Save("PatienceTrack",256,32,g=>{Box(g,2,2,252,28,14,C("74868D"));Box(g,5,5,246,22,11,C("DEE4DF"));});
        string[] fills={"Green","Gold","Coral","Tintable"};
        Color[] colors={C("44BC50"),C("F2BC32"),C("EE7969"),Color.White};
        for(int i=0;i<fills.Length;i++){int n=i;Save("PatienceFill"+fills[i],256,32,g=>{
            Box(g,3,3,250,26,13,colors[n]);Box(g,8,6,240,5,2,Color.FromArgb(70,Color.White));
        });}
        foreach(bool left in new[]{true,false}) {
            Save(left?"ChevronLeft":"ChevronRight",64,64,g=>Line(g,Ink,8,new PointF(left?39:25,14),new PointF(left?23:41,32),new PointF(left?39:25,50)));
            Save(left?"PointerLeft":"PointerRight",40,56,g=>{
                var pts=new[]{new PointF(left?34:6,5),new PointF(left?5:35,28),new PointF(left?34:6,51)};
                using(var p=new GraphicsPath()){p.AddPolygon(pts);using(var b=new SolidBrush(Cream))g.FillPath(b,p);using(var pen=new Pen(Cyan,5){LineJoin=LineJoin.Round})g.DrawPath(pen,p);}
            });
        }
        Save("IconLocationPin",128,128,g=>{
            using(var p=new GraphicsPath()){p.AddBezier(64,116,43,91,18,67,25,42);p.AddBezier(25,42,35,6,94,6,103,42);p.AddBezier(103,42,111,67,85,91,64,116);p.CloseFigure();using(var b=new SolidBrush(C("FFCB42")))g.FillPath(b,p);using(var pen=new Pen(Ink,7){LineJoin=LineJoin.Round})g.DrawPath(pen,p);}
            using(var b=new SolidBrush(Cream))g.FillEllipse(b,48,35,32,32);using(var pen=new Pen(Ink,6))g.DrawEllipse(pen,48,35,32,32);
        });
        Save("IconCashier",256,256,g=>{
            Box(g,85,40,90,65,12,Ink);Box(g,95,50,70,45,5,Cyan);
            Box(g,112,101,30,25,5,Ink);
            using(var p=new GraphicsPath()){p.AddPolygon(new[]{new PointF(62,120),new PointF(193,120),new PointF(221,206),new PointF(35,206)});using(var b=new SolidBrush(C("DCE5E9")))g.FillPath(b,p);using(var pen=new Pen(Ink,10){LineJoin=LineJoin.Round})g.DrawPath(pen,p);}
            Box(g,70,142,68,34,6,Ink);Box(g,78,149,52,20,3,C("FFF3CF"));
            for(int y=0;y<2;y++)for(int x=0;x<2;x++)Box(g,155+x*20,141+y*21,13,13,3,x==1?C("FFCA40"):Cyan);
            Line(g,Ink,6,new PointF(62,190),new PointF(192,190));
        });
        Preview(project);
    }
    static void Image(Graphics g,string name,Rectangle rect)
    {using(var b=new Bitmap(Path.Combine(root,"Components",name+".png")))g.DrawImage(b,rect);}
    static void Nine(Graphics g,string name,Rectangle d,int l,int b,int r,int t)
    {
        using(var img=new Bitmap(Path.Combine(root,"Components",name+".png"))) {
            int[] sx={0,l,img.Width-r,img.Width},sy={0,t,img.Height-b,img.Height};
            int[] dx={d.X,d.X+l/2,d.Right-r/2,d.Right},dy={d.Y,d.Y+t/2,d.Bottom-b/2,d.Bottom};
            for(int y=0;y<3;y++)for(int x=0;x<3;x++)g.DrawImage(img,new Rectangle(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]),new Rectangle(sx[x],sy[y],sx[x+1]-sx[x],sy[y+1]-sy[y]),GraphicsUnit.Pixel);
        }
    }
    static void Preview(string project)
    {
        using(var canvas=new Bitmap(1100,500,PixelFormat.Format32bppArgb))using(var g=G(canvas))
        using(var fonts=new PrivateFontCollection()) {
            fonts.AddFontFile(Path.Combine(project,"Assets/_Project/UI/Assets/Fonts/Fonts/LilitaOne-Regular.ttf"));
            using(var heading=new Font(fonts.Families[0],14))using(var body=new Font("Arial",10,FontStyle.Bold))using(var small=new Font("Arial",9))using(var ink=new SolidBrush(Ink))using(var white=new SolidBrush(Cream)) {
                g.Clear(C("17263A"));g.DrawString("CUSTOMER DRAWER / SPRITE PACK V1",heading,white,24,16);
                for(int n=0;n<2;n++){int x=24+n*350;Nine(g,"PanelFrame",new Rectangle(x,76,306,330),48,48,48,48);Nine(g,"HeaderTab",new Rectangle(x+15,65,204,34),32,24,32,24);g.DrawString(n==0?"CUSTOMERS":"CUSTOMER DETAILS",heading,white,x+29,72);}
                string[] names={"Pink","Green","Blue"},status={"Waiting to seat","Waiting for food","Eating"};
                for(int i=0;i<3;i++){int y=114+i*88; if(i==1)Nine(g,"SelectedRow",new Rectangle(36,y-3,280,84),16,16,16,16);
                    Image(g,"IconGuests",new Rectangle(42,y+4,42,42));g.DrawString(names[i]+" group",heading,ink,91,y);g.DrawString((i==0?3:i==1?2:4)+" guests  |  "+(i==0?"Not seated":"T"+(i==1?6:8)),small,ink,92,y+26);g.DrawString(status[i],body,ink,92,y+42);
                    Image(g,i==0?"IconNeutral":"IconHappy",new Rectangle(49,y+51,24,24));Nine(g,"PatienceTrack",new Rectangle(92,y+65,196,13),16,12,16,12);Nine(g,i==0?"PatienceFillGold":"PatienceFillGreen",new Rectangle(94,y+67,i==0?115:170,9),16,12,16,12);
                    if(i<2)Nine(g,"Divider",new Rectangle(40,y+83,270,4),4,3,4,3);
                }
                Nine(g,"ScrollTrack",new Rectangle(309,111,12,270),8,12,8,12);Nine(g,"ScrollThumb",new Rectangle(309,111,12,84),8,12,8,12);
                int d=374;Image(g,"ChevronLeft",new Rectangle(d+18,105,18,18));g.DrawString("All groups",small,ink,d+40,107);
                Image(g,"IconGuests",new Rectangle(d+19,133,42,42));g.DrawString("Green group",heading,ink,d+75,133);g.DrawString("2 guests",body,ink,d+76,160);
                Nine(g,"TableBadge",new Rectangle(d+228,148,55,24),24,16,24,16);g.DrawString("T6",body,white,d+246,153);
                Nine(g,"StatusStrip",new Rectangle(d+14,188,278,34),24,20,24,20);Image(g,"IconCloche",new Rectangle(d+21,190,31,29));g.DrawString("Waiting for food",body,ink,d+61,198);
                Image(g,"IconHappy",new Rectangle(d+20,235,25,25));g.DrawString("Happy",body,ink,d+49,240);Image(g,"IconClock",new Rectangle(d+134,235,24,24));g.DrawString("85%",body,ink,d+165,240);
                Nine(g,"PatienceTrack",new Rectangle(d+136,267,147,13),16,12,16,12);Nine(g,"PatienceFillGreen",new Rectangle(d+138,269,121,9),16,12,16,12);
                Image(g,"IconCheck",new Rectangle(d+20,285,25,25));g.DrawString("Order taken",body,ink,d+53,290);Image(g,"IconWaiting",new Rectangle(d+20,316,25,25));g.DrawString("Food not served",body,ink,d+53,321);
                Nine(g,"SecondaryButtonReady",new Rectangle(d+56,353,194,38),64,32,64,32);Image(g,"IconLocationPin",new Rectangle(d+67,355,32,32));g.DrawString("Find table",body,ink,d+111,366);
                Nine(g,"PanelFrame",new Rectangle(724,103,348,146),48,48,48,48);Nine(g,"HeaderTab",new Rectangle(761,92,207,34),32,24,32,24);g.DrawString("CURRENT TASK",heading,white,790,99);
                Image(g,"IconCash",new Rectangle(743,139,44,44));g.DrawString("Take payment",heading,ink,797,150);Nine(g,"Divider",new Rectangle(743,188,304,4),4,3,4,3);Image(g,"IconCashier",new Rectangle(744,199,32,32));g.DrawString("To cashier",body,ink,791,211);
                Nine(g,"TableBadge",new Rectangle(963,202,87,27),24,16,24,16);g.DrawString("TABLE 6",small,white,979,209);
                g.DrawString("Preview labels only. Production artwork is blank.",body,white,27,446);
                g.DrawString("Use the real customerImage portraits from CustomerTypeProfile.",small,white,27,469);
                canvas.Save(Path.Combine(root,"CustomerDrawerV1.preview.png"),ImageFormat.Png);
            }
        }
    }
}
