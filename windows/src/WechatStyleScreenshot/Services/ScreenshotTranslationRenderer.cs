using System.Drawing;
using System.Drawing.Imaging;
using WechatStyleScreenshot.Core;

namespace WechatStyleScreenshot.Services;

public static class ScreenshotTranslationRenderer
{
    public static Bitmap Render(Bitmap source, IReadOnlyList<OcrTextRegion> regions, IReadOnlyDictionary<int, string> translations)
    {
        Bitmap output = source.Clone(new Rectangle(Point.Empty, source.Size), PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(output);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        foreach (OcrTextRegion region in regions)
        {
            if (!translations.TryGetValue(region.Id, out string? text) || text == region.Text || string.IsNullOrWhiteSpace(text)) continue;
            Rectangle bounds = Rectangle.Intersect(new Rectangle(region.Bounds.X - 2, region.Bounds.Y - 2,
                region.Bounds.Width + 4, region.Bounds.Height + 4), new Rectangle(Point.Empty, source.Size));
            if (bounds.Width < 2 || bounds.Height < 2) continue;
            Color background = EstimateBackground(source, bounds);
            using SolidBrush fill = new(background);
            graphics.FillRectangle(fill, bounds);
            int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            using SolidBrush ink = new(brightness < 130 ? Color.White : Color.Black);
            using StringFormat format = new() { Trimming = StringTrimming.EllipsisCharacter, Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center };
            float size = Math.Clamp(region.Bounds.Height * 0.8f, 7f, 36f);
            Font font;
            do
            {
                font = new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Pixel);
                SizeF measured = graphics.MeasureString(text, font, Math.Max(1, bounds.Width), format);
                if (measured.Height <= bounds.Height || size <= 7f) break;
                font.Dispose();
                size -= 1f;
            } while (true);
            using (font) graphics.DrawString(text, font, ink, bounds, format);
        }
        return output;
    }

    private static Color EstimateBackground(Bitmap image, Rectangle bounds)
    {
        long r = 0, g = 0, b = 0;
        int count = 0;
        for (int x = bounds.Left; x < bounds.Right; x += Math.Max(1, bounds.Width / 12))
        {
            Color top = image.GetPixel(x, bounds.Top);
            Color bottom = image.GetPixel(x, bounds.Bottom - 1);
            r += top.R + bottom.R; g += top.G + bottom.G; b += top.B + bottom.B; count += 2;
        }
        for (int y = bounds.Top; y < bounds.Bottom; y += Math.Max(1, bounds.Height / 6))
        {
            Color left = image.GetPixel(bounds.Left, y);
            Color right = image.GetPixel(bounds.Right - 1, y);
            r += left.R + right.R; g += left.G + right.G; b += left.B + right.B; count += 2;
        }
        return Color.FromArgb((int)(r / count), (int)(g / count), (int)(b / count));
    }
}
