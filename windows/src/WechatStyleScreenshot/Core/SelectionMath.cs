using System.Drawing;

namespace WechatStyleScreenshot.Core;

public static class SelectionMath
{
    public const int MinimumCaptureSize = 2;

    public static Rectangle FromPoints(Point start, Point end)
    {
        int left = Math.Min(start.X, end.X);
        int top = Math.Min(start.Y, end.Y);
        int right = Math.Max(start.X, end.X);
        int bottom = Math.Max(start.Y, end.Y);

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    public static bool IsCapturable(Rectangle rectangle)
    {
        return rectangle.Width >= MinimumCaptureSize && rectangle.Height >= MinimumCaptureSize;
    }

    public static IReadOnlyList<Rectangle> GetHandleRectangles(Rectangle rectangle, int handleSize)
    {
        if (handleSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(handleSize), "Handle size must be positive.");
        }

        int half = handleSize / 2;
        int centerX = rectangle.Left + rectangle.Width / 2;
        int centerY = rectangle.Top + rectangle.Height / 2;

        Point[] points =
        [
            new(rectangle.Left, rectangle.Top),
            new(centerX, rectangle.Top),
            new(rectangle.Right, rectangle.Top),
            new(rectangle.Right, centerY),
            new(rectangle.Right, rectangle.Bottom),
            new(centerX, rectangle.Bottom),
            new(rectangle.Left, rectangle.Bottom),
            new(rectangle.Left, centerY)
        ];

        return points
            .Select(point => new Rectangle(point.X - half, point.Y - half, handleSize, handleSize))
            .ToArray();
    }

    public static SelectionHitTarget HitTest(Rectangle rectangle, Point point, int handleSize)
    {
        SelectionHitTarget[] targets =
        [
            SelectionHitTarget.TopLeft,
            SelectionHitTarget.Top,
            SelectionHitTarget.TopRight,
            SelectionHitTarget.Right,
            SelectionHitTarget.BottomRight,
            SelectionHitTarget.Bottom,
            SelectionHitTarget.BottomLeft,
            SelectionHitTarget.Left
        ];

        IReadOnlyList<Rectangle> handles = GetHandleRectangles(rectangle, handleSize);
        for (int i = 0; i < handles.Count; i++)
        {
            if (handles[i].Contains(point))
            {
                return targets[i];
            }
        }

        return rectangle.Contains(point) ? SelectionHitTarget.Move : SelectionHitTarget.None;
    }

    public static Rectangle ApplyDrag(Rectangle rectangle, SelectionHitTarget target, Size delta, Rectangle bounds)
    {
        Rectangle next = rectangle;

        switch (target)
        {
            case SelectionHitTarget.Move:
                next.Offset(delta.Width, delta.Height);
                next.X = Math.Clamp(next.X, bounds.Left, bounds.Right - next.Width);
                next.Y = Math.Clamp(next.Y, bounds.Top, bounds.Bottom - next.Height);
                return next;
            case SelectionHitTarget.TopLeft:
                next = Rectangle.FromLTRB(rectangle.Left + delta.Width, rectangle.Top + delta.Height, rectangle.Right, rectangle.Bottom);
                break;
            case SelectionHitTarget.Top:
                next = Rectangle.FromLTRB(rectangle.Left, rectangle.Top + delta.Height, rectangle.Right, rectangle.Bottom);
                break;
            case SelectionHitTarget.TopRight:
                next = Rectangle.FromLTRB(rectangle.Left, rectangle.Top + delta.Height, rectangle.Right + delta.Width, rectangle.Bottom);
                break;
            case SelectionHitTarget.Right:
                next = Rectangle.FromLTRB(rectangle.Left, rectangle.Top, rectangle.Right + delta.Width, rectangle.Bottom);
                break;
            case SelectionHitTarget.BottomRight:
                next = Rectangle.FromLTRB(rectangle.Left, rectangle.Top, rectangle.Right + delta.Width, rectangle.Bottom + delta.Height);
                break;
            case SelectionHitTarget.Bottom:
                next = Rectangle.FromLTRB(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom + delta.Height);
                break;
            case SelectionHitTarget.BottomLeft:
                next = Rectangle.FromLTRB(rectangle.Left + delta.Width, rectangle.Top, rectangle.Right, rectangle.Bottom + delta.Height);
                break;
            case SelectionHitTarget.Left:
                next = Rectangle.FromLTRB(rectangle.Left + delta.Width, rectangle.Top, rectangle.Right, rectangle.Bottom);
                break;
        }

        next = Rectangle.Intersect(next, bounds);
        if (next.Width < MinimumCaptureSize || next.Height < MinimumCaptureSize)
        {
            return rectangle;
        }

        return next;
    }

    public static SelectionMouseAction GetMouseAction(MouseButtons button, int clicks, bool hasSelection)
    {
        if (button == MouseButtons.Right)
        {
            return SelectionMouseAction.Cancel;
        }

        if (hasSelection && button == MouseButtons.Left && clicks >= 2)
        {
            return SelectionMouseAction.Confirm;
        }

        return SelectionMouseAction.None;
    }

    public static ToolbarButtonHit HitTestToolbarButtons(Rectangle cancelButton, Rectangle confirmButton, Point point)
    {
        if (cancelButton.Contains(point))
        {
            return ToolbarButtonHit.Cancel;
        }

        if (confirmButton.Contains(point))
        {
            return ToolbarButtonHit.Confirm;
        }

        return ToolbarButtonHit.None;
    }
}
