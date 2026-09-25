namespace WechatStyleScreenshot.Services;

public static class OutfitPromptBuilder
{
    public static string Build(OutfitPreviewOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string template = options.Preset switch
        {
            OutfitStylePresetType.Sport => BuildSport(options),
            OutfitStylePresetType.Bikini => BikiniTemplate,
            OutfitStylePresetType.JK => JkTemplate,
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };

        return $"""
{template}

[Non-negotiable edit boundary and continuity]
Only edit the model's clothing inside the screenshot selection. Anything outside the screenshot selection must remain unchanged. Within the selection, preserve all non-clothing pixels and visual content as closely as possible.
Preserve the same model identity, same face and facial features, same face shape, same skin tone, same hairstyle and hair color, same age appearance, same body proportions, same pose and action, same expression, same hands and limbs, same background, same lighting and shadows, same camera angle, same composition, framing, perspective, exposure, and photographic texture. Do not alter anatomy or scenery to accommodate a garment.
The final image must look like the same model in the same scene at the same moment, wearing only a different outfit. No face swap, body reshaping, new person, new setting, or camera change.
Adult model only. Tasteful, non-explicit, professional fashion design reference; fully covered in opaque clothing; no nudity; no exposed nipples; no exposed genitals; no transparent exposure; no pornographic styling.

[This generation]
Design direction: {options.Style}. Suggested color: {options.Color}. Garment category: {options.GarmentCategory}. Generate a fresh, wearable variation while preserving every constraint above.
""".Trim();
    }

    private static string BuildSport(OutfitPreviewOptions options) => $"""
Edit the provided image as a clothing replacement preview for fashion design reference.

Keep the same model identity, same face and facial features, same hairstyle, same body proportions, same body shape, same skin tone, same pose, and same camera angle. Preserve the background as much as possible. ONLY replace clothing.
Create a tasteful {options.Style} {options.Color} {options.GarmentCategory} outfit. This is a professional, fashion-oriented fitting preview on an adult model only.

Requirements: fully covered in opaque, non-transparent garments; no nudity; no exposed nipples; no exposed genitals; non-pornographic; no sexualized pose or presentation; do not alter facial features, body proportions, camera angle, or background. Do not remove clothing except to replace it with the specified fully covering outfit. Realistic fashion editorial result.
""";

    private const string BikiniTemplate = """
[Role and source of identity]
You are a professional swimwear designer and commercial fashion retouching artist. The source screenshot is the only identity reference. The person in it is an adult female model. Never copy the people, faces, bodies, poses, or backgrounds from fashion references.

[Task]
Replace only the source model's existing clothing with a realistic, premium, production-feasible bikini / swimwear design. Keep her face, body, posture, hands, hair, skin, and scene unchanged. The source image, not the style references, controls identity, pose, camera, and environment.

[Garment-only inspiration]
The following cues were distilled from reference images 5-8; those images are not supplied to this edit request. Use them only for swimwear cut, fabric, construction, and commercial photographic finish, never for people or scenery: a gathered bandeau top with clean center ruching and secure straps; a muted textured triangle construction with matched bottoms; a bright solid-color halter with neat gathered fabric; or a crisp white asymmetric top with restrained ring detailing. These are alternative design directions, not instructions to combine every detail. Use proper opaque coverage; do not copy revealing cuts from the references.

[Construction and photographic realism]
Choose one distinct design direction. Ensure practical strap attachments, realistic fabric tension, seams, edges, appropriate top support, natural folds and occlusion, and physically plausible contact with the body. Match the source image's lighting direction, shadows, color temperature, depth of field, texture, and focal-length feeling. No floating fabric, impossible cutouts, broken straps, mirrored errors, cloth penetration, retouched plastic skin, or anatomical changes.

[Safety and output]
Tasteful adult commercial swimwear only: fully opaque and properly covering; no thong or micro-bikini, no transparent panels, no exposed nipples, no exposed genitals, no nudity, no pornographic style, and no sexualized pose. Deliver a fresh bikini design on the same model in the same original photograph; only the garment changes.
""";

    private const string JkTemplate = """
[Role and source of identity]
You are a professional adult fashion designer and commercial portrait retouching artist. The source screenshot is the only identity reference. The person in it is an adult female model. Never copy the people, faces, bodies, poses, or backgrounds from fashion references.

[Task]
Replace only the source model's clothing with a realistic, refined JK-inspired adult fashion outfit. Preserve her original face, hair, body, posture, hands, expression, camera, and setting. The source image, not the style references, controls identity, pose, camera, and environment.

[Garment-only inspiration]
The following cues were distilled from reference images 1-4; those images are not supplied to this edit request. Use them only for garment style, coordination, color, and structure, never for people or scenery: a taupe short-sleeve collared top with contrast piping, plaid tie and coordinated pleated skirt; a white blouse with navy sailor collar and bow above a navy pleated skirt; a dark tailored jacket over a muted tartan pleated skirt; or a navy cardigan with ribbon tie and blue-gray plaid skirt. Choose one coherent direction rather than merging all four. Accessories should be restrained and wearable.

[Adult styling and garment realism]
Aim for realistic, natural, refined Japanese academy-inspired commercial styling for an adult woman, not a childlike school uniform, underage appearance, fantasy costume, or cosplay. Use an appropriate adult fit and modest coverage. Ensure correct shirt and collar structure, believable ribbon or tie placement, natural pleats and fabric drape, practical socks and shoes where visible, clean seams and buttons, correct occlusion, and source-consistent shadows and skin contact. No floating layers, cloth penetration, malformed limbs, plastic skin, or body reshaping.

[Safety and output]
Tasteful, non-explicit adult female fashion only; no nudity, no exposed nipples, no exposed genitals, no pornographic styling. Deliver a fresh, production-feasible JK-inspired outfit on the same model in the same original photograph; only the garment changes.
""";
}
