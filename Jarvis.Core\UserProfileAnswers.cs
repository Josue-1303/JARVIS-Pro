namespace Jarvis.Core;

internal static class UserProfileAnswers
{
    public static JarvisReply? TryAnswer(string question, IReadOnlyList<SavedMemory>? memories, string? preferredLanguage,
        string memoryManagementMode = "console")
    {
        if (memories is null) return null;

        if (IsGirlfriendQuestion(question))
        {
            var girlfriend = FindGirlfriend(memory: memories);
            if (girlfriend is not null)
            {
                string normalizedQuestion = ReplyLanguage.Normalize(question);
                bool girlfriendQuestionEnglish = normalizedQuestion.StartsWith("who is my girlfriend", StringComparison.Ordinal) ||
                    normalizedQuestion.StartsWith("what is my girlfriend", StringComparison.Ordinal) ||
                    normalizedQuestion.StartsWith("whats my girlfriend", StringComparison.Ordinal) ||
                    normalizedQuestion.StartsWith("what is my partner", StringComparison.Ordinal) ||
                    normalizedQuestion.StartsWith("who is my partner", StringComparison.Ordinal);
                string responseLanguage = preferredLanguage ?? ReplyLanguage.Requested(question) ?? (girlfriendQuestionEnglish ? "en" : "es");
                string nickname = girlfriend.Value.Nickname is { Length: > 0 } value
                    ? responseLanguage == "en" ? $" She also goes by {value}." : $" También la llaman {value}."
                    : string.Empty;
                return responseLanguage == "en"
                    ? new JarvisReply($"Your girlfriend's name is {girlfriend.Value.Name}, sir.{nickname}", "en")
                    : new JarvisReply($"El nombre de su novia es {girlfriend.Value.Name}, señor.{nickname}", "es");
            }
        }

        if (IsMemoryPermissionQuestion(question))
        {
            string normalizedQuestion = ReplyLanguage.Normalize(question);
            bool english = normalizedQuestion.StartsWith("can you", StringComparison.Ordinal) ||
                normalizedQuestion.StartsWith("may i", StringComparison.Ordinal) ||
                normalizedQuestion.StartsWith("are you allowed", StringComparison.Ordinal) ||
                normalizedQuestion.StartsWith("do you have permission", StringComparison.Ordinal);
            string responseLanguage = preferredLanguage ?? ReplyLanguage.Requested(question) ?? (english ? "en" : "es");
            bool desktop = memoryManagementMode == "desktop";
            if (responseLanguage == "en")
                return new JarvisReply(desktop
                    ? "Yes, sir. You may save it with the Memories button in the left sidebar. Enter the fact and confirm; I have not saved anything yet."
                    : "Yes, sir. You may save it by pressing M, choosing G, and confirming with SI. I have not saved anything yet.", "en");
            return new JarvisReply(desktop
                ? "Sí, señor. Puede guardarlo con el botón «Recuerdos» de la barra izquierda. Escriba el dato y confirme; todavía no he guardado nada."
                : "Sí, señor. Puede guardarlo con M, luego G, y confirmar escribiendo SI. Todavía no he guardado nada.", "es");
        }

        if (IsFullNameQuestion(question))
        {
            string fullNameQuestion = ReplyLanguage.Normalize(question);
            bool english = fullNameQuestion.StartsWith("what is my full name", StringComparison.Ordinal) ||
                fullNameQuestion.StartsWith("whats my full name", StringComparison.Ordinal) ||
                fullNameQuestion.StartsWith("tell me my full name", StringComparison.Ordinal);
            string responseLanguage = preferredLanguage ?? ReplyLanguage.Requested(question) ?? (english ? "en" : "es");
            bool savedFullName = memories.Any(memory =>
                ReplyLanguage.Normalize(memory.Text).Contains("mi nombre completo es", StringComparison.Ordinal) ||
                ReplyLanguage.Normalize(memory.Text).Contains("my full name is", StringComparison.Ordinal));
            if (!savedFullName)
                return responseLanguage == "en"
                    ? new JarvisReply("I don't have your full name saved yet, sir. I only have your first name.", "en")
                    : new JarvisReply("Aún no tengo guardado su nombre completo, señor; solo tengo su nombre de pila.", "es");

            return null; // Let the model use the explicitly saved full-name fact.
        }

        if (!IsStudyQuestion(question)) return null;

        bool hasStudyFact = memories.Any(memory =>
            memory.Text.Contains("IT Engineering", StringComparison.OrdinalIgnoreCase) &&
            memory.Text.Contains("Centria", StringComparison.OrdinalIgnoreCase));
        if (!hasStudyFact) return null;

        string normalized = ReplyLanguage.Normalize(question);
        bool englishQuestion = normalized.StartsWith("what do i study", StringComparison.Ordinal) ||
            normalized.StartsWith("what am i studying", StringComparison.Ordinal) ||
            normalized.StartsWith("what is my major", StringComparison.Ordinal) ||
            normalized.StartsWith("what course am i studying", StringComparison.Ordinal);
        string language = preferredLanguage ?? ReplyLanguage.Requested(question) ?? (englishQuestion ? "en" : "es");
        return language == "en"
            ? new JarvisReply("You study IT Engineering at Centria University of Applied Sciences in Kokkola, Finland, sir.", "en")
            : new JarvisReply("Usted estudia IT Engineering en Centria University of Applied Sciences, en Kokkola, Finlandia, señor.", "es");
    }

