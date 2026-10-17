namespace Jarvis.Core;

internal static class AssistantMemoryAnswers
{
    public static JarvisReply? TryAnswer(string question, IReadOnlyList<SavedMemory>? memories, string? preferredLanguage)
    {
        string normalized = ReplyLanguage.Normalize(question);
        if (!IsMemoryRecallQuestion(normalized)) return null;

        string language = preferredLanguage ?? ReplyLanguage.Requested(question) ?? ReplyLanguage.Detect(question) ??
            (ContainsSpanishMemoryWord(normalized) ? "es" : "en");
        bool spanish = language == "es";
        if (memories is not { Count: > 0 })
            return spanish
                ? new JarvisReply("Señor, ahora mismo no hay recuerdos personales guardados sobre usted.", "es")
                : new JarvisReply("Sir, there are no personal memories saved about you right now.", "en");

        string heading = spanish
            ? "Señor, estas son las memorias guardadas sobre usted:"
            : "Sir, these are the memories saved about you:";
        string list = string.Join("\n", memories.Select((memory, index) => $"{index + 1}. {memory.Text}"));
        return new JarvisReply(heading + "\n" + list, language);
    }

    private static bool ContainsSpanishMemoryWord(string question) =>
        question.Contains("memoria", StringComparison.Ordinal) || question.Contains("recuerdo", StringComparison.Ordinal) ||
        question.Contains("recuerdas", StringComparison.Ordinal);

    private static bool IsMemoryRecallQuestion(string question)
    {
        bool mentionsMemory = question.Contains("memoria", StringComparison.Ordinal) ||
            question.Contains("recuerdo", StringComparison.Ordinal) || question.Contains("memory", StringComparison.Ordinal) ||
            question.Contains("memorie", StringComparison.Ordinal) || question.Contains("remember", StringComparison.Ordinal);
        if (mentionsMemory && (question.Contains("cual", StringComparison.Ordinal) || question.Contains("que", StringComparison.Ordinal) ||
            question.Contains("tienes", StringComparison.Ordinal) || question.Contains("tus", StringComparison.Ordinal) ||
            question.Contains("lista", StringComparison.Ordinal) || question.Contains("dime", StringComparison.Ordinal) ||
            question.Contains("what", StringComparison.Ordinal) || question.Contains("which", StringComparison.Ordinal) ||
            question.Contains("have", StringComparison.Ordinal) || question.Contains("do you", StringComparison.Ordinal) ||
            question.Contains("list", StringComparison.Ordinal) || question.Contains("tell", StringComparison.Ordinal) ||
            question.Contains("show", StringComparison.Ordinal))) return true;

        return question.StartsWith("que recuerdas de mi", StringComparison.Ordinal) ||
            question.StartsWith("que sabes de mi", StringComparison.Ordinal) ||
            question.StartsWith("what do you remember about me", StringComparison.Ordinal) ||
            question.StartsWith("what do you know about me", StringComparison.Ordinal) ||
            question.StartsWith("what have you saved about me", StringComparison.Ordinal);
    }
}
