using System.Text.RegularExpressions;

namespace Jarvis.Core;

public static class WakePhrase
{
    private static readonly Regex Start = new(
        @"^\s*[¡¿\""']?\s*(?:(?:hola|oye|hey|hello|ok|okay)[\s,!.:;-]+)?(?:j[aá]{1,2}rvis|carvis|carbise)\b[\s,.:;!?¡¿\""'-]*(?<command>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline,
        TimeSpan.FromMilliseconds(100));

    public static bool TryExtract(string text, out string command)
    {
        var match = Start.Match(text.Trim());
        command = match.Success ? match.Groups["command"].Value.Trim() : "";
        return match.Success;
    }
}
