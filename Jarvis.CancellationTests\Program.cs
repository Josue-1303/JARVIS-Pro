using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Jarvis.Core;

try { await Tests.RunAsync(args.Contains("--live")); }
catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }

static class Tests
{
    private static int checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
        checks++;
    }
    private static async Task ExpectCancelled(Task operation, string name)
    {
        bool cancelled = false;
        try { await operation; } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, name);
    }
    public static async Task RunAsync(bool live)
    {
        string data = Path.Combine(Path.GetTempPath(), "jarvis-v06-" + Guid.NewGuid().ToString("N"));
        using var store = new MemoryStore(data);
        var session = new ConversationSession(store, store.CreateConversation());
        using var handler = new ControlledHandler();
        using var client = new HttpClient(handler);
        var ai = new JarvisAI(client, session);
        using (var alreadyCancelled = new CancellationTokenSource())
        {
            alreadyCancelled.Cancel();
            await ExpectCancelled(ai.AskReplyAsync("Do not send", alreadyCancelled.Token), "Pre-cancelled request rejected");
            Check(handler.Calls == 0, "Pre-cancellation does not contact Ollama");
        }
        using (var cancellation = new CancellationTokenSource())
        {
            var pending = ai.AskReplyAsync("Pending reply", cancellation.Token);
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await ExpectCancelled(pending.WaitAsync(TimeSpan.FromSeconds(5)), "Pending HTTP request cancelled promptly");
            Check(handler.SawCancellation, "Cancellation reached HTTP transport");
            Check(store.Load(session.Current.Id).Turns.Count == 0, "Cancelled response not saved");
        }
        handler.Wait = false;
        await ai.AskReplyAsync("Next question");
        Check(store.Load(session.Current.Id).Turns.Count == 1, "Next request works after cancellation");
        using (var body = JsonDocument.Parse(handler.LastRequest))
            Check(body.RootElement.GetProperty("messages").GetArrayLength() == 2, "Cancelled turn absent from following context");

        bool settled = false;
        await CancellableOperation.RunAsync(async ct =>
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { settled = true; }
        }, () => ConsoleKey.Escape);
        Check(settled, "Escape waits for cleanup before returning");
        bool nextRan = false;
        await CancellableOperation.RunAsync(ct => { Check(!ct.IsCancellationRequested, "Next operation has fresh token"); nextRan = true; return Task.CompletedTask; }, () => throw new Exception("Completed operation must not read input"));
        Check(nextRan, "Completed operation leaves keyboard alone");
        bool timeoutPropagated = false;
        try { await CancellableOperation.RunAsync(_ => Task.FromException(new OperationCanceledException("timeout")), () => null); }
        catch (OperationCanceledException) { timeoutPropagated = true; }
        Check(timeoutPropagated, "Timeout distinguished from user cancellation");
        settled = false;
        bool inputFailure = false;
        try
        {
            await CancellableOperation.RunAsync(async ct =>
            {
                try { await Task.Delay(Timeout.Infinite, ct); } finally { settled = true; }
            }, () => throw new IOException("keyboard unavailable"));
        }
        catch (IOException) { inputFailure = true; }
        Check(inputFailure && settled, "Keyboard failure cancels and observes active operation");

        if (live) await LiveAsync(store);
        Console.WriteLine($"{checks} cancellation checks passed. Isolated data: {data}");
    }

    private static async Task LiveAsync(MemoryStore store)
    {
        using var transport = new LiveHandler();
        using var client = new HttpClient(transport) { Timeout = TimeSpan.FromMinutes(5) };
        var session = new ConversationSession(store, store.CreateConversation());
        var ai = new JarvisAI(client, session);
        using (var cancellation = new CancellationTokenSource())
        {
            var pending = ai.AskReplyAsync("Escribe una explicación muy larga y detallada de la historia de la astronomía.", cancellation.Token);
            await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(400);
            cancellation.Cancel();
            await ExpectCancelled(pending.WaitAsync(TimeSpan.FromSeconds(10)), "Live Ollama request cancelled");
            Check(store.Load(session.Current.Id).Turns.Count == 0, "Live cancelled turn absent from disk");
        }
        var reply = await ai.AskReplyAsync("Responde solo con un saludo breve en español.");
        Check(!string.IsNullOrWhiteSpace(reply.Text) && session.Current.Turns.Count == 1, "Live Ollama responds after cancellation");

        var speech = new PiperSpeechService();
        var before = Directory.GetFiles(Path.GetTempPath(), "jarvis-piper-*.wav").ToHashSet();
        using (var cancellation = new CancellationTokenSource())
        {
            bool started = false;
            var timer = Stopwatch.StartNew();
            await ExpectCancelled(speech.SpeakAsync("Esta generación de audio debe cancelarse.", "es", cancellation.Token, stage =>
            {
                if (stage == SpeechStage.Synthesizing) { started = true; cancellation.Cancel(); }
            }).WaitAsync(TimeSpan.FromSeconds(15)), "Piper generation cancels and exits");
            Check(started && timer.Elapsed < TimeSpan.FromSeconds(15), "Started Piper process cleaned up promptly");
        }
        foreach (var (language, text) in new[]
        {
            ("es", "Esta es una prueba de interrupción de voz. La reproducción debe detenerse antes de terminar esta frase y permitir otra pregunta."),
            ("en", "This is a speech interruption test. Playback should stop before this sentence finishes and allow another question.")
        })
        {
            using var cancellation = new CancellationTokenSource();
            var playbackTimer = new Stopwatch();
            await ExpectCancelled(speech.SpeakAsync(text, language, cancellation.Token, stage =>
            {
                if (stage == SpeechStage.Speaking) { playbackTimer.Start(); cancellation.CancelAfter(350); }
            }).WaitAsync(TimeSpan.FromSeconds(130)), $"Playback interrupted [{language}]");
            Check(playbackTimer.IsRunning && playbackTimer.Elapsed < TimeSpan.FromSeconds(3), $"Playback stops promptly [{language}]");

            // Exercise the same keyboard-polling -> token -> Piper path used by Program.
            playbackTimer.Reset();
            bool escapeSent = false, speechCancelled = false;
            await CancellableOperation.RunAsync(async ct =>
            {
                try
                {
                    await speech.SpeakAsync(text, language, ct, stage =>
                    {
                        if (stage == SpeechStage.Speaking) playbackTimer.Start();
                    });
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { speechCancelled = true; throw; }
            }, () =>
            {
                if (!escapeSent && playbackTimer.IsRunning && playbackTimer.ElapsedMilliseconds >= 350)
                { escapeSent = true; return ConsoleKey.Escape; }
                return null;
            }).WaitAsync(TimeSpan.FromSeconds(130));
            Check(escapeSent && speechCancelled, $"Escape routed through operation stops real Piper [{language}]");
            Check(playbackTimer.Elapsed < TimeSpan.FromSeconds(3), $"Escape stops playback promptly [{language}]");
        }
        Check(store.Load(session.Current.Id).Turns.Count == 1, "Interrupting voice preserves completed reply");
        var speechStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool spokenStop = await SpokenInterruption.RunAsync(ct => speech.SpeakAsync(
            string.Join(" ", Enumerable.Repeat("Esta respuesta larga permite probar que Jarvis se detenga al recibir una orden hablada durante la reproducción.", 5)),
            "es", ct, stage => { if (stage == SpeechStage.Speaking) speechStarted.TrySetResult(); }),
            async ct =>
            {
                await speechStarted.Task.WaitAsync(ct);
                return "Jarvis, detente";
            });
        Check(spokenStop, "Wake-prefixed voice command cancels real Piper playback");
        await speech.SpeakAsync("Listo.", "es");
        await speech.SpeakAsync("Ready.", "en");
        Check(true, "Both voices work normally after interruption");
        Check(!Directory.GetFiles(Path.GetTempPath(), "jarvis-piper-*.wav").Except(before).Any(), "Cancelled and completed audio files cleaned up");
    }
}

sealed class ControlledHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public bool Wait { get; set; } = true;
    public bool SawCancellation { get; private set; }
    public string LastRequest { get; private set; } = "";
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        LastRequest = await request.Content!.ReadAsStringAsync(ct);
        Started.TrySetResult();
        if (Wait)
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { SawCancellation = true; throw; }
        }
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { text = LastRequest.Contains("current answer must be in English") ? "Ready." : "Listo.", language = LastRequest.Contains("current answer must be in English") ? "en" : "es" }) } }), Encoding.UTF8, "application/json")
        };
    }
}
sealed class LiveHandler : DelegatingHandler
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public LiveHandler() : base(new HttpClientHandler()) { }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var task = base.SendAsync(request, ct);
        Started.TrySetResult();
        return task;
    }
}
