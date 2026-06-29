using System;

namespace Chip8.Core.Audio;

public static class AudioSynth
{
    public const int patternBits = 128;

    public static short amplitudeToPcm(double amplitude)
    {
        double clamped = amplitude < 0 ? 0 : amplitude > 1 ? 1 : amplitude;
        return (short)(clamped * short.MaxValue);
    }

    public static bool[] decodePattern(byte[] pattern)
    {
        var bits = new bool[patternBits];
        for (int index = 0; index < patternBits; index++)
        {
            int sourceByte = index / 8;
            int bit = 7 - (index % 8);
            byte value = sourceByte < pattern.Length ? pattern[sourceByte] : (byte)0;
            bits[index] = (value & (1 << bit)) != 0;
        }
        return bits;
    }

    public static short[] square(double frequency, int sampleRate, int sampleCount, double amplitude = 0.4, double duty = 0.5)
    {
        var samples = new short[sampleCount];
        short high = amplitudeToPcm(amplitude);
        short low = (short)-high;
        double step = frequency / sampleRate;
        double phase = 0.0;

        for (int index = 0; index < sampleCount; index++)
        {
            samples[index] = (phase % 1.0) < duty ? high : low;
            phase += step;
        }

        return samples;
    }

    public static short[] renderPattern(byte[] pattern, double playbackRate, int sampleRate, int sampleCount, double amplitude = 0.4)
    {
        bool[] bits = decodePattern(pattern);
        var samples = new short[sampleCount];
        short high = amplitudeToPcm(amplitude);
        short low = (short)-high;
        double step = playbackRate / sampleRate;
        double position = 0.0;

        for (int index = 0; index < sampleCount; index++)
        {
            int bit = ((int)position) % patternBits;
            samples[index] = bits[bit] ? high : low;
            position += step;
        }

        return samples;
    }
}
