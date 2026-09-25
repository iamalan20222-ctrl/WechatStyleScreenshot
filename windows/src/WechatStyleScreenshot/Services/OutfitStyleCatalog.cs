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
            Prompt = """
请将服装替换为一套适合设计参考的成人商业泳装。

要求：
- 只替换衣物
- 保持人物身份、脸部、发型、肤色、身材比例、姿势、动作、背景、光线和构图不变
- 风格为真实、自然、专业的商业泳装摄影效果
- 服装必须真实可穿着，结构合理，面料自然，细节清晰
- 必须是正常完整覆盖的成人泳装
- 不透明
- 不露点
- 不暴露私密部位
- 非情色
- 非挑逗
- 适合作为泳装设计参考

可随机生成不同方向，例如：
- 极简高级泳装
- 运动感泳装
- 度假风泳装
- 复古泳装
- 时装感泳装

最终效果应为：
同一个成年女性模特，在同一场景和同一拍摄瞬间，仅将原有服装自然替换为一套高质量、真实、专业的成人商业泳装。
"""
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
