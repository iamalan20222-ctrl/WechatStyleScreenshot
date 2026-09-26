using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WechatStyleScreenshot.Core;

namespace WechatStyleScreenshot.Services;

public enum TranslationStatus { Success, ApiKeyMissing, Unauthorized, RateLimited, QuotaExceeded, TimedOut, NetworkError, ServerError, InvalidResponse, Failed }
public sealed record TranslationResult(TranslationStatus Status, IReadOnlyDictionary<int, string>? Translations = null);

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(IReadOnlyList<OcrTextRegion> regions, string targetLanguage, CancellationToken cancellationToken);
}

public sealed class DeepSeekTranslationService : ITranslationService
{
    private const string SystemPrompt = "你是一名专业界面与截图翻译助手。准确翻译短文本；不解释、不补充、不删除有效信息。UI按钮使用简洁自然译法。专有名词合理保留，URL、代码、数字、文件路径保持原样。只返回含 translations 数组的 JSON，每项保留 id 和 text。";
    private static readonly Uri Endpoint = new("https://api.deepseek.com/chat/completions");
    private readonly HttpClient _client;
    private readonly CredentialStore _credentials;
    private readonly OutfitSettingsStore _settings;

    public DeepSeekTranslationService(CredentialStore credentials, OutfitSettingsStore settings, HttpClient? client = null)
    {
        _credentials = credentials;
        _settings = settings;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    }

    public async Task<TranslationResult> TranslateAsync(IReadOnlyList<OcrTextRegion> regions, string targetLanguage, CancellationToken cancellationToken)
    {
        string? key = _credentials.GetTranslationCredential();
        if (string.IsNullOrWhiteSpace(key)) return new(TranslationStatus.ApiKeyMissing);
        var eligible = regions.Where(r => ShouldTranslate(r.Text)).ToArray();
        var output = regions.Where(r => !ShouldTranslate(r.Text)).ToDictionary(r => r.Id, r => r.Text);
        if (eligible.Length == 0) return new(TranslationStatus.Success, output);
        string input = JsonSerializer.Serialize(eligible.Select(r => new { id = r.Id, text = r.Text }));
        string model = _settings.Load().DeepSeekModel;
        string payload = JsonSerializer.Serialize(new
        {
            model,
            messages = new object[] { new { role = "system", content = SystemPrompt + " 目标语言：" + targetLanguage }, new { role = "user", content = input } },
            thinking = new { type = "disabled" },
            response_format = new { type = "json_object" },
            temperature = 0.1,
            stream = false
        });
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Post, Endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
                {
                    TimeSpan delay = response.Headers.RetryAfter?.Delta ??
                        (response.Headers.RetryAfter?.Date is DateTimeOffset date
                            ? date - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(attempt + 1));
                    await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, cancellationToken);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    return new(response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TranslationStatus.Unauthorized,
                        HttpStatusCode.TooManyRequests => TranslationStatus.RateLimited,
                        HttpStatusCode.PaymentRequired => TranslationStatus.QuotaExceeded,
                        HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout => TranslationStatus.ServerError,
                        _ => TranslationStatus.Failed
                    });
                using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
                string? content = body.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                if (string.IsNullOrWhiteSpace(content)) return new(TranslationStatus.InvalidResponse);
                using JsonDocument translated = JsonDocument.Parse(content);
                foreach (JsonElement item in translated.RootElement.GetProperty("translations").EnumerateArray())
                {
                    int id = item.GetProperty("id").GetInt32();
                    if (!eligible.Any(region => region.Id == id) || output.ContainsKey(id)) return new(TranslationStatus.InvalidResponse);
                    string? value = item.GetProperty("text").GetString();
                    if (string.IsNullOrWhiteSpace(value)) return new(TranslationStatus.InvalidResponse);
                    output[id] = value;
                }
                return output.Count == regions.Count ? new(TranslationStatus.Success, output) : new(TranslationStatus.InvalidResponse);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(TranslationStatus.TimedOut); }
            catch (HttpRequestException) { return new(TranslationStatus.NetworkError); }
            catch (JsonException) { return new(TranslationStatus.InvalidResponse); }
            catch (KeyNotFoundException) { return new(TranslationStatus.InvalidResponse); }
            catch (InvalidOperationException) { return new(TranslationStatus.InvalidResponse); }
        }
        return new(TranslationStatus.Failed);
    }

    public static bool ShouldTranslate(string text)
    {
        string value = text.Trim();
        if (value.Length < 2 || value.Length > 1000 || !value.Any(char.IsLetter)) return false;
        if (Regex.IsMatch(value, @"(?i)(https?://|www\.|\S+@\S+\.\S+|[a-z]:\\|[/\\][\w.-]+[/\\]|\b(?:ark|sk)-[a-z0-9-]{12,}\b)")) return false;
        if (Regex.IsMatch(value, @"^[\d\W_]+$|[{};=<>]{2,}")) return false;
        return true;
    }
}
