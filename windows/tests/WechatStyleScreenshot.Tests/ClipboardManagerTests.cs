using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.Tests;

public sealed class ClipboardManagerTests
{
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
}
