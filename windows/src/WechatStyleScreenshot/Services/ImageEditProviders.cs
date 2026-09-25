using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace WechatStyleScreenshot.Services;

public sealed class VolcanoArkImageEditProvider : IImageEditProvider
{
    private readonly AiOutfitPreviewService _service;
    public VolcanoArkImageEditProvider(HttpClient client, string? key, string model) =>
        _service = new AiOutfitPreviewService(client, key ?? "", model);

    public Task<OutfitPreviewResult> EditAsync(Bitmap image, string prompt, CancellationToken cancellationToken,
        Action<TimeSpan, OutfitPreviewStatus>? retryScheduled = null) =>
        _service.EditAsync(image, prompt, cancellationToken, retryScheduled);
}

public abstract class JsonImageEditProvider(HttpClient client, string? key, string model) : IImageEditProvider
{
    protected readonly HttpClient Client = client;
    protected readonly string? Key = key;
    protected readonly string Model = model;

    protected abstract HttpRequestMessage BuildRequest(string imageDataUri, string prompt);
    protected abstract Task<Bitmap?> DecodeImageAsync(JsonElement root, CancellationToken cancellationToken);

    public async Task<OutfitPreviewResult> EditAsync(Bitmap image, string prompt, CancellationToken cancellationToken,
        Action<TimeSpan, OutfitPreviewStatus>? retryScheduled = null)
    {
        if (string.IsNullOrWhiteSpace(Key)) return new(OutfitPreviewStatus.ApiKeyMissing);
        using MemoryStream source = new();
        image.Save(source, ImageFormat.Png);
        if (source.Length > 10 * 1024 * 1024) return new(OutfitPreviewStatus.InvalidRequest, SafeMessage: "输入图片超过 10 MB");
        string dataUri = "data:image/png;base64," + Convert.ToBase64String(source.ToArray());
        List<OutfitPreviewAttempt> attempts = [];
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using HttpRequestMessage request = BuildRequest(dataUri, prompt);
                using HttpResponseMessage response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    OutfitPreviewResult error = await ProviderHttpResult.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                    attempts.Add(new(error.HttpStatusCode, error.ProviderCode, error.RequestId, error.RetryAfterSeconds, error.SafeMessage));
                    if (attempt < 3 && ProviderHttpResult.IsRetryable(error.HttpStatusCode))
                    {
                        TimeSpan delay = TimeSpan.FromSeconds(error.RetryAfterSeconds ?? (2 << (attempt - 1)));
                        retryScheduled?.Invoke(delay, error.Status);
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    return error with { RetryCount = attempt - 1, Attempts = attempts };
                }

                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                Bitmap? result = await DecodeImageAsync(document.RootElement, cancellationToken).ConfigureAwait(false);
                string? requestId = response.Headers.TryGetValues("x-request-id", out IEnumerable<string>? ids) ? ids.FirstOrDefault() : null;
                attempts.Add(new((int)response.StatusCode, null, requestId, null, null));
                return result is null
                    ? new(OutfitPreviewStatus.NoUsableImage, HttpStatusCode: (int)response.StatusCode, RequestId: requestId, Attempts: attempts)
                    : new(OutfitPreviewStatus.Success, result, (int)response.StatusCode, RequestId: requestId,
                        RetryCount: attempt - 1, Attempts: attempts);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (TaskCanceledException) { return new(OutfitPreviewStatus.TimedOut, Attempts: attempts); }
            catch (HttpRequestException) { return new(OutfitPreviewStatus.NetworkError, Attempts: attempts); }
            catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or OutOfMemoryException)
            {
                return new(OutfitPreviewStatus.NoUsableImage, Attempts: attempts);
            }
        }
        return new(OutfitPreviewStatus.Failed, Attempts: attempts);
    }

    protected static Bitmap? DecodeBase64(JsonElement item)
    {
        if (!item.TryGetProperty("b64_json", out JsonElement encoded) || encoded.ValueKind != JsonValueKind.String) return null;
        byte[] bytes = Convert.FromBase64String(encoded.GetString() ?? "");
        using MemoryStream stream = new(bytes, false);
        using Image decoded = Image.FromStream(stream);
        return new Bitmap(decoded);
    }

    protected static JsonElement? FirstImage(JsonElement root) => root.TryGetProperty("data", out JsonElement data) &&
        data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0 ? data[0] : null;
}

public sealed class QwenImageEditProvider(HttpClient client, string? key, string model, string endpoint)
    : JsonImageEditProvider(client, key, model)
{
    public const string DefaultModel = "qwen-image-3.0-pro";

    protected override HttpRequestMessage BuildRequest(string imageDataUri, string prompt)
    {
        HttpRequestMessage request = new(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        request.Content = JsonContent.Create(new { model = Model, prompt, image = imageDataUri, n = 1, size = "auto", prompt_extend = false });
        return request;
    }

    protected override async Task<Bitmap?> DecodeImageAsync(JsonElement root, CancellationToken cancellationToken)
    {
        JsonElement? item = FirstImage(root);
        if (item is null) return null;
        Bitmap? embedded = DecodeBase64(item.Value);
        if (embedded is not null) return embedded;
        if (!item.Value.TryGetProperty("url", out JsonElement urlValue) || urlValue.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(urlValue.GetString(), UriKind.Absolute, out Uri? url) || url.Scheme != Uri.UriSchemeHttps ||
            IPAddress.TryParse(url.Host, out _) ||
            !(url.Host.EndsWith(".aliyuncs.com", StringComparison.OrdinalIgnoreCase) ||
              url.Host.EndsWith(".aliyun.com", StringComparison.OrdinalIgnoreCase))) return null;
        using HttpResponseMessage response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length > 25 * 1024 * 1024) return null;
        using MemoryStream stream = new(bytes, false);
        using Image decoded = Image.FromStream(stream);
        return new Bitmap(decoded);
    }
}

