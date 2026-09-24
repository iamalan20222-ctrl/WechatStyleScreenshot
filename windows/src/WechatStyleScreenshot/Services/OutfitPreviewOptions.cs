namespace WechatStyleScreenshot.Services;

public sealed record OutfitPreviewOptions(string Style, string Color, string GarmentCategory)
{
    private static readonly string[] Styles = ["sweet", "French", "minimal", "sporty", "seamless", "comfortable lounge"];
    private static readonly string[] Colors = ["black", "white", "nude", "pink", "burgundy", "blue-gray"];
    private static readonly string[] Categories = ["bra set", "wireless underwear", "sports bra", "bodysuit", "loungewear", "seamless basics"];

    public OutfitPreviewOptions() : this(Pick(Styles), Pick(Colors), Pick(Categories)) { }

    private static string Pick(string[] options) => options[Random.Shared.Next(options.Length)];
}
