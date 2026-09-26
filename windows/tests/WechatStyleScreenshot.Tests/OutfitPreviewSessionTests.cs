using System.Drawing;
using WechatStyleScreenshot.Core;

namespace WechatStyleScreenshot.Tests;

public class OutfitPreviewSessionTests
{
    [Fact]
    public void SessionMovesFromPreparingThroughGeneratingToSuccess()
    {
        using OutfitPreviewSession session = CreateSession(Color.Red);

        Assert.Equal(OutfitPreviewState.None, session.State);
        Assert.True(session.TryBegin());
        using Bitmap requestImage = session.CreateRequestImage();
        Assert.Equal(OutfitPreviewState.Preparing, session.State);
        session.MarkGenerating();
        Assert.Equal(OutfitPreviewState.Generating, session.State);
        session.MarkApplying();
        Assert.Equal(OutfitPreviewState.Applying, session.State);
        session.Complete(CreateBitmap(Color.Blue));

        Assert.Equal(OutfitPreviewState.Success, session.State);
        Assert.True(session.HasResult);
    }

    [Fact]
    public void SessionDoesNotStartAnotherRequestWhileBusy()
    {
        using OutfitPreviewSession session = CreateSession(Color.Red);
        Assert.True(session.TryBegin());
        using Bitmap first = session.CreateRequestImage();
        session.MarkGenerating();
        Assert.False(session.TryBegin());
        Assert.Equal(OutfitPreviewState.Generating, session.State);
    }

    [Fact]
    public void FailureRestoresOriginalForConfirmation()
    {
        using OutfitPreviewSession session = CreateSession(Color.Red);
        Assert.True(session.TryBegin());
        using Bitmap request = session.CreateRequestImage();
        session.MarkGenerating();

        session.Fail("生成失败，请重试");

        using Bitmap output = session.CreateConfirmationImage();
        Assert.Equal(OutfitPreviewState.Error, session.State);
        Assert.False(session.HasResult);
        Assert.Equal(Color.Red.ToArgb(), output.GetPixel(0, 0).ToArgb());
    }

    [Fact]
    public void CancellationRestoresOriginalAndAllowsRetry()
    {
        using OutfitPreviewSession session = CreateSession(Color.Red);
        Assert.True(session.TryBegin());
        using Bitmap request = session.CreateRequestImage();
        session.MarkGenerating();

        session.Cancel();

        Assert.Equal(OutfitPreviewState.None, session.State);
        using Bitmap output = session.CreateConfirmationImage();
        Assert.Equal(Color.Red.ToArgb(), output.GetPixel(0, 0).ToArgb());
        Assert.True(session.TryBegin());
        using Bitmap retry = session.CreateRequestImage();
    }

    [Fact]
    public void RegenerationAlwaysReturnsOriginalNotPreviousResult()
    {
        using OutfitPreviewSession session = CreateSession(Color.Red);
        Assert.True(session.TryBegin());
        using Bitmap firstRequest = session.CreateRequestImage();
        session.MarkGenerating();
        session.MarkApplying();
        session.Complete(CreateBitmap(Color.Blue));

        Assert.True(session.TryBegin());
        using Bitmap secondRequest = session.CreateRequestImage();
        Assert.Equal(Color.Red.ToArgb(), secondRequest.GetPixel(0, 0).ToArgb());
    }

    [Fact]
    public void ConfirmationUsesAiResultWhenPresentOtherwiseUsesOriginal()
    {
        using OutfitPreviewSession session = CreateSession(Color.Red);
        using (Bitmap original = session.CreateConfirmationImage())
        {
            Assert.Equal(Color.Red.ToArgb(), original.GetPixel(0, 0).ToArgb());
        }

        Assert.True(session.TryBegin());
        using Bitmap request = session.CreateRequestImage();
        session.MarkGenerating();
        session.MarkApplying();
        session.Complete(CreateBitmap(Color.Blue));

        using Bitmap result = session.CreateConfirmationImage();
        Assert.Equal(Color.Blue.ToArgb(), result.GetPixel(0, 0).ToArgb());
    }

    [Fact]
    public void ReplacingSelectionInvalidatesOldResultAndUsesNewOriginal()
    {
        using OutfitPreviewSession session = CreateSession(Color.Red);
        Assert.True(session.TryBegin());
        using Bitmap request = session.CreateRequestImage();
        session.MarkGenerating();
        session.MarkApplying();
        session.Complete(CreateBitmap(Color.Blue));

        using Bitmap newSelection = CreateBitmap(Color.Green);
        session.ReplaceOriginal(newSelection);

        Assert.Equal(OutfitPreviewState.None, session.State);
        Assert.False(session.HasResult);
        using Bitmap output = session.CreateConfirmationImage();
        Assert.Equal(Color.Green.ToArgb(), output.GetPixel(0, 0).ToArgb());
    }

    [Fact]
    public void SessionKeepsItsOwnCopyOfTheInitialSelection()
    {
        using Bitmap input = CreateBitmap(Color.Red);
        using OutfitPreviewSession session = new(input);
        using Graphics graphics = Graphics.FromImage(input);
        graphics.Clear(Color.Yellow);

        using Bitmap request = session.CreateRequestImage();

        Assert.Equal(Color.Red.ToArgb(), request.GetPixel(0, 0).ToArgb());
    }

    private static OutfitPreviewSession CreateSession(Color color) => new(CreateBitmap(color));

    private static Bitmap CreateBitmap(Color color)
    {
        Bitmap bitmap = new(2, 2);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }
}
