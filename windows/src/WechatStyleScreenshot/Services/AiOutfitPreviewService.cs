using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;

namespace WechatStyleScreenshot.Services;

public enum OutfitPreviewStatus
{
    Success,
    ApiKeyMissing,
    RateLimited,
    TimedOut,
    NetworkError,
    SafetyRejected,
    QuotaExceeded,
    InvalidRequest,
    ServerError,
    NoUsableImage,
    Failed
}

public sealed record OutfitPreviewResult(
    OutfitPreviewStatus Status,
    Bitmap? Image = null,
    int? HttpStatusCode = null,
    string? ProviderCode = null,
    string? RequestId = null,
    int? RetryAfterSeconds = null,
    string? SafeMessage = null);

public sealed class AiOutfitPreviewService : IDisposable
{
    private const int MaxErrorBodyBytes = 8 * 1024;
    private const string Endpoint = "https://ark.cn-beijing.volces.com/api/v3/images/generations";
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string? _apiKey;
    private readonly string _model;

    public AiOutfitPreviewService(HttpClient? httpClient = null, string? apiKey = null, string? model = null)
    {
        _ownsClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ARK_API_KEY");
        _model = model ?? Environment.GetEnvironmentVariable("ARK_MODEL") ?? "doubao-seedream-5-0-pro-260628";
    }

    public async Task<OutfitPreviewResult> GenerateAsync(Bitmap source, OutfitPreviewOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(_apiKey)) return new(OutfitPreviewStatus.ApiKeyMissing);

        try
        {
            string? imageDataUri = await Task.Run(() => CreateImageDataUri(source), cancellationToken).ConfigureAwait(false);
            if (imageDataUri is null) return new(OutfitPreviewStatus.Failed);

            using HttpRequestMessage request = new(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(new
            {
                model = _model,
                prompt = OutfitPromptBuilder.Build(options),
                image = imageDataUri,
                size = "1K",
                output_format = "png",
                response_format = "b64_json",
                watermark = true
            });

            using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                OutfitPreviewResult failure = await ReadProviderErrorAsync(response, cancellationToken).ConfigureAwait(false);
                Debug.WriteLine($"Outfit API failure: status={failure.HttpStatusCode}; code={failure.ProviderCode}; requestId={failure.RequestId}; retryAfterSeconds={failure.RetryAfterSeconds}");
                return failure;
            }

            await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.GetArrayLength() == 0 ||
                !data[0].TryGetProperty("b64_json", out JsonElement imageData))
            {
                return new(OutfitPreviewStatus.NoUsableImage);
            }

            byte[] bytes;
            try { bytes = Convert.FromBase64String(imageData.GetString() ?? string.Empty); }
            catch (FormatException) { return new(OutfitPreviewStatus.NoUsableImage); }

            using MemoryStream resultStream = new(bytes, writable: false);
            using Image decoded = Image.FromStream(resultStream);
            return new(OutfitPreviewStatus.Success, new Bitmap(decoded));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return new(OutfitPreviewStatus.TimedOut);
        }
        catch (HttpRequestException)
        {
            return new(OutfitPreviewStatus.NetworkError);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or OutOfMemoryException or ExternalException)
        {
            return new(OutfitPreviewStatus.Failed);
        }
    }

    public void Dispose()
    {
        if (_ownsClient) _httpClient.Dispose();
    }

    private static string? CreateImageDataUri(Bitmap source)
    {
        using MemoryStream imageStream = new();
        source.Save(imageStream, ImageFormat.Png);
        return imageStream.Length > 25 * 1024 * 1024
            ? null
            : $"data:image/png;base64,{Convert.ToBase64String(imageStream.ToArray())}";
    }

    private static async Task<OutfitPreviewResult> ReadProviderErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? providerCode = null;
        string? safeMessage = null;
        string? bodyRequestId = null;

        try
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            byte[] buffer = new byte[MaxErrorBodyBytes];
            int length = 0;
            while (length < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(length, buffer.Length - length), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                length += read;
            }

            using JsonDocument document = JsonDocument.Parse(buffer.AsMemory(0, length));
            JsonElement root = document.RootElement;
            JsonElement error = root.TryGetProperty("error", out JsonElement nestedError) ? nestedError : root;
            providerCode = ReadString(error, "code");
            safeMessage = SanitizeMessage(ReadString(error, "message"));
            bodyRequestId = ReadString(root, "request_id") ?? ReadString(error, "request_id");
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            // Provider error bodies are optional and untrusted; status and headers remain useful.
        }

        int statusCode = (int)response.StatusCode;
        string? requestId = SanitizeToken(bodyRequestId
            ?? ReadHeader(response, "x-request-id")
            ?? ReadHeader(response, "x-tt-logid")
            ?? ReadHeader(response, "request-id"));
        int? retryAfter = GetRetryAfterSeconds(response);
        OutfitPreviewStatus status = ClassifyError(statusCode, providerCode, safeMessage);

        return new OutfitPreviewResult(status, HttpStatusCode: statusCode, ProviderCode: SanitizeToken(providerCode),
            RequestId: requestId, RetryAfterSeconds: retryAfter, SafeMessage: safeMessage);
    }

    private static OutfitPreviewStatus ClassifyError(int statusCode, string? code, string? message)
    {
        string details = $"{code} {message}".ToLowerInvariant();
        if (statusCode is 408 or 504) return OutfitPreviewStatus.TimedOut;
        if (ContainsAny(details, "serveroverloaded", "server_overloaded")) return OutfitPreviewStatus.ServerError;
        if (statusCode == 429 || ContainsAny(details, "ratelimit", "rate_limit", "requestbursttoofast", "rpm", "ipm", "tpm"))
            return OutfitPreviewStatus.RateLimited;
        if (ContainsAny(details, "quota", "insufficientbalance", "insufficient_balance", "accountbalanceinsufficient", "billing"))
            return OutfitPreviewStatus.QuotaExceeded;
        if (ContainsAny(details, "safety", "moderation", "contentpolicy", "content_policy", "sensitivecontent", "sensitive_content"))
            return OutfitPreviewStatus.SafetyRejected;
        if (ContainsAny(details, "invalidparameter", "invalid_parameter", "invalidrequest", "invalid_request", "badrequest", "bad_request"))
            return OutfitPreviewStatus.InvalidRequest;
        if (statusCode >= 500 || ContainsAny(details, "serveroverloaded", "server_overloaded"))
            return OutfitPreviewStatus.ServerError;
        return OutfitPreviewStatus.Failed;
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.Ordinal));

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string? ReadHeader(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
    }

    private static int? GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta) return Math.Max(0, (int)Math.Ceiling(delta.TotalSeconds));
        if (response.Headers.RetryAfter?.Date is DateTimeOffset date) return Math.Max(0, (int)Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds));
        string? raw = ReadHeader(response, "Retry-After");
        return int.TryParse(raw, out int seconds) ? Math.Max(0, seconds) : null;
    }

    private static string? SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        string sanitized = string.Concat(message.Where(c => !char.IsControl(c))).Trim();
        if (sanitized.Contains("data:image", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("base64", StringComparison.OrdinalIgnoreCase)) return null;
        return sanitized.Length <= 240 ? sanitized : sanitized[..240];
    }

    private static string? SanitizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string sanitized = string.Concat(value.Where(c => !char.IsControl(c))).Trim();
        return sanitized.Length <= 120 ? sanitized : sanitized[..120];
    }
}
