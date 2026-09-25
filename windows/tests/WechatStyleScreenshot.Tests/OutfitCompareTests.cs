using System.Drawing;
using WechatStyleScreenshot.Core;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot.Tests;

public class OutfitCompareTests
{
    private static readonly Rectangle Selection = new(30, 40, 200, 150);
    private static readonly Point Center = new(130, 115);

    [Fact]
    public void HoldingSelectionShowsOriginalUntilMouseUpOutside()
    {
        RunOnSta(() =>
        {
            using ScreenshotOverlayForm overlay = CreateOverlayWithResult();
            Assert.Equal(Color.Blue.ToArgb(), overlay.RenderSelectionCenterForTesting().ToArgb());
            overlay.MouseDownAtForTesting(Center);
            Assert.True(overlay.IsHoldingOriginalPreviewForTesting);
            Assert.Equal(Color.Red.ToArgb(), overlay.RenderSelectionCenterForTesting().ToArgb());
            overlay.MouseMoveAtForTesting(new Point(5, 5));
            Assert.True(overlay.IsHoldingOriginalPreviewForTesting);
            overlay.MouseUpAtForTesting(new Point(5, 5));
            Assert.False(overlay.IsHoldingOriginalPreviewForTesting);
            Assert.Equal(Color.Blue.ToArgb(), overlay.RenderSelectionCenterForTesting().ToArgb());
        });
    }

    [Fact]
    public void ToolbarAndResizeHandleDoNotStartCompare()
    {
        RunOnSta(() =>
        {
            using ScreenshotOverlayForm overlay = CreateOverlayWithResult();
            Assert.True(overlay.ClickOutfitButtonForTesting());
            Assert.False(overlay.IsHoldingOriginalPreviewForTesting);
            overlay.ClickOutsideStylePickerForTesting();
            overlay.MouseDownAtForTesting(Selection.Location);
            Assert.False(overlay.IsHoldingOriginalPreviewForTesting);
            overlay.MouseUpAtForTesting(Selection.Location);
        });
    }

    [Fact]
    public void WithoutAiResultSelectionStillMoves()
    {
        RunOnSta(() =>
        {
            using ScreenshotOverlayForm overlay = CreateOverlay();
            overlay.MouseDownAtForTesting(Center);
            Assert.False(overlay.IsHoldingOriginalPreviewForTesting);
            overlay.MouseMoveAtForTesting(new Point(Center.X + 10, Center.Y + 12));
            overlay.MouseUpAtForTesting(new Point(Center.X + 10, Center.Y + 12));
            Assert.Equal(new Rectangle(40, 52, 200, 150), overlay.SelectionForTesting);
        });
    }

    [Fact]
    public void LostCaptureAndDeactivateRestoreResult()
    {
        RunOnSta(() =>
        {
            using ScreenshotOverlayForm overlay = CreateOverlayWithResult();
            overlay.MouseDownAtForTesting(Center);
            overlay.LoseCaptureForTesting();
            Assert.False(overlay.IsHoldingOriginalPreviewForTesting);
            Assert.Equal(Color.Blue.ToArgb(), overlay.RenderSelectionCenterForTesting().ToArgb());
            overlay.MouseDownAtForTesting(Center);
            overlay.DeactivateForTesting();
            Assert.False(overlay.IsHoldingOriginalPreviewForTesting);
            Assert.Equal(Color.Blue.ToArgb(), overlay.RenderSelectionCenterForTesting().ToArgb());
        });
    }

    [Fact]
    public void RegenerateWhileHoldingResetsCompareAndUsesOriginal()
    {
        RunOnSta(() =>
        {
            using ScreenshotOverlayForm overlay = CreateOverlayWithResult();
            overlay.MouseDownAtForTesting(Center);
            Assert.True(overlay.TryBeginOutfitPreview());
            Assert.False(overlay.IsHoldingOriginalPreviewForTesting);
            Assert.Equal(OutfitPreviewState.Preparing, overlay.OutfitStateForTesting);
            using Bitmap request = overlay.CreateOutfitRequestImage();
            Assert.Equal(Color.Red.ToArgb(), request.GetPixel(100, 75).ToArgb());
        });
    }

    [Fact]
    public void ConfirmAfterCompareStillProvidesAiResult()
    {
        RunOnSta(() =>
        {
            using ScreenshotOverlayForm overlay = CreateOverlayWithResult();
            overlay.MouseDownAtForTesting(Center);
            overlay.MouseUpAtForTesting(Center);
            bool confirmed = false;
            overlay.SelectionCompleted += (_, _) =>
            {
                using Bitmap? image = overlay.CreateOutfitResultImage();
                Assert.NotNull(image);
                Assert.Equal(Color.Blue.ToArgb(), image.GetPixel(100, 75).ToArgb());
                confirmed = true;
            };
            overlay.ClickConfirmButtonForTesting();
            Assert.True(confirmed);
        });
    }

    private static ScreenshotOverlayForm CreateOverlay()
    {
        using Bitmap source = new(320, 260);
        using (Graphics graphics = Graphics.FromImage(source)) graphics.Clear(Color.Red);
        ScreenshotOverlayForm overlay = new(new Rectangle(0, 0, 320, 260), new Bitmap(source));
        overlay.Show();
        overlay.SetSelectionForTesting(Selection);
        return overlay;
    }

    private static ScreenshotOverlayForm CreateOverlayWithResult()
    {
        ScreenshotOverlayForm overlay = CreateOverlay();
        Assert.True(overlay.TryBeginOutfitPreview());
        Bitmap result = new(Selection.Width, Selection.Height);
        using (Graphics graphics = Graphics.FromImage(result)) graphics.Clear(Color.Blue);
        overlay.CompleteOutfitPreview(result);
        return overlay;
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
