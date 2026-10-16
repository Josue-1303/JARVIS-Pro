namespace Jarvis.Core;

public sealed class ConversationInput
{
    private readonly VoiceInputSettings settings;
    public bool WakeEnabled { get; private set; }
    public bool FollowUpPending { get; private set; }
    public ConversationInput(VoiceInputSettings settings) { this.settings = settings; WakeEnabled = settings.WakeEnabled; }
    public void Pause() { WakeEnabled = false; FollowUpPending = false; }
    public void Toggle() { WakeEnabled = !WakeEnabled; FollowUpPending = false; }
    public void ResponseFinished() => FollowUpPending = WakeEnabled && settings.ContinuousConversation;

    public Task<IdleInput> WaitAsync(Func<CancellationToken, Task<string>> wake,
        Func<CancellationToken, Task<string>> followUp, Func<ConsoleKey?>? readKey = null)
    {
        if (!WakeEnabled) throw new InvalidOperationException("La escucha está pausada.");
        bool follow = FollowUpPending;
        FollowUpPending = false;
        Console.WriteLine(follow
            ? $"Te escucho durante {settings.FollowUpWaitSeconds} segundos; puedes responder sin decir Jarvis. [V] pausar."
            : "Di «Jarvis» seguido de tu pregunta y deja un segundo de silencio.");
        return IdleVoiceInput.WaitAsync(follow ? followUp : wake, readKey);
    }
}

public static class FollowUpListener
{
    public static async Task<string> WaitAsync(Func<CancellationToken, CancellationToken, Task<string>> capture,
        TimeSpan wait, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(wait);
        while (!deadline.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text = await capture(cancellationToken, deadline.Token);
            cancellationToken.ThrowIfCancellationRequested();
            if (WakePhrase.TryExtract(text, out string remainder)) text = remainder;
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        Console.WriteLine("Sin respuesta. Vuelvo a esperar «Jarvis».");
        return "";
    }
}
