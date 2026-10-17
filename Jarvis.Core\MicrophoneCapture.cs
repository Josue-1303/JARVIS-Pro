using NAudio.Wave;

namespace Jarvis.Core;

public static class MicrophoneCapture
{
    public static async Task<byte[]?> CaptureAsync(VoiceInputSettings settings, bool waitForever,
        CancellationToken cancellationToken, CancellationToken stopRecording = default,
        Func<IWaveIn>? createInput = null, CancellationToken stopWaiting = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        settings.Validate();
        if (createInput == null && WaveIn.DeviceCount <= settings.MicrophoneDevice)
            throw new InvalidOperationException("No se encuentra el micrófono seleccionado. Puedes usar T para escribir o V para reintentar la escucha.");
        using var microphone = createInput?.Invoke() ?? new WaveIn
        {
            DeviceNumber = settings.MicrophoneDevice,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 100
        };
        if (microphone.WaveFormat.SampleRate != 16000 || microphone.WaveFormat.Channels != 1 || microphone.WaveFormat.BitsPerSample != 16)
            throw new InvalidOperationException("La captura requiere PCM mono de 16 kHz / 16 bits.");
        using var segmenter = new SpeechSegmenter(settings, waitForever);
        var sync = new object();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? recordingError = null;
        microphone.DataAvailable += (_, e) =>
        {
            try
            {
                lock (sync)
                {
                    segmenter.Push(e.Buffer.AsSpan(0, e.BytesRecorded));
                    if (segmenter.Finished) finished.TrySetResult();
                }
            }
            catch (Exception ex) { recordingError = ex; finished.TrySetResult(); }
        };
        microphone.RecordingStopped += (_, e) =>
        {
            recordingError ??= e.Exception;
            stopped.TrySetResult();
            finished.TrySetResult();
        };
        using var cancel = cancellationToken.Register(() => finished.TrySetResult());
        using var stop = stopRecording.Register(() => finished.TrySetResult());
        using var idleDeadline = stopWaiting.Register(() =>
        {
            lock (sync) { if (!segmenter.Started) finished.TrySetResult(); }
        });
        bool started = false;
        try
        {
            // Avoid opening the device if a deadline/cancellation fired during initialization.
            cancellationToken.ThrowIfCancellationRequested();
            if (finished.Task.IsCompleted) return null;
            microphone.StartRecording();
            started = true;
            await finished.Task;
        }
        finally
        {
            if (started)
            {
                microphone.StopRecording();
                await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (recordingError != null) throw new IOException("Falló la captura del micrófono.", recordingError);
        return segmenter.GetWave();
    }
}
