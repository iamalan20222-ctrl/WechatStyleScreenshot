using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WechatStyleScreenshot.Core;
using WechatStyleScreenshot.Services;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot.Tests;

public class PinningTests
{
    [Fact]
    public void IconIsEmbeddedTransparentAndCachedAtUiSize()
    {
        Assert.Contains("WechatStyleScreenshot.Assets.pin.png",
            typeof(PinIcon).Assembly.GetManifestResourceNames());
        Assert.Equal(0, PinIcon.Original.GetPixel(0, 0).A);
        Bitmap first = PinIcon.Get(24);
        Assert.Same(first, PinIcon.Get(24));
        Assert.Equal(new Size(24, 24), first.Size);
        Assert.Equal(0, first.GetPixel(0, 0).A);
    }

    [Fact]
    public void PinButtonUsesTopRightAndSmallSelectionFallback()
    {
        Rectangle viewport = new(0, 0, 800, 600);
        Rectangle large = new(100, 100, 300, 180);
        Rectangle button = PinButtonLayout.GetBounds(large, viewport, 96);
        Assert.True(large.Contains(button));
        Assert.Equal(32, button.Width);
        Assert.True(button.Right < large.Right);

        Rectangle small = new(100, 100, 45, 40);
        Rectangle outside = PinButtonLayout.GetBounds(small, viewport, 96);
        Assert.True(outside.Left > small.Right);
        Assert.False(outside.IntersectsWith(small));
        Rectangle nearRight = new(750, 100, 40, 40);
        Rectangle left = PinButtonLayout.GetBounds(nearRight, viewport, 96);
        Assert.True(left.Right < nearRight.Left);
    }

    [Fact]
    public void OverlayPinsCleanCurrentVisualAndPinHitWinsOverResize()
    {
        OnSta(() =>
        {
            using Bitmap desktop = Solid(360, 240, Color.Blue);
            using ScreenshotOverlayForm overlay = new(new Rectangle(0, 0, 360, 240), new Bitmap(desktop));
            overlay.SetSelectionForTesting(new Rectangle(30, 30, 250, 150));
            int requests = 0;
            Bitmap? pinned = null;
            overlay.PinRequested += (_, _) => { requests++; pinned = overlay.CreateCurrentVisualSelectionImage(); };
            Rectangle button = overlay.PinButtonBoundsForTesting;
            overlay.ClickPinForTesting();
            Assert.Equal(1, requests);
            Assert.Equal(new Size(250, 150), pinned!.Size);
            Assert.Equal(Color.Blue.ToArgb(), pinned.GetPixel(button.Left - 30 + 15, button.Top - 30 + 15).ToArgb());
            Assert.Equal(new Rectangle(30, 30, 250, 150), overlay.SelectionForTesting);
            pinned.Dispose();

            using Bitmap outfit = Solid(250, 150, Color.Red);
            Assert.True(overlay.TryBeginOutfitPreview());
            overlay.MarkOutfitGenerating(); overlay.MarkOutfitApplying();
            overlay.CompleteOutfitPreview(new Bitmap(outfit));
            overlay.ClickPinForTesting();
            Assert.Equal(Color.Red.ToArgb(), pinned!.GetPixel(125, 75).ToArgb());
            pinned.Dispose();

            using Bitmap translated = Solid(250, 150, Color.Purple);
            Assert.True(overlay.TryBeginTranslation());
            overlay.CompleteTranslation(new Bitmap(translated));
            overlay.ClickPinForTesting();
            Assert.Equal(Color.Purple.ToArgb(), pinned!.GetPixel(125, 75).ToArgb());
            overlay.Dispose();
            Assert.Equal(Color.Purple.ToArgb(), pinned.GetPixel(125, 75).ToArgb());
            pinned.Dispose();
            Assert.Equal(3, requests);
        });
    }

    [Fact]
    public void BusyOverlayDoesNotPinAndDirectModeSkipsConfirmation()
    {
        OnSta(() =>
        {
            using Bitmap source = Solid(320, 220, Color.Blue);
            using ScreenshotOverlayForm overlay = new(new Rectangle(0, 0, 320, 220), new Bitmap(source));
            overlay.SetSelectionForTesting(new Rectangle(10, 10, 200, 140));
            int requests = 0;
            overlay.PinRequested += (_, _) => requests++;
            Assert.True(overlay.TryBeginOutfitPreview());
            overlay.ClickPinForTesting();
            Assert.Equal(0, requests);
            overlay.FinishOutfitCancellation();
            Assert.True(overlay.TryBeginTranslation());
            overlay.ClickPinForTesting();
            Assert.Equal(0, requests);
            overlay.SetTranslationState(TranslationState.None);

            using ScreenshotOverlayForm direct = new(new Rectangle(0, 0, 320, 220), new Bitmap(source), pinOnSelection: true);
            Assert.True(direct.PinOnSelectionForTesting);
            int directPins = 0;
            direct.PinRequested += (_, _) => directPins++;
            direct.MouseDownAtForTesting(new Point(15, 15));
            direct.MouseUpAtForTesting(new Point(165, 115));
            Assert.Equal(1, directPins);
            Assert.False(direct.ToolbarVisibleForTesting);
        });
    }

