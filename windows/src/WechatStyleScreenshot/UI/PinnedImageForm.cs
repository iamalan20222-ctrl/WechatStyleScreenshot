using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.UI;

public sealed class PinnedImageForm : Form
{
    private const int HandleSize = 12;
    private readonly Bitmap _originalImage;
    private readonly ClipboardManager _clipboard;
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _topmostItem;
    private bool _hovered;
    private bool _dragging;
    private bool _resizing;
    private Point _pointerStart;
    private Point _windowStart;
    private Size _sizeStart;
    private float _scale;

    public PinnedImageForm(Bitmap image, Point location, ClipboardManager clipboard)
    {
        ArgumentNullException.ThrowIfNull(image);
        _originalImage = new Bitmap(image);
        _clipboard = clipboard;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        _scale = Math.Min(1f, FitScale(location));
        Size = SizeForScale(_scale);
        Location = ClampVisible(location, Size);
        _menu.Items.Add("复制", null, (_, _) => CopyImage());
        _menu.Items.Add("保存图片", null, (_, _) => SaveImage());
        _menu.Items.Add("恢复原始大小", null, (_, _) => RestoreOriginalSize());
        _topmostItem = new ToolStripMenuItem("保持置顶") { CheckOnClick = true, Checked = true };
        _topmostItem.CheckedChanged += (_, _) => TopMost = _topmostItem.Checked;
        _menu.Items.Add(_topmostItem);
        _menu.Items.Add("关闭", null, (_, _) => Close());
        ContextMenuStrip = _menu;
    }

    internal Bitmap CloneOriginalForTesting() => new(_originalImage);
    internal bool CopyOriginalForTesting() => _clipboard.TrySetImage(_originalImage);
    internal float ScaleForTesting => _scale;
    internal void ZoomForTesting(Point anchor, float scale) => SetScale(anchor, scale);
    internal void RestoreForTesting() => RestoreOriginalSize();
    internal void SaveOriginalImage(string path) => _originalImage.Save(path, ImageFormat.Png);
    internal void ToggleTopMostForTesting() => _topmostItem.Checked = !_topmostItem.Checked;
    internal void DragToForTesting(Point pointer) => MoveTo(pointer);
    internal void ResizeToForTesting(Point pointer) => ResizeTo(pointer);
    internal void StartDragForTesting(Point pointer) { _dragging = true; _pointerStart = pointer; _windowStart = Location; }
    internal void StartResizeForTesting(Point pointer) { _resizing = true; _pointerStart = pointer; _sizeStart = Size; }
    internal void PressEscapeForTesting() => OnKeyDown(new KeyEventArgs(Keys.Escape));

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.InterpolationMode = _scale >= 1f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(_originalImage, ClientRectangle);
        if (!_hovered && !_resizing) return;
        using Pen border = new(Color.FromArgb(130, Color.White));
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        Rectangle handle = ResizeHandle;
        using SolidBrush grip = new(Color.FromArgb(190, 30, 30, 30));
        e.Graphics.FillRectangle(grip, handle);
        using Pen line = new(Color.White, 1.2f);
        e.Graphics.DrawLine(line, handle.Right - 8, handle.Bottom - 2, handle.Right - 2, handle.Bottom - 8);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_dragging || _resizing) return;
        _hovered = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _pointerStart = PointToScreen(e.Location);
        if (ResizeHandle.Contains(e.Location))
        {
            _resizing = true;
            _sizeStart = Size;
            Cursor = Cursors.SizeNWSE;
        }
        else
        {
            _dragging = true;
            _windowStart = Location;
            Cursor = Cursors.SizeAll;
        }
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point pointer = Cursor.Position;
        if (_resizing) ResizeTo(pointer);
        else if (_dragging) MoveTo(pointer);
        else Cursor = ResizeHandle.Contains(e.Location) ? Cursors.SizeNWSE : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        _dragging = _resizing = false;
        Capture = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (Capture) return;
        _dragging = _resizing = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.Delta == 0) return;
        float factor = MathF.Pow(1.1f, e.Delta / 120f);
        SetScale(PointToScreen(e.Location), _scale * factor);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
    }

    private Rectangle ResizeHandle => new(Math.Max(0, Width - HandleSize), Math.Max(0, Height - HandleSize), HandleSize, HandleSize);

    private void MoveTo(Point pointer)
    {
        if (!_dragging) return;
        Point next = new(_windowStart.X + pointer.X - _pointerStart.X, _windowStart.Y + pointer.Y - _pointerStart.Y);
        Location = ClampVisible(next, Size);
    }

    private void ResizeTo(Point pointer)
    {
        if (!_resizing) return;
        float wantedWidth = _sizeStart.Width + pointer.X - _pointerStart.X;
        SetScale(new Point(Left, Top), wantedWidth / _originalImage.Width);
    }

    private void SetScale(Point screenAnchor, float scale)
    {
        float next = Math.Clamp(scale, 0.25f, 4f);
        if (Math.Abs(next - _scale) < 0.0001f) return;
        float ratioX = Width > 0 ? (screenAnchor.X - Left) / (float)Width : 0;
        float ratioY = Height > 0 ? (screenAnchor.Y - Top) / (float)Height : 0;
        Size nextSize = SizeForScale(next);
        Point nextLocation = new((int)Math.Round(screenAnchor.X - ratioX * nextSize.Width),
            (int)Math.Round(screenAnchor.Y - ratioY * nextSize.Height));
        _scale = next;
        Size = nextSize;
        Location = ClampVisible(nextLocation, nextSize);
        Invalidate();
    }

    private Size SizeForScale(float scale) => new(Math.Max(1, (int)Math.Round(_originalImage.Width * scale)),
        Math.Max(1, (int)Math.Round(_originalImage.Height * scale)));

    private float FitScale(Point location)
    {
        Rectangle work = Screen.FromPoint(location).WorkingArea;
        return Math.Min(work.Width * 0.8f / _originalImage.Width, work.Height * 0.8f / _originalImage.Height);
    }

    private void RestoreOriginalSize() => SetScale(new Point(Left + Width / 2, Top + Height / 2), Math.Min(1f, FitScale(Location)));

    internal static Point ClampVisible(Point desired, Size size, IEnumerable<Rectangle>? workingAreas = null)
    {
        Rectangle[] areas = (workingAreas ?? Screen.AllScreens.Select(screen => screen.WorkingArea)).ToArray();
        if (areas.Length == 0) return desired;
        const int visible = 28;
        return areas.Select(area => new Point(
                Math.Clamp(desired.X, area.Left - Math.Max(0, size.Width - visible), area.Right - visible),
                Math.Clamp(desired.Y, area.Top - Math.Max(0, size.Height - visible), area.Bottom - visible)))
            .OrderBy(point => (long)(point.X - desired.X) * (point.X - desired.X) + (long)(point.Y - desired.Y) * (point.Y - desired.Y))
            .First();
    }

    private void CopyImage()
    {
        if (!_clipboard.TrySetImage(_originalImage)) MessageBox.Show(this, "复制失败，请重试", "贴图", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void SaveImage()
    {
        using SaveFileDialog dialog = new() { Filter = "PNG 图片|*.png", DefaultExt = "png", AddExtension = true,
            FileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { SaveOriginalImage(dialog.FileName); }
        catch (Exception) { MessageBox.Show(this, "保存失败，请检查目标位置", "贴图", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _menu.Dispose(); _originalImage.Dispose(); }
        base.Dispose(disposing);
    }
}
