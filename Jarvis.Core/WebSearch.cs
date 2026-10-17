using System.Globalization;
using System.Text;

namespace Jarvis.Core;

public sealed record WebSearchSource(string Title, string Url);
public sealed record WebSearchResult(string Title, string Url, string Content);

public static class WebSearchIntent
{
    private static readonly string[] Indicators =
    [
        "search the web", "search online", "search the internet", "search for", "look up online", "look up the latest",
        "find online", "latest", "recent", "current", "today", "this week", "news", "what happened recently",
        "what is happening", "what's happening", "up-to-date", "up to date",
        "busca en internet", "busca en la web", "busque en internet", "busque en la web", "investiga en internet",
        "busca", "busque", "buscar", "noticia", "actual", "reciente", "actualizado", "actualizada", "hoy", "ahora",
        "qué está pasando", "que esta pasando", "qué está ocurriendo", "que esta ocurriendo", "esta semana",
        "uutiset", "etsi verkosta", "etsi internetistä", "ajankohtainen", "tänään", "tanaan", "mitä tapahtuu", "mita tapahtuu"
    ];

    public static bool IsRequired(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return false;
        string normalized = RemoveDiacritics(question).ToLowerInvariant();
        return Indicators.Any(indicator => normalized.Contains(RemoveDiacritics(indicator).ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static string RemoveDiacritics(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (char character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) result.Append(character);
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
