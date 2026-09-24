namespace WechatStyleScreenshot.Services;

public static class OutfitPromptBuilder
{
    public static string Build(OutfitPreviewOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return $"""
Edit the provided image as a clothing replacement preview for fashion design reference.

Keep the same model identity, same face and facial features, same hairstyle, same body proportions, same body shape, same skin tone, and same pose. Preserve the background as much as possible. Only replace clothing.
Create a tasteful {options.Style} {options.Color} {options.GarmentCategory} outfit. This is a professional, fashion-oriented fitting preview on an adult model only.

Requirements: fully covered in opaque, non-transparent garments; no nudity; no exposed nipples; no exposed genitals; non-pornographic; no sexualized pose or presentation; do not alter facial features, body proportions, camera angle, or background. Do not remove clothing except to replace it with the specified fully covering outfit. Realistic fashion editorial result.
""".Trim();
    }
}
