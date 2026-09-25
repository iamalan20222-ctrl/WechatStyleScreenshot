using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using WechatStyleScreenshot.Core;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.UI;

public sealed class ScreenshotOverlayForm : Form
{
    private const int HandleSize = 9;
    private const int ToolbarWidth = 202;
    private const int ToolbarHeight = 46;
    private const int ToolbarGap = 14;
    private const int ToolbarButtonSize = 30;
    private const int StylePickerWidth = 168;
    private const int StylePickerHeight = 126;
    private static readonly Color AccentColor = Color.FromArgb(46, 190, 112);
    private static readonly Color CancelColor = Color.FromArgb(232, 89, 89);

    private readonly Rectangle _virtualBounds;
    private readonly Bitmap _desktopSnapshot;
    private readonly bool _extractTextOnSelection;
    private readonly System.Windows.Forms.Timer _outfitTimer;
    private readonly ToolTip _toolTip = new();
    private OutfitPreviewSession? _outfitSession;
    private Rectangle _selection;
    private Rectangle _toolbarBounds;
    private Rectangle _cancelButtonBounds;
    private Rectangle _ocrButtonBounds;
    private Rectangle _outfitButtonBounds;
    private Rectangle _confirmButtonBounds;
    private Rectangle _stylePickerBounds;
    private readonly Rectangle[] _styleOptionBounds = new Rectangle[3];
    private bool _stylePickerOpen;
    private int _hoveredStyleIndex = -1;
    private OutfitStylePresetType _lastChosenStyle = OutfitStylePresetType.Sport;
    private Point _dragStart;
    private Point _dragCurrent;
    private Point _adjustStart;
    private Rectangle _adjustOriginalSelection;
    private SelectionHitTarget _activeTarget = SelectionHitTarget.None;
    private ToolbarButtonHit _hoveredToolbarButton = ToolbarButtonHit.None;
    private bool _isDragging;
    private bool _hasSelection;
    private bool _isAdjusting;
    private bool _outfitCancellationRequested;
    private long _outfitStartedAt;
    private DateTime _outfitErrorExpiresAt;
    private DateTime _outfitNoticeExpiresAt;
    private string? _outfitNotice;
    private int _loadingFrame;

    public event EventHandler<Rectangle>? SelectionCompleted;
    public event EventHandler<Rectangle>? TextExtractionRequested;
    public event Func<ScreenshotOverlayForm, OutfitStylePresetType, bool>? OutfitPreviewStartRequested;
    public event EventHandler? OutfitPreviewCancellationRequested;
    public event EventHandler? CaptureCancelled;
    internal event Action<OutfitPreviewState>? OutfitStateChangedForTesting;

    internal OutfitPreviewState OutfitStateForTesting => _outfitSession?.State ?? OutfitPreviewState.None;
    internal bool HasOutfitResultForTesting => _outfitSession?.HasResult == true;
    internal bool IsStylePickerOpenForTesting => _stylePickerOpen;
    internal IReadOnlyList<string> StyleLabelsForTesting => OutfitStyleCatalog.All.Select(style => style.Label).ToArray();
    internal Bitmap CreateOutfitOriginalImageForTesting() =>
        _outfitSession?.CreateRequestImage() ?? throw new InvalidOperationException("No selection is active.");

    internal void SetSelectionForTesting(Rectangle selection)
    {
        if (!ClientRectangle.Contains(selection) || !SelectionMath.IsCapturable(selection))
            throw new ArgumentOutOfRangeException(nameof(selection));
        _selection = selection;
        _hasSelection = true;
        ResetOutfitForSelection();
        UpdateToolbarBounds();
        Invalidate();
    }

    internal bool ClickOutfitButtonForTesting()
    {
        Point center = new(_outfitButtonBounds.Left + _outfitButtonBounds.Width / 2,
            _outfitButtonBounds.Top + _outfitButtonBounds.Height / 2);
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
        return _stylePickerOpen;
    }

