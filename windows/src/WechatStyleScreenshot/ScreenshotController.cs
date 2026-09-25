using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Diagnostics;
using WechatStyleScreenshot.Core;
using WechatStyleScreenshot.Services;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot;

public sealed class ScreenshotController
{
    private readonly ScreenCaptureEngine _captureEngine;
    private readonly ClipboardManager _clipboardManager;
    private readonly OcrService _ocrService;
    private readonly IOutfitGenerationService _outfitPreviewService;
    private readonly Action<string> _notify;
    private readonly OutfitSettingsStore? _settingsStore;
    private ScreenshotOverlayForm? _overlay;
    private readonly OutfitPreviewRequestGate _outfitRequestGate = new();
    private bool _isCapturing;
    private bool _disposed;
    private int _outfitRequestSequence;

    internal event Action<int, string, CancellationTokenSource>? OutfitGateChangedForTesting;
    internal event Action<int, string>? OutfitInputHashForTesting;
    internal event Action<int, OutfitPreviewResult>? OutfitResponseForTesting;
    internal bool OutfitGateBusyForTesting => _outfitRequestGate.IsBusyForTesting;

    public ScreenshotController(
        ScreenCaptureEngine captureEngine,
        ClipboardManager clipboardManager,
        OcrService ocrService,
        Action<string> notify,
        IOutfitGenerationService? outfitPreviewService = null, OutfitSettingsStore? settingsStore = null)
    {
        _captureEngine = captureEngine;
        _clipboardManager = clipboardManager;
        _ocrService = ocrService;
        _outfitPreviewService = outfitPreviewService ?? new AiOutfitPreviewService();
        _notify = notify;
        _settingsStore = settingsStore;
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

        ScreenshotOverlayForm overlay = CreateOverlay(virtualScreenBounds, desktopSnapshot, extractTextOnSelection);
        overlay.Show();
        overlay.Activate();
    }

    internal ScreenshotOverlayForm BeginCaptureForTesting(Bitmap desktopSnapshot)
    {
        ArgumentNullException.ThrowIfNull(desktopSnapshot);
        if (_isCapturing) throw new InvalidOperationException("A capture is already active.");
        return CreateOverlay(new Rectangle(Point.Empty, desktopSnapshot.Size), new Bitmap(desktopSnapshot), false);
    }

