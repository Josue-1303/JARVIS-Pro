using System.Buffers.Binary;
using NAudio.Wave;

namespace Jarvis.Core;

// PCM mono 16 kHz / 16 bit. Time is measured in samples, not callback timing.
public sealed class SpeechSegmenter : IDisposable
{
    private const int AnalysisFrameSamples = 160; // 10 ms at 16 kHz.
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
        totalSamples += samples;
        bool foundSpeech = false;
        bool speechSeen = Started;
        for (int offset = 0; offset < bytes.Length;)
        {
            int frameSamples = Math.Min(AnalysisFrameSamples, (bytes.Length - offset) / 2);
            int frameBytes = frameSamples * 2;
            if (frameSamples == 0) break;
            double squares = 0;
            for (int i = offset; i < offset + frameBytes; i += 2)
            {
                double value = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(i, 2)) / 32768.0;
                squares += value * value;
            }
            bool voiced = Math.Sqrt(squares / frameSamples) >= settings.SpeechThreshold;
            if (voiced)
            {
                foundSpeech = true;
                speechSeen = true;
                voicedSamples += frameSamples;
                silentSamples = 0;
            }
            else if (speechSeen)
            {
                silentSamples += frameSamples;
            }
            offset += frameBytes;
        }

        if (!Started && foundSpeech)
        {
            Started = true;
            foreach (var buffer in preRoll) pcm.Write(buffer);
            preRoll.Clear();
            preRollBytes = 0;
        }
        else if (!Started)
        {
            preRoll.Enqueue(bytes.ToArray());
            preRollBytes += bytes.Length;
            // Keep half a second before the trigger, preserving quiet initial syllables.
            while (preRollBytes > 16000 && preRoll.Count > 0) preRollBytes -= preRoll.Dequeue().Length;
        }
        if (Started)
        {
            pcm.Write(bytes);
            phraseSamples += samples;
        }
        else
        {
            Finished = !waitForever && totalSamples >= settings.CommandWaitSeconds * 16000L;
            return;
        }
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
