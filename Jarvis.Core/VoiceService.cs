using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace Jarvis.Core;

public sealed class VoiceService : IDisposable
{
    private readonly PiperSpeechService speech = new();
    private readonly string modelPath;
    private readonly SemaphoreSlim transcriptionGate = new(1, 1);
    private WhisperFactory? factory;
    public VoiceInputSettings Settings { get; }

    public VoiceService(VoiceInputSettings? settings = null)
    {
        Settings = settings ?? VoiceInputSettings.Load();
        Settings.Validate();
        string root = Directory.GetParent(MemoryStore.FindDataPath())!.FullName;
        modelPath = Path.Combine(root, "models", $"ggml-{Settings.WhisperModel}.bin");
    }

    public async Task<string> ListenAsync(CancellationToken cancellationToken = default, CancellationToken stopRecording = default)
    {
        await EnsureModelAsync(cancellationToken);
        Console.WriteLine("Te escucho. Terminaré tras el silencio. [Espacio] terminar / [Esc] cancelar.");
        var wave = await MicrophoneCapture.CaptureAsync(Settings, false, cancellationToken, stopRecording);
        if (wave == null) return "";
        Console.WriteLine("Transcribiendo...");
        return await TranscribeAsync(wave, cancellationToken);
    }

    public async Task<string> WaitForWakeAsync(CancellationToken cancellationToken)
    {
        await EnsureModelAsync(cancellationToken);
        return await WakeListener.WaitAsync(async (waitForever, ct) =>
        {
            var wave = await MicrophoneCapture.CaptureAsync(Settings, waitForever, ct);
            return wave == null ? "" : await TranscribeAsync(wave, ct);
        }, cancellationToken, Console.WriteLine);
    }

    public Task<string> WaitForFollowUpAsync(CancellationToken cancellationToken) =>
        FollowUpListener.WaitAsync(async (ct, stopWaiting) =>
        {
            // This token ends idle capture gracefully. Once speech starts it may finish
            // after the follow-up window; the normal silence/length limits still apply.
            var wave = await MicrophoneCapture.CaptureAsync(Settings, true, ct, stopWaiting: stopWaiting);
            if (wave == null) return "";
            Console.WriteLine("Transcribiendo...");
            return await TranscribeAsync(wave, ct);
        }, TimeSpan.FromSeconds(Settings.FollowUpWaitSeconds), cancellationToken);

    public async Task<string> TranscribeAsync(byte[] wave, CancellationToken cancellationToken = default)
    {
        await transcriptionGate.WaitAsync(cancellationToken);
        try
        {
            await EnsureModelAsync(cancellationToken);
            // Native initialization and inference stay off the console input loop.
            return await Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                factory ??= WhisperFactory.FromPath(modelPath);
                cancellationToken.ThrowIfCancellationRequested();
                var builder = factory.CreateBuilder().WithLanguage("es").WithNoContext()
                    .WithTemperature(0).WithBeamSearchSamplingStrategy(strategy => strategy.WithBeamSize(5));
                builder.WithPrompt("Jarvis. Conversación: saludo, fecha, día, hora, hoy, mañana, tareas, universidad, trabajo. Conversation: greeting, date, day, time, today, tomorrow, tasks, university, work.");
                await using var processor = builder.Build();
                // Give the decoder a short quiet lead-in, including recordings that start immediately with speech.
                using var source = new NAudio.Wave.WaveFileReader(new MemoryStream(wave, writable: false));
                using var padded = new MemoryStream();
                using (var writer = new NAudio.Wave.WaveFileWriter(padded, source.WaveFormat))
                {
                    writer.Write(new byte[source.WaveFormat.AverageBytesPerSecond / 2]);
                    source.CopyTo(writer);
                }
                using var stream = new MemoryStream(padded.ToArray(), writable: false);
                // This version supports Spanish and English. Restrict acoustic language selection
                // instead of allowing a short Spanish question to be decoded as Italian.
                string language = Settings.RecognitionLanguage;
                if (language == "auto")
                {
                    using var reader = new NAudio.Wave.WaveFileReader(new MemoryStream(wave, writable: false));
                    var bytes = new byte[checked((int)reader.Length)];
                    int total = 0, read;
                    while (total < bytes.Length && (read = reader.Read(bytes, total, bytes.Length - total)) > 0) total += read;
                    if (reader.WaveFormat.SampleRate != 16000 || reader.WaveFormat.Channels != 1 || reader.WaveFormat.BitsPerSample != 16)
                        throw new InvalidDataException("Whisper requiere audio PCM mono de 16 kHz y 16 bits.");
                    var samples = new float[total / 2];
                    for (int i = 0; i < samples.Length; i++) samples[i] = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * 2, 2)) / 32768f;
                    language = processor.DetectLanguageWithProbability(samples, new[] { "es", "en" }).language ?? "es";
                    cancellationToken.ThrowIfCancellationRequested();
                }
                processor.ChangeLanguage(language);
                var text = new StringBuilder();
                await foreach (var segment in processor.ProcessAsync(stream, cancellationToken)) text.Append(segment.Text);
                cancellationToken.ThrowIfCancellationRequested();
                return text.ToString().Trim();
            }, cancellationToken);
        }
        finally { transcriptionGate.Release(); }
    }

    public async Task PrepareAsync(CancellationToken cancellationToken = default) => await EnsureModelAsync(cancellationToken);

    private async Task EnsureModelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(modelPath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        string temp = modelPath + "." + Guid.NewGuid().ToString("N") + ".part";
        Console.WriteLine($"Preparando modelo local de Whisper {Settings.WhisperModel}...");
        try
        {
            var model = Settings.WhisperModel == "small" ? GgmlType.Small : GgmlType.Base;
            using var source = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(model, cancellationToken: cancellationToken);
            using (var target = File.Create(temp)) await source.CopyToAsync(target, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temp, modelPath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public Task SpeakAsync(string text, string language = "es", CancellationToken cancellationToken = default,
        Action<SpeechStage>? progress = null) => speech.SpeakAsync(text, language, cancellationToken, progress);

    public void Dispose() { factory?.Dispose(); transcriptionGate.Dispose(); }
}
