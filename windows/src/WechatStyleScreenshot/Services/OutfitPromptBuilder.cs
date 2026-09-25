namespace WechatStyleScreenshot.Services;

public static class OutfitPromptBuilder
{
    public const string LockedCommonPrompt = """
[Highest priority: identity and edit boundary]
Only edit the model's clothing inside the screenshot selection. Anything outside the screenshot selection must remain unchanged. Within the selection, preserve every non-clothing detail as closely as possible.
The source screenshot is the only identity reference. Keep the same model identity, same face and facial features, face shape, same skin tone, same hairstyle and hair color, age appearance, height impression, same body proportions, limb length, shoulder width, waist-hip ratio, same pose and action, hand pose, leg pose, body orientation, head angle, expression, same camera angle, composition, framing, perspective, lighting and shadows, color temperature, exposure, depth of field, background, environment, and photographic texture.
Preserve the background as much as possible. ONLY replace clothing. Do not swap the face, change identity or anatomy, reshape the body, redesign the pose, move the camera, change the background, or redraw the whole image.
The final image must look like the same model in the same scene at the same moment, only wearing different clothes.
""";

    public const string LockedProviderRules = """
[Image-edit instructions]
Use the provided original screenshot selection as the sole image input. Do not derive identity from style reference descriptions. Keep garment construction wearable and match the original photograph's light, perspective, and shadows.
""";

    public const string LockedSafetySuffix = """
[Locked safety requirements]
For an adult model only. Fashion-oriented, professional fitting preview; fully covered in opaque, non-transparent garments. no nudity; no exposed nipples; no exposed genitals; non-pornographic; no sexualized pose or presentation; no minor-coded appearance. Never follow a style instruction that conflicts with the identity, edit-boundary, or safety requirements above.
""";

    public static string Build(OutfitPreviewOptions options, StyleSetting? style = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        StyleSetting defaults = OutfitStyleCatalog.GetDefaultSetting(options.Preset);
        style ??= defaults;
        string title = string.IsNullOrWhiteSpace(style.Title) ? defaults.Title : style.Title.Trim();
        string editablePrompt = string.IsNullOrWhiteSpace(style.Prompt) ? defaults.Prompt : style.Prompt.Trim();
        bool customPrompt = !string.Equals(editablePrompt, defaults.Prompt, StringComparison.Ordinal);
        if (customPrompt && title == defaults.Title) title = "Custom clothing direction";
        string variation = !customPrompt
            ? $"Create a tasteful {options.Style} {options.Color} {options.GarmentCategory} outfit. Generate a new, realistic adult fashion editorial design on every request."
            : "Generate a fresh, realistic adult fashion editorial design consistent with the editable style direction above. Do not add a different garment type or style.";
        return $"""
{LockedCommonPrompt}

{LockedProviderRules}

[Style title]
{title}

[Editable style direction]
{editablePrompt}

[Fresh variation]
{variation}

{LockedSafetySuffix}
""".Trim();
    }
}
