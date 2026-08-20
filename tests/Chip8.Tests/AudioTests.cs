using System;
using System.Linq;
using System.Text;
using Chip8.Core;
using Chip8.Core.Audio;
using Xunit;

namespace Chip8.Tests;

public class AudioTests
{
    [Fact]
    public void wavHeaderDescribesMonoSixteenBitPcm()
    {
        var samples = new short[] { 1000, -1000, 500, -500 };
        byte[] wav = WavFile.write(samples, 8000);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20));
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));
        Assert.Equal(8000, BitConverter.ToInt32(wav, 24));
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(samples.Length * 2, BitConverter.ToInt32(wav, 40));
        Assert.Equal(44 + samples.Length * 2, wav.Length);
        Assert.Equal(1000, BitConverter.ToInt16(wav, 44));
    }

    [Theory]
    [InlineData(0.4, 13106)]
    [InlineData(2.0, 32767)]
    [InlineData(-1.0, 0)]
    public void amplitudeIsClampedAndScaled(double amplitude, int expected)
    {
        Assert.Equal((short)expected, AudioSynth.amplitudeToPcm(amplitude));
    }

    [Fact]
    public void squareWaveHasHighThenLowHalfCycle()
    {
        short[] samples = AudioSynth.square(frequency: 1, sampleRate: 8, sampleCount: 8);
        Assert.Equal(8, samples.Length);
        Assert.All(samples[..4], sample => Assert.True(sample > 0));
        Assert.All(samples[4..], sample => Assert.True(sample < 0));
    }

    [Fact]
    public void decodePatternReadsMostSignificantBitFirst()
    {
        bool[] bits = AudioSynth.decodePattern(new byte[] { 0xAA });
        Assert.True(bits[0]);
        Assert.False(bits[1]);
        Assert.True(bits[2]);
        Assert.False(bits[7]);
        Assert.Equal(AudioSynth.patternBits, bits.Length);
    }

    [Fact]
    public void renderPatternFollowsBits()
    {
        var full = new byte[16];
        Array.Fill(full, (byte)0xFF);
        short[] loud = AudioSynth.renderPattern(full, playbackRate: 8, sampleRate: 8, sampleCount: 16);
        Assert.All(loud, sample => Assert.True(sample > 0));

        short[] quiet = AudioSynth.renderPattern(new byte[16], playbackRate: 8, sampleRate: 8, sampleCount: 16);
        Assert.All(quiet, sample => Assert.True(sample < 0));
    }

    [Fact]
    public void trackAccumulatesSamplesAndDuration()
    {
        var track = new AudioTrack(1000);
        track.appendSilence(500);
        track.appendSquare(200, 500);
        Assert.Equal(1000, track.sampleCount);
        Assert.Equal(1.0, track.durationSeconds, 3);

        short[] samples = track.toSamples();
        Assert.All(samples[..500], sample => Assert.Equal(0, sample));
        Assert.Contains(samples[500..], sample => sample != 0);
    }

    [Fact]
    public void trackProducesPlayableWav()
    {
        var track = new AudioTrack(8000);
        track.appendSquare(440, 8000);
        byte[] wav = track.toWav();
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(44 + 8000 * 2, wav.Length);
    }

    [Fact]
    public void recorderProducesSilenceForASilentProgram()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x1200);
        var track = new AudioRecorder(sampleRate: 8000).record(machine, frames: 5, instructionsPerFrame: 4);
        Assert.All(track.toSamples(), sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void recorderCapturesTheBeepRom()
    {
        var machine = new Machine(Quirks.modern(), 1);
        machine.loadRom(TestHarness.romBytes("7-beep.ch8"));
        var track = new AudioRecorder(sampleRate: 8000).record(machine, frames: 10, instructionsPerFrame: 11);
        Assert.Contains(track.toSamples(), sample => sample != 0);
    }
}
