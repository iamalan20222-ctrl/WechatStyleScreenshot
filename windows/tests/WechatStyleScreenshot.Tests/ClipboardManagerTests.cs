using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.Tests;

public sealed class ClipboardManagerTests
{
    [Fact]
    public void BusyTwiceThenWritesPersistentIndependentImage()
    {
        using FakeImageClipboard backend = new() { BusyWritesRemaining = 2 };
        List<int> delays = [];
        ClipboardManager clipboard = new(backend, delays.Add, maxImageAttempts: 3);
        using Bitmap source = new(12, 8, PixelFormat.Format24bppRgb);

        bool written = clipboard.TrySetImage(source);

        Assert.True(written);
        Assert.Equal(3, backend.WriteAttempts);
        Assert.Equal(new[] { 100, 100 }, delays);
        Assert.True(backend.PersistentCopyRequested);
        Assert.Equal(PixelFormat.Format32bppArgb, backend.WrittenPixelFormat);
        source.Dispose();
        using Image? readback = backend.GetImage();
        Assert.NotNull(readback);
        Assert.Equal(new Size(12, 8), readback.Size);
    }

    [Fact]
    public void AlwaysBusyReturnsFalseAfterBoundedRetries()
    {
        using FakeImageClipboard backend = new() { BusyWritesRemaining = 10 };
        int delays = 0;
        ClipboardManager clipboard = new(backend, _ => delays++, maxImageAttempts: 3);
        using Bitmap source = new(12, 8);

        Assert.False(clipboard.TrySetImage(source));
        Assert.Equal(3, backend.WriteAttempts);
        Assert.Equal(2, delays);
    }

    [Fact]
    public void ReadbackDimensionsMustMatchWrittenImage()
    {
        using FakeImageClipboard backend = new() { ReturnWrongSize = true };
        ClipboardManager clipboard = new(backend, _ => { }, maxImageAttempts: 2);
        using Bitmap source = new(12, 8);

        Assert.False(clipboard.TrySetImage(source));
        Assert.Equal(2, backend.WriteAttempts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \r\n ")]
    public void SetTextSkipsEmptyTextAndDoesNotTouchClipboard(string? text)
    {
        int writeCount = 0;
        ClipboardManager clipboard = new(_ => writeCount++);

        bool written = clipboard.TrySetText(text);

        Assert.False(written);
        Assert.Equal(0, writeCount);
    }

    [Fact]
    public void SetTextWritesNonEmptyText()
    {
        string? copied = null;
        ClipboardManager clipboard = new(value => copied = value);

        bool written = clipboard.TrySetText("Hello");

        Assert.True(written);
        Assert.Equal("Hello", copied);
    }

    private sealed class FakeImageClipboard : IImageClipboardBackend, IDisposable
    {
        private Bitmap? _image;
        public int BusyWritesRemaining { get; set; }
        public bool ReturnWrongSize { get; set; }
        public int WriteAttempts { get; private set; }
        public bool PersistentCopyRequested { get; private set; }
        public PixelFormat WrittenPixelFormat { get; private set; }

        public void Write(Bitmap image, bool persist)
        {
            WriteAttempts++;
            if (BusyWritesRemaining-- > 0)
                throw new ExternalException("Clipboard is busy", unchecked((int)0x800401D0));
            PersistentCopyRequested = persist;
            WrittenPixelFormat = image.PixelFormat;
            _image?.Dispose();
            _image = new Bitmap(image);
        }

        public bool ContainsImage() => _image is not null;
        public Image? GetImage() => _image is null ? null : ReturnWrongSize
            ? new Bitmap(_image.Width + 1, _image.Height)
            : new Bitmap(_image);

        public void Dispose() => _image?.Dispose();
    }
}
