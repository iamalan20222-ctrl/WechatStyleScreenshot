namespace WechatStyleScreenshot.Services;

public enum OutfitStylePresetType { Sport, Bikini, JK }

public sealed record OutfitStylePreset(OutfitStylePresetType Type, string Label, string Garment, string[] Directions);

public static class OutfitStyleCatalog
{
    public static readonly IReadOnlyList<OutfitStylePreset> All =
    [
        new(OutfitStylePresetType.Sport, "A 运动风", "sports bra and functional innerwear",
            ["sporty minimal", "technical athleisure", "seamless performance", "fashion athletic"]),
        new(OutfitStylePresetType.Bikini, "B 比基尼", "bikini / swimwear",
            ["secure gathered bandeau with center ruching", "muted textured triangle swimwear with proper coverage", "solid-color halter with modest neckline", "opaque asymmetric swimwear with restrained ring detail"]),
        new(OutfitStylePresetType.JK, "C JK穿搭", "adult JK-inspired outfit",
            ["taupe collared top with contrast piping and coordinated plaid pleats", "navy sailor collar and bow with pleated skirt", "dark tailored jacket with muted tartan pleats", "navy cardigan and blue-gray plaid with ribbon tie"])
    ];

    public static OutfitStylePreset Get(OutfitStylePresetType type) =>
        All.First(preset => preset.Type == type);
}
