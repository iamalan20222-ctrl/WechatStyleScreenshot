using System.Drawing;

namespace WechatStyleScreenshot.Core;

public enum AnnotationTool { None, Arrow }

public sealed record ArrowAnnotation(PointF Start, PointF End, Color Color, float Thickness);

public sealed class AnnotationSession
{
    private readonly List<ArrowAnnotation> _arrows = [];
    public AnnotationTool Tool { get; set; }
    public IReadOnlyList<ArrowAnnotation> Arrows => _arrows;

    public bool Add(PointF start, PointF end, Color color, float thickness)
    {
        if (MathF.Sqrt(MathF.Pow(end.X - start.X, 2) + MathF.Pow(end.Y - start.Y, 2)) < 6f)
            return false;
        _arrows.Add(new ArrowAnnotation(start, end, color, thickness));
        return true;
    }

    public bool Undo()
    {
        if (_arrows.Count == 0) return false;
        _arrows.RemoveAt(_arrows.Count - 1);
        return true;
    }

    public void Clear() => _arrows.Clear();
}
