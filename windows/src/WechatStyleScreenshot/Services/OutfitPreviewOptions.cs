namespace WechatStyleScreenshot.Services;

public sealed record OutfitPreviewOptions(string Style, string Color, string GarmentCategory, OutfitStylePresetType Preset = OutfitStylePresetType.Sport)
{
    private static readonly string[] Colors = ["black", "white", "nude", "pink", "burgundy", "blue-gray"];

    public OutfitPreviewOptions() : this(OutfitStylePresetType.Sport) { }

    public OutfitPreviewOptions(OutfitStylePresetType preset) : this(
        Pick(OutfitStyleCatalog.Get(preset).Directions), Pick(Colors), OutfitStyleCatalog.Get(preset).Garment, preset) { }

    private static string Pick(string[] options) => options[Random.Shared.Next(options.Length)];
}
