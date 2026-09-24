using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace WechatStyleScreenshot.Services;

public enum OutfitPreviewStatus { Success, ApiKeyMissing, NoUsableImage, Failed }

public sealed record OutfitPreviewResult(OutfitPreviewStatus Status, Bitmap? Image = null);

public sealed class AiOutfitPreviewService : IDisposable
{
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
            using MemoryStream imageStream = new();
            source.Save(imageStream, ImageFormat.Png);
            if (imageStream.Length > 25 * 1024 * 1024) return new(OutfitPreviewStatus.Failed);

            using HttpRequestMessage request = new(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(new
            {
                model = _model,
                prompt = OutfitPromptBuilder.Build(options),
                image = $"data:image/png;base64,{Convert.ToBase64String(imageStream.ToArray())}",
                size = "1K",
                output_format = "png",
                response_format = "b64_json",
                watermark = true
            });

            using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new(OutfitPreviewStatus.Failed);

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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or ArgumentException or InvalidOperationException or OutOfMemoryException or ExternalException)
        {
            return new(OutfitPreviewStatus.Failed);
        }
    }

    public void Dispose()
    {
        if (_ownsClient) _httpClient.Dispose();
    }
}
