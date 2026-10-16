namespace Jarvis.Core;

public static class CancellableOperation
{
    public static async Task RunAsync(Func<CancellationToken, Task> operation, Func<ConsoleKey?>? readKey = null)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancelHandler;
        readKey ??= () => !Console.IsInputRedirected && Console.KeyAvailable ? Console.ReadKey(true).Key : null;
        try
        {
            Task running = operation(cancellation.Token);
            // Poll without a background ReadKey that could steal the next menu input.
            try
            {
                while (!running.IsCompleted)
                {
                    if (readKey() == ConsoleKey.Escape)
                    {
                        cancellation.Cancel();
                        break; // Stop consuming keys while the operation releases audio/resources.
                    }
                    await Task.WhenAny(running, Task.Delay(40));
                }
            }
            catch
            {
                cancellation.Cancel();
                try { await running; } catch { /* Observe failure before returning to the menu. */ }
                throw;
            }
            try { await running; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                Console.WriteLine("Operación cancelada. Listo para otra pregunta.");
            }
        }
        finally { Console.CancelKeyPress -= cancelHandler; }
    }
}
