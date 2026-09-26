using System.Runtime.InteropServices;
using WechatStyleScreenshot.UI;

namespace WechatStyleScreenshot.Services;

public sealed class PinnedWindowManager : IDisposable
{
    private readonly List<PinnedImageForm> _windows = [];
    private readonly Action _flushCompositor;
    private bool _disposed;

    public PinnedWindowManager(Action? flushCompositor = null) => _flushCompositor = flushCompositor ?? (() => DwmFlush());

    public int Count => _windows.Count;
    internal IReadOnlyList<PinnedImageForm> WindowsForTesting => _windows;

    public PinnedImageForm Pin(Bitmap image, Point location, ClipboardManager clipboard)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PinnedImageForm window = new(image, location, clipboard);
        Register(window);
        try { window.Show(); }
        catch
        {
            window.FormClosed -= OnWindowClosed;
            _windows.Remove(window);
            window.Dispose();
            throw;
        }
        return window;
    }

    internal void Register(PinnedImageForm window)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_windows.Contains(window)) return;
        _windows.Add(window);
        window.FormClosed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is not PinnedImageForm window) return;
        window.FormClosed -= OnWindowClosed;
        _windows.Remove(window);
    }

    public IDisposable HideVisibleForCapture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PinnedImageForm[] visible = _windows.Where(window => !window.IsDisposed && window.Visible).ToArray();
        try
        {
            foreach (PinnedImageForm window in visible) window.Hide();
            if (visible.Length > 0) _flushCompositor();
            return new CaptureScope(visible);
        }
        catch
        {
            foreach (PinnedImageForm window in visible)
                if (!window.IsDisposed && !window.Visible) window.Show();
            throw;
        }
    }

    public void CloseAll()
    {
        foreach (PinnedImageForm window in _windows.ToArray())
        {
            if (!window.IsDisposed) window.Close();
            if (!window.IsDisposed) window.Dispose();
        }
        _windows.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        CloseAll();
        _disposed = true;
    }

    private sealed class CaptureScope(PinnedImageForm[] visible) : IDisposable
    {
        private bool _restored;
        public void Dispose()
        {
            if (_restored) return;
            _restored = true;
            foreach (PinnedImageForm window in visible)
                if (!window.IsDisposed) window.Show();
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
