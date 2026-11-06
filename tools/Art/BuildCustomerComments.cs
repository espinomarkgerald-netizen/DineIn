using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Uses the same rounded vector primitives/palette as the existing action pack.
public static class BuildCustomerComments
{
    static readonly Color Ink = Color.FromArgb(10, 30, 62);
    static readonly Color Cream = Color.FromArgb(255, 246, 226);
    static GraphicsPath Rounded(float x, float y, float w, float h, float radius)
    {
        var p = new GraphicsPath(); float d = radius * 2;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    static void Box(Graphics g, float x, float y, float w, float h, float r, Color color)
    { using (var p = Rounded(x, y, w, h, r)) using (var b = new SolidBrush(color)) g.FillPath(b, p); }
    static Bitmap Canvas(int w, int h) => new Bitmap(w, h, PixelFormat.Format32bppArgb);
    static Graphics Draw(Bitmap b)
    {
        var g = Graphics.FromImage(b); g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality; return g;
    }
    public static void Run(string root)
    {
        Directory.CreateDirectory(root + "/Components");
        string[] names = { "Happy", "Neutral", "Angry" };
        Color[] accents = { Color.FromArgb(87, 210, 150), Color.FromArgb(255, 201, 61), Color.FromArgb(255, 108, 92) };
        using (var atlas = Canvas(1024, 1024)) using (var sheet = Draw(atlas))
        {
            for (int i = 0; i < 3; i++)
            {
                using (var panel = Canvas(512, 256))
                {
                    using (var g = Draw(panel))
                    {
                        Box(g, 12, 18, 488, 230, 56, Color.FromArgb(42, Ink));
                        Box(g, 8, 8, 496, 232, 56, Ink);
                        Box(g, 14, 14, 484, 220, 50, accents[i]);
                        Box(g, 22, 22, 468, 204, 43, Color.White);
                        Box(g, 25, 25, 462, 198, 40, Cream);
                    }
                    panel.Save(root + "/Components/Comment" + names[i] + "Panel.png", ImageFormat.Png);
                    sheet.DrawImageUnscaled(panel, 16, 16 + i * 272);
                }
                using (var tail = Canvas(128, 96))
                {
                    using (var g = Draw(tail))
                    {
                        var outer = new[] { new Point(10, 2), new Point(118, 2), new Point(64, 86) };
                        using (var b = new SolidBrush(accents[i])) g.FillPolygon(b, outer);
                        using (var pen = new Pen(Ink, 7) { LineJoin = LineJoin.Round }) g.DrawLines(pen, new[] { outer[0], outer[2], outer[1] });
                        using (var b = new SolidBrush(Cream)) g.FillPolygon(b, new[] { new Point(27, 0), new Point(101, 0), new Point(64, 62) });
                    }
                    tail.Save(root + "/Components/Comment" + names[i] + "Tail.png", ImageFormat.Png);
                    sheet.DrawImageUnscaled(tail, 560, 16 + i * 144);
                }
                using (var badge = Canvas(128, 128))
                {
                    using (var g = Draw(badge))
                    {
                        using (var b = new SolidBrush(Ink)) g.FillEllipse(b, 4, 4, 120, 120);
                        using (var b = new SolidBrush(accents[i])) g.FillEllipse(b, 9, 9, 110, 110);
                        using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, 16, 16, 96, 96);
                        using (var b = new SolidBrush(Cream)) g.FillEllipse(b, 19, 19, 90, 90);
                    }
                    badge.Save(root + "/Components/Comment" + names[i] + "Badge.png", ImageFormat.Png);
                    sheet.DrawImageUnscaled(badge, 720, 16 + i * 144);
                }
            }
            atlas.Save(root + "/CustomerCommentsV1.png", ImageFormat.Png);
        }
    }
}
