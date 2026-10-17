namespace Jarvis.Core;

internal static class AssistantRuleAnswers
{
    public static JarvisReply? TryAnswer(string question, IReadOnlyList<SavedUserRule>? userRules, string? preferredLanguage)
    {
        string normalized = ReplyLanguage.Normalize(question);
        if (!IsRulesQuestion(normalized)) return null;

        string language = preferredLanguage ?? ReplyLanguage.Requested(question) ?? ReplyLanguage.Detect(question) ??
            (normalized.Contains("reglas", StringComparison.Ordinal) || normalized.Contains("regla", StringComparison.Ordinal) ? "es" : "en");
        bool english = language == "en";
        string coreRules = string.Join("\n", AssistantRules.All.Select(rule =>
            $"{rule.Number}. {(english ? rule.EnglishTitle : rule.SpanishTitle)}: {(english ? rule.EnglishText : rule.SpanishText)}"));
        string personalRules = userRules is { Count: > 0 }
            ? (english
                ? "\n\nYour personal preferences, which remain subordinate to these core rules:\n" +
                  string.Join("\n", userRules.Select((rule, index) => $"{index + 1}. {rule.Text}"))
                : "\n\nSus preferencias personales, subordinadas a estas directrices esenciales:\n" +
                  string.Join("\n", userRules.Select((rule, index) => $"{index + 1}. {rule.Text}")))
            : (english ? "\n\nYou have not added any personal rules." : "\n\nUsted no ha añadido reglas personales.");

        string answer = english
            ? "My eight immutable core rules are:\n" + coreRules + personalRules
            : "Mis ocho directrices esenciales son:\n" + coreRules + personalRules;
        return new JarvisReply(answer, english ? "en" : "es");
    }

    private static bool IsRulesQuestion(string normalized) =>
        (normalized.Contains("reglas", StringComparison.Ordinal) || normalized.Contains("regla", StringComparison.Ordinal) ||
         normalized.Contains("rules", StringComparison.Ordinal) || normalized.Contains("rule", StringComparison.Ordinal)) &&
        (normalized.Contains("cuales", StringComparison.Ordinal) || normalized.Contains("que reglas", StringComparison.Ordinal) ||
         normalized.Contains("tus reglas", StringComparison.Ordinal) || normalized.Contains("sus reglas", StringComparison.Ordinal) ||
         normalized.Contains("mis reglas", StringComparison.Ordinal) || normalized.Contains("lista", StringComparison.Ordinal) ||
         normalized.Contains("enumer", StringComparison.Ordinal) || normalized.Contains("tell me", StringComparison.Ordinal) ||
         normalized.Contains("what", StringComparison.Ordinal) || normalized.Contains("which", StringComparison.Ordinal) ||
         normalized.Contains("show", StringComparison.Ordinal) || normalized.Contains("list", StringComparison.Ordinal));
}
