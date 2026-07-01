using System.Drawing;
using WechatStyleScreenshot.Core;
using WechatStyleScreenshot.Services;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot;

public sealed class ScreenshotController
{
    private readonly ScreenCaptureEngine _captureEngine;
    private readonly ClipboardManager _clipboardManager;
    private ScreenshotOverlayForm? _overlay;
    private bool _isCapturing;

    public ScreenshotController(ScreenCaptureEngine captureEngine, ClipboardManager clipboardManager)
    {
        _captureEngine = captureEngine;
        _clipboardManager = clipboardManager;
    }

    public void BeginCapture()
    {
        if (_isCapturing)
        {
            _overlay?.Activate();
            return;
        }

        Rectangle virtualScreenBounds = GetVirtualScreenBounds();
        Bitmap desktopSnapshot = _captureEngine.Capture(virtualScreenBounds);

        _isCapturing = true;
        _overlay = new ScreenshotOverlayForm(virtualScreenBounds, desktopSnapshot);
        _overlay.SelectionCompleted += OnSelectionCompleted;
        _overlay.CaptureCancelled += OnCaptureCancelled;
        _overlay.FormClosed += OnOverlayClosed;
        _overlay.Show();
        _overlay.Activate();
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
}
