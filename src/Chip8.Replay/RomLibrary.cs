using System;
using System.Collections.Generic;
using System.IO;

namespace Chip8.Replay;

public sealed class RomLibrary
{
    readonly string romDirectory;
    readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>(StringComparer.Ordinal);

    public RomLibrary(string romDirectory)
    {
        this.romDirectory = romDirectory;
    }

    public string directory => romDirectory;

    public static RomLibrary locate()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            foreach (string candidate in new[] { "roms", Path.Combine("tests", "roms") })
            {
                string path = Path.Combine(dir.FullName, candidate);
                if (Directory.Exists(path))
                {
                    return new RomLibrary(path);
                }
            }
        }

        throw new DirectoryNotFoundException("could not locate a 'roms' or 'tests/roms' directory above " + AppContext.BaseDirectory);
    }

    public bool has(string name) => File.Exists(pathOf(name));

    public string pathOf(string name) => Path.Combine(romDirectory, name);

    public byte[] load(string name)
    {
        if (cache.TryGetValue(name, out byte[]? cached))
        {
            return cached;
        }

        string path = pathOf(name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("rom '" + name + "' not found under " + romDirectory, path);
        }

        byte[] bytes = File.ReadAllBytes(path);
        cache[name] = bytes;
        return bytes;
    }

    public IReadOnlyList<string> available()
    {
        var names = new List<string>();
        if (!Directory.Exists(romDirectory))
        {
            return names;
        }

        foreach (string path in Directory.GetFiles(romDirectory, "*.ch8"))
        {
            names.Add(Path.GetFileName(path));
        }
        names.Sort(StringComparer.Ordinal);
        return names;
    }
}