public sealed class OpenAiImageEditProvider(HttpClient client, string? key, string model)
    : JsonImageEditProvider(client, key, model)
{
    public const string DefaultModel = "gpt-image-2.5-sunburst";
    private const string Endpoint = "https://api.openai.com/v1/images/edits";

    protected override HttpRequestMessage BuildRequest(string imageDataUri, string prompt)
    {
        HttpRequestMessage request = new(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        request.Content = JsonContent.Create(new
        {
            model = Model,
            prompt,
            images = new[] { new { image_url = imageDataUri } },
            input_fidelity = "high",
            output_format = "png",
            size = "auto",
            n = 1
        });
        return request;
    }

    protected override Task<Bitmap?> DecodeImageAsync(JsonElement root, CancellationToken cancellationToken) =>
        Task.FromResult(FirstImage(root) is JsonElement item ? DecodeBase64(item) : null);
}

public static class QwenEndpointBuilder
{
    public static string Build(OutfitAppSettings settings)
    {
        string baseUrl;
        if (settings.QwenRegion == "Custom")
        {
            baseUrl = settings.QwenCustomBaseUrl.TrimEnd('/');
        }
        else
        {
            string region = settings.QwenRegion switch
            {
                "Beijing" => "cn-beijing",
                "Singapore" => "ap-southeast-1",
                "Virginia" => "us-east-1",
                _ => throw new ArgumentException("Unsupported Qwen region")
            };
            if (string.IsNullOrWhiteSpace(settings.QwenWorkspaceId) ||
                !settings.QwenWorkspaceId.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
                throw new ArgumentException("Qwen Workspace ID is required");
            baseUrl = $"https://{settings.QwenWorkspaceId}.{region}.maas.aliyuncs.com/compatible-mode/v1";
        }
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            IPAddress.TryParse(uri.Host, out _)) throw new ArgumentException("Qwen Base URL must be HTTPS");
        return baseUrl.EndsWith("/images/generations", StringComparison.OrdinalIgnoreCase)
            ? baseUrl : baseUrl + "/images/generations";
    }
}

internal static class ProviderHttpResult
{
    public static bool IsRetryable(int? status) => status == 429 || status is 500 or 502 or 503 or 504;

    public static async Task<OutfitPreviewResult> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? code = null;
        string? message = null;
        string? requestId = response.Headers.TryGetValues("x-request-id", out IEnumerable<string>? ids) ? ids.FirstOrDefault() : null;
        try
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            byte[] buffer = new byte[8192];
            int length = 0;
            while (length < buffer.Length)
            {
                int count = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
                if (count == 0) break;
                length += count;
            }
            using JsonDocument document = JsonDocument.Parse(buffer.AsMemory(0, length));
            JsonElement error = document.RootElement.TryGetProperty("error", out JsonElement nested) ? nested : document.RootElement;
            code = error.TryGetProperty("code", out JsonElement c) ? c.ToString() : null;
            message = error.TryGetProperty("message", out JsonElement m) ? m.ToString() : null;
            if (document.RootElement.TryGetProperty("request_id", out JsonElement id)) requestId ??= id.ToString();
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException) { }

        int status = (int)response.StatusCode;
        string details = (code + " " + message).ToLowerInvariant();
        OutfitPreviewStatus result = status switch
        {
            401 => OutfitPreviewStatus.Unauthorized,
            403 when details.Contains("safety") || details.Contains("policy") => OutfitPreviewStatus.SafetyRejected,
            403 => OutfitPreviewStatus.Unauthorized,
            429 => OutfitPreviewStatus.RateLimited,
            402 => OutfitPreviewStatus.QuotaExceeded,
            408 or 504 => OutfitPreviewStatus.TimedOut,
            >= 500 => OutfitPreviewStatus.ServerError,
            400 when details.Contains("safety") || details.Contains("policy") => OutfitPreviewStatus.SafetyRejected,
            400 => OutfitPreviewStatus.InvalidRequest,
            _ => OutfitPreviewStatus.Failed
        };
        int? retryAfter = response.Headers.RetryAfter?.Delta is TimeSpan delay ? (int)Math.Ceiling(delay.TotalSeconds) : null;
        return new(result, HttpStatusCode: status, ProviderCode: Clip(code, 80), RequestId: Clip(requestId, 120),
            RetryAfterSeconds: retryAfter);
    }

    private static string? Clip(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("base64", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("data:image", StringComparison.OrdinalIgnoreCase)) return null;
        string cleaned = string.Concat(value.Where(c => !char.IsControl(c)));
        return cleaned.Length > length ? cleaned[..length] : cleaned;
    }
}
