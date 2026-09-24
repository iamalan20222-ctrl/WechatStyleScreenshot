using System.Drawing;
using TesseractOCR;
using TesseractOCR.Enums;
using PixImage = TesseractOCR.Pix.Image;

namespace WechatStyleScreenshot.Services;

public sealed class OcrService : IDisposable
{
    private readonly string _baseDirectory;
    private readonly SemaphoreSlim _engineLock = new(1, 1);
    private Engine? _engine;
    private bool _disposed;

    public OcrService(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
    }

    public Task<string> RecognizeAsync(Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.Run(() => Recognize(image));
    }

    private string Recognize(Bitmap image)
    {
        _engineLock.Wait();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureAssetsExist();

            _engine ??= new Engine(
                Path.Combine(_baseDirectory, "tessdata"),
                "chi_sim+eng",
                EngineMode.LstmOnly);

            using MemoryStream stream = new();
            image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            using PixImage pixImage = PixImage.LoadFromMemory(stream.ToArray());
            using Page page = _engine.Process(pixImage);
            return OcrTextProcessor.Clean(page.Text);
        }
        catch (DllNotFoundException ex)
        {
            throw new OcrDependencyException(
                "OCR 原生运行组件缺失。请重新下载完整发布包，并将 DLL 与程序放在同一目录。",
                ex);
        }
        catch (BadImageFormatException ex)
        {
            throw new OcrDependencyException("OCR 原生运行组件与当前程序架构不匹配。请使用 Windows x64 发布包。", ex);
        }
        finally
        {
            _engineLock.Release();
        }
    }

    private void EnsureAssetsExist()
    {
        string tessdata = Path.Combine(_baseDirectory, "tessdata");
        foreach (string model in new[] { "chi_sim.traineddata", "eng.traineddata" })
        {
            if (!File.Exists(Path.Combine(tessdata, model)))
            {
                throw new OcrDependencyException($"OCR 模型文件缺失：{model}。请重新解压完整发布包。");
            }
        }

        string x64Directory = Path.Combine(_baseDirectory, "x64");
        foreach (string library in new[] { "tesseract55.dll", "leptonica-1.85.0.dll" })
        {
            if (!File.Exists(Path.Combine(x64Directory, library)))
            {
                throw new OcrDependencyException($"OCR 原生运行组件缺失：{library}。请重新解压完整发布包。");
            }
        }

        string runtimeDirectory = Path.Combine(_baseDirectory, "runtimes", "win-x64", "native");
        foreach (string runtime in new[] { "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll" })
        {
            if (!File.Exists(Path.Combine(_baseDirectory, runtime)) &&
                !File.Exists(Path.Combine(runtimeDirectory, runtime)))
            {
                throw new OcrDependencyException($"OCR 所需的 app-local Visual C++ Runtime 文件缺失：{runtime}。请重新解压完整发布包。");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _engineLock.Wait();
        try
        {
            if (_disposed)
            {
                return;
            }

            _engine?.Dispose();
            _engine = null;
            _disposed = true;
        }
        finally
        {
            _engineLock.Release();
        }
    }
}

public sealed class OcrDependencyException : Exception
{
    public OcrDependencyException(string message) : base(message) { }

    public OcrDependencyException(string message, Exception innerException) : base(message, innerException) { }
}
