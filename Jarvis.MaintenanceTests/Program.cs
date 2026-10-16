using Jarvis.Core;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Globalization;

try { await Checks.RunAsync(args); }
catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }

static class Checks
{
    static int count;
    static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
    public static async Task RunAsync(string[] args)
    {
        var friday = new DateTimeOffset(2026, 10, 16, 23, 59, 0, TimeSpan.FromHours(3));
        foreach (var q in new[] { "¿Qué día es hoy?", "Dije que día es hoy.", "Te pregunté qué día es hoy", "Jarvis, dime qué día es hoy", "¿En qué día estamos?", "¿Qué día de la semana es hoy?", "¿Cuál es la fecha de hoy?" })
            Check(LocalClock.Answer(LocalClock.Parse(q) ?? throw new Exception("Unrecognized: " + q), friday).Text.Contains("viernes"), q);
        foreach (var q in new[] { "What day is today?", "What day of the week is it today?", "And what's the date?", "I said what day is today", "Can you tell me what day is today?" })
            Check(LocalClock.Answer(LocalClock.Parse(q) ?? throw new Exception("Unrecognized: " + q), friday).Text.Contains("Friday"), q);
        foreach (var q in new[] { "¿Qué hora es en Tokio?", "What day is Christmas?", "¿Qué día nací?", "Dime qué día fue el 16 de octubre de 2025", "Translate what day is today into French", "What time is it and explain relativity" })
            Check(LocalClock.Parse(q) == null, "No false clock match: " + q);
        Check(LocalClock.Answer(LocalClock.Parse("What day is tomorrow?")!, friday).Text.Contains("Saturday"), "Tomorrow rollover");
        Check(LocalClock.Answer(LocalClock.Parse("¿Qué día fue ayer?")!, friday).Text.Contains("jueves"), "Yesterday");
        Check(LocalClock.Answer(LocalClock.Parse("¿Qué día es hoy?")!, friday.AddMinutes(2)).Text.Contains("sábado"), "Midnight refresh");
        Check(LocalClock.Parse("Are you sure?") == null, "Unrelated recheck does not use clock");
        Check(LocalClock.Parse("¿Qué diga eso hoy?") == null && LocalClock.Suggest("¿Qué diga eso hoy?")?.Kind == "date", "Uncertain short speech needs confirmation, never silent correction");
        Check(LocalClock.Suggest("What day is Christmas?") == null && LocalClock.Suggest("¿Qué hora es en Tokio?") == null, "Suggestions do not reinterpret other dates/places");
        Check(JarvisAI.ParseReply("{\"text\":\"Today is Saturday.\",\"language\":\"es\"}").Language == "en", "Repair actual mislabeled English response");
        Check(JarvisAI.ParseReply("{\"text\":\"Hoy es viernes.\",\"language\":\"en\"}").Language == "es", "Repair mislabeled Spanish response");
        var voices = new VoiceSelection();
        voices.HandleKey(ConsoleKey.D1);
        Check(voices.Resolve(new JarvisReply("Today is Saturday.", "es")) == "en", "Manual selection cannot mispronounce clear English");
        voices.HandleKey(ConsoleKey.D2);
        Check(voices.Resolve(new JarvisReply("Hoy es viernes.", "en")) == "es", "Manual selection cannot mispronounce clear Spanish");
        voices.HandleKey(ConsoleKey.A);
        Check(voices.Resolve(new JarvisReply("42", "unknown")) == "es", "Ambiguous answer defaults to Spanish");
        Check(ReplyLanguage.Requested("Now reply only in Spanish with a short greeting") == "es", "Explicit language overrides question language");
        Check(ReplyLanguage.Requested("Explain Spanish history") == null, "Mention is not a language command");

        string data = Path.Combine(Path.GetTempPath(), "jarvis-maintenance-" + Guid.NewGuid().ToString("N"));
        using var store = new MemoryStore(data);
        var session = new ConversationSession(store, store.CreateConversation());
        session.Append("What day is today?", new("Today is Saturday.", "en"));
        session.Append("And what's the date?", new("The date is October 16, 2026.", "en"));
        session.Append("Then how come it's Saturday, it's supposed to be Friday?", new("It is indeed Saturday.", "en"));
        session.Append("But my calendars as Friday.", new("Check your calendar settings.", "en"));
        session.Append("Che di esso è?", new("unknown", "unknown"));
        session.Append("Dije que día es hoy.", new("Today is Saturday.", "es"));
        using var handler = new Fake();
        using var client = new HttpClient(handler);
        var ai = new JarvisAI(client, session, () => friday);
        var reply = await ai.AskReplyAsync("Dije que día es hoy.");
        Check(reply.Language == "es" && reply.Text.Contains("viernes") && handler.Calls == 0, "Exact user failure bypasses wrong history and model");
        Check(voices.Resolve(reply) == "es", "Correct Spanish response and Piper route");
        reply = await ai.AskReplyAsync("Are you sure?");
        Check(reply.Language == "en" && reply.Text.Contains("Friday"), "Recheck changes language correctly");
        reply = await ai.AskReplyAsync("Check again");
        Check(reply.Text.Contains("Friday"), "Repeated verification retains clock context");
        reply = await ai.AskReplyAsync("What day is today?", preferredLanguage: "es");
        Check(reply.Language == "es" && reply.Text.Contains("viernes"), "Manual Spanish selects answer language too");
        Check(store.Load(session.Current.Id).Turns.Last().Answer == reply.Text, "Clock replies persist");
        int before = session.Current.Turns.Count;
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { await ai.AskReplyAsync("What day is today?", cancelled.Token); throw new Exception("Missing cancellation"); }
            catch (OperationCanceledException) { }
        }
        Check(session.Current.Turns.Count == before, "Cancelled clock does not save");
        handler.Responses.Enqueue("{\"text\":\"El agua contiene hidrógeno y oxígeno.\",\"language\":\"es\"}");
        await ai.AskReplyAsync("Explica el agua");
        Check(!handler.LastRequest.Contains("Today is Saturday"), "Stale clock answers removed only from model context");
        Check(!handler.LastRequest.Contains("Che di esso"), "Unusable historical answer excluded from model context");
        Check(store.Load(session.Current.Id).Turns.Any(t => t.Answer == "Today is Saturday."), "Original saved history preserved");
        handler.Responses.Enqueue("{\"text\":\"Today is Friday.\",\"language\":\"es\"}");
        handler.Responses.Enqueue("{\"text\":\"La respuesta está en español.\",\"language\":\"es\"}");
        reply = await ai.AskReplyAsync("Explica el agua", preferredLanguage: "es");
        Check(reply.Language == "es" && !reply.Text.Contains("Today"), "Wrong language repaired before saving/speaking");
        before = session.Current.Turns.Count;
        handler.Responses.Enqueue("{\"text\":\"Today is Friday.\",\"language\":\"es\"}");
        handler.Responses.Enqueue("{\"text\":\"Today is Friday.\",\"language\":\"es\"}");
        try { await ai.AskReplyAsync("Explica el agua", preferredLanguage: "es"); throw new Exception("Expected mismatch rejection"); }
        catch (InvalidDataException) { }
        Check(session.Current.Turns.Count == before, "Unrepaired wrong-language reply is not saved");
        foreach (var bad in new[] { "bad json", "{\"text\":\"unknown\",\"language\":\"unknown\"}", "{\"text\":\"  \"}" })
        {
            handler.Responses.Enqueue(bad);
            try { await ai.AskReplyAsync("Pregunta"); throw new Exception("Expected invalid response rejection"); } catch (InvalidDataException) { }
            Check(session.Current.Turns.Count == before, "Invalid reply not saved: " + bad);
        }
        handler.Failure = new HttpRequestException("Ollama unavailable");
        try { await ai.AskReplyAsync("Pregunta"); throw new Exception("Expected unavailable failure"); } catch (HttpRequestException) { }
        Check(session.Current.Turns.Count == before, "Unavailable Ollama leaves history intact");
        reply = await ai.AskReplyAsync("¿Qué día es hoy?");
        Check(reply.Text.Contains("viernes"), "Local clock works when Ollama is unavailable");
        handler.Failure = null;
        reply = await ai.AskReplyAsync("¿Qué diga eso hoy?");
        Check(reply.Text.StartsWith("¿Querías") && !reply.Text.Contains("viernes"), "Uncertain clock phrase asks before answering");
        reply = await ai.AskReplyAsync("no");
        Check(!reply.Text.Contains("viernes"), "Rejected interpretation does not answer the clock");
        await ai.AskReplyAsync("¿Qué diga eso hoy?");
        reply = await ai.AskReplyAsync("sí");
        Check(reply.Text.Contains("viernes"), "Confirmed interpretation uses fresh clock");
        var resumed = new JarvisAI(client, session, () => friday.AddMinutes(2));
        reply = await resumed.AskReplyAsync("Compruébalo");
        Check(reply.Text.Contains("sábado"), "Confirmed clock context survives restart and refreshes across midnight");
        Check(WakePhrase.TryExtract("Jaarvis.", out string wakeOnly) && wakeOnly.Length == 0 && !WakePhrase.TryExtract("Jaarvision", out _), "Observed doubled wake vowel accepted only as a complete word");

