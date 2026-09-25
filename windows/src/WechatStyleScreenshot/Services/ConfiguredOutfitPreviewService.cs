using System.Drawing;

namespace WechatStyleScreenshot.Services;

public static class ImageEditProviderFactory
{
    public static IImageEditProvider Create(ImageEditProviderKind kind, OutfitAppSettings settings,
        string key, HttpClient client) => kind switch
    {
        ImageEditProviderKind.Volcano => new VolcanoArkImageEditProvider(client, key, settings.VolcanoModel),
        ImageEditProviderKind.Qwen => new QwenImageEditProvider(client, key, settings.QwenModel, QwenEndpointBuilder.Build(settings)),
        ImageEditProviderKind.OpenAI => new OpenAiImageEditProvider(client, key, settings.OpenAiModel),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

public sealed class ConfiguredOutfitPreviewService : IOutfitGenerationService
{
    private readonly OutfitSettingsStore _settings;
    private readonly CredentialStore _credentials;
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public ConfiguredOutfitPreviewService(OutfitSettingsStore settings, CredentialStore credentials, HttpClient? client = null)
    {
        _settings = settings;
        _credentials = credentials;
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    }

    public async Task<OutfitPreviewResult> GenerateAsync(Bitmap source, OutfitPreviewOptions options,
        CancellationToken cancellationToken = default,
        Action<TimeSpan, OutfitPreviewStatus>? retryScheduled = null)
    {
        OutfitAppSettings settings = _settings.Load();
        ImageEditProviderKind kind = settings.DefaultProvider;
        string? key;
        try { key = _credentials.GetCredential(kind); }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return new(OutfitPreviewStatus.ApiKeyMissing, ProviderCode: kind.ToString());
        }
        if (string.IsNullOrWhiteSpace(key)) return new(OutfitPreviewStatus.ApiKeyMissing, ProviderCode: kind.ToString());

        IImageEditProvider provider;
        try { provider = ImageEditProviderFactory.Create(kind, settings, key, _client); }
        catch (ArgumentException)
        {
            return new(OutfitPreviewStatus.InvalidRequest, ProviderCode: kind.ToString(), SafeMessage: "请检查服务商区域和地址设置");
        }
        string prompt = OutfitPromptBuilder.Build(options, settings.GetStyle(options.Preset));
        return await provider.EditAsync(source, prompt, cancellationToken, retryScheduled).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }
}
