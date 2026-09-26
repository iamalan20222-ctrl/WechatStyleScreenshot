using System.Drawing;
using System.Drawing.Drawing2D;
using WechatStyleScreenshot.Core;

namespace WechatStyleScreenshot.Services;

public static class AnnotationRenderer
{
    public static void Draw(Graphics graphics, IEnumerable<ArrowAnnotation> arrows, Rectangle bounds)
    {
        GraphicsState state = graphics.Save();
        graphics.SetClip(bounds);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (ArrowAnnotation arrow in arrows)
            DrawArrow(graphics, arrow, bounds.Location);
        graphics.Restore(state);
    }

    private static void DrawArrow(Graphics graphics, ArrowAnnotation arrow, Point origin)
    {
        PointF start = new(origin.X + arrow.Start.X, origin.Y + arrow.Start.Y);
        PointF end = new(origin.X + arrow.End.X, origin.Y + arrow.End.Y);
        float dx = end.X - start.X, dy = end.Y - start.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 6f) return;
        float ux = dx / length, uy = dy / length;
        float headLength = MathF.Min(length * .45f, MathF.Max(10f, arrow.Thickness * 4f));
        float headWidth = MathF.Max(5f, arrow.Thickness * 1.8f);
        PointF basePoint = new(end.X - ux * headLength, end.Y - uy * headLength);
        PointF left = new(basePoint.X - uy * headWidth, basePoint.Y + ux * headWidth);
        PointF right = new(basePoint.X + uy * headWidth, basePoint.Y - ux * headWidth);
        using Pen pen = new(arrow.Color, arrow.Thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using SolidBrush brush = new(arrow.Color);
        graphics.DrawLine(pen, start, basePoint);
        graphics.FillPolygon(brush, [end, left, right]);
    }
}
