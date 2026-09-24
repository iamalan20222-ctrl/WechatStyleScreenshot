using System.Drawing;
using WechatStyleScreenshot.Core;

namespace WechatStyleScreenshot.UI;

public sealed class ScreenshotOverlayForm : Form
{
    private const int HandleSize = 9;
    private const int ToolbarWidth = 202;
    private const int ToolbarHeight = 46;
    private const int ToolbarGap = 14;
    private const int ToolbarButtonSize = 30;
    private static readonly Color AccentColor = Color.FromArgb(46, 190, 112);
    private static readonly Color CancelColor = Color.FromArgb(232, 89, 89);

    private readonly Rectangle _virtualBounds;
    private readonly Bitmap _desktopSnapshot;
    private readonly bool _extractTextOnSelection;
    private Rectangle _selection;
    private Rectangle _toolbarBounds;
    private Rectangle _cancelButtonBounds;
    private Rectangle _ocrButtonBounds;
    private Rectangle _outfitButtonBounds;
    private Rectangle _confirmButtonBounds;
    private Point _dragStart;
    private Point _dragCurrent;
    private Point _adjustStart;
    private Rectangle _adjustOriginalSelection;
    private SelectionHitTarget _activeTarget = SelectionHitTarget.None;
    private ToolbarButtonHit _hoveredToolbarButton = ToolbarButtonHit.None;
    private bool _isDragging;
    private bool _hasSelection;
    private bool _isAdjusting;

    public event EventHandler<Rectangle>? SelectionCompleted;
    public event EventHandler<Rectangle>? TextExtractionRequested;
    public event EventHandler<Rectangle>? OutfitPreviewRequested;
    public event EventHandler? CaptureCancelled;

    public ScreenshotOverlayForm(Rectangle virtualBounds, Bitmap desktopSnapshot, bool extractTextOnSelection = false)
    {
        _virtualBounds = virtualBounds;
        _desktopSnapshot = desktopSnapshot;
        _extractTextOnSelection = extractTextOnSelection;

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        Bounds = virtualBounds;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        MinimumSize = Size.Empty;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        WindowState = FormWindowState.Normal;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        SelectionMouseAction action = SelectionMath.GetMouseAction(e.Button, e.Clicks, _hasSelection);
        if (action == SelectionMouseAction.Cancel)
        {
            CancelCapture();
            return;
        }

        if (action == SelectionMouseAction.Confirm)
        {
            ConfirmSelection();
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_hasSelection)
        {
            if (_confirmButtonBounds.Contains(e.Location))
            {
                ConfirmSelection();
                return;
            }

            if (_cancelButtonBounds.Contains(e.Location))
            {
                CancelCapture();
                return;
            }

            if (_ocrButtonBounds.Contains(e.Location))
            {
                RequestTextExtraction();
                return;
            }

            if (_outfitButtonBounds.Contains(e.Location))
            {
                RequestOutfitPreview();
                return;
            }

            SelectionHitTarget target = SelectionMath.HitTest(_selection, e.Location, HandleSize + 6);
            if (target != SelectionHitTarget.None)
            {
                _isAdjusting = true;
                _activeTarget = target;
                _adjustStart = e.Location;
                _adjustOriginalSelection = _selection;
                Cursor = GetCursorForTarget(target);
                return;
            }
        }

        _hasSelection = false;
        _isDragging = true;
        _dragStart = e.Location;
        _dragCurrent = e.Location;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isAdjusting)
        {
            Size delta = new(e.X - _adjustStart.X, e.Y - _adjustStart.Y);
            _selection = SelectionMath.ApplyDrag(_adjustOriginalSelection, _activeTarget, delta, ClientRectangle);
            UpdateToolbarBounds();
            Invalidate();
            return;
        }

        if (!_isDragging)
        {
            if (_hasSelection)
            {
                ToolbarButtonHit hoveredButton = SelectionMath.HitTestToolbarButtons(_cancelButtonBounds, _ocrButtonBounds, _outfitButtonBounds, _confirmButtonBounds, e.Location);
                UpdateHoveredToolbarButton(hoveredButton);
                Cursor = hoveredButton == ToolbarButtonHit.None
                    ? GetCursorForTarget(SelectionMath.HitTest(_selection, e.Location, HandleSize + 6))
                    : Cursors.Default;
            }

            return;
        }

