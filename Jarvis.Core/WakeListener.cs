namespace Jarvis.Core;

public static class WakeListener
{
    // nextPhrase(true): idle wake wait; nextPhrase(false): bounded command wait.
    public static async Task<string> WaitAsync(Func<bool, CancellationToken, Task<string>> nextPhrase,
        CancellationToken ct, Action<string>? status = null)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            string text = await nextPhrase(true, ct);
            if (!WakePhrase.TryExtract(text, out string command)) continue;
            status?.Invoke("Jarvis detectado.");
            if (command.Length > 0) return command;
            status?.Invoke("Te escucho: di tu pregunta. [V] pausar / [Esc] salir.");
            command = await nextPhrase(false, ct);
            if (WakePhrase.TryExtract(command, out string remainder)) command = remainder;
            ct.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(command)) return command;
            status?.Invoke("No recibí una pregunta. Esperando «Jarvis»...");
        }
    }
}
