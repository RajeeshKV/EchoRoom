using System.Text.RegularExpressions;

namespace Chat.Api.Helpers;

public static partial class MessageSanitizer
{
    private const int MaxMessageLength = 500;

    [GeneratedRegex("<.*?>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultiSpaceRegex();

    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var withoutHtml = HtmlTagRegex().Replace(input, string.Empty);
        var normalized = MultiSpaceRegex().Replace(withoutHtml, " ").Trim();

        return normalized.Length > MaxMessageLength
            ? normalized[..MaxMessageLength]
            : normalized;
    }
}