        _dragCurrent = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isAdjusting && e.Button == MouseButtons.Left)
        {
            _isAdjusting = false;
            _activeTarget = SelectionHitTarget.None;
            Cursor = Cursors.Cross;
            Invalidate();
            return;
        }

        if (!_isDragging || e.Button != MouseButtons.Left)
        {
            return;
        }

        _isDragging = false;
        _dragCurrent = e.Location;

        Rectangle localSelection = SelectionMath.FromPoints(_dragStart, _dragCurrent);
        if (!SelectionMath.IsCapturable(localSelection))
        {
            _hasSelection = false;
            Invalidate();
            return;
        }

        _selection = localSelection;
        _hasSelection = true;
        _hoveredToolbarButton = ToolbarButtonHit.None;
        if (_extractTextOnSelection)
        {
            RequestTextExtraction();
            return;
        }

        UpdateToolbarBounds();
        Invalidate();
    }

    private void ConfirmSelection()
    {
        if (!_hasSelection || !SelectionMath.IsCapturable(_selection))
        {
            return;
        }

        Rectangle screenSelection = new(
            _selection.X + _virtualBounds.X,
            _selection.Y + _virtualBounds.Y,
            _selection.Width,
            _selection.Height);

        Hide();
        SelectionCompleted?.Invoke(this, screenSelection);
    }

    private void RequestTextExtraction()
    {
        if (!_hasSelection || !SelectionMath.IsCapturable(_selection))
        {
            return;
        }

        Rectangle screenSelection = new(
            _selection.X + _virtualBounds.X,
            _selection.Y + _virtualBounds.Y,
            _selection.Width,
            _selection.Height);

        Hide();
        TextExtractionRequested?.Invoke(this, screenSelection);
    }

    private void RequestOutfitPreview()
    {
        if (!_hasSelection || !SelectionMath.IsCapturable(_selection)) return;
        Rectangle screenSelection = new(
            _selection.X + _virtualBounds.X,
            _selection.Y + _virtualBounds.Y,
            _selection.Width,
            _selection.Height);
        Hide();
        OutfitPreviewRequested?.Invoke(this, screenSelection);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
        {
            CancelCapture();
        }
        else if (e.KeyCode == Keys.Enter && _hasSelection)
        {
            ConfirmSelection();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.DrawImageUnscaled(_desktopSnapshot, Point.Empty);

        using SolidBrush overlayBrush = new(Color.FromArgb(115, 0, 0, 0));
        e.Graphics.FillRectangle(overlayBrush, ClientRectangle);

        if (!_isDragging && !_hasSelection)
        {
            return;
        }

        Rectangle selection = _hasSelection ? _selection : SelectionMath.FromPoints(_dragStart, _dragCurrent);
        e.Graphics.DrawImage(
            _desktopSnapshot,
            selection,
            selection,
            GraphicsUnit.Pixel);

        using Pen borderPen = new(AccentColor, 2)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        e.Graphics.DrawRectangle(borderPen, selection);

        using SolidBrush handleBrush = new(AccentColor);
        foreach (Rectangle handle in SelectionMath.GetHandleRectangles(selection, HandleSize))
        {
            e.Graphics.FillRectangle(handleBrush, handle);
        }

        if (SelectionMath.IsCapturable(selection))
        {
            DrawSizeLabel(e.Graphics, selection);
        }

        if (_hasSelection)
        {
            DrawToolbar(e.Graphics);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _desktopSnapshot.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void DrawSizeLabel(Graphics graphics, Rectangle selection)
    {
        string text = $"{selection.Width} x {selection.Height}";
        using Font font = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        SizeF textSize = graphics.MeasureString(text, font);
        RectangleF labelBounds = new(
            selection.Left,
            Math.Max(0, selection.Top - textSize.Height - 8),
            textSize.Width + 12,
            textSize.Height + 6);

        using SolidBrush backgroundBrush = new(Color.FromArgb(210, 20, 20, 20));
        using SolidBrush textBrush = new(Color.White);
        graphics.FillRectangle(backgroundBrush, labelBounds);
        graphics.DrawString(text, font, textBrush, labelBounds.Left + 6, labelBounds.Top + 3);
    }

    private void UpdateToolbarBounds()
    {
        int x = _selection.Right - ToolbarWidth;
        x = Math.Clamp(x, 8, Math.Max(8, ClientRectangle.Right - ToolbarWidth - 8));

        int y = _selection.Bottom + ToolbarGap;
        if (y + ToolbarHeight > ClientRectangle.Bottom - 8)
        {
            y = _selection.Top - ToolbarGap - ToolbarHeight;
        }

        y = Math.Clamp(y, 8, ClientRectangle.Bottom - ToolbarHeight - 8);
        _toolbarBounds = new Rectangle(x, y, ToolbarWidth, ToolbarHeight);
        _cancelButtonBounds = new Rectangle(_toolbarBounds.Left + 16, _toolbarBounds.Top + 8, ToolbarButtonSize, ToolbarButtonSize);
        _ocrButtonBounds = new Rectangle(_toolbarBounds.Left + 63, _toolbarBounds.Top + 8, ToolbarButtonSize, ToolbarButtonSize);
        _outfitButtonBounds = new Rectangle(_toolbarBounds.Left + 109, _toolbarBounds.Top + 8, ToolbarButtonSize, ToolbarButtonSize);
        _confirmButtonBounds = new Rectangle(_toolbarBounds.Right - 16 - ToolbarButtonSize, _toolbarBounds.Top + 8, ToolbarButtonSize, ToolbarButtonSize);
    }

    private void DrawToolbar(Graphics graphics)
    {
        using System.Drawing.Drawing2D.GraphicsPath path = RoundedRectangle(_toolbarBounds, 8);
        using SolidBrush toolbarBrush = new(Color.FromArgb(232, 32, 32, 36));
        graphics.FillPath(toolbarBrush, path);

        using Pen cancelPen = new(CancelColor, 3);
        DrawButtonHover(graphics, _cancelButtonBounds, ToolbarButtonHit.Cancel);
        graphics.DrawLine(cancelPen, _cancelButtonBounds.Left + 8, _cancelButtonBounds.Top + 8, _cancelButtonBounds.Right - 8, _cancelButtonBounds.Bottom - 8);
        graphics.DrawLine(cancelPen, _cancelButtonBounds.Right - 8, _cancelButtonBounds.Top + 8, _cancelButtonBounds.Left + 8, _cancelButtonBounds.Bottom - 8);

        DrawButtonHover(graphics, _ocrButtonBounds, ToolbarButtonHit.Ocr);
        using Font ocrFont = new("Microsoft YaHei UI", 12f, FontStyle.Bold, GraphicsUnit.Point);
        using SolidBrush ocrBrush = new(Color.WhiteSmoke);
        using StringFormat ocrFormat = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString("文", ocrFont, ocrBrush, _ocrButtonBounds, ocrFormat);

        DrawButtonHover(graphics, _outfitButtonBounds, ToolbarButtonHit.OutfitPreview);
        graphics.DrawString("试", ocrFont, ocrBrush, _outfitButtonBounds, ocrFormat);

        using Pen confirmPen = new(AccentColor, 3);
        DrawButtonHover(graphics, _confirmButtonBounds, ToolbarButtonHit.Confirm);
        Point checkStart = new(_confirmButtonBounds.Left + 7, _confirmButtonBounds.Top + 16);
        Point checkMiddle = new(_confirmButtonBounds.Left + 13, _confirmButtonBounds.Top + 22);
        Point checkEnd = new(_confirmButtonBounds.Right - 6, _confirmButtonBounds.Top + 8);
        graphics.DrawLines(confirmPen, new[] { checkStart, checkMiddle, checkEnd });
    }

    private void DrawButtonHover(Graphics graphics, Rectangle buttonBounds, ToolbarButtonHit button)
    {
        if (_hoveredToolbarButton != button)
        {
            return;
        }

        Rectangle hoverBounds = buttonBounds;
        hoverBounds.Inflate(3, 3);
        using SolidBrush hoverBrush = new(Color.FromArgb(45, Color.White));
        graphics.FillEllipse(hoverBrush, hoverBounds);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        System.Drawing.Drawing2D.GraphicsPath path = new();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void CancelCapture()
    {
        Hide();
        CaptureCancelled?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateHoveredToolbarButton(ToolbarButtonHit hoveredButton)
    {
        if (_hoveredToolbarButton == hoveredButton)
        {
            return;
        }

        _hoveredToolbarButton = hoveredButton;
        Invalidate(_toolbarBounds);
    }

    private static Cursor GetCursorForTarget(SelectionHitTarget target)
    {
        return target switch
        {
            SelectionHitTarget.Move => Cursors.SizeAll,
            SelectionHitTarget.TopLeft or SelectionHitTarget.BottomRight => Cursors.SizeNWSE,
            SelectionHitTarget.TopRight or SelectionHitTarget.BottomLeft => Cursors.SizeNESW,
            SelectionHitTarget.Top or SelectionHitTarget.Bottom => Cursors.SizeNS,
            SelectionHitTarget.Left or SelectionHitTarget.Right => Cursors.SizeWE,
            _ => Cursors.Cross
        };
    }
}
