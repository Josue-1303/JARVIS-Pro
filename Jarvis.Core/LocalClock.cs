using System.Globalization;
using System.Text.RegularExpressions;

namespace Jarvis.Core;

public sealed record ClockIntent(string Kind, string Language, int DayOffset = 0);

public static class LocalClock
{
    public static bool Replay(string question, ref ClockIntent? current, ref ClockIntent? pending)
    {
        var next = Parse(question, current);
        if (pending != null && ReplyLanguage.Normalize(question) is "si" or "si eso" or "correcto" or "yes" or "yes please" or "correct") next = pending;
        current = next;
        pending = next == null ? Suggest(question) : null;
        return current != null || pending != null;
    }

    // Never silently rewrite uncertain speech. Suggest only nearby, bounded clock commands,
    // and let JarvisAI ask for confirmation before answering from the clock.
    public static ClockIntent? Suggest(string question)
    {
        string q = ReplyLanguage.Normalize(question);
        if (q.Length < 8 || q.Length > 35) return null;
        string[] commands = { "que dia es hoy", "que hora es", "what day is today", "what time is it" };
        var matches = commands.Select(command => (command, distance: Distance(q, command)))
            .Where(x => x.distance is > 0 and <= 2).OrderBy(x => x.distance).ToArray();
        if (matches.Length == 0 || (matches.Length > 1 && matches[0].distance == matches[1].distance)) return null;
        return Parse(matches[0].command);
    }

    private static int Distance(string a, string b)
    {
        int[] row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (int i = 1; i <= a.Length; i++)
        {
            int diagonal = row[0]; row[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int old = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = old;
            }
        }
        return row[^1];
    }

    public static ClockIntent? Parse(string question, ClockIntent? previous = null)
    {
        string q = ReplyLanguage.Normalize(question);
        string? requested = ReplyLanguage.Requested(question);
        q = Regex.Replace(q, @"\s+(?:y |and )?(?:responde|respondeme|contesta|answer|respond|reply)(?:\s+(?:solo|solamente|only|please|in|en))*\s+(?:espanol|spanish|ingles|english)(?: por favor| please)?$", "");
        q = Regex.Replace(q, @"^(?:(?:hola|hello|hey|jarvis|oye|por favor|please|dije|dije que|te pregunte|queria saber|quiero saber|me refiero a|i said|i asked|i mean|and|y|entonces|so|dime|puedes decirme|me puedes decir|can you tell me|could you tell me|tell me|estas seguro|compruebalo de nuevo y dime)\s+)+", "");
        q = Regex.Replace(q, @"(?: por favor| please| ahora mismo| right now)$", "");
        string? kind = null;
        string? language = null;
        int offset = Regex.IsMatch(q, @"\b(?:ayer|yesterday)\b") ? -1 : Regex.IsMatch(q, @"\b(?:manana|tomorrow)\b") ? 1 : 0;
        // Full matches prevent current-clock answers to historical dates, other cities, or unrelated tasks.
        if (Regex.IsMatch(q, @"^(?:que dia (?:es|era|fue|sera|estamos)(?: (?:hoy|ayer|manana))?|en que dia estamos|que fecha es(?: hoy)?|cual es la fecha(?: de hoy)?|que dia de la semana (?:es|era|fue|sera)(?: (?:hoy|ayer|manana))?)$")) { kind = "date"; language = "es"; }
        if (Regex.IsMatch(q, @"^(?:what day (?:of the week )?(?:is|was|will) (?:it|today|yesterday|tomorrow)(?: be| today)?|what s (?:the date|today s date|the day today)|what is (?:the date|today s date|the current date))$")) { kind = "date"; language = "en"; }
        if (Regex.IsMatch(q, @"^(?:que hora es(?: ahora)?|cual es la hora(?: actual)?)$")) { kind = "time"; language = "es"; }
        if (Regex.IsMatch(q, @"^(?:what time (?:is it|it is)|what s the time|what is the (?:current )?time)$")) { kind = "time"; language = "en"; }
        if (kind != null) return new(kind, requested ?? language!, offset);
        string original = ReplyLanguage.Normalize(question);
        if (previous != null && (Regex.IsMatch(original, @"^(?:estas seguro|compruebalo(?: de nuevo)?|revisalo|check again|are you sure|verifica(?:lo)?|no es correcto|eso esta mal)$") ||
            (Regex.IsMatch(original, @"\b(?:calendario|calendar|calendars|supposed to be|how come|pero hoy|hoy es|today is)\b") &&
             Regex.IsMatch(original, @"\b(?:viernes|sabado|domingo|lunes|martes|miercoles|jueves|friday|saturday|sunday|monday|tuesday|wednesday|thursday)\b"))))
            return previous with { Language = requested ?? ReplyLanguage.Detect(question) ?? previous.Language };
        return null;
    }

    public static JarvisReply Answer(ClockIntent intent, DateTimeOffset now, string? languageOverride = null)
    {
        string language = languageOverride ?? intent.Language;
        var culture = CultureInfo.GetCultureInfo(language == "en" ? "en-US" : "es-ES");
        var date = now.AddDays(intent.DayOffset);
        string text;
        if (intent.Kind == "time")
            text = language == "en" ? $"It is {now:HH:mm}, according to the computer's local clock." : $"Son las {now:HH:mm}, según el reloj local del equipo.";
        else if (language == "en")
            text = $"{(intent.DayOffset == -1 ? "Yesterday was" : intent.DayOffset == 1 ? "Tomorrow is" : "Today is")} {date.ToString("dddd, MMMM d, yyyy", culture)}, according to the computer's local clock.";
        else
            text = $"{(intent.DayOffset == -1 ? "Ayer fue" : intent.DayOffset == 1 ? "Mañana es" : "Hoy es")} {date.ToString("dddd, d 'de' MMMM 'de' yyyy", culture)}, según el reloj local del equipo.";
        return new(text, language);
    }
}

