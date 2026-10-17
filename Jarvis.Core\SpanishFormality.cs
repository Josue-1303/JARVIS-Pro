using System.Text.RegularExpressions;

namespace Jarvis.Core;

internal static class SpanishFormality
{
    private static readonly Regex InformalAddress = new(
        @"\b(?:tu|te|ti|contigo|eres|estas|tienes|quieres|puedes|sabes|vas|haces|dices|estudias|conoces|necesitas|piensas|trabajas|naciste|viviste|dijiste|fuiste|seras|dime|cuentame|explicame|ayudame|mira|ven|espera|imagina|recuerda)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool UsesInformalAddress(string text) =>
        InformalAddress.IsMatch(ReplyLanguage.Normalize(text));
}
