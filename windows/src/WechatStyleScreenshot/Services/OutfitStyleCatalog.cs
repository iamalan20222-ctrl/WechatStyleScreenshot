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
            ["minimal luxury", "lace-detail", "cut-out design", "sporty", "thin-strap", "vacation resort", "runway-inspired", "retro", "futuristic", "soft lightweight fully covered"]),
        new(OutfitStylePresetType.JK, "C JK穿搭", "adult JK-inspired outfit",
            ["classic academy", "fresh Japanese soft", "dark-tone uniform", "sweet-cool", "retro campus", "urban fashion"])
    ];

    public static OutfitStylePreset Get(OutfitStylePresetType type) =>
        All.First(preset => preset.Type == type);
}