        string missingVoiceConfig = Path.Combine(data, "missing-voice.json");
        File.WriteAllText(missingVoiceConfig, "{\"SpanishModel\":\"voices/missing-maintenance-test.onnx\"}");
        try { await new PiperSpeechService(missingVoiceConfig).SpeakAsync("Prueba"); throw new Exception("Expected missing voice error"); }
        catch (FileNotFoundException) { }
        Check(true, "Missing Piper model produces a controlled failure before playback");
        foreach (bool failOnStart in new[] { true, false })
        {
            var device = new FailedMicrophone(failOnStart);
            try { await MicrophoneCapture.CaptureAsync(new VoiceInputSettings(), true, CancellationToken.None, createInput: () => device); throw new Exception("Expected capture failure"); }
            catch (IOException) { }
            Check(device.Disposed, "Failed microphone releases resources: " + failOnStart);
        }

        int audioIndex = Array.IndexOf(args, "--audio");
        if (audioIndex >= 0)
        {
            using var voice = new VoiceService(args.Contains("--base") ? new VoiceInputSettings { WhisperModel = "base" } : null);
            var audioFailures = new List<string>();
            foreach (var (file, language) in new[] { ("day-es.wav", "es"), ("correction-es.wav", "es"), ("day-en.wav", "en") })
            {
                try
                {
                using var reader = new NAudio.Wave.WaveFileReader(Path.Combine(args[audioIndex + 1], file));
                using var resampler = new NAudio.Wave.MediaFoundationResampler(reader, new NAudio.Wave.WaveFormat(16000, 16, 1));
                using var segmenter = new SpeechSegmenter(voice.Settings, false);
                var frame = new byte[3200];
                int read;
                while ((read = resampler.Read(frame)) > 0 && !segmenter.Finished) segmenter.Push(frame.AsSpan(0, read));
                for (int i = 0; i < 15 && !segmenter.Finished; i++) segmenter.Push(new byte[3200]);
                string transcript = await voice.TranscribeAsync(segmenter.GetWave()!);
                Console.WriteLine($"AUDIO [{file}]: {transcript}");
                Check(ReplyLanguage.Detect(transcript) == language, "Acoustic language selection: " + file);
                var answer = await ai.AskReplyAsync(transcript);
                if (file == "day-es.wav" && LocalClock.Parse(transcript) == null)
                {
                    // Keep the observed ASR limitation visible: passing here requires an explicit
                    // clarification, never changing the recorded phrase or silently assuming intent.
                    Check(answer.Text.StartsWith("¿Querías") && !answer.Text.Contains("viernes"), "Ambiguous short audio asks for confirmation");
                    Console.WriteLine("KNOWN ASR LIMITATION: very short sample requires confirmation. " + answer.Text);
                    answer = await ai.AskReplyAsync("sí");
                }
                Check(answer.Language == language && answer.Text.Contains(language == "es" ? "viernes" : "Friday"), "Audio -> correct clock answer: " + file);
                string selected = voices.Resolve(answer);
                Check(selected == language, "Text -> matching Piper voice: " + file);
                await voice.SpeakAsync(answer.Text, selected);
                Check(true, "Piper playback completes: " + file);
                }
                catch (Exception ex) { audioFailures.Add(file + ": " + ex.Message); Console.WriteLine("FAIL AUDIO: " + file + ": " + ex.Message); }
            }
            if (audioFailures.Count > 0) throw new Exception(string.Join(Environment.NewLine, audioFailures));
        }

