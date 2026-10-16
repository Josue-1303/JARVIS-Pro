using NAudio.Wave;
using System.Reflection;
using System.Text.Json;

namespace Jarvis.Core;

internal static class Diagnostics
{
    public static async Task RunAsync()
    {
        Console.WriteLine($"JARVIS {Assembly.GetExecutingAssembly().GetName().Version} | .NET {Environment.Version}");
        Console.WriteLine("Ejecutable: " + AppContext.BaseDirectory);
        Console.WriteLine($"Reloj local: {DateTimeOffset.Now:dddd yyyy-MM-dd HH:mm:ss zzz} ({TimeZoneInfo.Local.Id})");
        string root = Directory.GetParent(MemoryStore.FindDataPath())!.FullName;
        var settings = VoiceInputSettings.Load();
        Console.WriteLine($"Micrófonos: {WaveIn.DeviceCount}; seleccionado: {settings.MicrophoneDevice}. Salidas: {WaveOut.DeviceCount}.");
        if (WaveIn.DeviceCount <= settings.MicrophoneDevice) throw new IOException("No está disponible el micrófono configurado.");
        if (WaveOut.DeviceCount == 0) throw new IOException("No hay salida de audio.");
        foreach (string file in new[] { ".venv-piper/Scripts/python.exe", $"models/ggml-{settings.WhisperModel}.bin", "voices/es_MX-claude-high.onnx", "voices/en_GB-alan-medium.onnx" })
            if (!File.Exists(Path.Combine(root, file))) throw new FileNotFoundException("Falta " + file);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var response = await client.GetAsync("http://localhost:11434/api/tags");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!document.RootElement.GetProperty("models").EnumerateArray().Any(m => m.GetProperty("name").GetString() == "qwen3:8b"))
            throw new IOException("Ollama no tiene qwen3:8b disponible.");
        Console.WriteLine("PASS: configuración, dispositivos, modelos locales y conexión con Ollama.");
    }
}
