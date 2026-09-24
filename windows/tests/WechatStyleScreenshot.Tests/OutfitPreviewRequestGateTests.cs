using System.Drawing;
using WechatStyleScreenshot.Core;

namespace WechatStyleScreenshot.Tests;

public class OutfitPreviewRequestGateTests
{
    [Fact]
    public void RapidSecondClickDoesNotAcquireRequestOrAdvanceSelectionState()
    {
        using Bitmap image = new(2, 2);
        using OutfitPreviewSession session = new(image);
        OutfitPreviewRequestGate gate = new();

        Assert.True(gate.TryAcquire(out CancellationTokenSource first));
        Assert.True(session.TryBegin());
        session.MarkGenerating();
        Assert.True(session.IsBusy);
        session.MarkApplying();
        session.Complete(new Bitmap(2, 2));
        Assert.Equal(OutfitPreviewState.Success, session.State);

        Assert.False(gate.TryAcquire(out _));
        Assert.Equal(OutfitPreviewState.Success, session.State);

        CancellationToken firstToken = first.Token;
        gate.Release(first);
        Assert.True(gate.TryAcquire(out CancellationTokenSource next));
        Assert.NotSame(first, next);
        Assert.NotEqual(firstToken, next.Token);
        Assert.False(next.IsCancellationRequested);
        gate.Release(next);
    }

    [Fact]
    public void CancellationSourceIsFreshForEachCompletedRequest()
    {
        OutfitPreviewRequestGate gate = new();
        Assert.True(gate.TryAcquire(out CancellationTokenSource first));
        gate.CancelActive();
        Assert.True(first.IsCancellationRequested);
        gate.Release(first);

        Assert.True(gate.TryAcquire(out CancellationTokenSource second));
        Assert.NotSame(first, second);
        Assert.False(second.IsCancellationRequested);
        gate.Release(second);
    }
}