        if (args.Contains("--live"))
        {
            using var liveClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            var live = new JarvisAI(liveClient, session, () => friday);
            foreach (var (question, language) in new[] { ("Tell me in one sentence what water is.", "en"), ("Ahora responde solamente en español con un saludo breve.", "es"), ("Now reply only in English with a short greeting.", "en") })
            {
                var answer = await live.AskReplyAsync(question);
                Console.WriteLine("LIVE: " + answer);
                Check(answer.Language == language && voices.Resolve(answer) == language, "Live answer/voice alignment: " + language);
            }
        }
        Console.WriteLine($"{count} maintenance checks passed. Isolated data: {data}");
    }
}

sealed class FailedMicrophone(bool failOnStart) : NAudio.Wave.IWaveIn
{
    public bool Disposed;
    public NAudio.Wave.WaveFormat WaveFormat { get; set; } = new(16000, 16, 1);
    public event EventHandler<NAudio.Wave.WaveInEventArgs>? DataAvailable { add { } remove { } }
    public event EventHandler<NAudio.Wave.StoppedEventArgs>? RecordingStopped;
    public void StartRecording()
    {
        if (failOnStart) throw new IOException("Microphone unavailable");
        RecordingStopped?.Invoke(this, new NAudio.Wave.StoppedEventArgs(new IOException("Device disconnected")));
    }
    public void StopRecording() => RecordingStopped?.Invoke(this, new NAudio.Wave.StoppedEventArgs());
    public void Dispose() => Disposed = true;
}

sealed class Fake : HttpMessageHandler
{
    public Queue<string> Responses { get; } = new();
    public int Calls;
    public string LastRequest = "";
    public Exception? Failure;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        LastRequest = await request.Content!.ReadAsStringAsync(ct);
        if (Failure != null) throw Failure;
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { message = new { content = Responses.Dequeue() } }), Encoding.UTF8, "application/json") };
    }
}
