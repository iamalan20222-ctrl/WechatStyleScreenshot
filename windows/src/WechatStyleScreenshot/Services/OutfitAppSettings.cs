using System.Text.Json;

namespace WechatStyleScreenshot.Services;

public enum ImageEditProviderKind { Volcano, Qwen, OpenAI }

public sealed class StyleSetting
{
    public string Title { get; set; } = "";
    public string Prompt { get; set; } = "";
}

public sealed class OutfitAppSettings
{
    public Dictionary<string, StyleSetting> Styles { get; set; } = new();
    public ImageEditProviderKind DefaultProvider { get; set; } = ImageEditProviderKind.Volcano;
    public bool LegacyVolcanoKeyMigrated { get; set; }
    public string VolcanoModel { get; set; } = AiOutfitPreviewService.DefaultModel;
    public string QwenModel { get; set; } = "qwen-image-3.0-pro";
    public string QwenRegion { get; set; } = "Beijing";
    public string QwenWorkspaceId { get; set; } = "";
    public string QwenCustomBaseUrl { get; set; } = "";
    public string OpenAiModel { get; set; } = "gpt-image-2.5-sunburst";

    public StyleSetting GetStyle(OutfitStylePresetType type)
    {
        Styles ??= new();
        string key = type switch
        {
            OutfitStylePresetType.Sport => "A",
            OutfitStylePresetType.Bikini => "B",
            OutfitStylePresetType.JK => "C",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        StyleSetting defaults = OutfitStyleCatalog.GetDefaultSetting(type);
        if (!Styles.TryGetValue(key, out StyleSetting? current) || current is null) return defaults;
        return new StyleSetting
        {
            Title = string.IsNullOrWhiteSpace(current.Title) ||
                (type == OutfitStylePresetType.Bikini && current.Title.Trim() == "比基尼")
                ? defaults.Title : current.Title.Trim(),
            Prompt = string.IsNullOrWhiteSpace(current.Prompt) ||
                (type == OutfitStylePresetType.Bikini &&
                 (current.Prompt.Contains("比基尼", StringComparison.OrdinalIgnoreCase) ||
                  current.Prompt.Contains("bikini", StringComparison.OrdinalIgnoreCase)))
                ? defaults.Prompt : current.Prompt.Trim()
        };
    }

    public void SetStyle(OutfitStylePresetType type, StyleSetting setting) =>
        (Styles ??= new())[type switch { OutfitStylePresetType.Sport => "A", OutfitStylePresetType.Bikini => "B", _ => "C" }] = setting;

    public void ResetStyle(OutfitStylePresetType type) => SetStyle(type, OutfitStyleCatalog.GetDefaultSetting(type));

    public void ResetAllStyles()
    {
        foreach (OutfitStylePreset preset in OutfitStyleCatalog.All) ResetStyle(preset.Type);
    }
}

public sealed class OutfitSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public string FilePath { get; }

    public OutfitSettingsStore(string? filePath = null) => FilePath = filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WechatStyleScreenshot", "settings.json");

    public OutfitAppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new OutfitAppSettings();
            OutfitAppSettings settings = JsonSerializer.Deserialize<OutfitAppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new OutfitAppSettings();
            if (string.IsNullOrWhiteSpace(settings.VolcanoModel) ||
                settings.VolcanoModel == "doubao-seedream-5-0-pro-260628")
                settings.VolcanoModel = AiOutfitPreviewService.DefaultModel;
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new OutfitAppSettings();
        }
    }

    public void Save(OutfitAppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, FilePath, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
