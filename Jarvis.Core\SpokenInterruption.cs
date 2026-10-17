using System.Globalization;
using System.Text;

namespace Jarvis.Core;

public static class SpokenInterruption
{
    private static readonly HashSet<string> StopCommands = new(StringComparer.Ordinal)
    {
        "para", "detente", "deten", "deten la voz", "deten la respuesta",
        "para de hablar", "silencio", "cancela", "cancela la voz",
        "stop", "stop talking", "stop speaking", "be quiet", "silence",
        "cancel", "cancel that"
    };

    public static bool IsStopCommand(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript) || !WakePhrase.TryExtract(transcript, out string command)) return false;
        return StopCommands.Contains(Normalize(command));
    }

    public static async Task<bool> RunAsync(Func<CancellationToken, Task> speak,
        Func<CancellationToken, Task<string>> listenForCommand,
        Action? interrupted = null, Action<Exception>? listenerFailed = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(speak);
        ArgumentNullException.ThrowIfNull(listenForCommand);
        cancellationToken.ThrowIfCancellationRequested();

        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        int stoppedByVoice = 0;
        Task monitor = MonitorAsync();
        try
        {
            try { await speak(operation.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested &&
                operation.IsCancellationRequested && Volatile.Read(ref stoppedByVoice) != 0)
            {
                // Spoken interruption is a successful stop, not a failed response.
            }
        }
        finally
        {
            operation.Cancel();
            try { await monitor; }
            catch (OperationCanceledException) { }
        }
        return Volatile.Read(ref stoppedByVoice) != 0;

        async Task MonitorAsync()
        {
            try
            {
                while (!operation.IsCancellationRequested)
                {
                    string transcript = await listenForCommand(operation.Token);
                    operation.Token.ThrowIfCancellationRequested();
                    if (!IsStopCommand(transcript)) continue;
                    Interlocked.Exchange(ref stoppedByVoice, 1);
                    interrupted?.Invoke();
                    operation.Cancel();
                    return;
                }
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested) { }
            catch (Exception ex) { listenerFailed?.Invoke(ex); }
        }
    }

    private static string Normalize(string value)
    {
        string decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        bool pendingSpace = false;
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSpace && result.Length > 0) result.Append(' ');
                result.Append(char.ToLowerInvariant(character));
                pendingSpace = false;
            }
            else if (char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character))
            {
                pendingSpace = true;
            }
        }
        return result.ToString();
    }
}