    private ScreenshotOverlayForm CreateOverlay(Rectangle virtualScreenBounds, Bitmap desktopSnapshot, bool extractTextOnSelection)
    {
        _isCapturing = true;
        _overlay = new ScreenshotOverlayForm(virtualScreenBounds, desktopSnapshot, extractTextOnSelection,
            _settingsStore is null ? null : type => _settingsStore.Load().GetStyle(type).Title,
            _settingsStore is null ? null : () => _settingsStore.Load().GetEnabledStyles());
        _overlay.SelectionCompleted += OnSelectionCompleted;
        _overlay.TextExtractionRequested += OnTextExtractionRequested;
        _overlay.OutfitPreviewStartRequested += TryStartOutfitPreview;
        _overlay.OutfitPreviewCancellationRequested += OnOutfitCancellationRequested;
        _overlay.CaptureCancelled += OnCaptureCancelled;
        _overlay.FormClosed += OnOverlayClosed;
        return _overlay;
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
        if (!SelectionMath.IsCapturable(selection) || sender is not ScreenshotOverlayForm overlay) return;

        try
        {
            Bitmap? outfitResult = overlay.CreateOutfitResultImage();
            using Bitmap bitmap = outfitResult ?? overlay.CreateOriginalSelectionImage();
            Trace.WriteLine($"[Clipboard] CONFIRM_CLICKED HAS_OUTFIT_RESULT={outfitResult is not null} IMAGE_WIDTH={bitmap.Width} IMAGE_HEIGHT={bitmap.Height} PIXEL_FORMAT={bitmap.PixelFormat} CLIPBOARD_THREAD_APARTMENT={Thread.CurrentThread.GetApartmentState()}");
            if (!_clipboardManager.TrySetImage(bitmap))
            {
                overlay.ShowOutfitNotice("复制失败，请再试一次", TimeSpan.FromSeconds(3));
                _notify("剪贴板被其他程序占用，请再次点击 ✓");
                return;
            }

            _notify(outfitResult is null ? "截图已复制" : "AI 图片已复制到剪贴板");
            ResetOverlay();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Clipboard] CLIPBOARD_WRITE_RESULT=FAIL Exception={ex.GetType().Name} HRESULT=0x{ex.HResult:X8}");
            if (!overlay.IsDisposed && !overlay.Disposing)
                overlay.ShowOutfitNotice("复制失败，请再试一次", TimeSpan.FromSeconds(3));
            _notify("复制失败，请再次点击 ✓");
        }
    }

    private void OnCaptureCancelled(object? sender, EventArgs e)
    {
        _outfitRequestGate.CancelActive();
        ResetOverlay();
    }

    private bool TryStartOutfitPreview(ScreenshotOverlayForm overlay, OutfitStylePresetType style)
    {
        if (_disposed || overlay.IsDisposed || overlay.Disposing || !ReferenceEquals(_overlay, overlay))
        {
            return false;
        }

        if (_settingsStore is not null && !_settingsStore.Load().GetStyle(style).IsEnabled) return false;

        if (!_outfitRequestGate.TryAcquire(out CancellationTokenSource cancellation)) return false;
        int requestNumber = ++_outfitRequestSequence;
        OutfitGateChangedForTesting?.Invoke(requestNumber, "ACQUIRED", cancellation);
        if (!overlay.TryBeginOutfitPreview())
        {
            _outfitRequestGate.Release(cancellation);
            OutfitGateChangedForTesting?.Invoke(requestNumber, "RELEASED", cancellation);
            return false;
        }

        _ = RunOutfitPreviewAsync(overlay, cancellation, requestNumber, style);
        return true;
    }

    private async Task RunOutfitPreviewAsync(ScreenshotOverlayForm overlay, CancellationTokenSource cancellation, int requestNumber, OutfitStylePresetType style)
    {
        Bitmap? resultImageToDispose = null;
        bool requestReleased = false;
        void ReleaseRequest()
        {
            if (requestReleased) return;
            _outfitRequestGate.Release(cancellation);
            requestReleased = true;
            OutfitGateChangedForTesting?.Invoke(requestNumber, "RELEASED", cancellation);
        }
        try
        {
            using Bitmap originalSelection = overlay.CreateOutfitRequestImage();
            if (OutfitInputHashForTesting is not null)
            {
                using MemoryStream imageStream = new();
                originalSelection.Save(imageStream, ImageFormat.Png);
                OutfitInputHashForTesting.Invoke(requestNumber, Convert.ToHexString(SHA256.HashData(imageStream.ToArray())));
            }
            await Task.Yield();
            if (_disposed || overlay.IsDisposed || overlay.Disposing || cancellation.IsCancellationRequested)
            {
                ReleaseRequest();
                overlay.FinishOutfitCancellation();
                return;
            }

            overlay.MarkOutfitGenerating();
            OutfitPreviewResult result = await _outfitPreviewService.GenerateAsync(
                originalSelection,
                new OutfitPreviewOptions(style),
                cancellation.Token,
                (delay, status) => ShowRetryNotice(overlay, delay, status));
            OutfitResponseForTesting?.Invoke(requestNumber, result);

            if (_disposed || overlay.IsDisposed || overlay.Disposing || cancellation.IsCancellationRequested)
            {
                result.Image?.Dispose();
                ReleaseRequest();
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
                    ReleaseRequest();
                    overlay.FinishOutfitCancellation();
                    return;
                }

                ReleaseRequest();
                overlay.CompleteOutfitPreview(resultImageToDispose);
                resultImageToDispose = null;
                return;
            }

            string message = BuildOutfitErrorMessage(result, style);
            ReleaseRequest();
            overlay.ShowOutfitError(message);
            if (result.Status == OutfitPreviewStatus.ApiKeyMissing) _notify(BuildOutfitErrorMessage(result, style));
            else if (result.HttpStatusCode is not null)
                _notify(BuildOutfitDiagnostic(result));
        }
        catch (OperationCanceledException)
        {
            ReleaseRequest();
            if (!overlay.IsDisposed && !overlay.Disposing) overlay.FinishOutfitCancellation();
        }
        catch (Exception)
        {
            ReleaseRequest();
            if (!overlay.IsDisposed && !overlay.Disposing) overlay.ShowOutfitError("生成失败，请重试");
        }
        finally
        {
            resultImageToDispose?.Dispose();
            ReleaseRequest();
        }
    }

    private static void ShowRetryNotice(ScreenshotOverlayForm overlay, TimeSpan delay, OutfitPreviewStatus status)
    {
        if (overlay.IsDisposed || overlay.Disposing || !overlay.IsHandleCreated) return;
        if (overlay.InvokeRequired)
        {
            try { overlay.BeginInvoke(new Action(() => overlay.SetOutfitRetryNotice(delay, status))); }
            catch (InvalidOperationException) { }
        }
        else
        {
            overlay.SetOutfitRetryNotice(delay, status);
        }
    }

    private void OnOutfitCancellationRequested(object? sender, EventArgs e)
    {
        _outfitRequestGate.CancelActive();
    }

    internal static string BuildOutfitErrorMessage(OutfitPreviewResult result, OutfitStylePresetType style)
    {
        string message = result.Status switch
        {
            OutfitPreviewStatus.ApiKeyMissing => result.ProviderCode switch
            {
                "OpenAI" => "尚未配置 OpenAI API Key",
                "Qwen" => "尚未配置 Qwen API Key",
                "Volcano" => "尚未配置火山方舟 API Key",
                _ => "未配置 AI 接口"
            },
            OutfitPreviewStatus.Unauthorized => "API Key 无效或没有权限",
            OutfitPreviewStatus.RateLimited => "请求过于频繁，请稍后重试",
            OutfitPreviewStatus.TimedOut => "生成超时，请重试",
            OutfitPreviewStatus.NetworkError => "网络连接失败",
            OutfitPreviewStatus.SafetyRejected => style == OutfitStylePresetType.Bikini
                ? "当前泳衣提示词未通过内容安全审核，请调整后重试"
                : "请求未通过内容安全审核",
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
        if (!string.IsNullOrWhiteSpace(result.SafeMessage)) parts.Add(result.SafeMessage);
        return string.Join(" · ", parts);
    }

    private void OnOverlayClosed(object? sender, FormClosedEventArgs e)
    {
        _outfitRequestGate.CancelActive();
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

        _outfitRequestGate.CancelActive();

        overlay.SelectionCompleted -= OnSelectionCompleted;
        overlay.TextExtractionRequested -= OnTextExtractionRequested;
        overlay.OutfitPreviewStartRequested -= TryStartOutfitPreview;
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
