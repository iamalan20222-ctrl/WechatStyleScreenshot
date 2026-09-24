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
    private CancellationTokenSource? _outfitRequestCancellation;
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
        _overlay.OutfitPreviewCancellationRequested += OnOutfitCancellationRequested;
        _overlay.CaptureCancelled += OnCaptureCancelled;
        _overlay.FormClosed += OnOverlayClosed;
        _overlay.Show();
        _overlay.Activate();
    }

    private async void OnTextExtractionRequested(object? sender, Rectangle selection)
    {
        try
        {
            using Bitmap? outfitResult = (sender as ScreenshotOverlayForm)?.CreateOutfitResultImage();
            using Bitmap bitmap = outfitResult ?? _captureEngine.Capture(selection);
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
            using Bitmap? outfitResult = (sender as ScreenshotOverlayForm)?.CreateOutfitResultImage();
            using Bitmap bitmap = outfitResult ?? _captureEngine.Capture(selection);
            _clipboardManager.SetImage(bitmap);
        }
        finally
        {
            ResetOverlay();
        }
    }

    private void OnCaptureCancelled(object? sender, EventArgs e)
    {
        _outfitRequestCancellation?.Cancel();
        ResetOverlay();
    }

    private async void OnOutfitPreviewRequested(object? sender, EventArgs e)
    {
        if (_disposed || sender is not ScreenshotOverlayForm overlay || overlay.IsDisposed || _outfitRequestCancellation is not null)
        {
            return;
        }

        CancellationTokenSource cancellation = new();
        _outfitRequestCancellation = cancellation;
        Bitmap? resultImageToDispose = null;
        try
        {
            using Bitmap originalSelection = overlay.CreateOutfitRequestImage();
            await Task.Yield();
            if (_disposed || overlay.IsDisposed || overlay.Disposing || cancellation.IsCancellationRequested)
            {
                overlay.FinishOutfitCancellation();
                return;
            }

            overlay.MarkOutfitGenerating();
            OutfitPreviewResult result = await _outfitPreviewService.GenerateAsync(
                originalSelection,
                new OutfitPreviewOptions(),
                cancellation.Token);

            if (_disposed || overlay.IsDisposed || overlay.Disposing || cancellation.IsCancellationRequested)
            {
                result.Image?.Dispose();
                overlay.FinishOutfitCancellation();
                return;
            }

            if (result.Status == OutfitPreviewStatus.Success && result.Image is not null)
            {
                resultImageToDispose = result.Image;
                overlay.MarkOutfitApplying();
                await Task.Yield();
                if (_disposed || overlay.IsDisposed || overlay.Disposing || cancellation.IsCancellationRequested)
                {
                    overlay.FinishOutfitCancellation();
                    return;
                }

                overlay.CompleteOutfitPreview(resultImageToDispose);
                resultImageToDispose = null;
                return;
            }

            string message = BuildOutfitErrorMessage(result);
            overlay.ShowOutfitError(message);
            if (result.Status == OutfitPreviewStatus.ApiKeyMissing) _notify("未配置 ARK_API_KEY");
            else if (result.HttpStatusCode is not null)
                _notify(BuildOutfitDiagnostic(result));
        }
        catch (OperationCanceledException)
        {
            if (!overlay.IsDisposed && !overlay.Disposing) overlay.FinishOutfitCancellation();
        }
        catch (Exception)
        {
            if (!overlay.IsDisposed && !overlay.Disposing) overlay.ShowOutfitError("生成失败，请重试");
        }
        finally
        {
            resultImageToDispose?.Dispose();
            if (ReferenceEquals(_outfitRequestCancellation, cancellation)) _outfitRequestCancellation = null;
            cancellation.Dispose();
        }
    }

    private void OnOutfitCancellationRequested(object? sender, EventArgs e)
    {
        _outfitRequestCancellation?.Cancel();
    }

    private static string BuildOutfitErrorMessage(OutfitPreviewResult result)
    {
        string message = result.Status switch
        {
            OutfitPreviewStatus.ApiKeyMissing => "未配置 AI 接口",
            OutfitPreviewStatus.RateLimited => "请求过于频繁，请稍后重试",
            OutfitPreviewStatus.TimedOut => "生成超时，请重试",
            OutfitPreviewStatus.NetworkError => "网络连接失败",
            OutfitPreviewStatus.SafetyRejected => "请求未通过内容安全审核",
            OutfitPreviewStatus.QuotaExceeded => "接口额度不足",
            OutfitPreviewStatus.InvalidRequest => "请求参数无效",
            OutfitPreviewStatus.ServerError => "AI 服务暂时不可用",
            OutfitPreviewStatus.NoUsableImage => "未返回可用图片",
            _ => "生成失败，请重试"
        };

        string diagnostic = BuildOutfitDiagnostic(result);
        return string.IsNullOrEmpty(diagnostic) ? message : $"{message}（{diagnostic}）";
    }

    private static string BuildOutfitDiagnostic(OutfitPreviewResult result)
    {
        List<string> parts = [];
        if (result.HttpStatusCode is int statusCode) parts.Add($"HTTP {statusCode}");
        if (!string.IsNullOrWhiteSpace(result.ProviderCode)) parts.Add(result.ProviderCode);
        if (result.RetryAfterSeconds is int retryAfter) parts.Add($"{retryAfter}s 后重试");
        if (!string.IsNullOrWhiteSpace(result.RequestId)) parts.Add($"请求 ID {result.RequestId}");
        return string.Join(" · ", parts);
    }

    private void OnOverlayClosed(object? sender, FormClosedEventArgs e)
    {
        _outfitRequestCancellation?.Cancel();
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

        _outfitRequestCancellation?.Cancel();

        overlay.SelectionCompleted -= OnSelectionCompleted;
        overlay.TextExtractionRequested -= OnTextExtractionRequested;
        overlay.OutfitPreviewRequested -= OnOutfitPreviewRequested;
        overlay.OutfitPreviewCancellationRequested -= OnOutfitCancellationRequested;
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
