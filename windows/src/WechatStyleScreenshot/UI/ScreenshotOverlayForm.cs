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
    private const int ToolbarWidth = 294;
    private const int ToolbarHeight = 46;
    private const int ToolbarGap = 14;
    private const int ToolbarButtonSize = 30;
    private const int StylePickerWidth = 168;
    private static readonly Color AccentColor = Color.FromArgb(46, 190, 112);
    private static readonly Color CancelColor = Color.FromArgb(232, 89, 89);

    private readonly Rectangle _virtualBounds;
    private readonly Bitmap _desktopSnapshot;
    private readonly Func<OutfitStylePresetType, string>? _styleTitleProvider;
    private readonly Func<IReadOnlyList<OutfitStylePreset>>? _availableStylesProvider;
    private readonly bool _extractTextOnSelection;
    private readonly bool _pinOnSelection;
    private readonly System.Windows.Forms.Timer _outfitTimer;
    private readonly ToolTip _toolTip = new();
    private OutfitPreviewSession? _outfitSession;
    private Rectangle _selection;
    private Rectangle _toolbarBounds;
    private Rectangle _cancelButtonBounds;
    private Rectangle _arrowButtonBounds;
    private Rectangle _ocrButtonBounds;
    private Rectangle _translationButtonBounds;
    private Rectangle _outfitButtonBounds;
    private Rectangle _confirmButtonBounds;
    private Rectangle _pinButtonBounds;
    private Rectangle _arrowPaletteBounds;
    private readonly Rectangle[] _arrowColorBounds = new Rectangle[6];
    private readonly Rectangle[] _arrowWidthBounds = new Rectangle[3];
    private static readonly Color[] ArrowColors = [Color.Red, Color.Yellow, Color.LimeGreen, Color.DodgerBlue, Color.White, Color.Black];
    private static readonly int[] ArrowWidths = [2, 4, 7];
    private static Color _lastArrowColor = Color.Red;
    private static int _lastArrowWidth = 4;
    private readonly AnnotationSession _annotations = new();
    private bool _arrowPaletteOpen;
    private bool _drawingArrow;
    private PointF _arrowStart;
    private PointF _arrowEnd;
    private bool _pinHovered;
    private bool _pinPressed;
    private Rectangle _stylePickerBounds;
    private readonly Rectangle[] _styleOptionBounds = new Rectangle[8];
    private IReadOnlyList<OutfitStylePreset> _visibleStyles = OutfitStyleCatalog.BuiltIn;
    private bool _stylePickerOpen;
    private bool _isHoldingOriginalPreview;
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
    private TranslationState _translationState;
    private Bitmap? _translationResultImage;
    private string? _translationError;
    private long _translationStartedAt;

    public event EventHandler<Rectangle>? SelectionCompleted;
    public event EventHandler? PinRequested;
    public event EventHandler<Rectangle>? TextExtractionRequested;
    public event Func<ScreenshotOverlayForm, bool>? TranslationStartRequested;
    public event EventHandler? TranslationCancellationRequested;
    public event Func<ScreenshotOverlayForm, OutfitStylePresetType, bool>? OutfitPreviewStartRequested;
    public event EventHandler? OutfitPreviewCancellationRequested;
    public event EventHandler? CaptureCancelled;
    internal event Action<OutfitPreviewState>? OutfitStateChangedForTesting;

    internal OutfitPreviewState OutfitStateForTesting => _outfitSession?.State ?? OutfitPreviewState.None;
    internal bool HasOutfitResultForTesting => _outfitSession?.HasResult == true;
    internal bool HasOutfitResult => _outfitSession?.HasResult == true;
    internal TranslationState TranslationStateForTesting => _translationState;
    internal bool HasTranslationResultForTesting => _translationResultImage is not null;
    internal bool HasTranslationResult => _translationResultImage is not null;
    internal Rectangle PinButtonBoundsForTesting => _pinButtonBounds;
    internal Rectangle ArrowButtonBoundsForTesting => _arrowButtonBounds;
    internal Rectangle ArrowPaletteBoundsForTesting => _arrowPaletteBounds;
    internal int ArrowCountForTesting => _annotations.Arrows.Count;
    internal bool ArrowModeForTesting => _annotations.Tool == AnnotationTool.Arrow;
    internal bool ArrowPaletteOpenForTesting => _arrowPaletteOpen;
    internal void ClickArrowForTesting() => MouseDownAtForTesting(new Point(_arrowButtonBounds.Left + 15, _arrowButtonBounds.Top + 15));
    internal void PressUndoForTesting() => OnKeyDown(new KeyEventArgs(Keys.Control | Keys.Z));
    internal void PressEscapeForTesting() => OnKeyDown(new KeyEventArgs(Keys.Escape));
    public Point CurrentSelectionScreenLocation => new(_selection.Left + _virtualBounds.Left, _selection.Top + _virtualBounds.Top);
    internal bool PinOnSelectionForTesting => _pinOnSelection;
    internal bool ToolbarVisibleForTesting => _hasSelection && !_pinOnSelection;
    internal void ClickPinForTesting()
    {
        Point center = new(_pinButtonBounds.Left + _pinButtonBounds.Width / 2, _pinButtonBounds.Top + _pinButtonBounds.Height / 2);
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
        OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
    }
    internal bool ClickTranslationButtonForTesting()
    {
        Point center = new(_translationButtonBounds.Left + 15, _translationButtonBounds.Top + 15);
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
        return _translationState == TranslationState.Recognizing;
    }
    internal bool IsHoldingOriginalPreviewForTesting => _isHoldingOriginalPreview;
    internal Rectangle SelectionForTesting => _selection;
    internal bool IsStylePickerOpenForTesting => _stylePickerOpen;
    internal IReadOnlyList<string> StyleLabelsForTesting => (_stylePickerOpen ? _visibleStyles : AvailableStyles()).Select(style => StyleLabel(style.Type)).ToArray();
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
        int index = _visibleStyles.ToList().FindIndex(item => item.Type == style);
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

    internal void MouseDownAtForTesting(Point point) =>
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));

    internal void MouseUpAtForTesting(Point point) =>
        OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));

    internal void MouseMoveAtForTesting(Point point) =>
        OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));

    internal void LoseCaptureForTesting()
    {
        Capture = false;
        OnMouseCaptureChanged(EventArgs.Empty);
    }

    internal void DeactivateForTesting() => OnDeactivate(EventArgs.Empty);

    internal Color RenderSelectionCenterForTesting()
    {
        using Bitmap frame = new(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
        using Graphics graphics = Graphics.FromImage(frame);
        OnPaint(new PaintEventArgs(graphics, ClientRectangle));
        return frame.GetPixel(_selection.Left + _selection.Width / 2, _selection.Top + _selection.Height / 2);
    }

    public ScreenshotOverlayForm(Rectangle virtualBounds, Bitmap desktopSnapshot, bool extractTextOnSelection = false,
        Func<OutfitStylePresetType, string>? styleTitleProvider = null,
        Func<IReadOnlyList<OutfitStylePreset>>? availableStylesProvider = null,
        bool pinOnSelection = false)
    {
        _virtualBounds = virtualBounds;
        _desktopSnapshot = desktopSnapshot;
        _extractTextOnSelection = extractTextOnSelection;
        _pinOnSelection = pinOnSelection;
        _styleTitleProvider = styleTitleProvider;
        _availableStylesProvider = availableStylesProvider;
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

        if (_drawingArrow && e.Button == MouseButtons.Right)
        {
            StopArrowDraft();
            return;
        }

        if (IsTranslationBusy)
        {
            if (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && _cancelButtonBounds.Contains(e.Location)))
                TranslationCancellationRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_outfitSession?.IsBusy == true)
        {
            if (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && _cancelButtonBounds.Contains(e.Location)))
            {
                RequestOutfitCancellation();
            }
            return;
        }

        if (_hasSelection && e.Button == MouseButtons.Left && !_pinOnSelection && _pinButtonBounds.Contains(e.Location))
        {
            _pinPressed = true;
            Capture = true;
            Invalidate(_pinButtonBounds);
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
                        SelectOutfitStyle(_visibleStyles[index].Type);
                        return;
                    }
                }
            }
            CloseStylePicker();
            return;
        }

        if (_arrowPaletteOpen && e.Button == MouseButtons.Left)
        {
            for (int i = 0; i < _arrowColorBounds.Length; i++)
                if (_arrowColorBounds[i].Contains(e.Location)) { _lastArrowColor = ArrowColors[i]; Invalidate(_arrowPaletteBounds); return; }
            for (int i = 0; i < _arrowWidthBounds.Length; i++)
                if (_arrowWidthBounds[i].Contains(e.Location)) { _lastArrowWidth = ArrowWidths[i]; Invalidate(_arrowPaletteBounds); return; }
            if (!_arrowButtonBounds.Contains(e.Location))
            {
                CloseArrowPalette();
                if (!_selection.Contains(e.Location)) return;
            }
            else CloseArrowPalette();
        }

        SelectionMouseAction action = SelectionMath.GetMouseAction(e.Button, e.Clicks, _hasSelection);
        if (action == SelectionMouseAction.Cancel)
        {
            CancelCapture();
            return;
        }

        if (action == SelectionMouseAction.Confirm && _annotations.Tool != AnnotationTool.Arrow)
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
            if (_arrowButtonBounds.Contains(e.Location))
            {
                _annotations.Tool = _annotations.Tool == AnnotationTool.Arrow ? AnnotationTool.None : AnnotationTool.Arrow;
                _arrowPaletteOpen = _annotations.Tool == AnnotationTool.Arrow;
                Invalidate();
                return;
            }
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
                ExitArrowMode();
                RequestTextExtraction();
                return;
            }

            if (_translationButtonBounds.Contains(e.Location))
            {
                ExitArrowMode();
                TranslationStartRequested?.Invoke(this);
                return;
            }

            if (_outfitButtonBounds.Contains(e.Location))
            {
                ExitArrowMode();
                RequestOutfitPreview();
                return;
            }

            SelectionHitTarget target = SelectionMath.HitTest(_selection, e.Location, HandleSize + 6);
            if (_annotations.Tool == AnnotationTool.Arrow)
            {
                if (_selection.Contains(e.Location) && target == SelectionHitTarget.Move && !_pinButtonBounds.Contains(e.Location))
                {
                    _arrowStart = ToSelectionPoint(e.Location);
                    _arrowEnd = _arrowStart;
                    _drawingArrow = true;
                    Cursor = Cursors.Cross;
                }
                return;
            }
            if (target == SelectionHitTarget.Move && !_toolbarBounds.Contains(e.Location) &&
                (_outfitSession is { State: OutfitPreviewState.Success, HasResult: true } || _translationResultImage is not null))
            {
                _isHoldingOriginalPreview = true;
                Capture = true;
                Invalidate(_selection);
                return;
            }
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

        if (_drawingArrow)
        {
            _arrowEnd = ToSelectionPoint(e.Location);
            Invalidate(_selection);
            return;
        }

        if (_pinPressed)
        {
            Cursor = Cursors.Hand;
            return;
        }

        if (_isHoldingOriginalPreview)
        {
            Cursor = Cursors.Hand;
            return;
        }

        if (_outfitSession?.IsBusy == true || IsTranslationBusy)
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
        if (_arrowPaletteOpen && _arrowPaletteBounds.Contains(e.Location))
        {
            Cursor = Cursors.Hand;
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
                bool pinHovered = !_pinOnSelection && _pinButtonBounds.Contains(e.Location);
                if (_pinHovered != pinHovered)
                {
                    _pinHovered = pinHovered;
                    Invalidate(_pinButtonBounds);
                }
                if (pinHovered)
                {
                    Cursor = Cursors.Hand;
                    _toolTip.Show("钉在桌面", this, e.X + 8, e.Y + 8, 900);
                    UpdateHoveredToolbarButton(ToolbarButtonHit.None);
                    return;
                }
                ToolbarButtonHit hoveredButton = _arrowButtonBounds.Contains(e.Location) ? ToolbarButtonHit.Arrow : SelectionMath.HitTestToolbarButtons(_cancelButtonBounds, _ocrButtonBounds, _translationButtonBounds, _outfitButtonBounds, _confirmButtonBounds, e.Location);
                UpdateHoveredToolbarButton(hoveredButton);
                if (hoveredButton == ToolbarButtonHit.OutfitPreview)
                    _toolTip.Show("再试一款", this, e.X + 8, e.Y + 8, 900);
                else
                    _toolTip.Hide(this);
                Cursor = hoveredButton == ToolbarButtonHit.None
                    ? _annotations.Tool == AnnotationTool.Arrow ? Cursors.Cross : GetCursorForTarget(SelectionMath.HitTest(_selection, e.Location, HandleSize + 6))
                    : Cursors.Default;
            }

            return;
        }

        _dragCurrent = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        bool wasPressingPin = _pinPressed;
        base.OnMouseUp(e);

        if (_drawingArrow && e.Button == MouseButtons.Left)
        {
            _arrowEnd = ToSelectionPoint(e.Location);
            _annotations.Add(_arrowStart, _arrowEnd, _lastArrowColor, _lastArrowWidth * DeviceDpi / 96f);
            StopArrowDraft();
            return;
        }

        if (wasPressingPin && e.Button == MouseButtons.Left)
        {
            _pinPressed = false;
            Capture = false;
            Invalidate(_pinButtonBounds);
            if (_pinButtonBounds.Contains(e.Location) && !IsPinDisabled) PinRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_isHoldingOriginalPreview && e.Button == MouseButtons.Left)
        {
            ResetOriginalComparePreview();
            return;
        }

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
        if (_pinOnSelection)
        {
            PinRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (_extractTextOnSelection)
        {
            RequestTextExtraction();
            return;
        }

        UpdateToolbarBounds();
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (_pinPressed && !Capture) { _pinPressed = false; Invalidate(_pinButtonBounds); }
        if (_isHoldingOriginalPreview && !Capture) ResetOriginalComparePreview();
        if (_drawingArrow && !Capture) StopArrowDraft();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        StopArrowDraft();
        ResetOriginalComparePreview();
        base.OnDeactivate(e);
    }

    private void ResetOriginalComparePreview()
    {
        bool wasHolding = _isHoldingOriginalPreview;
        _isHoldingOriginalPreview = false;
        if (Capture) Capture = false;
        if (wasHolding && !IsDisposed && !Disposing) Invalidate(_selection);
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
        _visibleStyles = AvailableStyles();
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
        ResetOriginalComparePreview();
        if (IsDisposed || Disposing || !_hasSelection || !SelectionMath.IsCapturable(_selection) ||
            _outfitSession is null || IsTranslationBusy || !_outfitSession.TryBegin()) return false;
        ClearTranslationResult();
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
            if (_drawingArrow) { StopArrowDraft(); return; }
            if (_annotations.Tool == AnnotationTool.Arrow) { ExitArrowMode(); return; }
            ResetOriginalComparePreview();
            if (IsTranslationBusy) TranslationCancellationRequested?.Invoke(this, EventArgs.Empty);
            else if (_outfitSession?.IsBusy == true) RequestOutfitCancellation();
            else if (_stylePickerOpen) CloseStylePicker();
            else CancelCapture();
        }
        else if (e.Control && e.KeyCode == Keys.Z)
        {
            if (_annotations.Undo()) Invalidate(_selection);
            else if (_translationResultImage is not null) ClearTranslationResult();
            else RestoreOutfitOriginal();
        }
        else if (e.KeyCode == Keys.Enter && _hasSelection && _outfitSession?.IsBusy != true && !IsTranslationBusy)
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
        if (_translationResultImage is Bitmap translationResult)
        {
            if (_isHoldingOriginalPreview) e.Graphics.DrawImage(_desktopSnapshot, selection, selection, GraphicsUnit.Pixel);
            else e.Graphics.DrawImage(translationResult, selection);
        }
        else if (_outfitSession?.HasResult == true && _outfitSession.ResultImage is Bitmap outfitResult)
        {
            if (_isHoldingOriginalPreview)
                e.Graphics.DrawImage(_outfitSession.OriginalImage, selection);
            else
                DrawImageCover(e.Graphics, outfitResult, selection);
        }
        else
        {
            e.Graphics.DrawImage(_desktopSnapshot, selection, selection, GraphicsUnit.Pixel);
        }

        if (_hasSelection && !_isHoldingOriginalPreview)
            AnnotationRenderer.Draw(e.Graphics, _annotations.Arrows, selection);
        if (_drawingArrow)
            AnnotationRenderer.Draw(e.Graphics, [new ArrowAnnotation(_arrowStart, _arrowEnd, _lastArrowColor, _lastArrowWidth * DeviceDpi / 96f)], selection);

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

        if (IsTranslationBusy || _translationState == TranslationState.Error ||
            _outfitSession is { State: not OutfitPreviewState.None and not OutfitPreviewState.Success } || _outfitNotice is not null)
        {
            if (IsTranslationBusy || _translationState == TranslationState.Error) DrawTranslationStatus(e.Graphics, selection);
            else DrawOutfitStatus(e.Graphics, selection);
        }

        if (ToolbarVisibleForTesting)
        {
            DrawToolbar(e.Graphics);
            if (_stylePickerOpen) DrawStylePicker(e.Graphics);
            if (_arrowPaletteOpen) DrawArrowPalette(e.Graphics);
            DrawPinButton(e.Graphics);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ResetOriginalComparePreview();
            _outfitTimer.Stop();
            _outfitTimer.Dispose();
            _toolTip.Dispose();
            _outfitSession?.Dispose();
            _translationResultImage?.Dispose();
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
        _pinButtonBounds = PinButtonLayout.GetBounds(_selection, ClientRectangle, DeviceDpi);
        _cancelButtonBounds = new Rectangle(_toolbarBounds.Left + 16, _toolbarBounds.Top + 8, ToolbarButtonSize, ToolbarButtonSize);
        _arrowButtonBounds = new Rectangle(_toolbarBounds.Left + 63, _toolbarBounds.Top + 8, ToolbarButtonSize, ToolbarButtonSize);
        _ocrButtonBounds = new Rectangle(_toolbarBounds.Left + 109, _toolbarBounds.Top + 8, ToolbarButtonSize, ToolbarButtonSize);
        _translationButtonBounds = new Rectangle(_toolbarBounds.Left + 155, _toolbarBounds.Top + 8, ToolbarButtonSize, ToolbarButtonSize);
        _outfitButtonBounds = new Rectangle(_toolbarBounds.Left + 201, _toolbarBounds.Top + 8, ToolbarButtonSize, ToolbarButtonSize);
        _confirmButtonBounds = new Rectangle(_toolbarBounds.Right - 16 - ToolbarButtonSize, _toolbarBounds.Top + 8, ToolbarButtonSize, ToolbarButtonSize);
        int paletteY = _toolbarBounds.Top - 50;
        if (paletteY < 8) paletteY = _toolbarBounds.Bottom + 6;
        _arrowPaletteBounds = new Rectangle(Math.Clamp(_arrowButtonBounds.Left - 5, 8, Math.Max(8, ClientRectangle.Right - 260)), paletteY, 260, 44);
        for (int i = 0; i < 6; i++) _arrowColorBounds[i] = new Rectangle(_arrowPaletteBounds.Left + 9 + i * 27, paletteY + 10, 22, 22);
        for (int i = 0; i < 3; i++) _arrowWidthBounds[i] = new Rectangle(_arrowPaletteBounds.Left + 176 + i * 26, paletteY + 8, 24, 26);
        UpdateStylePickerBounds();
    }

    private void UpdateStylePickerBounds()
    {
        int height = 12 + _visibleStyles.Count * 38;
        int x = Math.Clamp(_outfitButtonBounds.Left - 69, 8, Math.Max(8, ClientRectangle.Right - StylePickerWidth - 8));
        int y = _toolbarBounds.Top - height - 7;
        if (y < 8) y = _toolbarBounds.Bottom + 7;
        y = Math.Clamp(y, 8, Math.Max(8, ClientRectangle.Bottom - height - 8));
        _stylePickerBounds = new Rectangle(x, y, StylePickerWidth, height);
        for (int index = 0; index < _styleOptionBounds.Length; index++)
            _styleOptionBounds[index] = index < _visibleStyles.Count
                ? new Rectangle(x + 6, y + 6 + index * 38, StylePickerWidth - 12, 36) : Rectangle.Empty;
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

    public Bitmap? CreateTranslationResultImage() => _translationResultImage is null ? null : new Bitmap(_translationResultImage);

    public Bitmap CreateCurrentVisualSelectionImage(bool includeAnnotations = false)
    {
        Bitmap image = CreateTranslationResultImage() ?? CreateOutfitResultImage() ?? CreateOriginalSelectionImage();
        if (includeAnnotations && _annotations.Arrows.Count > 0)
        {
            using Graphics graphics = Graphics.FromImage(image);
            AnnotationRenderer.Draw(graphics, _annotations.Arrows, new Rectangle(Point.Empty, image.Size));
        }
        return image;
    }

    public bool TryBeginTranslation()
    {
        if (!_hasSelection || IsTranslationBusy || _outfitSession?.IsBusy == true) return false;
        ClearTranslationResult();
        _translationState = TranslationState.Recognizing;
        _translationStartedAt = Stopwatch.GetTimestamp();
        _outfitTimer.Start();
        Invalidate(_selection);
        return true;
    }

    public Bitmap CreateTranslationSourceImage() => CreateOutfitResultImage() ?? CreateOriginalSelectionImage();

    public void SetTranslationState(TranslationState state)
    {
        if (IsDisposed || Disposing) return;
        _translationState = state;
        Invalidate(_selection);
    }

    public void CompleteTranslation(Bitmap result)
    {
        if (IsDisposed || Disposing) { result.Dispose(); return; }
        _translationResultImage?.Dispose();
        _translationResultImage = result;
        _translationState = TranslationState.Success;
        _outfitTimer.Stop();
        Invalidate(_selection);
    }

    public void FailTranslation(string message)
    {
        if (IsDisposed || Disposing) return;
        _translationError = message;
        _translationState = TranslationState.Error;
        _outfitErrorExpiresAt = DateTime.UtcNow.AddSeconds(2);
        _outfitTimer.Start();
        Invalidate(_selection);
    }

    public void ClearTranslationResult()
    {
        _translationResultImage?.Dispose();
        _translationResultImage = null;
        _translationState = TranslationState.None;
        if (!IsDisposed && !Disposing && _hasSelection) Invalidate(_selection);
    }

    private bool IsTranslationBusy => _translationState is TranslationState.Recognizing or TranslationState.Translating or TranslationState.Applying;
    private bool IsPinDisabled => IsTranslationBusy || _outfitSession?.IsBusy == true;

    private void DrawPinButton(Graphics graphics)
    {
        if (_pinHovered || _pinPressed)
        {
            using SolidBrush hover = new(Color.FromArgb(_pinPressed ? 125 : 75, Color.White));
            graphics.FillEllipse(hover, _pinButtonBounds);
        }
        int iconSize = Math.Max(20, (int)Math.Round((_pinPressed ? 22 : _pinHovered ? 26 : 24) * DeviceDpi / 96d));
        Bitmap icon = PinIcon.Get(iconSize);
        Rectangle destination = new(_pinButtonBounds.Left + (_pinButtonBounds.Width - iconSize) / 2,
            _pinButtonBounds.Top + (_pinButtonBounds.Height - iconSize) / 2, iconSize, iconSize);
        if (IsPinDisabled)
        {
            using ImageAttributes attributes = new();
            System.Drawing.Imaging.ColorMatrix matrix = new() { Matrix33 = 0.4f };
            attributes.SetColorMatrix(matrix);
            graphics.DrawImage(icon, destination, 0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, attributes);
        }
        else graphics.DrawImage(icon, destination);
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
        ResetOriginalComparePreview();
        if (_outfitSession?.HasResult != true || _outfitSession.IsBusy) return;
        _outfitSession.RestoreOriginal();
        _outfitTimer.Stop();
        Invalidate(_selection);
    }

    private void ResetOutfitForSelection()
    {
        StopArrowDraft();
        _annotations.Clear();
        ExitArrowMode();
        ResetOriginalComparePreview();
        ClearTranslationResult();
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
        if (IsTranslationBusy) { _loadingFrame = (_loadingFrame + 1) % 16; Invalidate(_selection); return; }
        if (_translationState == TranslationState.Error)
        {
            if (DateTime.UtcNow >= _outfitErrorExpiresAt) { _translationState = TranslationState.None; _outfitTimer.Stop(); Invalidate(_selection); }
            return;
        }
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

    private void DrawTranslationStatus(Graphics graphics, Rectangle selection)
    {
        using SolidBrush shade = new(Color.FromArgb(110, Color.Black));
        using SolidBrush ink = new(Color.White);
        using Font font = new("Microsoft YaHei UI", 12f, FontStyle.Bold);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.FillRectangle(shade, selection);
        string message = _translationState switch
        {
            TranslationState.Recognizing => "正在识别文字…",
            TranslationState.Translating => "正在翻译…",
            TranslationState.Applying => "正在生成翻译截图…",
            TranslationState.Error => _translationError ?? "翻译失败，请重试",
            _ => ""
        };
        graphics.DrawString(message, font, ink, selection, centered);
        if (IsTranslationBusy)
        {
            int elapsed = (int)Stopwatch.GetElapsedTime(_translationStartedAt).TotalSeconds;
            Rectangle timer = new(selection.Left, selection.Top + selection.Height / 2 + 22, selection.Width, 28);
            graphics.DrawString($"已等待 {elapsed} 秒", font, ink, timer, centered);
        }
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

        DrawButtonHover(graphics, _arrowButtonBounds, ToolbarButtonHit.Arrow);
        if (_annotations.Tool == AnnotationTool.Arrow)
        {
            using SolidBrush active = new(Color.FromArgb(75, AccentColor));
            graphics.FillEllipse(active, _arrowButtonBounds);
        }
        using Pen arrowIcon = new(Color.WhiteSmoke, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawLine(arrowIcon, _arrowButtonBounds.Left + 7, _arrowButtonBounds.Bottom - 7, _arrowButtonBounds.Right - 7, _arrowButtonBounds.Top + 7);
        graphics.DrawLines(arrowIcon, new Point[] { new(_arrowButtonBounds.Right - 15, _arrowButtonBounds.Top + 7), new(_arrowButtonBounds.Right - 7, _arrowButtonBounds.Top + 7), new(_arrowButtonBounds.Right - 7, _arrowButtonBounds.Top + 15) });

        DrawButtonHover(graphics, _ocrButtonBounds, ToolbarButtonHit.Ocr);
        using Font ocrFont = new("Microsoft YaHei UI", 12f, FontStyle.Bold, GraphicsUnit.Point);
        using SolidBrush ocrBrush = new(Color.WhiteSmoke);
        using StringFormat ocrFormat = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString("文", ocrFont, ocrBrush, _ocrButtonBounds, ocrFormat);

        DrawButtonHover(graphics, _translationButtonBounds, ToolbarButtonHit.Translation);
        graphics.DrawString("译", ocrFont, ocrBrush, _translationButtonBounds, ocrFormat);

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
        using StringFormat format = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        for (int index = 0; index < _visibleStyles.Count; index++)
        {
            OutfitStylePreset style = _visibleStyles[index];
            Rectangle row = _styleOptionBounds[index];
            if (index == _hoveredStyleIndex || style.Type == _lastChosenStyle)
            {
                using SolidBrush highlight = new(index == _hoveredStyleIndex
                    ? Color.FromArgb(80, AccentColor) : Color.FromArgb(36, AccentColor));
                using GraphicsPath rowPath = RoundedRectangle(row, 5);
                graphics.FillPath(highlight, rowPath);
            }
            using SolidBrush text = new(Color.WhiteSmoke);
            graphics.DrawString(StyleLabel(style.Type), font, text,
                new Rectangle(row.Left + 12, row.Top, row.Width - 20, row.Height), format);
        }
    }

    private void DrawArrowPalette(Graphics graphics)
    {
        using GraphicsPath path = RoundedRectangle(_arrowPaletteBounds, 7);
        using SolidBrush background = new(Color.FromArgb(244, 32, 32, 36));
        graphics.FillPath(background, path);
        for (int i = 0; i < ArrowColors.Length; i++)
        {
            using SolidBrush swatch = new(ArrowColors[i]);
            graphics.FillEllipse(swatch, _arrowColorBounds[i]);
            if (ArrowColors[i].ToArgb() == _lastArrowColor.ToArgb())
            {
                using Pen selected = new(Color.White, 2);
                Rectangle ring = _arrowColorBounds[i]; ring.Inflate(2, 2);
                graphics.DrawEllipse(selected, ring);
            }
        }
        for (int i = 0; i < ArrowWidths.Length; i++)
        {
            Rectangle box = _arrowWidthBounds[i];
            if (_lastArrowWidth == ArrowWidths[i])
            {
                using SolidBrush selected = new(Color.FromArgb(75, AccentColor));
                graphics.FillRectangle(selected, box);
            }
            using Pen line = new(Color.WhiteSmoke, ArrowWidths[i]);
            graphics.DrawLine(line, box.Left + 5, box.Top + box.Height / 2, box.Right - 5, box.Top + box.Height / 2);
        }
    }

    private PointF ToSelectionPoint(Point point) => new(
        Math.Clamp(point.X - _selection.Left, 0, _selection.Width - 1),
        Math.Clamp(point.Y - _selection.Top, 0, _selection.Height - 1));

    private void StopArrowDraft()
    {
        if (!_drawingArrow) return;
        _drawingArrow = false;
        if (Capture) Capture = false;
        if (!IsDisposed && !Disposing) Invalidate(_selection);
    }

    private void CloseArrowPalette()
    {
        _arrowPaletteOpen = false;
        Invalidate(_arrowPaletteBounds);
    }

    private void ExitArrowMode()
    {
        StopArrowDraft();
        _annotations.Tool = AnnotationTool.None;
        CloseArrowPalette();
        Invalidate(_arrowButtonBounds);
    }

    private string StyleLabel(OutfitStylePresetType type)
    {
        OutfitStylePreset preset = OutfitStyleCatalog.Get(type);
        string title = _styleTitleProvider?.Invoke(type) ?? OutfitStyleCatalog.GetDefaultSetting(type).Title;
        return $"{preset.Label[0]} {title}";
    }

    private IReadOnlyList<OutfitStylePreset> AvailableStyles() =>
        _availableStylesProvider?.Invoke() ?? OutfitStyleCatalog.BuiltIn;

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
        ResetOriginalComparePreview();
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
