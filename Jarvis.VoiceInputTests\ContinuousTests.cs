using Jarvis.Core;
using NAudio.Wave;
using System.Buffers.Binary;

static class ContinuousTests
{
    public static async Task RunAsync()
    {
        int count = 0;
        void Check(bool condition, string text) { if (!condition) throw new Exception("FAIL: " + text); count++; Console.WriteLine("PASS: " + text); }
        var state = new ConversationInput(new VoiceInputSettings());
        int wakeCalls = 0, followCalls = 0;
        Task<string> Wake(CancellationToken ct) { wakeCalls++; return Task.FromResult("salúdame"); }
        Task<string> Follow(CancellationToken ct) { followCalls++; return Task.FromResult("bien, gracias"); }
        await state.WaitAsync(Wake, Follow);
        Check(wakeCalls == 1 && followCalls == 0, "First input requires wake");
        state.ResponseFinished();
        var answer = await state.WaitAsync(Wake, Follow);
        Check(answer.Text == "bien, gracias" && wakeCalls == 1 && followCalls == 1, "Follow-up accepts answer without Jarvis");
        state.ResponseFinished();
        await state.WaitAsync(Wake, Follow);
        Check(followCalls == 2, "Conversation continues across several completed replies");
        state.ResponseFinished();
        await state.WaitAsync(Wake, _ => Task.FromResult(""));
        await state.WaitAsync(Wake, Follow);
        Check(wakeCalls == 2, "No follow-up returns to wake mode");
        state.ResponseFinished(); state.Pause();
        Check(!state.WakeEnabled && !state.FollowUpPending, "Pause clears continuous listening");
        state.Toggle(); await state.WaitAsync(Wake, Follow);
        Check(wakeCalls == 3, "Resuming listening starts with Jarvis again");
        state.ResponseFinished();
        bool released = false;
        var typed = await state.WaitAsync(Wake, async ct => { try { await Task.Delay(Timeout.Infinite, ct); return ""; } finally { released = true; } }, () => ConsoleKey.T);
        Check(typed.Key == ConsoleKey.T && released && !state.FollowUpPending, "Keyboard takes over and clears follow-up");
        var disabled = new ConversationInput(new VoiceInputSettings { ContinuousConversation = false });
        disabled.ResponseFinished();
        Check(!disabled.FollowUpPending, "Continuous mode can be disabled");
        string text = await FollowUpListener.WaitAsync((ct, stop) => Task.FromResult("Estoy bien"), TimeSpan.FromSeconds(12), CancellationToken.None);
        Check(text == "Estoy bien", "Follow-up content preserved");
        text = await FollowUpListener.WaitAsync((ct, stop) => Task.FromResult("Jarvis, estoy bien"), TimeSpan.FromSeconds(12), CancellationToken.None);
        Check(text == "estoy bien", "Optional repeated wake word stripped");
        text = await FollowUpListener.WaitAsync(async (ct, stop) =>
        {
            try { await Task.Delay(Timeout.Infinite, stop); } catch (OperationCanceledException) { }
            return "";
        }, TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Check(text == "", "Idle follow-up deadline returns no message");
        var timedOut = await IdleVoiceInput.WaitAsync(_ => Task.FromResult(""), () => throw new Exception("No key should be read"));
        Check(timedOut.Key == null && timedOut.Text == null, "Silent follow-up timeout returns to wake mode without a blank input");

        var frames = new byte[3200];
        for (int i = 0; i < frames.Length; i += 2) BinaryPrimitives.WriteInt16LittleEndian(frames.AsSpan(i, 2), 220);
        using (var vad = new SpeechSegmenter(new VoiceInputSettings(), false))
        {
            vad.Push(frames); vad.Push(frames);
            Check(vad.HasSpeech, "Soft speech now crosses the more sensitive threshold");
        }
        using (var vad = new SpeechSegmenter(new VoiceInputSettings { SpeechThreshold = 0.008 }, false))
        {
            vad.Push(frames); vad.Push(frames);
            Check(!vad.HasSpeech, "Comparison confirms sensitivity changed");
        }

        var briefBurst = new byte[3200];
        for (int i = 0; i < 160 * 2; i += 2) BinaryPrimitives.WriteInt16LittleEndian(briefBurst.AsSpan(i, 2), 2500);
        using (var vad = new SpeechSegmenter(new VoiceInputSettings(), false))
        {
            vad.Push(briefBurst); vad.Push(briefBurst);
            Check(!vad.HasSpeech, "Short bursts inside a callback are measured by actual voiced duration");
        }
        var halfVoiced = new byte[3200];
        for (int i = 0; i < 1600 * 2; i += 2) BinaryPrimitives.WriteInt16LittleEndian(halfVoiced.AsSpan(i, 2), 2500);
        using (var vad = new SpeechSegmenter(new VoiceInputSettings(), false))
        {
            vad.Push(halfVoiced); vad.Push(halfVoiced);
            Check(vad.HasSpeech, "Sustained speech is retained when callbacks also contain silence");
        }

        var mic = new DrivenMicrophone();
        using (var deadline = new CancellationTokenSource())
        {
            var recording = MicrophoneCapture.CaptureAsync(new VoiceInputSettings(), true, CancellationToken.None,
                createInput: () => mic, stopWaiting: deadline.Token);
            mic.Push(frames); mic.Push(frames);
            deadline.Cancel();
            await Task.Delay(20);
            Check(!recording.IsCompleted, "Follow-up deadline does not cut speech already started");
            for (int i = 0; i < 10; i++) mic.Push(new byte[3200]);
            Check(await recording.WaitAsync(TimeSpan.FromSeconds(2)) != null && mic.Disposed, "Phrase finishes on silence after follow-up deadline");
        }
        mic = new DrivenMicrophone();
        using (var deadline = new CancellationTokenSource())
        {
            var recording = MicrophoneCapture.CaptureAsync(new VoiceInputSettings(), true, CancellationToken.None,
                createInput: () => mic, stopWaiting: deadline.Token);
            deadline.Cancel();
            Check(await recording.WaitAsync(TimeSpan.FromSeconds(2)) == null && mic.Disposed, "No speech releases microphone at follow-up deadline");
        }
        Console.WriteLine($"{count} continuous-conversation checks passed.");
    }
}

sealed class DrivenMicrophone : IWaveIn
{
    public bool Disposed { get; private set; }
    public WaveFormat WaveFormat { get; set; } = new(16000, 16, 1);
    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;
    public void StartRecording() { }
    public void Push(byte[] data) => DataAvailable?.Invoke(this, new WaveInEventArgs(data, data.Length));
    public void StopRecording() => RecordingStopped?.Invoke(this, new StoppedEventArgs());
    public void Dispose() => Disposed = true;
}