    [Fact]
    public void PinButtonTracksSelectionMoveAndResize()
    {
        OnSta(() =>
        {
            using Bitmap source = Solid(500, 400, Color.Blue);
            using ScreenshotOverlayForm overlay = new(new Rectangle(0, 0, 500, 400), new Bitmap(source));
            overlay.SetSelectionForTesting(new Rectangle(40, 40, 250, 180));
            Rectangle before = overlay.PinButtonBoundsForTesting;
            overlay.MouseDownAtForTesting(new Point(100, 100));
            overlay.MouseMoveAtForTesting(new Point(120, 115));
            overlay.MouseUpAtForTesting(new Point(120, 115));
            Assert.Equal(before.X + 20, overlay.PinButtonBoundsForTesting.X);
            Assert.Equal(before.Y + 15, overlay.PinButtonBoundsForTesting.Y);
            Rectangle moved = overlay.SelectionForTesting;
            overlay.MouseDownAtForTesting(new Point(moved.Right, moved.Bottom));
            overlay.MouseMoveAtForTesting(new Point(moved.Right + 30, moved.Bottom + 20));
            overlay.MouseUpAtForTesting(new Point(moved.Right + 30, moved.Bottom + 20));
            Assert.Equal(before.X + 50, overlay.PinButtonBoundsForTesting.X);
        });
    }

