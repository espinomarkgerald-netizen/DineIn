using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Packages the approved generated artwork without redrawing its shapes or colors.
public static class BuildReceptionActionIcons
{
    static readonly Color Ink = Color.FromArgb(10, 30, 62);
    static readonly Color Cream = Color.FromArgb(255, 246, 226);
    static Bitmap Canvas(int w, int h) => new Bitmap(w, h, PixelFormat.Format32bppArgb);
    static Graphics Draw(Bitmap b)
    {
        var g = Graphics.FromImage(b);
        g.Clear(Color.Transparent);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        return g;
    }
    static Bitmap ImportIcon(string path)
    {
        using (var source = new Bitmap(path))
        {
            int left = source.Width, top = source.Height, right = -1, bottom = -1;
            for (int y = 0; y < source.Height; y++) for (int x = 0; x < source.Width; x++)
                if (source.GetPixel(x, y).A >= 16)
                { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
            if (right < left) throw new InvalidDataException("Approved icon has no opaque artwork: " + path);
            float scale = 208f / Math.Max(right - left + 1, bottom - top + 1);
            int width = (int)Math.Round((right - left + 1) * scale), height = (int)Math.Round((bottom - top + 1) * scale);
            var output = Canvas(256, 256);
            using (var g = Draw(output))
                g.DrawImage(source, new Rectangle((256 - width) / 2, (256 - height) / 2, width, height),
                    new Rectangle(left, top, right - left + 1, bottom - top + 1), GraphicsUnit.Pixel);
            return output;
        }
    }
    public static void Run(string pack)
    {
        string components = Path.Combine(pack, "Components");
        Directory.CreateDirectory(components);
        using (var greet = ImportIcon(Path.Combine(pack, "Sources", "GreetCustomerApproved.png"))) greet.Save(Path.Combine(components, "IconGreetCustomer.png"), ImageFormat.Png);
        using (var seat = ImportIcon(Path.Combine(pack, "Sources", "SeatCustomerApproved.png"))) seat.Save(Path.Combine(components, "IconSeatCustomer.png"), ImageFormat.Png);

        string atlasPath = Path.Combine(pack, "RestaurantControlsV1.png");
        string atlasTempPath = atlasPath + ".generated.png";
        using (var atlas = new Bitmap(atlasPath))
        using (var g = Graphics.FromImage(atlas))
        using (var clear = new SolidBrush(Color.Transparent))
        using (var greet = new Bitmap(Path.Combine(components, "IconGreetCustomer.png")))
        using (var seat = new Bitmap(Path.Combine(components, "IconSeatCustomer.png")))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.FillRectangle(clear, 704, 1744, 528, 256);
            g.DrawImageUnscaled(greet, 704, 1744);
            g.DrawImageUnscaled(seat, 976, 1744);
            atlas.Save(atlasTempPath, ImageFormat.Png);
        }
        File.Copy(atlasTempPath, atlasPath, true);
        File.Delete(atlasTempPath);

        string previewPath = Path.Combine(pack, "ReceptionActionsV1.preview.png");
        using (var preview = Canvas(960, 420))
        using (var g = Draw(preview))
        using (var titleFont = new Font("Arial", 24, FontStyle.Bold))
        using (var labelFont = new Font("Arial", 18, FontStyle.Bold))
        using (var titleBrush = new SolidBrush(Ink))
        using (var labelBrush = new SolidBrush(Cream))
        using (var greetBg = new Bitmap(Path.Combine(Path.GetDirectoryName(pack), "ActionButtonSpritePackV1", "Components", "ButtonCyanReady.png")))
        using (var seatBg = new Bitmap(Path.Combine(Path.GetDirectoryName(pack), "ActionButtonSpritePackV1", "Components", "ButtonGreenReady.png")))
        using (var greet = new Bitmap(Path.Combine(components, "IconGreetCustomer.png")))
        using (var seat = new Bitmap(Path.Combine(components, "IconSeatCustomer.png")))
        {
            g.Clear(Color.FromArgb(244, 236, 217));
            g.DrawString("RECEPTION ACTION ICONS", titleFont, titleBrush, 32, 22);
            g.DrawImage(greetBg, new Rectangle(40, 112, 408, 150));
            g.DrawImage(greet, new Rectangle(65, 125, 120, 120));
            g.DrawString("GREET CUSTOMER", labelFont, labelBrush, 185, 170);
            g.DrawImage(seatBg, new Rectangle(512, 112, 408, 150));
            g.DrawImage(seat, new Rectangle(537, 125, 120, 120));
            g.DrawString("SEAT TABLE", labelFont, labelBrush, 666, 170);
            g.DrawString("Transparent 256 x 256 pictograms; labels are preview-only.", new Font("Arial", 15), titleBrush, 42, 330);
            preview.Save(previewPath, ImageFormat.Png);
        }
    }
}
