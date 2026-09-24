using System.Drawing;
using WechatStyleScreenshot.Core;
using WechatStyleScreenshot.Services;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot;

public sealed class ScreenshotController
{
    private readonly ScreenCaptureEngine _captureEngine;
    private readonly ClipboardManager _clipboardManager;
    private readonly OcrService _ocrService;
    private readonly AiOutfitPreviewService _outfitPreviewService;
    private readonly Action<string> _notify;
    private ScreenshotOverlayForm? _overlay;
    private bool _isCapturing;
    private bool _disposed;

    public ScreenshotController(
        ScreenCaptureEngine captureEngine,
        ClipboardManager clipboardManager,
        OcrService ocrService,
        Action<string> notify,
        AiOutfitPreviewService? outfitPreviewService = null)
    {
        _captureEngine = captureEngine;
        _clipboardManager = clipboardManager;
        _ocrService = ocrService;
        _outfitPreviewService = outfitPreviewService ?? new AiOutfitPreviewService();
        _notify = notify;
    }

    public void BeginCapture(bool extractTextOnSelection = false)
    {
        if (_isCapturing)
        {
            _overlay?.Activate();
            return;
        }

        Rectangle virtualScreenBounds = GetVirtualScreenBounds();
        Bitmap desktopSnapshot = _captureEngine.Capture(virtualScreenBounds);

        _isCapturing = true;
        _overlay = new ScreenshotOverlayForm(virtualScreenBounds, desktopSnapshot, extractTextOnSelection);
        _overlay.SelectionCompleted += OnSelectionCompleted;
        _overlay.TextExtractionRequested += OnTextExtractionRequested;
        _overlay.OutfitPreviewRequested += OnOutfitPreviewRequested;
        _overlay.CaptureCancelled += OnCaptureCancelled;
        _overlay.FormClosed += OnOverlayClosed;
        _overlay.Show();
        _overlay.Activate();
    }

    private async void OnTextExtractionRequested(object? sender, Rectangle selection)
    {
        try
        {
            using Bitmap bitmap = _captureEngine.Capture(selection);
            string text = await _ocrService.RecognizeAsync(bitmap);
            if (_disposed)
            {
                return;
            }

            if (_clipboardManager.TrySetText(text))
            {
                _notify("文字已提取并复制");
            }
            else
            {
                _notify("未识别到文字");
            }
        }
        catch (OcrDependencyException ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (!_disposed)
            {
                _notify($"文字提取失败：{ex.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (!_disposed)
            {
                _notify("文字提取失败");
            }
        }
        finally
        {
            ResetOverlay();
        }
    }

    private void OnSelectionCompleted(object? sender, Rectangle selection)
    {
        if (!SelectionMath.IsCapturable(selection))
        {
            ResetOverlay();
            return;
        }

        try
        {
            Application.DoEvents();
            using Bitmap bitmap = _captureEngine.Capture(selection);
            _clipboardManager.SetImage(bitmap);
        }
        finally
        {
            ResetOverlay();
        }
    }

    private void OnCaptureCancelled(object? sender, EventArgs e)
    {
        ResetOverlay();
    }

    private void OnOutfitPreviewRequested(object? sender, Rectangle selection)
    {
        try
        {
            Application.DoEvents();
            Bitmap selectedImage = _captureEngine.Capture(selection);
            ResetOverlay();
            PreviewResultForm form = new(selectedImage, _outfitPreviewService, _clipboardManager);
            form.Show();
        }
        catch (Exception)
        {
            ResetOverlay();
            if (!_disposed) _notify("生成失败，请稍后重试");
        }
    }

    private void OnOverlayClosed(object? sender, FormClosedEventArgs e)
    {
        _isCapturing = false;
        _overlay = null;
    }

    private void ResetOverlay()
    {
        ScreenshotOverlayForm? overlay = _overlay;
        if (overlay is null)
        {
            _isCapturing = false;
            return;
        }

        overlay.SelectionCompleted -= OnSelectionCompleted;
        overlay.TextExtractionRequested -= OnTextExtractionRequested;
        overlay.OutfitPreviewRequested -= OnOutfitPreviewRequested;
        overlay.CaptureCancelled -= OnCaptureCancelled;
        overlay.FormClosed -= OnOverlayClosed;
        _overlay = null;
        _isCapturing = false;

        if (!overlay.IsDisposed)
        {
            overlay.Close();
            overlay.Dispose();
        }
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        return SystemInformation.VirtualScreen;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ResetOverlay();
        _ocrService.Dispose();
        _outfitPreviewService.Dispose();
    }
}
