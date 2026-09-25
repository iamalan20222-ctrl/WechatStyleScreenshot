namespace WechatStyleScreenshot.Services;

public static class OutfitPromptBuilder
{
    public static string Build(OutfitPreviewOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Preset != OutfitStylePresetType.Sport)
        {
            string template = options.Preset switch
            {
                OutfitStylePresetType.Bikini => BikiniTemplate,
                OutfitStylePresetType.JK => JkTemplate,
                _ => throw new ArgumentOutOfRangeException(nameof(options))
            };
            return $"{template}\n\nSelected design direction: {options.Style}. Suggested color: {options.Color}. Keep all coverage, adult-only, identity-lock and garment-construction rules above. Generate a fresh design, not a copy of any earlier result.";
        }

        return $"""
Edit the provided image as a clothing replacement preview for fashion design reference.

Keep the same model identity, same face and facial features, same hairstyle, same body proportions, same body shape, same skin tone, same pose, and same camera angle. Preserve the background as much as possible. ONLY replace clothing.
Create a tasteful {options.Style} {options.Color} {options.GarmentCategory} outfit. This is a professional, fashion-oriented fitting preview on an adult model only.

Requirements: fully covered in opaque, non-transparent garments; no nudity; no exposed nipples; no exposed genitals; non-pornographic; no sexualized pose or presentation; do not alter facial features, body proportions, camera angle, or background. Do not remove clothing except to replace it with the specified fully covering outfit. Realistic fashion editorial result.
""".Trim();
    }

    private const string BikiniTemplate = """
You are a professional swimwear / bikini fashion designer and commercial fashion retouching artist.
Use the uploaded image as the only character reference. The person in the image is an adult female model.

Task: Only replace the model's current clothing and redesign it into a professionally designed bikini / swimwear look suitable for fashion design reference. Keep all other core visual content as unchanged as possible.

Highest-priority identity lock: Strictly preserve the same person, including the same face, facial features, face shape, skin tone, hairstyle and hair color, age appearance, height impression, body proportions, limb length, shoulder width and waist-hip ratio, body orientation, head angle, facial expression, hand pose, leg pose, original standing / sitting posture / movement, camera angle, composition, framing, and perspective. Do not change identity or anatomy to fit the clothing. Do not redesign the pose.

Clothing replacement: Completely replace the existing outfit with a realistic, well-constructed, high-quality bikini / swimwear design. If no subtype is given, create a distinct design direction: minimal luxury, lace-detail, cut-out, sporty, thin-strap, vacation / resort, runway-inspired, retro, futuristic, or soft lightweight but fully covered swimwear. Randomization may vary top structure, straps, bottom cut, waist height, cut-out placement, lace, fabric, color, pattern, metal accessories, ties, asymmetry, and styling. The model herself must remain unchanged.

Realism and garment construction: The swimsuit must be physically wearable and production-feasible. Require correct strap connections, realistic fabric tension, natural seams and edges, natural fit and folds, correct occlusion, original-environment lighting and shadows, natural skin contact, no floating fabric, broken straps, mirrored structural errors, penetration, or impossible construction.

Coverage and safety: Adult model only. Tasteful, non-explicit fashion swimwear; no nudity; no exposed nipples; no exposed genitals; no pornographic style; no transparent exposure; maintain normal, proper bikini coverage.

Photography consistency: Preserve original lighting direction, shadows, color temperature, exposure, depth of field, photographic texture, background, environment, and focal-length feeling. The outfit should look worn during the original shoot.

Human quality control: Avoid body distortion, changed bust / waist / hip proportions, altered leg length, abnormal fingers, extra limbs, twisted edges, broken waist, face changes, plastic AI skin, and over-smoothed skin.

Final result: The same original person, pose, expression, composition, and photography style, with only clothing professionally replaced by a newly designed bikini. Realistic, natural, premium commercial swimwear photography; fashion-forward, production-feasible, sharp garment detail. If no additional requirement is given, make the bikini clearly different from the previous one.
""";

    private const string JkTemplate = """
You are a professional fashion designer and commercial portrait retouching artist.
Use the uploaded image as the only character reference. The person in the image is an adult female model.

Task: Without changing model identity, pose, composition, or photography style, only replace current clothing with a complete, realistic, refined JK-inspired outfit for fashion design and styling reference.

Highest-priority identity lock: Strictly preserve the same person: same face, facial features and shape, skin tone, hairstyle and hair color, age appearance, height impression, body proportions, limb length, shoulder width and waist-hip ratio, body orientation, head angle, facial expression, hand pose, leg pose, original standing / sitting posture / movement, camera angle, composition, framing, and perspective. Do not change identity, anatomy, proportions, or pose to fit clothing.

Clothing replacement: Completely replace the existing outfit with high-quality JK-inspired styling for an adult woman. It may combine a realistic blouse or white shirt, ribbon / bow / necktie, pleated skirt, blazer / cardigan / sweater vest, socks, loafers, and restrained campus-inspired accessories. Distinct directions include classic academy, fresh Japanese soft, dark-tone uniform, sweet-cool, retro campus, and urban fashion. Vary shirt and outerwear, skirt length and plaid, ribbon, sock length, shoe type, palette, fabric, and editorial styling, but never change the model herself.

Garment quality: Realistic, wearable, structurally correct shirt, bow / tie, pleats, socks, shoes, fabric drape, seams, edges, folds, occlusion, lighting, shadows, and skin contact. No floating or penetrating clothing, left-right confusion, or AI errors on buttons, collar, and hemline. This is an adult fashion look, not fantasy costume, stage costume, or childlike school uniform cosplay.

Safety and age framing: Adult female model only; tasteful, realistic, non-explicit, fashion-oriented; no nudity, no exposed nipples, no exposed genitals, no pornographic style, and no minor-coded appearance.

Photography consistency: Preserve original lighting direction, shadows, color temperature, exposure, depth of field, photographic texture, background, environment, and focal-length feeling. The outfit should look naturally worn during the original shoot.

Human quality control: Avoid body distortion, changed bust / waist / hip proportions, altered leg length, abnormal fingers, extra limbs, distorted body edges, broken waist, face changes, unnatural skin texture, over-retouching, and mannequin-like appearance.

Final result: Same original person, pose, expression, composition, and photography style, only clothing naturally replaced by refined realistic adult JK-inspired styling. Clean Japanese academy-inspired commercial portrait quality with clear garment details. If no additional requirement is given, make it clearly different from the previous one.
""";
}