    internal bool ClickStyleForTesting(OutfitStylePresetType style)
    {
        if (!_stylePickerOpen) return false;
        int index = OutfitStyleCatalog.All.ToList().FindIndex(item => item.Type == style);
        if (index < 0) return false;
        Rectangle bounds = _styleOptionBounds[index];
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, bounds.Left + bounds.Width / 2,
            bounds.Top + bounds.Height / 2, 0));
        return OutfitStateForTesting == OutfitPreviewState.Preparing;
    }

    internal void ClickOutsideStylePickerForTesting() =>
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));

    internal void ClickConfirmButtonForTesting()
    {
        Point center = new(_confirmButtonBounds.Left + _confirmButtonBounds.Width / 2,
            _confirmButtonBounds.Top + _confirmButtonBounds.Height / 2);
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
    }

    public ScreenshotOverlayForm(Rectangle virtualBounds, Bitmap desktopSnapshot, bool extractTextOnSelection = false)
    {
        _virtualBounds = virtualBounds;
        _desktopSnapshot = desktopSnapshot;
        _extractTextOnSelection = extractTextOnSelection;
        _outfitTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _outfitTimer.Tick += OnOutfitTimerTick;

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

        if (_outfitSession?.IsBusy == true)
        {
            if (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && _cancelButtonBounds.Contains(e.Location)))
            {
                RequestOutfitCancellation();
            }
            return;
        }

        if (_stylePickerOpen)
        {
            if (e.Button == MouseButtons.Left)
            {
                for (int index = 0; index < _styleOptionBounds.Length; index++)
                {
                    if (_styleOptionBounds[index].Contains(e.Location))
                    {
                        SelectOutfitStyle(OutfitStyleCatalog.All[index].Type);
                        return;
                    }
                }
            }
            CloseStylePicker();
            return;
        }

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
        ResetOutfitForSelection();
        _dragStart = e.Location;
        _dragCurrent = e.Location;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_outfitSession?.IsBusy == true)
        {
            Cursor = Cursors.Default;
            return;
        }

        if (_stylePickerOpen)
        {
            int hovered = Array.FindIndex(_styleOptionBounds, bounds => bounds.Contains(e.Location));
            if (hovered != _hoveredStyleIndex)
            {
                _hoveredStyleIndex = hovered;
                Invalidate(_stylePickerBounds);
            }
            Cursor = hovered >= 0 ? Cursors.Hand : Cursors.Default;
            return;
        }

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
                if (hoveredButton == ToolbarButtonHit.OutfitPreview)
                    _toolTip.Show("再试一款", this, e.X + 8, e.Y + 8, 900);
                else
                    _toolTip.Hide(this);
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
            if (_selection != _adjustOriginalSelection)
            {
                ResetOutfitForSelection();
            }
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
            ResetOutfitForSelection();
            Invalidate();
            return;
        }

        _selection = localSelection;
        _hasSelection = true;
        ResetOutfitForSelection();
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
        if (!_hasSelection || !SelectionMath.IsCapturable(_selection) || _outfitSession is null) return;
        _stylePickerOpen = true;
        _hoveredStyleIndex = -1;
        UpdateStylePickerBounds();
        Invalidate(_stylePickerBounds);
    }

    private void SelectOutfitStyle(OutfitStylePresetType style)
    {
        CloseStylePicker();
        if (OutfitPreviewStartRequested?.Invoke(this, style) == true)
        {
            _lastChosenStyle = style;
            return;
        }
        ShowOutfitNotice("AI 服务准备中…", TimeSpan.FromSeconds(1));
    }

    private void CloseStylePicker()
    {
        if (!_stylePickerOpen) return;
        _stylePickerOpen = false;
        _hoveredStyleIndex = -1;
        Invalidate(_stylePickerBounds);
    }

    public bool TryBeginOutfitPreview()
    {
        if (IsDisposed || Disposing || !_hasSelection || !SelectionMath.IsCapturable(_selection) ||
            _outfitSession is null || !_outfitSession.TryBegin()) return false;
        CloseStylePicker();
        _outfitCancellationRequested = false;
        _outfitNotice = null;
        _outfitStartedAt = Stopwatch.GetTimestamp();
        _loadingFrame = 0;
        _outfitTimer.Start();
        Invalidate(_selection);
        OutfitStateChangedForTesting?.Invoke(OutfitPreviewState.Preparing);
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
        {
            if (_outfitSession?.IsBusy == true) RequestOutfitCancellation();
            else if (_stylePickerOpen) CloseStylePicker();
            else CancelCapture();
        }
        else if (e.Control && e.KeyCode == Keys.Z)
        {
            RestoreOutfitOriginal();
        }
        else if (e.KeyCode == Keys.Enter && _hasSelection && _outfitSession?.IsBusy != true)
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
        if (_outfitSession?.HasResult == true && _outfitSession.ResultImage is Bitmap outfitResult)
        {
            DrawImageCover(e.Graphics, outfitResult, selection);
        }
        else
        {
            e.Graphics.DrawImage(_desktopSnapshot, selection, selection, GraphicsUnit.Pixel);
        }

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

        if (_outfitSession is { State: not OutfitPreviewState.None and not OutfitPreviewState.Success } || _outfitNotice is not null)
        {
            DrawOutfitStatus(e.Graphics, selection);
        }

        if (_hasSelection)
        {
            DrawToolbar(e.Graphics);
            if (_stylePickerOpen) DrawStylePicker(e.Graphics);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _outfitTimer.Stop();
            _outfitTimer.Dispose();
            _toolTip.Dispose();
            _outfitSession?.Dispose();
            _outfitSession = null;
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
        UpdateStylePickerBounds();
    }

    private void UpdateStylePickerBounds()
    {
        int x = Math.Clamp(_outfitButtonBounds.Left - 69, 8, Math.Max(8, ClientRectangle.Right - StylePickerWidth - 8));
        int y = _toolbarBounds.Top - StylePickerHeight - 7;
        if (y < 8) y = _toolbarBounds.Bottom + 7;
        y = Math.Clamp(y, 8, Math.Max(8, ClientRectangle.Bottom - StylePickerHeight - 8));
        _stylePickerBounds = new Rectangle(x, y, StylePickerWidth, StylePickerHeight);
        for (int index = 0; index < _styleOptionBounds.Length; index++)
            _styleOptionBounds[index] = new Rectangle(x + 6, y + 6 + index * 38, StylePickerWidth - 12, 36);
    }

    public Bitmap CreateOutfitRequestImage()
    {
        if (_outfitSession is null || _outfitSession.State != OutfitPreviewState.Preparing)
        {
            throw new InvalidOperationException("No outfit preview request is being prepared.");
        }

        return _outfitSession.CreateRequestImage();
    }

    public void MarkOutfitGenerating()
    {
        if (IsDisposed || Disposing || _outfitSession is null) return;
        _outfitSession.MarkGenerating();
        Invalidate(_selection);
        OutfitStateChangedForTesting?.Invoke(OutfitPreviewState.Generating);
    }

    public void MarkOutfitApplying()
    {
        if (IsDisposed || Disposing || _outfitSession is null) return;
        _outfitSession.MarkApplying();
        Invalidate(_selection);
        OutfitStateChangedForTesting?.Invoke(OutfitPreviewState.Applying);
    }

    public void CompleteOutfitPreview(Bitmap resultImage)
    {
        if (IsDisposed || Disposing || _outfitSession is null)
        {
            resultImage.Dispose();
            return;
        }

        _outfitSession.Complete(resultImage);
        _outfitCancellationRequested = false;
        _outfitNotice = null;
        _outfitTimer.Stop();
        Invalidate(_selection);
        OutfitStateChangedForTesting?.Invoke(OutfitPreviewState.Success);
    }

    public void ShowOutfitError(string message)
    {
        if (IsDisposed || Disposing || _outfitSession is null) return;
        _outfitSession.Fail(message);
        _outfitCancellationRequested = false;
        _outfitNotice = null;
        _outfitErrorExpiresAt = DateTime.UtcNow.AddSeconds(3);
        _outfitTimer.Start();
        Invalidate(_selection);
        OutfitStateChangedForTesting?.Invoke(OutfitPreviewState.Error);
    }

    public void ShowOutfitNotice(string message, TimeSpan duration)
    {
        if (IsDisposed || Disposing) return;
        _outfitNotice = message;
        _outfitNoticeExpiresAt = DateTime.UtcNow + duration;
        _outfitTimer.Start();
        Invalidate(_selection);
    }

    public void SetOutfitRetryNotice(TimeSpan delay, OutfitPreviewStatus status)
    {
        int seconds = Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));
        string message = status == OutfitPreviewStatus.RateLimited
            ? $"AI 请求较快，{seconds} 秒后自动继续…"
            : $"AI 服务繁忙，{seconds} 秒后自动继续…";
        ShowOutfitNotice(message, delay);
    }

    public void FinishOutfitCancellation()
    {
        if (IsDisposed || Disposing || _outfitSession is null) return;
        _outfitSession.Cancel();
        _outfitCancellationRequested = false;
        _outfitNotice = null;
        _outfitTimer.Stop();
        Invalidate(_selection);
    }

    public Bitmap? CreateOutfitResultImage()
    {
        if (!_hasSelection || _outfitSession?.ResultImage is not Bitmap result) return null;
        Bitmap finalImage = new(_selection.Width, _selection.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(finalImage);
        DrawImageCover(graphics, result, new Rectangle(Point.Empty, _selection.Size));
        return finalImage;
    }

    internal Bitmap CreateOriginalSelectionImage()
    {
        if (!_hasSelection || !SelectionMath.IsCapturable(_selection))
            throw new InvalidOperationException("No screenshot selection is active.");
        return _desktopSnapshot.Clone(_selection, PixelFormat.Format32bppArgb);
    }

    private void RequestOutfitCancellation()
    {
        if (_outfitSession?.IsBusy != true || _outfitCancellationRequested) return;
        _outfitCancellationRequested = true;
        Invalidate(_selection);
        OutfitPreviewCancellationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreOutfitOriginal()
    {
        if (_outfitSession?.HasResult != true || _outfitSession.IsBusy) return;
        _outfitSession.RestoreOriginal();
        _outfitTimer.Stop();
        Invalidate(_selection);
    }

    private void ResetOutfitForSelection()
    {
        CloseStylePicker();
        _outfitTimer.Stop();
        _outfitSession?.Dispose();
        _outfitSession = null;
        _outfitCancellationRequested = false;
        _outfitNotice = null;
        if (_hasSelection && SelectionMath.IsCapturable(_selection))
        {
            using Bitmap original = _desktopSnapshot.Clone(_selection, PixelFormat.Format32bppArgb);
            _outfitSession = new OutfitPreviewSession(original);
        }
    }

    private void OnOutfitTimerTick(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing) return;
        if (_outfitSession?.IsBusy == true)
        {
            _loadingFrame = (_loadingFrame + 1) % 16;
            if (_outfitNotice is not null && DateTime.UtcNow >= _outfitNoticeExpiresAt) _outfitNotice = null;
            Invalidate(_selection);
            return;
        }

        if (_outfitSession?.State == OutfitPreviewState.Error && DateTime.UtcNow >= _outfitErrorExpiresAt)
        {
            _outfitSession.ClearError();
            _outfitTimer.Stop();
            Invalidate(_selection);
        }
        else if (_outfitNotice is not null && DateTime.UtcNow >= _outfitNoticeExpiresAt)
        {
            _outfitNotice = null;
            _outfitTimer.Stop();
            Invalidate(_selection);
        }
        else if (_outfitSession?.State != OutfitPreviewState.Error && _outfitNotice is null)
        {
            _outfitTimer.Stop();
        }
    }

    private void DrawOutfitStatus(Graphics graphics, Rectangle selection)
    {
        using SolidBrush shade = new(Color.FromArgb(100, Color.Black));
        graphics.FillRectangle(shade, selection);
        string message = _outfitNotice ?? (_outfitSession?.State switch
        {
            OutfitPreviewState.Preparing => "正在准备图片…",
            OutfitPreviewState.Generating => _outfitCancellationRequested ? "正在取消…" : "AI 正在替换穿搭" + new string('.', _loadingFrame / 4 % 3 + 1),
            OutfitPreviewState.Applying => "正在应用生成结果…",
            OutfitPreviewState.Error => _outfitSession?.ErrorMessage ?? "生成失败，请重试",
            _ => string.Empty
        } ?? string.Empty);

        using Font font = new("Microsoft YaHei UI", 12f, FontStyle.Bold, GraphicsUnit.Point);
        using SolidBrush text = new(Color.White);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        Rectangle messageBounds = new(selection.Left + 6, selection.Top + selection.Height / 2 - 14, Math.Max(1, selection.Width - 12), 30);
        graphics.DrawString(message, font, text, messageBounds, centered);

        if (_outfitSession?.IsBusy == true)
        {
            int centerX = selection.Left + selection.Width / 2;
            int centerY = selection.Top + selection.Height / 2;
            using Pen spinner = new(Color.White, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawArc(spinner, centerX - 11, centerY - 48, 22, 22, _loadingFrame * 24, 260);
            int elapsed = (int)Stopwatch.GetElapsedTime(_outfitStartedAt).TotalSeconds;
            graphics.DrawString($"已等待 {elapsed} 秒", font, text, new Rectangle(selection.Left + 4, centerY + 19, Math.Max(1, selection.Width - 8), 24), centered);
            int barWidth = Math.Min(150, Math.Max(40, selection.Width - 24));
            int barLeft = centerX - barWidth / 2;
            int barTop = centerY + 48;
            using SolidBrush track = new(Color.FromArgb(130, Color.White));
            using SolidBrush segment = new(Color.FromArgb(220, Color.White));
            graphics.FillRectangle(track, barLeft, barTop, barWidth, 4);
            int segmentWidth = Math.Max(12, barWidth / 4);
            int offset = (int)((_loadingFrame / 16f) * (barWidth + segmentWidth)) - segmentWidth;
            GraphicsState clipState = graphics.Save();
            graphics.SetClip(selection);
            graphics.FillRectangle(segment, barLeft + offset, barTop, segmentWidth, 4);
            graphics.Restore(clipState);
        }
    }

    private static void DrawImageCover(Graphics graphics, Image image, Rectangle destination)
    {
        float sourceAspect = image.Width / (float)image.Height;
        float destinationAspect = destination.Width / (float)destination.Height;
        float sourceX = 0, sourceY = 0, sourceWidth = image.Width, sourceHeight = image.Height;
        if (sourceAspect > destinationAspect)
        {
            sourceWidth = image.Height * destinationAspect;
            sourceX = (image.Width - sourceWidth) / 2;
        }
        else
        {
            sourceHeight = image.Width / destinationAspect;
            sourceY = (image.Height - sourceHeight) / 2;
        }

        GraphicsState state = graphics.Save();
        graphics.SetClip(destination);
        graphics.DrawImage(image, destination, sourceX, sourceY, sourceWidth, sourceHeight, GraphicsUnit.Pixel);
        graphics.Restore(state);
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

    private void DrawStylePicker(Graphics graphics)
    {
        using GraphicsPath panel = RoundedRectangle(_stylePickerBounds, 8);
        using SolidBrush background = new(Color.FromArgb(244, 32, 32, 36));
        graphics.FillPath(background, panel);
        using Font font = new("Microsoft YaHei UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
        using StringFormat format = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        for (int index = 0; index < OutfitStyleCatalog.All.Count; index++)
        {
            OutfitStylePreset style = OutfitStyleCatalog.All[index];
            Rectangle row = _styleOptionBounds[index];
            if (index == _hoveredStyleIndex || style.Type == _lastChosenStyle)
            {
                using SolidBrush highlight = new(index == _hoveredStyleIndex
                    ? Color.FromArgb(80, AccentColor) : Color.FromArgb(36, AccentColor));
                using GraphicsPath rowPath = RoundedRectangle(row, 5);
                graphics.FillPath(highlight, rowPath);
            }
            using SolidBrush text = new(Color.WhiteSmoke);
            graphics.DrawString(style.Label, font, text,
                new Rectangle(row.Left + 12, row.Top, row.Width - 20, row.Height), format);
        }
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
