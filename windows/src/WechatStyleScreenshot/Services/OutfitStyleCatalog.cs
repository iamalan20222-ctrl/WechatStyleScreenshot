namespace WechatStyleScreenshot.Services;

public enum OutfitStylePresetType { Sport, Bikini, JK }

public sealed record OutfitStylePreset(OutfitStylePresetType Type, string Label, string Garment, string[] Directions);

public static class OutfitStyleCatalog
{
    public static StyleSetting GetDefaultSetting(OutfitStylePresetType type) => type switch
    {
        OutfitStylePresetType.Sport => new StyleSetting
        {
            Title = "运动风",
            Prompt = "Create a realistic, tasteful sports bra and functional innerwear outfit for adult fashion design. Combine technical fabric, comfortable support, neat seams, and a modern athletic silhouette."
        },
        OutfitStylePresetType.Bikini => new StyleSetting
        {
            Title = "比基尼",
            Prompt = "Create premium, production-feasible bikini / swimwear. Garment-only cues distilled from reference images 5-8 (the images are not uploaded): gathered bandeau with center ruching, muted textured triangle with matching bottoms, solid-color halter with a modest neckline, or opaque asymmetric top with restrained ring detail. Use reference photos only for swimwear cut, fabric, construction, and commercial photographic finish. Never copy the people, faces, bodies, poses, or scenery. Choose one coherent direction; maintain proper opaque coverage, secure straps, natural seams, fabric tension, occlusion, and source-consistent lighting; no thong or micro-bikini."
        },
        OutfitStylePresetType.JK => new StyleSetting
        {
            Title = "JK穿搭",
            Prompt = "Create a refined, realistic JK-inspired adult fashion outfit for an adult female model. Garment-only cues distilled from reference images 1-4 (the images are not uploaded): taupe collared top with contrast piping and coordinated plaid pleats; white blouse with navy sailor collar and bow; dark tailored jacket with muted tartan pleats; or navy cardigan and blue-gray plaid with ribbon tie. Use references only for garment style, coordination, color, and structure. Never copy the people, faces, bodies, poses, or scenery. Choose one coherent direction with believable collar, tie, pleats, seams, drape, and shadows. Japanese academy-inspired commercial styling, not a childlike school uniform or cosplay."
        },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

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
