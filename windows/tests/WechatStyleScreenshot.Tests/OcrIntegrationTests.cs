using System.Drawing;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.Tests;

public sealed class OcrIntegrationTests
{
    [Fact]
    public async Task RecognizesEnglishTextFromFixedBitmap()
    {
        using Bitmap bitmap = LoadFixture("ocr-english.png");
        using OcrService service = new();
        string text = await service.RecognizeAsync(bitmap);

        Assert.Contains("Hello", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("123", text);
    }

    [Fact]
    public async Task RecognizesSimplifiedChineseFromFixedBitmap()
    {
        using Bitmap bitmap = LoadFixture("ocr-chinese.png");
        using OcrService service = new();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            string text = await service.RecognizeAsync(bitmap);

            Assert.Contains("你好世界", text);
        }
    }

    private static Bitmap LoadFixture(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        return new Bitmap(path);
    }
}
