using System;
using System.IO;
using Chip8.Core;
using Raylib_cs;

namespace Chip8.Cli;

static class RaylibFrontend
{
    static readonly (int chip8Key, KeyboardKey physical)[] keyMap =
    {
        (0x1, KeyboardKey.KEY_ONE), (0x2, KeyboardKey.KEY_TWO), (0x3, KeyboardKey.KEY_THREE), (0xC, KeyboardKey.KEY_FOUR),
        (0x4, KeyboardKey.KEY_Q), (0x5, KeyboardKey.KEY_W), (0x6, KeyboardKey.KEY_E), (0xD, KeyboardKey.KEY_R),
        (0x7, KeyboardKey.KEY_A), (0x8, KeyboardKey.KEY_S), (0x9, KeyboardKey.KEY_D), (0xE, KeyboardKey.KEY_F),
        (0xA, KeyboardKey.KEY_Z), (0x0, KeyboardKey.KEY_X), (0xB, KeyboardKey.KEY_C), (0xF, KeyboardKey.KEY_V),
    };

    static readonly Color background = new(15, 15, 20, 255);
    static readonly Color foreground = new(180, 240, 200, 255);

    public static void run(Machine machine, Options options)
    {
        int width = Machine.displayWidth * options.scale;
        int height = Machine.displayHeight * options.scale;

        Raylib.InitWindow(width, height, "chip8-sharp");
        Raylib.SetTargetFPS(60);
        Raylib.InitAudioDevice();

        AudioStream beep = Raylib.LoadAudioStream(44100, 16, 1);
        Raylib.PlayAudioStream(beep);
        var phase = new BeepPhase();

        int frame = 0;
        while (!Raylib.WindowShouldClose())
        {
            pumpInput(machine);
            machine.runFrame(options.instructionsPerFrame);
            feedAudio(beep, machine.soundActive, phase);
            draw(machine, options.scale);

            frame++;
            if (options.maxFrames > 0 && frame >= options.maxFrames)
            {
                saveScreenshot(options.screenshotPath);
                break;
            }
        }

        Raylib.UnloadAudioStream(beep);
        Raylib.CloseAudioDevice();
        Raylib.CloseWindow();
    }

    static void saveScreenshot(string? path)
    {
        if (path is null)
        {
            return;
        }

        string fileName = Path.GetFileName(path);
        Raylib.TakeScreenshot(fileName);

        string produced = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        string target = Path.GetFullPath(path);
        if (produced != target)
        {
            File.Move(produced, target, overwrite: true);
        }
    }

    static void pumpInput(Machine machine)
    {
        foreach ((int chip8Key, KeyboardKey physical) in keyMap)
        {
            if (Raylib.IsKeyDown(physical))
            {
                machine.pressKey(chip8Key);
            }
            else
            {
                machine.releaseKey(chip8Key);
            }
        }
    }

    static void draw(Machine machine, int scale)
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(background);

        for (int y = 0; y < Machine.displayHeight; y++)
        {
            for (int x = 0; x < Machine.displayWidth; x++)
            {
                if (machine.display[y * Machine.displayWidth + x])
                {
                    Raylib.DrawRectangle(x * scale, y * scale, scale, scale, foreground);
                }
            }
        }

        Raylib.EndDrawing();
    }

    static void feedAudio(AudioStream beep, bool active, BeepPhase phase)
    {
        const int sampleRate = 44100;
        const int toneHz = 440;
        const short amplitude = 4000;
        int samplesPerHalfPeriod = sampleRate / (toneHz * 2);

        while (Raylib.IsAudioStreamProcessed(beep))
        {
            var buffer = new short[512];
            for (int n = 0; n < buffer.Length; n++)
            {
                if (!active)
                {
                    buffer[n] = 0;
                    continue;
                }

                buffer[n] = phase.high ? amplitude : (short)-amplitude;
                phase.counter++;
                if (phase.counter >= samplesPerHalfPeriod)
                {
                    phase.counter = 0;
                    phase.high = !phase.high;
                }
            }

            unsafe
            {
                fixed (short* samples = buffer)
                {
                    Raylib.UpdateAudioStream(beep, samples, buffer.Length);
                }
            }
        }
    }

    sealed class BeepPhase
    {
        public int counter;
        public bool high;
    }
}
