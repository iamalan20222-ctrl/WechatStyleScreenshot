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
    public async Task RateLimitResponseIncludesSafeProviderDiagnostics()
    {
        using HttpClient client = new(new StubHandler((_, _) =>
        {
            HttpResponseMessage response = new(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\":{\"code\":\"RequestBurstTooFast\",\"message\":\"Slow down\"},\"request_id\":\"body-id\"}")
            };
            response.Headers.TryAddWithoutValidation("x-request-id", "header-id");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
            return Task.FromResult(response);
        }));
        AiOutfitPreviewService service = new(client, "test-key", "test-model");
        using Bitmap image = new(2, 2);

        OutfitPreviewResult result = await service.GenerateAsync(image, new OutfitPreviewOptions());

        Assert.Equal(OutfitPreviewStatus.RateLimited, result.Status);
        Assert.Equal(429, result.HttpStatusCode);
        Assert.Equal("RequestBurstTooFast", result.ProviderCode);
        Assert.Equal("body-id", result.RequestId);
        Assert.Equal(7, result.RetryAfterSeconds);
        Assert.Equal("Slow down", result.SafeMessage);
        Assert.Null(result.Image);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "ContentPolicyViolation", OutfitPreviewStatus.SafetyRejected)]
    [InlineData(HttpStatusCode.BadRequest, "InvalidParameter", OutfitPreviewStatus.InvalidRequest)]
    [InlineData(HttpStatusCode.PaymentRequired, "QuotaExceeded", OutfitPreviewStatus.QuotaExceeded)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "ServerOverloaded", OutfitPreviewStatus.ServerError)]
    public async Task ProviderErrorCodeAndHttpStatusAreClassified(HttpStatusCode httpStatus, string providerCode, OutfitPreviewStatus expectedStatus)
    {
        using HttpClient client = new(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(httpStatus)
        {
            Content = new StringContent($"{{\"error\":{{\"code\":\"{providerCode}\",\"message\":\"safe diagnostic\"}}}}")
        })));
        AiOutfitPreviewService service = new(client, "test-key", "test-model");
        using Bitmap image = new(2, 2);

        OutfitPreviewResult result = await service.GenerateAsync(image, new OutfitPreviewOptions());

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal((int)httpStatus, result.HttpStatusCode);
        Assert.Equal(providerCode, result.ProviderCode);
        Assert.Equal("safe diagnostic", result.SafeMessage);
    }

    [Fact]
    public async Task NetworkErrorsHaveDistinctStatus()
    {
        using HttpClient client = new(new StubHandler((_, _) => throw new HttpRequestException()));
        AiOutfitPreviewService service = new(client, "test-key", "test-model");
        using Bitmap image = new(2, 2);

        OutfitPreviewResult result = await service.GenerateAsync(image, new OutfitPreviewOptions());

        Assert.Equal(OutfitPreviewStatus.NetworkError, result.Status);
    }

    [Fact]
    public async Task TimeoutsHaveDistinctStatus()
    {
        using HttpClient client = new(new StubHandler((_, _) => throw new TaskCanceledException()));
        AiOutfitPreviewService service = new(client, "test-key", "test-model");
        using Bitmap image = new(2, 2);

        OutfitPreviewResult result = await service.GenerateAsync(image, new OutfitPreviewOptions());

        Assert.Equal(OutfitPreviewStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task CallerCancellationPropagatesToThePendingRequest()
    {
        using HttpClient client = new(new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        AiOutfitPreviewService service = new(client, "test-key", "test-model");
        using Bitmap image = new(2, 2);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GenerateAsync(image, new OutfitPreviewOptions(), cancellation.Token));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }
}
