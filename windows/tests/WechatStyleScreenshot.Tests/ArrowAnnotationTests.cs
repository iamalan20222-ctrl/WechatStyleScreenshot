using System.Drawing;
using WechatStyleScreenshot.Core;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot.Tests;

public class ArrowAnnotationTests
{
    [Fact]
    public void SessionRejectsTinyArrowsAndUndoesLast()
    {
        AnnotationSession session = new();
        Assert.False(session.Add(new PointF(1, 1), new PointF(4, 4), Color.Red, 4));
        Assert.True(session.Add(new PointF(5, 5), new PointF(40, 40), Color.Red, 4));
        Assert.True(session.Add(new PointF(5, 20), new PointF(40, 20), Color.Blue, 2));
        Assert.True(session.Undo());
        Assert.Single(session.Arrows);
        Assert.Equal(Color.Red, session.Arrows[0].Color);
    }

    [Fact]
    public void OverlayExportsArrowsButOriginalAndAiInputRemainClean()
    {
        OnSta(() =>
        {
            using Bitmap desktop = Solid(420, 300, Color.White);
            using ScreenshotOverlayForm overlay = new(new Rectangle(0, 0, 420, 300), new Bitmap(desktop));
            overlay.SetSelectionForTesting(new Rectangle(40, 40, 250, 180));
            overlay.ClickArrowForTesting();
            Assert.True(overlay.ArrowModeForTesting);
            Assert.True(overlay.ArrowPaletteOpenForTesting);
            overlay.MouseDownAtForTesting(new Point(85, 95));
            overlay.MouseMoveAtForTesting(new Point(180, 95));
            overlay.MouseUpAtForTesting(new Point(180, 95));
            Assert.Equal(1, overlay.ArrowCountForTesting);

            using Bitmap clean = overlay.CreateCurrentVisualSelectionImage();
            using Bitmap marked = overlay.CreateCurrentVisualSelectionImage(includeAnnotations: true);
            Assert.Equal(Color.White.ToArgb(), clean.GetPixel(100, 55).ToArgb());
            Assert.NotEqual(Color.White.ToArgb(), marked.GetPixel(100, 55).ToArgb());
            Assert.True(overlay.TryBeginOutfitPreview());
            using Bitmap aiInput = overlay.CreateOutfitRequestImage();
            Assert.Equal(Color.White.ToArgb(), aiInput.GetPixel(100, 55).ToArgb());
            overlay.FinishOutfitCancellation();
            overlay.PressUndoForTesting();
            Assert.Equal(0, overlay.ArrowCountForTesting);
        });
    }

    [Fact]
    public void ToolbarAndSelectionChangesDoNotCreateStrayArrows()
    {
        OnSta(() =>
        {
            using Bitmap desktop = Solid(420, 300, Color.White);
            using ScreenshotOverlayForm overlay = new(new Rectangle(0, 0, 420, 300), new Bitmap(desktop));
            overlay.SetSelectionForTesting(new Rectangle(40, 40, 250, 180));
            overlay.ClickArrowForTesting();
            Rectangle button = overlay.ArrowButtonBoundsForTesting;
            overlay.MouseDownAtForTesting(new Point(button.Left + 15, button.Top + 15));
            Assert.Equal(0, overlay.ArrowCountForTesting);
            overlay.ClickArrowForTesting();
            overlay.MouseDownAtForTesting(new Point(85, 95));
            overlay.MouseMoveAtForTesting(new Point(180, 95));
            overlay.MouseUpAtForTesting(new Point(180, 95));
            Assert.Equal(1, overlay.ArrowCountForTesting);
            overlay.SetSelectionForTesting(new Rectangle(50, 50, 240, 170));
            Assert.Equal(0, overlay.ArrowCountForTesting);
        });
    }

    private static Bitmap Solid(int width, int height, Color color)
    {
        Bitmap bitmap = new(width, height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        Assert.Null(failure);
    }
}
