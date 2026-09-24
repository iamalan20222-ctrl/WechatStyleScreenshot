using System.Drawing;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.Tests;

public sealed class OcrDependencyTests
{
    [Fact]
    public async Task RecognizeReportsMissingModelInsteadOfFailingWithNativeLoaderError()
    {
        string emptyDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDirectory);
        using Bitmap image = new(8, 8);
        using OcrService service = new(emptyDirectory);

        try
        {
            OcrDependencyException exception = await Assert.ThrowsAsync<OcrDependencyException>(
                () => service.RecognizeAsync(image));

            Assert.Contains("chi_sim.traineddata", exception.Message);
        }
        finally
        {
            Directory.Delete(emptyDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RecognizeReportsMissingAppLocalRuntime()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string tessdata = Path.Combine(directory, "tessdata");
        string x64 = Path.Combine(directory, "x64");
        Directory.CreateDirectory(tessdata);
        Directory.CreateDirectory(x64);
        File.WriteAllBytes(Path.Combine(tessdata, "chi_sim.traineddata"), []);
        File.WriteAllBytes(Path.Combine(tessdata, "eng.traineddata"), []);
        File.WriteAllBytes(Path.Combine(x64, "tesseract55.dll"), []);
        File.WriteAllBytes(Path.Combine(x64, "leptonica-1.85.0.dll"), []);
        using Bitmap image = new(8, 8);
        using OcrService service = new(directory);

        try
        {
            OcrDependencyException exception = await Assert.ThrowsAsync<OcrDependencyException>(
                () => service.RecognizeAsync(image));

            Assert.Contains("vcruntime140.dll", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
