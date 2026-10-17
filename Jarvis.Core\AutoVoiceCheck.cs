namespace Jarvis.Core;

internal static class AutoVoiceCheck
{
    public static async Task RunAsync()
    {
        var ai = new JarvisAI();
        var selection = new VoiceSelection();
        var speech = new PiperSpeechService();
        var cases = new[]
        {
            ("Salúdame en español en una sola frase breve.", "es"),
            ("Ahora responde solamente en inglés británico con un saludo breve.", "en"),
            ("Now reply only in Spanish with a short greeting.", "es")
        };
        foreach (var (prompt, expected) in cases)
        {
            var reply = await ai.AskReplyAsync(prompt);
            string selected = selection.Resolve(reply);
            Console.WriteLine($"Respuesta [{reply.Language}], voz [{selected}]: {reply.Text}");
            if (reply.Language != expected || selected != expected)
                throw new InvalidOperationException($"Expected {expected}, got {reply.Language}/{selected}.");
            await speech.SpeakAsync(reply.Text, selected);
            Console.WriteLine($"PASS: Ollama -> selección automática -> Piper [{selected}].");
        }
    }
}
