using System;
using System.IO;
using System.Text;

namespace Chip8.Core.Audio;

public static class WavFile
{
    public const int bitsPerSample = 16;
    public const int channels = 1;

    public static byte[] write(short[] samples, int sampleRate)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "sample rate must be positive");
        }

        int dataBytes = samples.Length * sizeof(short);
        int blockAlign = channels * (bitsPerSample / 8);
        int byteRate = sampleRate * blockAlign;

        using var stream = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        foreach (short sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static void writeFile(string path, short[] samples, int sampleRate) =>
        File.WriteAllBytes(path, write(samples, sampleRate));
}
