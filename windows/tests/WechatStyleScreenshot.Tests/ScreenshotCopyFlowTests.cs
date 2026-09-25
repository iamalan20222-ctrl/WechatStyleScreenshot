using System.Drawing;
using System.Runtime.InteropServices;
using WechatStyleScreenshot.Services;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot.Tests;

public class ScreenshotCopyFlowTests
{
    [Fact]
    public void NormalConfirmCopiesOriginalSelection()
    {
        using TestImageClipboard backend = new();
        List<string> notices = [];
        RunOnUiThread(backend, notices.Add, overlay =>
        {
            overlay.ClickConfirmButtonForTesting();
            Assert.True(overlay.IsDisposed);
        });

        using Image? copied = backend.GetImage();
        Assert.NotNull(copied);
        Assert.Equal(new Size(100, 80), copied.Size);
        Assert.Equal(Color.Blue.ToArgb(), ((Bitmap)copied).GetPixel(50, 40).ToArgb());
        Assert.Contains("截图已复制", notices);
    }

    [Fact]
    public void FailedAiCopyKeepsOverlayThenRetryCopiesVisibleCrop()
    {
        using TestImageClipboard backend = new() { BusyWritesRemaining = 100 };
        List<string> notices = [];
        RunOnUiThread(backend, notices.Add, overlay =>
        {
            using Bitmap aiResult = new(6, 2);
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 6; x++)
                aiResult.SetPixel(x, y, x < 2 ? Color.Red : x < 4 ? Color.Green : Color.Yellow);

            Assert.True(overlay.TryBeginOutfitPreview());
            overlay.MarkOutfitGenerating();
            overlay.MarkOutfitApplying();
            overlay.CompleteOutfitPreview(new Bitmap(aiResult));

            overlay.ClickConfirmButtonForTesting();
            Assert.True(overlay.Visible);
            Assert.False(overlay.IsDisposed);
            Assert.True(overlay.HasOutfitResultForTesting);
            Assert.Contains(notices, message => message.Contains("剪贴板", StringComparison.Ordinal));

            backend.BusyWritesRemaining = 0;
            overlay.ClickConfirmButtonForTesting();
            Assert.True(overlay.IsDisposed);
        });

        using Image? copied = backend.GetImage();
        Assert.NotNull(copied);
        Assert.Equal(new Size(100, 80), copied.Size);
        Assert.Equal(Color.Green.ToArgb(), ((Bitmap)copied).GetPixel(50, 40).ToArgb());
        Assert.Contains("AI 图片已复制到剪贴板", notices);
    }

    private static void RunOnUiThread(TestImageClipboard backend, Action<string> notify,
        Action<ScreenshotOverlayForm> verify)
    {
        Exception? failure = null;
        using ManualResetEventSlim finished = new();
        Thread thread = new(() =>
        {
            try
            {
                using Bitmap source = new(320, 200);
                using (Graphics graphics = Graphics.FromImage(source)) graphics.Clear(Color.Blue);
                using AiOutfitPreviewService service = new(apiKey: "test-only");
                ScreenshotController controller = new(new ScreenCaptureEngine(),
                    new ClipboardManager(backend, _ => { }, maxImageAttempts: 3), new OcrService(), notify, service);
                ScreenshotOverlayForm overlay = controller.BeginCaptureForTesting(source);
                overlay.Shown += (_, _) => overlay.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        overlay.SetSelectionForTesting(new Rectangle(10, 10, 100, 80));
                        verify(overlay);
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                        if (!overlay.IsDisposed) overlay.Close();
                    }
                }));
                Application.Run(overlay);
                controller.Dispose();
            }
            catch (Exception ex) { failure = ex; }
            finally { finished.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(finished.Wait(TimeSpan.FromSeconds(15)), "UI copy test timed out.");
        Assert.Null(failure);
    }

    private sealed class TestImageClipboard : IImageClipboardBackend, IDisposable
    {
        private Bitmap? _copy;
        public int BusyWritesRemaining { get; set; }

        public void Write(Bitmap image, bool persist)
        {
            Assert.True(persist);
            if (BusyWritesRemaining-- > 0)
                throw new ExternalException("Clipboard busy", unchecked((int)0x800401D0));
            _copy?.Dispose();
            _copy = new Bitmap(image);
        }

        public bool ContainsImage() => _copy is not null;
        public Image? GetImage() => _copy is null ? null : new Bitmap(_copy);
        public void Dispose() => _copy?.Dispose();
    }
}
