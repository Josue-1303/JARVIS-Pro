using System.Buffers.Binary;
using Jarvis.Core;
using NAudio.Wave;

try
{
    if (args.Contains("--prepare"))
    {
        using var voice = new VoiceService();
        await voice.PrepareAsync();
        Console.WriteLine("Whisper preparado.");
    }
    else
    {
        await ContinuousTests.RunAsync();
        await VoiceTests.RunAsync(args);
    }
}
catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }

static class VoiceTests
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        checks++; Console.WriteLine("PASS: " + label);
    }
    private static byte[] Frame(short amplitude = 0)
    {
        var data = new byte[3200];
        for (int i = 0; i < data.Length; i += 2) BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i, 2), amplitude);
        return data;
    }
    public static async Task RunAsync(string[] args)
    {
        var settings = new VoiceInputSettings();
        using (var vad = new SpeechSegmenter(settings, false))
        {
            for (int i = 0; i < 120; i++) vad.Push(Frame());
            Check(vad.Finished && !vad.HasSpeech && vad.GetWave() == null, "Manual silence times out without transcription");
        }
        using (var vad = new SpeechSegmenter(settings, true))
        {
            for (int i = 0; i < 1000; i++) vad.Push(Frame(5));
            Check(!vad.Finished && !vad.Started && vad.GetWave() == null, "Wake mode ignores quiet input without an audio file");
            for (int i = 0; i < 4; i++) vad.Push(Frame(1000));
            for (int i = 0; i < 9; i++) vad.Push(Frame());
            Check(!vad.Finished, "Short pause does not end phrase");
            vad.Push(Frame());
            Check(vad.Finished && vad.HasSpeech, "One second of silence ends phrase");
            using var reader = new WaveFileReader(new MemoryStream(vad.GetWave()!));
            Check(reader.WaveFormat.SampleRate == 16000 && reader.WaveFormat.Channels == 1 && reader.TotalTime.TotalSeconds < 2,
                "Bounded pre-roll and correct Whisper WAV format");
        }
        using (var vad = new SpeechSegmenter(settings, false))
        {
            vad.Push(Frame(1000));
            for (int i = 0; i < 10; i++) vad.Push(Frame());
            Check(vad.Finished && !vad.HasSpeech && vad.GetWave() == null, "Short click is rejected");
        }
        using (var vad = new SpeechSegmenter(settings, true))
        {
            for (int i = 0; i < 200; i++) vad.Push(Frame(1000));
            Check(vad.Finished && vad.GetWave()!.Length < 650000, "Continuous noise/speech is bounded to twenty seconds");
        }
        foreach (string phrase in new[] { "Jarvis, dime la hora.", "¡Hola, Jarvis! Dime la hora.", "HEY JARVIS, tell me the time.", "Járvis, hola." })
            Check(WakePhrase.TryExtract(phrase, out string command) && command.Length > 0, "Wake prefix: " + phrase);
        Check(WakePhrase.TryExtract("Jarvis.", out string empty) && empty == "", "Wake-only phrase recognized");
        Check(WakePhrase.TryExtract("\"Carvis\", dime la hora", out string alias) && alias == "dime la hora", "Observed Spanish Whisper spelling accepted at phrase start");
        Check(WakePhrase.TryExtract("Carbise.", out string shortAlias) && shortAlias.Length == 0, "Observed isolated wake-word spelling accepted");
        foreach (string phrase in new[] { "", "Dime la hora", "Me gusta Jarvis", "Jarvision", "un jarvis al final" })
            Check(!WakePhrase.TryExtract(phrase, out _), "Non-wake phrase ignored: " + phrase);

        Check(SpokenInterruption.IsStopCommand("Jarvis, detente."), "Spanish wake-prefixed stop command recognized");
        Check(SpokenInterruption.IsStopCommand("Hey Jarvis, stop talking!"), "English wake-prefixed stop command recognized");
        Check(SpokenInterruption.IsStopCommand("Hola, Carvis, detén la voz."), "Wake alias and accents normalized for stop command");
        foreach (string phrase in new[] { "detente", "Jarvis, dime la hora", "Jarvis, para cuándo empieza" })
            Check(!SpokenInterruption.IsStopCommand(phrase), "Non-interruption phrase ignored: " + phrase);

        var interruptionPhrases = new Queue<string>(new[] { "detente", "Jarvis, dime la hora", "Hola Jarvis, detén la respuesta." });
        int interruptionSignals = 0;
        bool speechCancelledByVoice = false;
        bool interruptedByVoice = await SpokenInterruption.RunAsync(async ct =>
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { speechCancelledByVoice = true; throw; }
        }, _ => Task.FromResult(interruptionPhrases.Dequeue()),
        () => interruptionSignals++, cancellationToken: CancellationToken.None);

        Check(interruptedByVoice && speechCancelledByVoice && interruptionSignals == 1 && interruptionPhrases.Count == 0,
            "Explicit spoken command cancels speech once after ignoring non-command audio");

        bool listenerReleasedAfterSpeech = false;
        bool naturalFinishInterrupted = await SpokenInterruption.RunAsync(_ => Task.CompletedTask, async ct =>
        {
            try { await Task.Delay(Timeout.Infinite, ct); return ""; }
            finally { listenerReleasedAfterSpeech = true; }
        });
        Check(!naturalFinishInterrupted && listenerReleasedAfterSpeech, "Speech completion cancels and releases the interruption listener");
        var words = new Queue<string>(new[] { "ruido", "Jarvis, hola" });
        string result = await WakeListener.WaitAsync((forever, ct) => Task.FromResult(words.Dequeue()), CancellationToken.None);
        Check(result == "hola" && words.Count == 0, "No question returned before wake prefix");
        words = new Queue<string>(new[] { "Jarvis", "mi pregunta" });
        var waits = new List<bool>();
        result = await WakeListener.WaitAsync((forever, ct) => { waits.Add(forever); return Task.FromResult(words.Dequeue()); }, CancellationToken.None);
        Check(result == "mi pregunta" && waits.SequenceEqual(new[] { true, false }), "Wake-only starts bounded command capture");
        words = new Queue<string>(new[] { "Jarvis", "", "ignored", "Jarvis, nueva pregunta" });
        result = await WakeListener.WaitAsync((forever, ct) => Task.FromResult(words.Dequeue()), CancellationToken.None);
        Check(result == "nueva pregunta", "No command after wake returns to waiting");

        var fake = new FakeMicrophone(new[] { Frame(1000), Frame(1000) }.Concat(Enumerable.Range(0, 10).Select(_ => Frame())).ToArray());
        byte[]? wave = await MicrophoneCapture.CaptureAsync(settings, true, CancellationToken.None, createInput: () => fake);
        Check(wave != null && fake.Stopped && fake.Disposed, "Silence stops and disposes capture");
        fake = new FakeMicrophone(Array.Empty<byte[]>());
        using (var cancel = new CancellationTokenSource())
        {
            var capture = MicrophoneCapture.CaptureAsync(settings, true, cancel.Token, createInput: () => fake);
            cancel.Cancel();
            bool cancelled = false;
            try { await capture.WaitAsync(TimeSpan.FromSeconds(2)); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && fake.Stopped && fake.Disposed, "Cancellation releases microphone before returning");
        }
        fake = new FakeMicrophone(new[] { Frame(1000), Frame(1000), Frame(1000) });
        using (var stop = new CancellationTokenSource())
        {
            var capture = MicrophoneCapture.CaptureAsync(settings, false, CancellationToken.None, stop.Token, () => fake);
            stop.Cancel();
            Check(await capture != null && fake.Disposed, "Manual stop preserves recorded phrase");
        }
        bool released = false;
        var idle = await IdleVoiceInput.WaitAsync(async ct =>
        {
            try { await Task.Delay(Timeout.Infinite, ct); return ""; } finally { released = true; }
        }, () => ConsoleKey.T);
        Check(idle.Key == ConsoleKey.T && idle.Text == null && released, "Typing key cancels listening and waits for cleanup");
        idle = await IdleVoiceInput.WaitAsync(_ => Task.FromResult("pregunta"), () => throw new Exception("No input should be read"));
        Check(idle.Key == null && idle.Text == "pregunta", "Wake question returns without a key");
        idle = await IdleVoiceInput.WaitAsync(async ct => { await Task.Delay(Timeout.Infinite, ct); return ""; }, () => ConsoleKey.V);
        Check(idle.Key == ConsoleKey.V, "V requests pause after stopping listener");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel(); bool rejected = false;
            try { await WakeListener.WaitAsync((_, _) => throw new Exception("Must not capture"), cancelled.Token); }
            catch (OperationCanceledException) { rejected = true; }
            Check(rejected, "Cancelled wake listener never starts capture");
        }
        bool invalid = false;
        try { new VoiceInputSettings { SpeechThreshold = double.NaN }.Validate(); } catch (InvalidDataException) { invalid = true; }
        Check(invalid, "Invalid threshold rejected");
        if (args.Length == 2 && args[0] == "--fixtures") await TestFixturesAsync(args[1]);
        Console.WriteLine($"{checks} voice input checks passed.");
    }

    private static async Task TestFixturesAsync(string directory)
    {
        using var voice = new VoiceService();
        var failures = new List<string>();
        foreach (var (file, shouldWake) in new[] { ("wake-es.wav", true), ("wake-en.wav", true), ("ordinary-es.wav", false), ("wake-only-es.wav", true), ("ordinary-en.wav", false), ("greeting-es.wav", true) })
        {
            try
            {
            using var reader = new WaveFileReader(Path.Combine(directory, file));
            using var resampler = new MediaFoundationResampler(reader, new WaveFormat(16000, 16, 1));
            using var vad = new SpeechSegmenter(voice.Settings, false);
            var frame = new byte[3200];
            int read;
            while ((read = resampler.Read(frame)) > 0 && !vad.Finished) vad.Push(frame.AsSpan(0, read));
            for (int i = 0; i < 15 && !vad.Finished; i++) vad.Push(Frame());
            Check(vad.HasSpeech && vad.Finished, "Fixture ends on silence: " + file);
            string transcript = await voice.TranscribeAsync(vad.GetWave()!);
            Console.WriteLine(file + ": " + transcript);
            Check(voice.LastTranscriptionConfidence is >= 0 and <= 1, "Whisper confidence is available: " + file);
            Console.WriteLine($"confidence [{file}]: {voice.LastTranscriptionConfidence:P1}");
            Check(WakePhrase.TryExtract(transcript, out string command) == shouldWake, "Whisper wake decision: " + file);
            if (shouldWake) Check(file == "wake-only-es.wav" ? command.Length == 0 : !string.IsNullOrWhiteSpace(command), "Wake-only or question extracted correctly: " + file);
            if (file == "greeting-es.wav")
            {
                string normalized = transcript.ToLowerInvariant().Replace("ú", "u").Replace("ñ", "n");
                Check(normalized.Contains("saludame") && normalized.Contains("espanol"), "Recognizes salúdame en español in controlled audio");
            }
            if (file == "wake-es.wav")
            {
                using var cancel = new CancellationTokenSource();
                cancel.CancelAfter(20);
                bool cancelled = false;
                try { await voice.TranscribeAsync(vad.GetWave()!, cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled, "Whisper transcription can be cancelled before keyboard takeover");
            }
            }
            catch (Exception ex) { failures.Add(file + ": " + ex.Message); Console.WriteLine(ex.Message); }
        }
        if (failures.Count > 0) throw new Exception(string.Join(Environment.NewLine, failures));
    }
}

sealed class FakeMicrophone : IWaveIn
{
    private readonly byte[][] frames;
    public bool Stopped { get; private set; }
    public bool Disposed { get; private set; }
    public FakeMicrophone(byte[][] frames) { this.frames = frames; }
    public WaveFormat WaveFormat { get; set; } = new(16000, 16, 1);
    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;
    public void StartRecording() { foreach (byte[] frame in frames) DataAvailable?.Invoke(this, new WaveInEventArgs(frame, frame.Length)); }
    public void StopRecording() { Stopped = true; RecordingStopped?.Invoke(this, new StoppedEventArgs()); }
    public void Dispose() { Disposed = true; }
}
