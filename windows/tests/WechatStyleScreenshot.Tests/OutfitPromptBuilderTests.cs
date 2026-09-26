using System.Text.RegularExpressions;
using WechatStyleScreenshot.Services;

namespace WechatStyleScreenshot.Tests;

public class OutfitPromptBuilderTests
{
    [Fact]
    public void BuildIncludesIdentityAndSafetyConstraints()
    {
        string prompt = OutfitPromptBuilder.Build(new OutfitPreviewOptions("French", "Black", "Bra set"));

        Assert.Contains("same model identity", prompt);
        Assert.Contains("same face", prompt);
        Assert.Contains("same hairstyle", prompt);
        Assert.Contains("same body proportions", prompt);
        Assert.Contains("same pose", prompt);
        Assert.Contains("Preserve the background", prompt);
        Assert.Contains("ONLY replace clothing", prompt);
        Assert.Contains("same camera angle", prompt);
        Assert.Contains("adult model", prompt);
        Assert.Contains("no nudity", prompt);
        Assert.Contains("no exposed nipples", prompt);
        Assert.Contains("no exposed genitals", prompt);
        Assert.Contains("fully covered", prompt);
        Assert.Contains("non-pornographic", prompt);
        Assert.Contains("French", prompt);
        Assert.Contains("Black", prompt);
        Assert.Contains("Bra set", prompt);
    }

    [Fact]
    public void LockedPromptLayersStayStyleNeutral()
    {
        string locked = string.Join("\n", OutfitPromptBuilder.LockedCommonPrompt,
            OutfitPromptBuilder.LockedProviderRules, OutfitPromptBuilder.LockedSafetySuffix);
        foreach (string styleTerm in new[] { "sporty", "sports bra", "minimal black", "functional innerwear",
                     "bikini", "JK", "pleated skirt", "lace", "swimwear", "school style" })
            Assert.False(Regex.IsMatch(locked, $@"\b{Regex.Escape(styleTerm)}\b", RegexOptions.IgnoreCase), styleTerm);
    }

    [Fact]
    public void EditedSportPromptDoesNotReintroduceDefaultSportOrBlackGarment()
    {
        OutfitPreviewOptions options = new("sporty minimal", "black", "sports bra and functional innerwear",
            OutfitStylePresetType.Sport);
        StyleSetting edited = new() { Title = "运动风", Prompt = "Replace the clothing with a tailored white linen blouse." };
        string prompt = OutfitPromptBuilder.Build(options, edited);
        Assert.Contains(edited.Prompt, prompt);
        foreach (string oldDirection in new[] { "sporty", "sports bra", "minimal black", "functional innerwear" })
            Assert.DoesNotContain(oldDirection, prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("运动风", prompt);
        Assert.Contains("Do not add a different garment type or style", prompt);
    }

    [Theory]
    [InlineData(OutfitStylePresetType.Bikini)]
    [InlineData(OutfitStylePresetType.JK)]
    public void OtherPresetsDoNotInheritSportGarment(OutfitStylePresetType preset)
    {
        string prompt = OutfitPromptBuilder.Build(new OutfitPreviewOptions(preset));
        Assert.DoesNotContain("sports bra", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("functional innerwear", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sporty minimal", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(OutfitStylePresetType.Sport, "sports bra")]
    [InlineData(OutfitStylePresetType.Bikini, "swimwear")]
    [InlineData(OutfitStylePresetType.JK, "JK-inspired")]
    public void PresetsKeepAdultIdentityAndCoverageRules(OutfitStylePresetType preset, string garment)
    {
        string prompt = OutfitPromptBuilder.Build(new OutfitPreviewOptions(preset));
        Assert.Contains(garment, prompt);
        Assert.Contains("same face", prompt);
        Assert.Contains("body proportions", prompt);
        Assert.Contains("pose", prompt);
        Assert.Contains("background", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("adult", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no nudity", prompt);
        Assert.Contains("no exposed nipples", prompt);
        Assert.Contains("no exposed genitals", prompt);
    }

    [Theory]
    [InlineData(OutfitStylePresetType.Sport)]
    [InlineData(OutfitStylePresetType.Bikini)]
    [InlineData(OutfitStylePresetType.JK)]
    public void EveryPresetLocksSelectionBoundaryAndOriginalScene(OutfitStylePresetType preset)
    {
        string prompt = OutfitPromptBuilder.Build(new OutfitPreviewOptions(preset));
        Assert.Contains("Only edit the model's clothing inside the screenshot selection", prompt);
        Assert.Contains("Anything outside the screenshot selection must remain unchanged", prompt);
        Assert.Contains("same model in the same scene at the same moment", prompt);
        Assert.Contains("face", prompt);
        Assert.Contains("hairstyle", prompt);
        Assert.Contains("body proportions", prompt);
        Assert.Contains("pose", prompt);
        Assert.Contains("background", prompt);
        Assert.Contains("lighting", prompt);
        Assert.Contains("composition", prompt);
    }

    [Fact]
    public void BikiniDefaultUsesAdultCommercialSwimwearPrompt()
    {
        StyleSetting style = OutfitStyleCatalog.GetDefaultSetting(OutfitStylePresetType.Bikini);
        Assert.Equal("泳衣", style.Title);
        Assert.StartsWith("请将服装替换为一套适合设计参考的成人商业泳衣。", style.Prompt);
        Assert.Contains("同一个成年女性模特，在同一场景和同一拍摄瞬间", style.Prompt);
        Assert.Contains("不透明", style.Prompt);
        Assert.Contains("非情色", style.Prompt);
        string prompt = OutfitPromptBuilder.Build(new OutfitPreviewOptions(OutfitStylePresetType.Bikini));
        Assert.Contains(style.Prompt, prompt);
        Assert.Contains("source screenshot is the only identity reference", prompt);
        Assert.DoesNotContain("比基尼", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bikini", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("B 泳衣", OutfitStyleCatalog.Get(OutfitStylePresetType.Bikini).Label);
    }

    [Fact]
    public void JkUsesAdultFashionReferencesButNotReferencePeople()
    {
        string prompt = OutfitPromptBuilder.Build(new OutfitPreviewOptions(OutfitStylePresetType.JK));
        Assert.Contains("source screenshot is the only identity reference", prompt);
        Assert.Contains("reference images 1-4", prompt);
        Assert.Contains("garment style, coordination, color, and structure", prompt);
        Assert.Contains("Never copy the people", prompt);
        Assert.Contains("adult female model", prompt);
        Assert.Contains("not a childlike school uniform", prompt);
    }

    [Fact]
    public void RandomStyleDirectionsStayWithinTheGarmentReferenceFamilies()
    {
        Assert.Equal(5, OutfitStyleCatalog.Get(OutfitStylePresetType.Bikini).Directions.Length);
        Assert.Equal(4, OutfitStyleCatalog.Get(OutfitStylePresetType.JK).Directions.Length);
        Assert.DoesNotContain(OutfitStyleCatalog.Get(OutfitStylePresetType.Bikini).Directions,
            direction => direction.Contains("cut-out", StringComparison.OrdinalIgnoreCase) ||
                direction.Contains("thin-strap", StringComparison.OrdinalIgnoreCase));
    }
}
