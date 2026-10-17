using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NAudio.Wave;

namespace Jarvis.Core;

public enum SpeechStage { Synthesizing, Speaking }

public sealed class PiperSpeechService
{
    private readonly PiperSettings settings;
    private readonly string root;

    public PiperSpeechService(string? configurationPath = null)
    {
        string path = configurationPath ?? Path.Combine(AppContext.BaseDirectory, "piper-settings.json");
        settings = JsonSerializer.Deserialize<PiperSettings>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("La configuración de Piper está vacía.");
        if (settings.SynthesisTimeoutSeconds <= 0 || settings.SynthesisTimeoutSeconds > 600)
            throw new InvalidOperationException("El tiempo de espera de Piper debe estar entre 1 y 600 segundos.");
        // Resolve from the executable, never from the terminal's working directory.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".venv-piper")))
            directory = directory.Parent;
        root = directory?.FullName ?? throw new DirectoryNotFoundException(
            "No se encontró .venv-piper. Coloca JARVIS dentro de la carpeta JARVIS-Pro.");
    }

    public async Task SpeakAsync(string text, string language = "es", CancellationToken cancellationToken = default,
        Action<SpeechStage>? progress = null, Action<TimeSpan, TimeSpan>? playbackProgress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text)) return;
        string model = language switch
        {
            "es" => settings.SpanishModel,
            "en" => settings.EnglishModel,
            _ => throw new ArgumentException("Idioma de voz no admitido. Usa es o en.", nameof(language))
        };
        string python = Path.GetFullPath(Path.Combine(root, settings.PythonPath));
        model = Path.GetFullPath(Path.Combine(root, model));
        foreach (string file in new[] { python, model, model + ".json" })
            if (!File.Exists(file)) throw new FileNotFoundException("Falta un archivo de Piper: " + file, file);

        string wav = Path.Combine(Path.GetTempPath(), "jarvis-piper-" + Guid.NewGuid() + ".wav");
        try
        {
            var start = new ProcessStartInfo(python)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = root
            };
            start.Environment["PYTHONUTF8"] = "1";
            foreach (string argument in new[] { "-m", "piper", "--model", model, "--output-file", wav })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("No se pudo iniciar Piper.");
            Task<string> errors = process.StandardError.ReadToEndAsync();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.SynthesisTimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
            try
            {
                progress?.Invoke(SpeechStage.Synthesizing);
                await process.StandardInput.WriteLineAsync(text.AsMemory(), linked.Token);
                process.StandardInput.Close();
                await process.WaitForExitAsync(linked.Token);
                await Task.WhenAll(errors, output);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Piper no pudo generar audio: " + await errors);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
            {
                throw new TimeoutException("Piper superó el tiempo de espera para generar la voz.");
            }
            finally
            {
                if (!process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                    await process.WaitForExitAsync();
                }
                await Task.WhenAll(errors, output);
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var reader = new WaveFileReader(wav);
            if (reader.Length == 0) throw new InvalidDataException("Piper generó un audio vacío.");
            using var player = new WaveOut();
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            player.PlaybackStopped += (_, e) =>
            {
                if (e.Exception != null) completed.TrySetException(e.Exception);
                else completed.TrySetResult(true);
            };
            player.Init(reader);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                player.Play();
                progress?.Invoke(SpeechStage.Speaking);
                var playbackTimeout = Task.Delay(reader.TotalTime + TimeSpan.FromSeconds(15), cancellationToken);
                while (!completed.Task.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    playbackProgress?.Invoke(reader.CurrentTime, reader.TotalTime);
                    var tick = Task.Delay(TimeSpan.FromMilliseconds(33), cancellationToken);
                    var finished = await Task.WhenAny(completed.Task, tick, playbackTimeout);
                    if (finished == playbackTimeout)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw new TimeoutException("La reproducción de Piper superó el tiempo esperado.");
                    }
                    if (finished == tick) await tick;
                }
                await completed.Task;
                playbackProgress?.Invoke(reader.TotalTime, reader.TotalTime);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally { player.Stop(); }
            Console.WriteLine($"Piper [{language}]: {reader.TotalTime.TotalSeconds:F1} s, {reader.WaveFormat.SampleRate} Hz.");
        }
        finally
        {
            if (File.Exists(wav)) File.Delete(wav);
        }
    }

    private sealed class PiperSettings
    {
        public string PythonPath { get; set; } = ".venv-piper/Scripts/python.exe";
        public string SpanishModel { get; set; } = "voices/es_MX-claude-high.onnx";
        public string EnglishModel { get; set; } = "voices/en_GB-alan-medium.onnx";
        public int SynthesisTimeoutSeconds { get; set; } = 120;
    }
}
