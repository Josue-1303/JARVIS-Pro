using System.Text.Json;

namespace Jarvis.Core;

public sealed class VoiceInputSettings
{
    public bool WakeEnabled { get; set; } = true;
    public double SpeechThreshold { get; set; } = 0.006;
    public bool ContinuousConversation { get; set; } = true;
    public int FollowUpWaitSeconds { get; set; } = 12;
    public string WhisperModel { get; set; } = "small";
    public string RecognitionLanguage { get; set; } = "auto";
    public int SilenceMilliseconds { get; set; } = 1000;
    public int MinimumSpeechMilliseconds { get; set; } = 200;
    public int MaximumPhraseSeconds { get; set; } = 20;
    public int CommandWaitSeconds { get; set; } = 12;
    public int MicrophoneDevice { get; set; } = 0;

    public void Validate()
    {
        if (!double.IsFinite(SpeechThreshold) || SpeechThreshold <= 0 || SpeechThreshold > 0.5 ||
            SilenceMilliseconds < 300 || SilenceMilliseconds > 5000 ||
            MinimumSpeechMilliseconds < 100 || MinimumSpeechMilliseconds > 1000 ||
            MaximumPhraseSeconds < 5 || MaximumPhraseSeconds > 60 ||
            CommandWaitSeconds < 2 || CommandWaitSeconds > 60 || MicrophoneDevice < 0 ||
            FollowUpWaitSeconds < 2 || FollowUpWaitSeconds > 60 || WhisperModel is not ("base" or "small") ||
            RecognitionLanguage is not ("auto" or "es" or "en"))
            throw new InvalidDataException("Configuración de voz inválida. Revisa voice-input-settings.json.");
    }
    public static VoiceInputSettings Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "voice-input-settings.json");
        var settings = File.Exists(path)
            ? JsonSerializer.Deserialize<VoiceInputSettings>(File.ReadAllText(path)) ?? throw new InvalidDataException("Configuración de voz vacía.")
            : new VoiceInputSettings();
        settings.Validate();
        return settings;
    }
}
