using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jarvis.Core;

public static class ReplyLanguage
{
    public static string Normalize(string text) => Regex.Replace(new string(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
        .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ').ToArray()), @"\s+", " ").Trim();

    private static readonly HashSet<string> Spanish = new("hola gracias hoy ayer manana viernes sabado domingo lunes martes miercoles jueves son las los una uno como estas estoy bien puedes dime dije que cual porque pero espanol hora fecha dia segun reloj equipo respuesta pregunta puedo ayudarte tengo tienes tenemos esta esto eso para del por favor saludame cuenta explicame historia".Split(' '));
    private static readonly HashSet<string> English = new("hello thanks today yesterday tomorrow friday saturday sunday monday tuesday wednesday thursday the is are what which when why how you your can could would tell said mean time date day according clock computer answer question please english about this that with have has will good morning evening afternoon twelve seven five".Split(' '));

    // Conservative fallback: the model's label remains useful for very short/ambiguous text.
    public static string? Detect(string text)
    {
        text = Regex.Replace(text, @"```[\s\S]*?```|`[^`]*`", " ");
        var words = Normalize(text).Split(' ').Distinct().ToArray();
        int es = words.Count(Spanish.Contains), en = words.Count(English.Contains);
        if (en >= 2 && en >= es + 2) return "en";
        if (es >= 2 && es >= en + 2) return "es";
        if (words.Length <= 3 && es > 0 && en == 0) return "es";
        if (words.Length <= 3 && en > 0 && es == 0) return "en";
        return null;
    }

    public static string? Requested(string question)
    {
        string q = Normalize(question);
        // Only an explicit response-language request; quoted titles and ordinary language mentions don't count.
        var match = Regex.Match(q, @"(?:responde|respondeme|contesta|contestame|habla|dimelo|saludame|answer|respond|reply|say|greet me)(?:\s+(?:solo|solamente|only|please|por favor|in|en))*\s+(espanol|spanish|ingles|english)(?:\s|$)");
        return match.Success ? (match.Groups[1].Value is "ingles" or "english" ? "en" : "es") : null;
    }

    public static string Resolve(string text, string? label) => Detect(text) ?? (label is "es" or "en" ? label : "unknown");
}
