using System.Drawing;

namespace WechatStyleScreenshot.Core;

public static class PinButtonLayout
{
    public static Rectangle GetBounds(Rectangle selection, Rectangle viewport, int dpi)
    {
        int size = Math.Max(30, (int)Math.Round(32 * dpi / 96d));
        int gap = Math.Max(6, (int)Math.Round(10 * dpi / 96d));
        int y = Math.Clamp(selection.Top + gap, viewport.Top, Math.Max(viewport.Top, viewport.Bottom - size));
        if (selection.Width < size + 48 || selection.Height < size + 28)
        {
            if (selection.Right + gap + size <= viewport.Right)
                return new Rectangle(selection.Right + gap, y, size, size);
            if (selection.Left - gap - size >= viewport.Left)
                return new Rectangle(selection.Left - gap - size, y, size, size);
        }
        int x = Math.Clamp(selection.Right - size - gap, viewport.Left, Math.Max(viewport.Left, viewport.Right - size));
        return new Rectangle(x, y, size, size);
    }
}
