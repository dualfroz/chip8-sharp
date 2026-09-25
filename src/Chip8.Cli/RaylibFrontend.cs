using System;
using System.IO;
using System.Numerics;
using Chip8.Core;
using Chip8.Replay;
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

    static readonly Color letterbox = new(15, 15, 20, 255);

    public static void run(Machine machine, Options options, InputMovie? recording = null, InputMovie? playback = null)
    {
        int instructionsPerFrame = playback?.instructionsPerFrame ?? options.instructionsPerFrame;
        Palette palette = options.buildPalette();
        (int width, int height) = FrameLayout.windowSize(machine.platform, options.scale);

        Raylib.SetConfigFlags(ConfigFlags.FLAG_WINDOW_RESIZABLE);
        Raylib.InitWindow(width, height, "chip8-sharp");
        Raylib.SetWindowMinSize(Video.loWidth, Video.loHeight);
        Raylib.SetTargetFPS(60);
        Raylib.InitAudioDevice();

        AudioStream beep = Raylib.LoadAudioStream(44100, 16, 1);
        Raylib.PlayAudioStream(beep);
        var phase = new BeepPhase();
        Texture2D? screen = null;

        int frame = 0;
        while (!Raylib.WindowShouldClose())
        {
            if (playback is not null)
            {
                playback.keysAt(frame).applyTo(machine.keypad);
            }
            else
            {
                pumpInput(machine);
            }
            recording?.recordFrame(frame, KeyMask.fromKeypad(machine.keypad));
            machine.runFrame(instructionsPerFrame);
            feedAudio(beep, machine.soundActive, phase);
            screen = draw(screen, FrameImage.fromMachine(machine, palette));

            frame++;
            if (options.maxFrames > 0 && frame >= options.maxFrames)
            {
                saveScreenshot(options.screenshotPath);
                break;
            }
        }

        if (screen is not null)
        {
            Raylib.UnloadTexture(screen.Value);
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

    static Texture2D draw(Texture2D? screen, FrameImage image)
    {
        Texture2D texture = textureFor(screen, image.width, image.height);
        Raylib.UpdateTexture(texture, image.rgba);

        FrameLayout layout = FrameLayout.fit(image.width, image.height, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        var source = new Rectangle(0, 0, image.width, image.height);
        var target = new Rectangle(layout.x, layout.y, layout.width, layout.height);

        Raylib.BeginDrawing();
        Raylib.ClearBackground(letterbox);
        Raylib.DrawTexturePro(texture, source, target, Vector2.Zero, 0, Color.WHITE);
        Raylib.EndDrawing();

        return texture;
    }

    static Texture2D textureFor(Texture2D? screen, int width, int height)
    {
        if (screen is { } current && current.width == width && current.height == height)
        {
            return current;
        }
        if (screen is not null)
        {
            Raylib.UnloadTexture(screen.Value);
        }

        Image blank = Raylib.GenImageColor(width, height, Color.BLACK);
        Texture2D texture = Raylib.LoadTextureFromImage(blank);
        Raylib.UnloadImage(blank);
        Raylib.SetTextureFilter(texture, TextureFilter.TEXTURE_FILTER_POINT);
        return texture;
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
