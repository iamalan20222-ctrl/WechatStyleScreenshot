using System.Drawing.Drawing2D;
using System.Reflection;

namespace WechatStyleScreenshot.UI;

internal static class PinIcon
{
    private static readonly Lazy<Bitmap> Source = new(() =>
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WechatStyleScreenshot.Assets.pin.png")
            ?? throw new InvalidOperationException("Embedded pin icon is missing.");
        using Bitmap loaded = new(stream);
        return new Bitmap(loaded);
    });
    private static readonly Dictionary<int, Bitmap> Cache = [];

    internal static Bitmap Get(int size)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(size, out Bitmap? bitmap)) return bitmap;
            Bitmap scaled = new(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(scaled);
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(Source.Value, new Rectangle(0, 0, size, size));
            return Cache[size] = scaled;
        }
    }

    internal static Bitmap Original => Source.Value;
}
