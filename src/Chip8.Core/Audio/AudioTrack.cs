using System;
using System.Collections.Generic;

namespace Chip8.Core.Audio;

public sealed class AudioTrack
{
    readonly List<short> samples = new();
    readonly int sampleRate;

    double squarePhase;
    double patternPosition;

    public AudioTrack(int sampleRate)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "sample rate must be positive");
        }
        this.sampleRate = sampleRate;
    }

    public int sampleCount => samples.Count;

    public double durationSeconds => (double)samples.Count / sampleRate;

    public int samplesForSeconds(double seconds) => (int)Math.Round(seconds * sampleRate);

    public void appendSilence(int count)
    {
        for (int index = 0; index < count; index++)
        {
            samples.Add(0);
        }
    }

    public void appendSquare(double frequency, int count, double amplitude = 0.4, double duty = 0.5)
    {
        short high = AudioSynth.amplitudeToPcm(amplitude);
        short low = (short)-high;
        double step = frequency / sampleRate;

        for (int index = 0; index < count; index++)
        {
            samples.Add((squarePhase % 1.0) < duty ? high : low);
            squarePhase += step;
        }
    }

    public void appendPattern(byte[] pattern, double playbackRate, int count, double amplitude = 0.4)
    {
        bool[] bits = AudioSynth.decodePattern(pattern);
        short high = AudioSynth.amplitudeToPcm(amplitude);
        short low = (short)-high;
        double step = playbackRate / sampleRate;

        for (int index = 0; index < count; index++)
        {
            int bit = ((int)patternPosition) % AudioSynth.patternBits;
            samples.Add(bits[bit] ? high : low);
            patternPosition += step;
        }
    }

    public short[] toSamples() => samples.ToArray();

    public byte[] toWav() => WavFile.write(samples.ToArray(), sampleRate);
}
