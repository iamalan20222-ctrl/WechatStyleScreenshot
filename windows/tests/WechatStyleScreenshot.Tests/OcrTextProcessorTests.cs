using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.Tests;

public sealed class OcrTextProcessorTests
{
    [Fact]
    public void CleanCollapsesExcessBlankLinesAndTrimsEdges()
    {
        string actual = OcrTextProcessor.Clean("  Hello World  \n\n\n你好世界  ");

        Assert.Equal("Hello World\n\n你好世界", actual);
    }

    [Fact]
    public void CleanRemovesSpacesBetweenChineseCharactersButKeepsEnglishSpacing()
    {
        string actual = OcrTextProcessor.Clean("你 好 世 界\nHello   World 123");

        Assert.Equal("你好世界\nHello World 123", actual);
    }

    [Fact]
    public void CleanPreservesCommonPunctuationAndDigits()
    {
        string actual = OcrTextProcessor.Clean("价格： 123.45 元，OK!");

        Assert.Equal("价格： 123.45 元，OK!", actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n \t")]
    public void CleanReturnsEmptyForBlankInput(string? input)
    {
        Assert.Equal(string.Empty, OcrTextProcessor.Clean(input));
    }
}
