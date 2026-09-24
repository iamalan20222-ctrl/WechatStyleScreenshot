using System.Drawing;
using System.Net;
using System.Text;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.Tests;

public class AiOutfitPreviewServiceTests
{
    [Fact]
    public async Task MissingApiKeyReturnsConfigurationErrorWithoutRequest()
    {
        using HttpClient client = new(new StubHandler((_, _) => throw new Xunit.Sdk.XunitException("Unexpected HTTP request.")));
        AiOutfitPreviewService service = new(client, apiKey: null);
        using Bitmap image = new(2, 2);

        OutfitPreviewResult result = await service.GenerateAsync(image, new OutfitPreviewOptions());

        Assert.Equal(OutfitPreviewStatus.ApiKeyMissing, result.Status);
    }

    [Fact]
    public async Task SendsImagePromptAndSafetyWatermarkThenReturnsImageInMemory()
    {
        byte[] pixelPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jgXcAAAAASUVORK5CYII=");
        using HttpClient client = new(new StubHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer test-key", request.Headers.Authorization?.ToString());
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("same model identity", body);
            Assert.Contains("data:image/png;base64,", body);
            Assert.Contains("\"watermark\":true", body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"data\":[{{\"b64_json\":\"{Convert.ToBase64String(pixelPng)}\"}}]}}", Encoding.UTF8, "application/json")
            };
        }));
        AiOutfitPreviewService service = new(client, "test-key", "test-model");
        using Bitmap image = new(2, 2);

        OutfitPreviewResult result = await service.GenerateAsync(image, new OutfitPreviewOptions());

        Assert.Equal(OutfitPreviewStatus.Success, result.Status);
        Assert.NotNull(result.Image);
        result.Image!.Dispose();
    }

    [Fact]
    public async Task EmptyProviderResultIsReportedWithoutImage()
    {
        using HttpClient client = new(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}")
        })));
        AiOutfitPreviewService service = new(client, "test-key", "test-model");
        using Bitmap image = new(2, 2);

        OutfitPreviewResult result = await service.GenerateAsync(image, new OutfitPreviewOptions());

        Assert.Equal(OutfitPreviewStatus.NoUsableImage, result.Status);
        Assert.Null(result.Image);
    }

    [Fact]
    public async Task ProviderErrorsAreSanitized()
    {
        using HttpClient client = new(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("private provider diagnostics")
        })));
        AiOutfitPreviewService service = new(client, "test-key", "test-model");
        using Bitmap image = new(2, 2);

        OutfitPreviewResult result = await service.GenerateAsync(image, new OutfitPreviewOptions());

        Assert.Equal(OutfitPreviewStatus.Failed, result.Status);
        Assert.Null(result.Image);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }
}
