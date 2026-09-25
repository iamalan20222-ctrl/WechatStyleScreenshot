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
    Unauthorized,
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
    string? SafeMessage = null,
    int RetryCount = 0,
    IReadOnlyList<OutfitPreviewAttempt>? Attempts = null);

public sealed record OutfitPreviewAttempt(
    int? HttpStatusCode,
    string? ProviderCode,
    string? RequestId,
    int? RetryAfterSeconds,
    string? SafeMessage);

public sealed class AiOutfitPreviewService : IImageEditProvider, IOutfitGenerationService
{
    public const string DefaultModel = "doubao-seedream-5-0-flash-260915";
    private const int MaxErrorBodyBytes = 8 * 1024;
    private const string Endpoint = "https://ark.cn-beijing.volces.com/api/v3/images/generations";
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public AiOutfitPreviewService(HttpClient? httpClient = null, string? apiKey = null, string? model = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _ownsClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ARK_API_KEY");
        _model = model ?? Environment.GetEnvironmentVariable("ARK_MODEL") ?? DefaultModel;
        _delayAsync = delayAsync ?? ((delay, token) => Task.Delay(delay, token));
    }

    public Task<OutfitPreviewResult> GenerateAsync(Bitmap source, OutfitPreviewOptions options,
        CancellationToken cancellationToken = default, Action<TimeSpan, OutfitPreviewStatus>? retryScheduled = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        return EditAsync(source, OutfitPromptBuilder.Build(options), cancellationToken, retryScheduled);
    }

    public async Task<OutfitPreviewResult> EditAsync(Bitmap source, string prompt,
        CancellationToken cancellationToken = default, Action<TimeSpan, OutfitPreviewStatus>? retryScheduled = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(prompt);
        if (string.IsNullOrWhiteSpace(_apiKey)) return new(OutfitPreviewStatus.ApiKeyMissing);

        List<OutfitPreviewAttempt> attempts = [];
        int attemptNumber = 0;
        try
        {
            string? imageDataUri = await Task.Run(() => CreateImageDataUri(source), cancellationToken).ConfigureAwait(false);
            if (imageDataUri is null) return new(OutfitPreviewStatus.Failed);

            for (attemptNumber = 1; attemptNumber <= 3; attemptNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using HttpRequestMessage request = CreateRequest(imageDataUri, prompt, source.Width, source.Height);
                using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    OutfitPreviewResult failure = await ReadProviderErrorAsync(response, cancellationToken).ConfigureAwait(false);
                    attempts.Add(ToAttempt(failure));
                    Debug.WriteLine($"Outfit API failure: status={failure.HttpStatusCode}; code={failure.ProviderCode}; requestId={failure.RequestId}; retryAfterSeconds={failure.RetryAfterSeconds}");

                    if (attemptNumber < 3 && IsRetryable(failure.HttpStatusCode))
                    {
                        TimeSpan delay = failure.RetryAfterSeconds is int retryAfter
                            ? TimeSpan.FromSeconds(retryAfter)
                            : TimeSpan.FromSeconds(2 << (attemptNumber - 1));
                        retryScheduled?.Invoke(delay, failure.Status);
                        await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return failure with { RetryCount = attemptNumber - 1, Attempts = attempts.ToArray() };
                }

                string? requestId = GetResponseRequestId(response);
                await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.GetArrayLength() == 0 ||
                    !data[0].TryGetProperty("b64_json", out JsonElement imageData))
                {
                    attempts.Add(new((int)response.StatusCode, null, requestId, null, null));
                    OutfitPreviewResult empty = new(OutfitPreviewStatus.NoUsableImage, HttpStatusCode: (int)response.StatusCode,
                        RequestId: requestId, RetryCount: attemptNumber - 1, Attempts: attempts.ToArray());
                    return empty;
                }

                byte[] bytes;
                try { bytes = Convert.FromBase64String(imageData.GetString() ?? string.Empty); }
                catch (FormatException)
                {
                    attempts.Add(new((int)response.StatusCode, null, requestId, null, null));
                    return new OutfitPreviewResult(OutfitPreviewStatus.NoUsableImage, HttpStatusCode: (int)response.StatusCode,
                        RequestId: requestId, RetryCount: attemptNumber - 1, Attempts: attempts.ToArray());
                }

                using MemoryStream resultStream = new(bytes, writable: false);
                using Image decoded = Image.FromStream(resultStream);
                attempts.Add(new((int)response.StatusCode, null, requestId, null, null));
                return new OutfitPreviewResult(OutfitPreviewStatus.Success, new Bitmap(decoded),
                    HttpStatusCode: (int)response.StatusCode, RequestId: requestId,
                    RetryCount: attemptNumber - 1, Attempts: attempts.ToArray());
            }
            return new(OutfitPreviewStatus.Failed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            attempts.Add(new(null, null, null, null, "Request timed out"));
            return new OutfitPreviewResult(OutfitPreviewStatus.TimedOut, RetryCount: Math.Max(0, attemptNumber - 1), Attempts: attempts.ToArray());
        }
        catch (HttpRequestException)
        {
            attempts.Add(new(null, null, null, null, "Network error"));
            return new OutfitPreviewResult(OutfitPreviewStatus.NetworkError, RetryCount: Math.Max(0, attemptNumber - 1), Attempts: attempts.ToArray());
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or OutOfMemoryException or ExternalException)
        {
            attempts.Add(new(null, null, null, null, "Invalid or unusable provider response"));
            return new OutfitPreviewResult(OutfitPreviewStatus.Failed, RetryCount: Math.Max(0, attemptNumber - 1), Attempts: attempts.ToArray());
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

    private HttpRequestMessage CreateRequest(string imageDataUri, string prompt, int sourceWidth, int sourceHeight)
    {
        HttpRequestMessage request = new(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = JsonContent.Create(new
        {
            model = _model,
            prompt,
            image = imageDataUri,
            size = GetOutputSize(sourceWidth, sourceHeight),
            output_format = "png",
            response_format = "b64_json",
            watermark = true
        });
        return request;
    }

    private static string GetOutputSize(int sourceWidth, int sourceHeight)
    {
        const double minimumPixels = 921_600;
        const double maximumPixels = 4_624_220;
        double sourcePixels = (double)sourceWidth * sourceHeight;
        if (sourcePixels is >= minimumPixels and <= maximumPixels)
            return $"{sourceWidth}x{sourceHeight}";

        double targetPixels = sourcePixels < minimumPixels ? 1_000_000 : 4_500_000;
        double scale = Math.Sqrt(targetPixels / sourcePixels);
        int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return $"{width}x{height}";
    }

    private static bool IsRetryable(int? statusCode) => statusCode == 429 || statusCode is 500 or 502 or 503 or 504;

    private static OutfitPreviewAttempt ToAttempt(OutfitPreviewResult result) =>
        new(result.HttpStatusCode, result.ProviderCode, result.RequestId, result.RetryAfterSeconds, result.SafeMessage);

    private static string? GetResponseRequestId(HttpResponseMessage response) => SanitizeToken(
        ReadHeader(response, "x-request-id") ?? ReadHeader(response, "x-tt-logid") ?? ReadHeader(response, "request-id"));

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
        if (statusCode is 401 or 403) return OutfitPreviewStatus.Unauthorized;
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
