using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WechatStyleScreenshot.Services;

public sealed class ClipboardManager
{
    private readonly Action<string> _setText;
    private readonly IImageClipboardBackend _imageClipboard;
    private readonly Action<int> _delay;
    private readonly int _maxImageAttempts;

    public ClipboardManager(Action<string>? setText = null)
    {
        _setText = setText ?? Clipboard.SetText;
        _imageClipboard = new WindowsImageClipboardBackend();
        _delay = Thread.Sleep;
        _maxImageAttempts = 10;
    }

    internal ClipboardManager(IImageClipboardBackend imageClipboard, Action<int> delay, int maxImageAttempts)
    {
        _setText = Clipboard.SetText;
        _imageClipboard = imageClipboard;
        _delay = delay;
        _maxImageAttempts = Math.Max(1, maxImageAttempts);
    }

    public bool TrySetImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0) return false;

        using Bitmap copy = new(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(copy)) graphics.DrawImageUnscaled(image, Point.Empty);
        Trace.WriteLine($"[Clipboard] Type=Image Width={copy.Width} Height={copy.Height} PixelFormat={copy.PixelFormat} Apartment={Thread.CurrentThread.GetApartmentState()}");

        for (int attempt = 1; attempt <= _maxImageAttempts; attempt++)
        {
            try
            {
                Trace.WriteLine($"[Clipboard] CLIPBOARD_WRITE_ATTEMPT={attempt}");
                _imageClipboard.Write(copy, persist: true);
                using Image? readback = _imageClipboard.ContainsImage() ? _imageClipboard.GetImage() : null;
                bool verified = readback is not null && readback.Width == copy.Width && readback.Height == copy.Height;
                Trace.WriteLine($"[Clipboard] CLIPBOARD_VERIFY_RESULT={(verified ? "PASS" : "FAIL")}");
                if (verified)
                {
                    Trace.WriteLine($"[Clipboard] CLIPBOARD_WRITE_RESULT=PASS Attempt={attempt}");
                    return true;
                }
            }
            catch (ExternalException ex)
            {
                Trace.WriteLine($"[Clipboard] Attempt={attempt} Exception={ex.GetType().Name} HRESULT=0x{ex.HResult:X8}");
            }
            catch (ThreadStateException ex)
            {
                Trace.WriteLine($"[Clipboard] CLIPBOARD_WRITE_RESULT=FAIL Exception={ex.GetType().Name}");
                return false;
            }

            if (attempt < _maxImageAttempts) _delay(100);
        }

        Trace.WriteLine($"[Clipboard] CLIPBOARD_WRITE_RESULT=FAIL Attempts={_maxImageAttempts}");
        return false;
    }

    public bool TrySetText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        _setText(text);
        return true;
    }
}

internal interface IImageClipboardBackend
{
    void Write(Bitmap image, bool persist);
    bool ContainsImage();
    Image? GetImage();
}

internal sealed class WindowsImageClipboardBackend : IImageClipboardBackend
{
    public void Write(Bitmap image, bool persist) => Clipboard.SetDataObject(image, persist, 1, 0);
    public bool ContainsImage() => Clipboard.ContainsImage();
    public Image? GetImage() => Clipboard.GetImage();
}
