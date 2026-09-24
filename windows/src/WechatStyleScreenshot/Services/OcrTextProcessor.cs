using System.Text.RegularExpressions;

namespace WechatStyleScreenshot.Services;

public static partial class OcrTextProcessor
{
    private static readonly Regex RepeatedSpaces = new(@"[\t ]+", RegexOptions.Compiled);
    private static readonly Regex SpacesBetweenCjk = new(
        @"(?<=[\u3400-\u4DBF\u4E00-\u9FFF]) +(?=[\u3000-\u303F\u3400-\u4DBF\u4E00-\u9FFF])|(?<=[\u3000-\u303F]) +(?=[\u3400-\u4DBF\u4E00-\u9FFF])",
        RegexOptions.Compiled);

    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        List<string> cleanedLines = new(lines.Length);
        bool previousWasBlank = false;

        foreach (string line in lines)
        {
            string cleaned = SpacesBetweenCjk.Replace(RepeatedSpaces.Replace(line.Trim(), " "), string.Empty);
            bool isBlank = cleaned.Length == 0;
            if (!isBlank || !previousWasBlank)
            {
                cleanedLines.Add(cleaned);
            }

            previousWasBlank = isBlank;
        }

        return string.Join('\n', cleanedLines).Trim();
    }
}