    private static bool IsMemoryPermissionQuestion(string question)
    {
        string normalized = ReplyLanguage.Normalize(question);
        return normalized.StartsWith("puedes guardar", StringComparison.Ordinal) ||
            normalized.StartsWith("puedes recordar", StringComparison.Ordinal) ||
            normalized.StartsWith("puedes agregar", StringComparison.Ordinal) ||
            normalized.StartsWith("puedes anadir", StringComparison.Ordinal) ||
            normalized.StartsWith("tienes permiso para guardar", StringComparison.Ordinal) ||
            normalized.StartsWith("no tienes permiso", StringComparison.Ordinal) ||
            normalized.StartsWith("can you save", StringComparison.Ordinal) ||
            normalized.StartsWith("can you remember", StringComparison.Ordinal) ||
            normalized.StartsWith("can you store", StringComparison.Ordinal) ||
            normalized.StartsWith("are you allowed to save", StringComparison.Ordinal) ||
            normalized.StartsWith("do you have permission to save", StringComparison.Ordinal);
    }

    private static bool IsFullNameQuestion(string question)
    {
        string normalized = ReplyLanguage.Normalize(question);
        return normalized.StartsWith("cual es mi nombre completo", StringComparison.Ordinal) ||
            normalized.StartsWith("como es mi nombre completo", StringComparison.Ordinal) ||
            normalized.StartsWith("dime mi nombre completo", StringComparison.Ordinal) ||
            normalized.StartsWith("what is my full name", StringComparison.Ordinal) ||
            normalized.StartsWith("whats my full name", StringComparison.Ordinal) ||
            normalized.StartsWith("tell me my full name", StringComparison.Ordinal);
    }

    private static bool IsGirlfriendQuestion(string question)
    {
        string normalized = ReplyLanguage.Normalize(question);
        return normalized.StartsWith("cual es el nombre de mi novia", StringComparison.Ordinal) ||
            normalized.StartsWith("como se llama mi novia", StringComparison.Ordinal) ||
            normalized.StartsWith("quien es mi novia", StringComparison.Ordinal) ||
            normalized.StartsWith("sabes quien es mi novia", StringComparison.Ordinal) ||
            normalized.StartsWith("cual es el nombre de mi pareja", StringComparison.Ordinal) ||
            normalized.StartsWith("como se llama mi pareja", StringComparison.Ordinal) ||
            normalized.StartsWith("what is my girlfriend", StringComparison.Ordinal) ||
            normalized.StartsWith("whats my girlfriend", StringComparison.Ordinal) ||
            normalized.StartsWith("who is my girlfriend", StringComparison.Ordinal) ||
            normalized.StartsWith("do you know who my girlfriend", StringComparison.Ordinal) ||
            normalized.StartsWith("what is my partner", StringComparison.Ordinal) ||
            normalized.StartsWith("who is my partner", StringComparison.Ordinal);
    }

    private static (string Name, string? Nickname)? FindGirlfriend(IReadOnlyList<SavedMemory> memory)
    {
        foreach (var item in memory)
        {
            string fact = item.Text;
            if (!fact.Contains("novia", StringComparison.OrdinalIgnoreCase) &&
                !fact.Contains("girlfriend", StringComparison.OrdinalIgnoreCase)) continue;
            int nameStart = fact.IndexOf("se llama ", StringComparison.OrdinalIgnoreCase);
            if (nameStart < 0) nameStart = fact.IndexOf("is called ", StringComparison.OrdinalIgnoreCase);
            if (nameStart < 0) continue;
            nameStart += fact.AsSpan(nameStart).StartsWith("se llama ", StringComparison.OrdinalIgnoreCase) ? 9 : 10;
            string remainder = fact[nameStart..].Trim();
            int nameEnd = remainder.IndexOfAny([';', '.', '\n', '\r']);
            string name = (nameEnd < 0 ? remainder : remainder[..nameEnd]).Trim();
            if (name.Length == 0) continue;

            int nicknameStart = fact.IndexOf("llamarla ", StringComparison.OrdinalIgnoreCase);
            if (nicknameStart < 0) nicknameStart = fact.IndexOf("goes by ", StringComparison.OrdinalIgnoreCase);
            string? nickname = null;
            if (nicknameStart >= 0)
            {
                nicknameStart += fact.AsSpan(nicknameStart).StartsWith("llamarla ", StringComparison.OrdinalIgnoreCase) ? 9 : 8;
                string nicknameRemainder = fact[nicknameStart..].Trim();
                int nicknameEnd = nicknameRemainder.IndexOfAny([';', '.', '\n', '\r']);
                nickname = (nicknameEnd < 0 ? nicknameRemainder : nicknameRemainder[..nicknameEnd]).Trim();
                if (nickname.Length == 0) nickname = null;
            }
            return (name, nickname);
        }
        return null;
    }

    private static bool IsStudyQuestion(string question)
    {
        string normalized = ReplyLanguage.Normalize(question);
        return normalized.StartsWith("que estudio", StringComparison.Ordinal) ||
            normalized.StartsWith("que estoy estudiando", StringComparison.Ordinal) ||
            normalized.StartsWith("que carrera estudio", StringComparison.Ordinal) ||
            normalized.StartsWith("cual es mi carrera", StringComparison.Ordinal) ||
            normalized.StartsWith("what do i study", StringComparison.Ordinal) ||
            normalized.StartsWith("what am i studying", StringComparison.Ordinal) ||
            normalized.StartsWith("what is my major", StringComparison.Ordinal) ||
            normalized.StartsWith("what course am i studying", StringComparison.Ordinal);
    }
}
