using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WechatStyleScreenshot.Services;

public sealed class HotkeyManager : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    public const int ScreenshotHotkeyId = 0x4141;
    public const int OcrHotkeyId = 0x4142;
    public const int PinHotkeyId = 0x4143;
    private const uint ModAlt = 0x0001;
    private const uint ModShift = 0x0004;

    private readonly HashSet<int> _registeredIds = [];
    private bool _disposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public HotkeyManager()
    {
        CreateHandle(new CreateParams());
    }

    public void RegisterScreenshotHotkey()
    {
        Register(ScreenshotHotkeyId, ModAlt, Keys.A);
    }

    public void RegisterOcrHotkey()
    {
        Register(OcrHotkeyId, ModAlt | ModShift, Keys.A);
    }

    public void RegisterPinHotkey() => Register(PinHotkeyId, ModAlt | ModShift, Keys.P);

    public void RegisterAltA()
    {
        RegisterScreenshotHotkey();
    }

    private void Register(int id, uint modifiers, Keys key)
    {
        if (_registeredIds.Contains(id))
        {
            return;
        }

        if (!RegisterHotKey(Handle, id, modifiers, (uint)key))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Hotkey {GetActionForId(id)} is already in use or cannot be registered.");
        }

        _registeredIds.Add(id);
    }

    public void Unregister()
    {
        foreach (int id in _registeredIds)
            UnregisterHotKey(Handle, id);
        _registeredIds.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && GetActionForId(m.WParam.ToInt32()) is HotkeyAction action)
        {
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(action));
            return;
        }

        base.WndProc(ref m);
    }

    public static HotkeyAction? GetActionForId(int id)
    {
        return id switch
        {
            ScreenshotHotkeyId => HotkeyAction.Screenshot,
            OcrHotkeyId => HotkeyAction.Ocr,
            PinHotkeyId => HotkeyAction.Pin,
            _ => null
        };
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

public enum HotkeyAction
{
    Screenshot,
    Ocr,
    Pin
}

public sealed class HotkeyPressedEventArgs : EventArgs
{
    public HotkeyAction Action { get; }

    public HotkeyPressedEventArgs(HotkeyAction action)
    {
        Action = action;
    }
}
