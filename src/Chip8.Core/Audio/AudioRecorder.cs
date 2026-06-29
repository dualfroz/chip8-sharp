using System;

namespace Chip8.Core.Audio;

public sealed class AudioRecorder
{
    public const int defaultSampleRate = 44100;
    public const double defaultBuzzerFrequency = 440.0;
    public const double frameSeconds = 1.0 / 60.0;

    readonly int sampleRate;
    readonly double buzzerFrequency;

    public AudioRecorder(int sampleRate = defaultSampleRate, double buzzerFrequency = defaultBuzzerFrequency)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "sample rate must be positive");
        }
        this.sampleRate = sampleRate;
        this.buzzerFrequency = buzzerFrequency;
    }

    public AudioTrack record(Machine machine, int frames, int instructionsPerFrame)
    {
        if (frames < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frames), "frame count cannot be negative");
        }

        var track = new AudioTrack(sampleRate);
        int samplesPerFrame = track.samplesForSeconds(frameSeconds);

        for (int frame = 0; frame < frames && !machine.exited; frame++)
        {
            for (int cycle = 0; cycle < instructionsPerFrame; cycle++)
            {
                machine.step();
            }

            appendFrame(machine, track, samplesPerFrame);
            machine.tickTimers();
        }

        return track;
    }

    void appendFrame(Machine machine, AudioTrack track, int samplesPerFrame)
    {
        if (!machine.soundActive)
        {
            track.appendSilence(samplesPerFrame);
            return;
        }

        if (machine.platform == Platform.XoChip)
        {
            track.appendPattern(machine.audioPattern, machine.audioFrequency, samplesPerFrame);
        }
        else
        {
            track.appendSquare(buzzerFrequency, samplesPerFrame);
        }
    }
}
