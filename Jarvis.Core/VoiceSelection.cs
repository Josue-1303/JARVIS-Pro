namespace Jarvis.Core;

public sealed class VoiceSelection
{
    // null means automatic; an explicit selection persists until A is pressed.
    public string? ManualLanguage { get; private set; }
    public string Description => ManualLanguage switch
    {
        "es" => "Español (manual)",
        "en" => "English (manual)",
        _ => "Automático"
    };

    public bool HandleKey(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.D1:
            case ConsoleKey.NumPad1: ManualLanguage = "es"; return true;
            case ConsoleKey.D2:
            case ConsoleKey.NumPad2: ManualLanguage = "en"; return true;
            case ConsoleKey.A: ManualLanguage = null; return true;
            default: return false;
        }
    }

    public string Resolve(string? responseLanguage) =>
        ManualLanguage ?? (responseLanguage == "en" ? "en" : "es");

    // A model label or manual preference must never make clearly English text use a Spanish voice.
    // Manual selection is also passed to JarvisAI as the requested answer language.
    public string Resolve(JarvisReply reply) => ReplyLanguage.Detect(reply.Text) ??
        (reply.Language is "en" or "es" ? reply.Language : ManualLanguage ?? "es");
}
