using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Text;
using System.Text.Json;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.Tests;

public class ImageEditProviderTests
{
    [Fact]
    public async Task QwenUsesOfficialCompatibleEditWithOriginalImageAndDownloadsResult()
    {
        string? body = null;
        int requests = 0;
        using HttpClient client = new(new StubHandler(async (request, _) =>
        {
            requests++;
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("https://result.aliyuncs.com/test.png", request.RequestUri!.ToString());
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Png()) };
            }
            Assert.Equal("https://workspace123.cn-beijing.maas.aliyuncs.com/compatible-mode/v1/images/generations",
                request.RequestUri!.ToString());
            Assert.Equal("test-qwen-key", request.Headers.Authorization!.Parameter);
            body = await request.Content!.ReadAsStringAsync();
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"url\":\"https://result.aliyuncs.com/test.png\"}]}")
            };
        }));
        OutfitAppSettings settings = new() { QwenWorkspaceId = "workspace123" };
        IImageEditProvider provider = ImageEditProviderFactory.Create(ImageEditProviderKind.Qwen, settings, "test-qwen-key", client);
        using Bitmap source = new(20, 30);
        OutfitPreviewResult result = await provider.EditAsync(source, "locked prompt", CancellationToken.None);
        using Bitmap? output = result.Image;
        Assert.Equal(OutfitPreviewStatus.Success, result.Status);
        Assert.Equal(2, requests);
        using JsonDocument json = JsonDocument.Parse(body!);
        Assert.StartsWith("data:image/png;base64,", json.RootElement.GetProperty("image").GetString());
        Assert.Equal("locked prompt", json.RootElement.GetProperty("prompt").GetString());
        Assert.Equal("qwen-image-3.0-pro", json.RootElement.GetProperty("model").GetString());
        Assert.False(json.RootElement.GetProperty("prompt_extend").GetBoolean());
    }

    [Theory]
    [InlineData(ImageEditProviderKind.Qwen, HttpStatusCode.Unauthorized, OutfitPreviewStatus.Unauthorized)]
    [InlineData(ImageEditProviderKind.Qwen, HttpStatusCode.Forbidden, OutfitPreviewStatus.Unauthorized)]
    [InlineData(ImageEditProviderKind.OpenAI, HttpStatusCode.BadRequest, OutfitPreviewStatus.InvalidRequest)]
    [InlineData(ImageEditProviderKind.OpenAI, HttpStatusCode.PaymentRequired, OutfitPreviewStatus.QuotaExceeded)]
    public async Task ProviderErrorsAreStandardized(ImageEditProviderKind kind, HttpStatusCode code, OutfitPreviewStatus expected)
    {
        using HttpClient client = new(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(code)
        {
            Content = new StringContent("{\"error\":{\"code\":\"TestError\",\"message\":\"safe\"}}")
        })));
        OutfitAppSettings settings = new() { QwenWorkspaceId = "workspace123" };
        IImageEditProvider provider = ImageEditProviderFactory.Create(kind, settings, "test-key", client);
        using Bitmap image = new(10, 10);
        OutfitPreviewResult result = await provider.EditAsync(image, "prompt", CancellationToken.None);
        Assert.Equal(expected, result.Status);
        Assert.Equal((int)code, result.HttpStatusCode);
        Assert.Equal("TestError", result.ProviderCode);
    }

    [Theory]
    [InlineData(ImageEditProviderKind.Qwen)]
    [InlineData(ImageEditProviderKind.OpenAI)]
    public async Task InvalidSuccessResponseHasNoUsableImage(ImageEditProviderKind kind)
    {
        using HttpClient client = new(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}")
        })));
        IImageEditProvider provider = ImageEditProviderFactory.Create(kind,
            new OutfitAppSettings { QwenWorkspaceId = "workspace123" }, "test-key", client);
        using Bitmap image = new(10, 10);
        OutfitPreviewResult result = await provider.EditAsync(image, "prompt", CancellationToken.None);
        Assert.Equal(OutfitPreviewStatus.NoUsableImage, result.Status);
    }

    [Fact]
    public async Task OpenAiEditRequestHasImageAndHighFidelity()
    {
        string? body = null;
        using HttpClient client = new(new StubHandler(async (request, _) =>
        {
            Assert.Equal("https://api.openai.com/v1/images/edits", request.RequestUri!.ToString());
            body = await request.Content!.ReadAsStringAsync();
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"data\":[{{\"b64_json\":\"{Convert.ToBase64String(Png())}\"}}]}}")
            };
        }));
        IImageEditProvider provider = ImageEditProviderFactory.Create(ImageEditProviderKind.OpenAI,
            new OutfitAppSettings(), "test-key", client);
        using Bitmap image = new(12, 18);
        OutfitPreviewResult result = await provider.EditAsync(image, "locked prompt", CancellationToken.None);
        using Bitmap? output = result.Image;
        Assert.Equal(OutfitPreviewStatus.Success, result.Status);
        using JsonDocument json = JsonDocument.Parse(body!);
        Assert.Equal("gpt-image-2.5-sunburst", json.RootElement.GetProperty("model").GetString());
        Assert.Equal("high", json.RootElement.GetProperty("input_fidelity").GetString());
        Assert.StartsWith("data:image/png;base64,", json.RootElement.GetProperty("images")[0].GetProperty("image_url").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, OutfitPreviewStatus.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, OutfitPreviewStatus.ServerError)]
    public async Task TransientErrorsRetryThenReturnStandardizedStatus(HttpStatusCode status, OutfitPreviewStatus expected)
    {
        int attempts = 0;
        using HttpClient client = new(new StubHandler((_, _) =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{\"error\":{\"code\":\"Transient\",\"message\":\"retry later\"}}")
            });
        }));
        IImageEditProvider provider = ImageEditProviderFactory.Create(ImageEditProviderKind.OpenAI,
            new OutfitAppSettings(), "test-key", client);
        using Bitmap image = new(10, 10);
        OutfitPreviewResult result = await provider.EditAsync(image, "prompt", CancellationToken.None);
        Assert.Equal(expected, result.Status);
        Assert.Equal(3, attempts);
        Assert.Equal(2, result.RetryCount);
    }

    [Theory]
    [InlineData(true, OutfitPreviewStatus.TimedOut)]
    [InlineData(false, OutfitPreviewStatus.NetworkError)]
    public async Task TransportFailuresAreStandardized(bool timeout, OutfitPreviewStatus expected)
    {
        using HttpClient client = new(new StubHandler((_, _) => timeout
            ? throw new TaskCanceledException()
            : throw new HttpRequestException()));
        IImageEditProvider provider = ImageEditProviderFactory.Create(ImageEditProviderKind.OpenAI,
            new OutfitAppSettings(), "test-key", client);
        using Bitmap image = new(10, 10);
        OutfitPreviewResult result = await provider.EditAsync(image, "prompt", CancellationToken.None);
        Assert.Equal(expected, result.Status);
    }

    private static byte[] Png()
    {
        using Bitmap image = new(2, 2);
        using MemoryStream stream = new();
        image.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }
}
