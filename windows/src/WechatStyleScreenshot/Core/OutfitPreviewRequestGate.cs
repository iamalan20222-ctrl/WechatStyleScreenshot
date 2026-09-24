namespace WechatStyleScreenshot.Core;

public sealed class OutfitPreviewRequestGate
{
    private readonly object _sync = new();
    private CancellationTokenSource? _activeRequest;

    public bool TryAcquire(out CancellationTokenSource request)
    {
        lock (_sync)
        {
            if (_activeRequest is not null)
            {
                request = null!;
                return false;
            }

            request = new CancellationTokenSource();
            _activeRequest = request;
            return true;
        }
    }

    public void CancelActive()
    {
        CancellationTokenSource? request;
        lock (_sync) request = _activeRequest;
        try { request?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    public void Release(CancellationTokenSource request)
    {
        bool release;
        lock (_sync)
        {
            release = ReferenceEquals(_activeRequest, request);
            if (release) _activeRequest = null;
        }

        if (release) request.Dispose();
    }
}
