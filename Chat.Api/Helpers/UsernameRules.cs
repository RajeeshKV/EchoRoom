using System.Text.RegularExpressions;

namespace Chat.Api.Helpers;

public static partial class UsernameRules
{
    [GeneratedRegex("^[a-zA-Z0-9]{3,20}$", RegexOptions.Compiled)]
    private static partial Regex UsernameRegex();

    public static bool IsValid(string username) => UsernameRegex().IsMatch(username);
}
