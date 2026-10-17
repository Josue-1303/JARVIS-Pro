using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Jarvis.Core;

namespace Jarvis.Desktop;

public partial class MainWindow
{
    private async Task SpeakReplyIfEnabled(JarvisReply reply, CancellationToken cancellationToken,
        ChatMessageView assistantMessage)
    {
        if (SpeakCheckBox.IsChecked != true)
        {
            await RevealReplyQuicklyAsync(assistantMessage);
            return;
        }
        string language = ReplyLanguage.Detect(reply.Text) ??
            (reply.Language is "es" or "en" ? reply.Language : "es");
        SetCoreState(CoreMode.Speaking);
        SetStatus(language == "en" ? "speakingEnglish" : "speakingSpanish");

        try
        {
            bool stoppedByVoice = await SpokenInterruption.RunAsync(
                ct => voice.SpeakAsync(reply.Text, language, ct, stage =>
                {
                    string key = stage == SpeechStage.Synthesizing
                        ? "synthesizing"
                        : language == "en" ? "speakingEnglish" : "speakingSpanish";
                    _ = Dispatcher.BeginInvoke(() => SetStatus(key));
                }, (position, duration) => _ = Dispatcher.BeginInvoke(() =>
                    assistantMessage.RevealProgress(duration <= TimeSpan.Zero ? 1 : position.TotalSeconds / duration.TotalSeconds),
                    DispatcherPriority.Render)),
                voice.ListenForInterruptionAsync,
                () => Dispatcher.InvokeAsync(() => SetStatus("interruptRecognized")),
                ex => Dispatcher.InvokeAsync(() => SetStatus("interruptMicUnavailable", ex.Message)),
                cancellationToken);

            if (stoppedByVoice) SetStatus("speechStopped");
        }
        finally
        {
            // Keep the complete answer readable if playback is stopped or fails.
            assistantMessage.RevealProgress(1);
        }
    }

    private void SetCoreState(CoreMode mode)
    {
        string color = mode switch
        {
            CoreMode.Listening => "#46D9FF",
            CoreMode.Thinking => "#A778FF",
            CoreMode.Speaking => "#D9B96E",
            CoreMode.Error => "#EA5262",
            _ => "#D9A441"
        };
        var accent = (Color)ColorConverter.ConvertFromString(color)!;
        ParticleCore.AccentColor = accent;

        StatusDot.Fill = new SolidColorBrush(accent);

    }
}
