using System.Drawing;
using System.Globalization;
using WechatStyleScreenshot.Core;

namespace WechatStyleScreenshot.Services;

public static class OcrRegionParser
{
    public static IReadOnlyList<OcrTextRegion> Parse(string tsv, Size imageSize)
    {
        var groups = new Dictionary<string, List<(string Text, Rectangle Bounds, float Confidence)>>();
        foreach (string row in tsv.Split('\n').Skip(1))
        {
            string[] cells = row.TrimEnd('\r').Split('\t');
            if (cells.Length < 12 || cells[0] != "5" || !float.TryParse(cells[10], NumberStyles.Float, CultureInfo.InvariantCulture, out float confidence) || confidence < 20) continue;
            string value = OcrTextProcessor.Clean(cells[11]);
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!int.TryParse(cells[6], out int x) || !int.TryParse(cells[7], out int y) ||
                !int.TryParse(cells[8], out int width) || !int.TryParse(cells[9], out int height)) continue;
            Rectangle bounds = Rectangle.Intersect(new Rectangle(x, y, width, height), new Rectangle(Point.Empty, imageSize));
            if (bounds.Width < 1 || bounds.Height < 1) continue;
            string key = string.Join(':', cells[1], cells[2], cells[3], cells[4]);
            if (!groups.TryGetValue(key, out var words)) groups[key] = words = [];
            words.Add((value, bounds, confidence));
        }
        var result = new List<OcrTextRegion>();
        foreach (var words in groups.Values)
        {
            string text = string.Join(" ", words.Select(word => word.Text));
            Rectangle bounds = words.Select(word => word.Bounds).Aggregate(Rectangle.Union);
            result.Add(new OcrTextRegion(result.Count, text, bounds, words.Average(word => word.Confidence)));
        }
        return result;
    }
}