    [Fact]
    public void PinnedWindowKeepsOriginalResolutionThroughZoomDragResizeAndSave()
    {
        OnSta(() =>
        {
            using Bitmap source = Solid(240, 120, Color.Green);
            ClipboardManager clipboard = new TestClipboardOwner().Manager;
            using PinnedImageForm pin = new(source, new Point(300, 200), clipboard);
            Assert.True(pin.TopMost);
            Assert.False(pin.ShowInTaskbar);
            Assert.Equal(FormBorderStyle.None, pin.FormBorderStyle);
            Assert.Equal(new Size(240, 120), pin.Size);
            Point anchor = new(pin.Left + 60, pin.Top + 30);
            pin.ZoomForTesting(anchor, 2f);
            Assert.Equal(new Size(480, 240), pin.Size);
            Assert.InRange(Math.Abs((pin.Left + 120) - anchor.X), 0, 1);
            Assert.InRange(Math.Abs((pin.Top + 60) - anchor.Y), 0, 1);
            pin.ZoomForTesting(anchor, 0.01f);
            Assert.Equal(0.25f, pin.ScaleForTesting);
            pin.ZoomForTesting(anchor, 10f);
            Assert.Equal(4f, pin.ScaleForTesting);
            pin.RestoreForTesting();
            Assert.Equal(1f, pin.ScaleForTesting);
            pin.ToggleTopMostForTesting(); Assert.False(pin.TopMost);
            pin.ToggleTopMostForTesting(); Assert.True(pin.TopMost);
            Point old = pin.Location;
            pin.StartDragForTesting(new Point(350, 250));
            pin.DragToForTesting(new Point(390, 280));
            Assert.Equal(old.X + 40, pin.Left);
            Assert.Equal(old.Y + 30, pin.Top);
            pin.StartResizeForTesting(new Point(pin.Right, pin.Bottom));
            pin.ResizeToForTesting(new Point(pin.Right + 120, pin.Bottom + 50));
            Assert.InRange(Math.Abs(pin.Width / (double)pin.Height - 2), 0, 0.02);
            using Bitmap original = pin.CloneOriginalForTesting();
            Assert.Equal(new Size(240, 120), original.Size);
            string path = Path.Combine(Path.GetTempPath(), "WechatStyleScreenshot-pin-" + Guid.NewGuid().ToString("N") + ".png");
            try { pin.SaveOriginalImage(path); using Bitmap saved = new(path); Assert.Equal(original.Size, saved.Size); }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }

    [Fact]
    public void ManagerSupportsManyWindowsAndCaptureRestoresOnlyVisible()
    {
        OnSta(() =>
        {
            using Bitmap source = Solid(100, 70, Color.Yellow);
            TestClipboardOwner backend = new();
            using PinnedWindowManager manager = new(() => { });
            PinnedImageForm[] forms = Enumerable.Range(0, 12).Select(i => manager.Pin(source, new Point(50 + i * 10, 50), backend.Manager)).ToArray();
            Assert.Equal(12, manager.Count);
            forms[0].Hide();
            using (manager.HideVisibleForCapture())
                Assert.All(forms, form => Assert.False(form.Visible));
            Assert.False(forms[0].Visible);
            Assert.All(forms.Skip(1), form => Assert.True(form.Visible));
            forms[1].Close();
            Assert.Equal(11, manager.Count);
            Assert.Equal(new[] { "复制", "保存图片", "恢复原始大小", "保持置顶", "关闭" },
                forms[2].ContextMenuStrip!.Items.Cast<ToolStripItem>().Select(item => item.Text));
            forms[2].PressEscapeForTesting();
            Assert.Equal(10, manager.Count);
            Assert.False(forms[3].IsDisposed);
            manager.CloseAll();
            Assert.Equal(0, manager.Count);
        });
    }

    [Fact]
    public void PinHotkeyMapsWithoutChangingExistingActions()
    {
        Assert.Equal(HotkeyAction.Screenshot, HotkeyManager.GetActionForId(HotkeyManager.ScreenshotHotkeyId));
        Assert.Equal(HotkeyAction.Ocr, HotkeyManager.GetActionForId(HotkeyManager.OcrHotkeyId));
        Assert.Equal(HotkeyAction.Pin, HotkeyManager.GetActionForId(HotkeyManager.PinHotkeyId));
    }

    [Fact]
    public void DirectPinThroughControllerClosesOverlayAndCreatesWindow()
    {
        OnSta(() =>
        {
            using PinnedWindowManager manager = new(() => { });
            using Bitmap source = Solid(320, 220, Color.Cyan);
            TestClipboardOwner clipboard = new();
            ScreenshotController controller = new(new ScreenCaptureEngine(), clipboard.Manager, new OcrService(), _ => { },
                pinnedWindows: manager);
            ScreenshotOverlayForm overlay = controller.BeginCaptureForTesting(source, pinOnSelection: true);
            overlay.MouseDownAtForTesting(new Point(20, 20));
            overlay.MouseUpAtForTesting(new Point(220, 140));
            Assert.True(overlay.IsDisposed);
            Assert.Equal(1, manager.Count);
            using Bitmap pinned = manager.WindowsForTesting[0].CloneOriginalForTesting();
            Assert.Equal(new Size(200, 120), pinned.Size);
            Assert.Equal(Color.Cyan.ToArgb(), pinned.GetPixel(100, 60).ToArgb());
            controller.Dispose();
        });
    }

    [Fact]
    public void CopyPinnedImageUsesOriginalPixelsAfterShrinkingWindow()
    {
        OnSta(() =>
        {
            TestClipboardOwner clipboard = new();
            using Bitmap source = Solid(400, 240, Color.Orange);
            using PinnedImageForm pin = new(source, new Point(200, 200), clipboard.Manager);
            pin.ZoomForTesting(new Point(pin.Left + 20, pin.Top + 20), 0.5f);
            Assert.True(pin.CopyOriginalForTesting());
            using Bitmap copied = clipboard.GetImage()!;
            Assert.Equal(new Size(400, 240), copied.Size);
            Assert.Equal(Color.Orange.ToArgb(), copied.GetPixel(200, 120).ToArgb());
        });
    }

    [Fact]
    public void DragClampLeavesWindowReachable()
    {
        Point clamped = PinnedImageForm.ClampVisible(new Point(9999, 9999), new Size(400, 200),
            [new Rectangle(0, 0, 800, 600)]);
        Assert.Equal(new Point(772, 572), clamped);
    }

    private static Bitmap Solid(int width, int height, Color color)
    {
        Bitmap image = new(width, height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(image);
        graphics.Clear(color);
        return image;
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        Assert.Null(failure);
    }

    private sealed class TestClipboardOwner
    {
        private readonly Backend _backend = new();
        public ClipboardManager Manager { get; }
        public TestClipboardOwner() => Manager = new ClipboardManager(_backend, _ => { }, 1);
        public Bitmap? GetImage() => _backend.GetImage() as Bitmap;
        private sealed class Backend : IImageClipboardBackend
        {
            private Bitmap? _image;
            public void Write(Bitmap image, bool persist) { _image?.Dispose(); _image = new Bitmap(image); }
            public bool ContainsImage() => _image is not null;
            public Image? GetImage() => _image is null ? null : new Bitmap(_image);
        }
    }
}
