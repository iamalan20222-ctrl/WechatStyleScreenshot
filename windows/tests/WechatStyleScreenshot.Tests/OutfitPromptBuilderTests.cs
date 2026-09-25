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
}
