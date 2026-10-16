namespace Jarvis.Core;

public sealed record IdleInput(ConsoleKey? Key, string? Text);

public static class IdleVoiceInput
{
    public static async Task<IdleInput> WaitAsync(Func<CancellationToken, Task<string>> listen,
        Func<ConsoleKey?>? readKey = null)
    {
        readKey ??= () => !Console.IsInputRedirected && Console.KeyAvailable ? Console.ReadKey(true).Key : null;
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            Task<string> listening = listen(cancellation.Token);
            ConsoleKey? selected = null;
            try
            {
                while (!listening.IsCompleted && !cancellation.IsCancellationRequested)
                {
                    selected = readKey();
                    if (selected != null) { cancellation.Cancel(); break; }
                    await Task.WhenAny(listening, Task.Delay(40));
                }
                if (cancellation.IsCancellationRequested)
                {
                    try { await listening; } catch (OperationCanceledException) { }
                    return new IdleInput(selected ?? ConsoleKey.V, null);
                }
                return new IdleInput(null, await listening);
            }
            finally
            {
                cancellation.Cancel();
                // Always release the microphone before opening a menu or starting speech.
                try { await listening; } catch { /* Original result/error is handled above. */ }
            }
        }
        finally { Console.CancelKeyPress -= handler; }
    }
}
