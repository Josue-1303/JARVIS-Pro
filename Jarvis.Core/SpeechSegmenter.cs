using System.Buffers.Binary;
using NAudio.Wave;

namespace Jarvis.Core;

// PCM mono 16 kHz / 16 bit. Time is measured in samples, not callback timing.
public sealed class SpeechSegmenter : IDisposable
{
    private readonly VoiceInputSettings settings;
    private readonly bool waitForever;
    private readonly Queue<byte[]> preRoll = new();
    private readonly MemoryStream pcm = new();
    private int preRollBytes;
    private long totalSamples, phraseSamples, voicedSamples, silentSamples;
    public bool Started { get; private set; }
    public bool Finished { get; private set; }
    public bool HasSpeech => voicedSamples >= settings.MinimumSpeechMilliseconds * 16L;

    public SpeechSegmenter(VoiceInputSettings settings, bool waitForever)
    {
        settings.Validate(); this.settings = settings; this.waitForever = waitForever;
    }
    public void Push(ReadOnlySpan<byte> bytes)
    {
        if (Finished || bytes.Length == 0) return;
        if (bytes.Length % 2 != 0) throw new ArgumentException("PCM debe contener muestras completas de 16 bits.");
        long samples = bytes.Length / 2;
        double squares = 0;
        for (int i = 0; i < bytes.Length; i += 2)
        {
            double value = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(i, 2)) / 32768.0;
            squares += value * value;
        }
        bool voiced = Math.Sqrt(squares / samples) >= settings.SpeechThreshold;
        totalSamples += samples;
        if (!Started && !voiced)
        {
            preRoll.Enqueue(bytes.ToArray());
            preRollBytes += bytes.Length;
            // Keep half a second before the trigger, preserving quiet initial syllables.
            while (preRollBytes > 16000 && preRoll.Count > 0) preRollBytes -= preRoll.Dequeue().Length;
            Finished = !waitForever && totalSamples >= settings.CommandWaitSeconds * 16000L;
            return;
        }
        if (!Started)
        {
            Started = true;
            foreach (var buffer in preRoll) pcm.Write(buffer);
            preRoll.Clear();
        }
        pcm.Write(bytes);
        phraseSamples += samples;
        if (voiced) { voicedSamples += samples; silentSamples = 0; }
        else silentSamples += samples;
        Finished = silentSamples >= settings.SilenceMilliseconds * 16L || phraseSamples >= settings.MaximumPhraseSeconds * 16000L;
    }
    public byte[]? GetWave()
    {
        if (!HasSpeech) return null;
        using var stream = new MemoryStream();
        using (var writer = new WaveFileWriter(stream, new WaveFormat(16000, 16, 1))) writer.Write(pcm.ToArray());
        return stream.ToArray();
    }
    public void Dispose() => pcm.Dispose();
}
