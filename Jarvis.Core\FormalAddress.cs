namespace Jarvis.Core;

internal static class FormalAddress
{
    public static JarvisReply Apply(JarvisReply reply)
    {
        if (reply.Language is not ("es" or "en")) return reply;

        string honorific = reply.Language == "es" ? "Señor" : "Sir";
        if (ReplyLanguage.Normalize(reply.Text).Split(' ').Contains(ReplyLanguage.Normalize(honorific), StringComparer.Ordinal))
            return reply;

        string body = reply.Text.TrimStart();
        if (body.Length == 0) return reply with { Text = honorific + "." };
        if (body.StartsWith("- ", StringComparison.Ordinal) || body.StartsWith("* ", StringComparison.Ordinal) ||
            body.StartsWith("#", StringComparison.Ordinal) || body.StartsWith("```", StringComparison.Ordinal))
            return reply with { Text = honorific + ".\n\n" + body };

        if (char.IsLetter(body[0])) body = char.ToLowerInvariant(body[0]) + body[1..];
        return reply with { Text = honorific + ", " + body };
    }
}
