using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WechatStyleScreenshot.Services;

public sealed class HotkeyManager : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4141;
    private const uint ModAlt = 0x0001;

    private bool _registered;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    public HotkeyManager()
    {
        CreateHandle(new CreateParams());
    }

    public void RegisterAltA()
    {
        if (_registered)
        {
            return;
        }

        if (!RegisterHotKey(Handle, HotkeyId, ModAlt, (uint)Keys.A))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Alt+A is already in use or cannot be registered.");
        }

        _registered = true;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        DestroyHandle();
        _disposed = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
