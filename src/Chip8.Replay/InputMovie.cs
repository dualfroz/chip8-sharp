using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Chip8.Core;

namespace Chip8.Replay;

public sealed class InputMovie
{
    public const string textMagic = "CHIP8MOVIE";
    public const int textVersion = 1;
    static readonly byte[] binaryMagic = Encoding.ASCII.GetBytes("C8MV");
    const byte binaryVersion = 1;

    public Platform platform { get; set; } = Platform.Chip8;
    public Quirks quirks { get; set; } = Quirks.modern();
    public int? randomSeed { get; set; }
    public int instructionsPerFrame { get; set; } = 11;
    public int length { get; private set; }
    public ulong romHash { get; set; }
    public string romName { get; set; } = "-";

    readonly SortedDictionary<int, KeyMask> frames = new SortedDictionary<int, KeyMask>();

    public int entryCount => frames.Count;

    public IReadOnlyCollection<int> recordedIndices => frames.Keys;

    public void setFrame(int index, KeyMask keys)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (keys.isEmpty)
        {
            frames.Remove(index);
        }
        else
        {
            frames[index] = keys;
        }

        if (index + 1 > length)
        {
            length = index + 1;
        }
    }

    public void recordFrame(int index, KeyMask keys)
    {
        setFrame(index, keys);
        if (index + 1 > length)
        {
            length = index + 1;
        }
    }

    public void extendLength(int frameCount)
    {
        if (frameCount > length)
        {
            length = frameCount;
        }
    }

    public KeyMask keysAt(int index) =>
        frames.TryGetValue(index, out KeyMask keys) ? keys : KeyMask.empty;

    public IEnumerable<MovieFrame> entries()
    {
        foreach (var pair in frames)
        {
            yield return new MovieFrame(pair.Key, pair.Value);
        }
    }

    public void setRom(byte[] rom, string name)
    {
        romHash = RomHash.compute(rom);
        romName = string.IsNullOrEmpty(name) ? "-" : name;
    }

    public bool matchesRom(byte[] rom) => RomHash.compute(rom) == romHash;

    public string toText()
    {
        var builder = new StringBuilder();
        builder.Append(textMagic).Append(' ').Append(textVersion).Append('\n');
        builder.Append("platform ").Append(platform).Append('\n');
        builder.Append("quirks ").Append(QuirksCodec.toTokens(quirks)).Append('\n');
        builder.Append("seed ").Append(randomSeed.HasValue
            ? randomSeed.Value.ToString(CultureInfo.InvariantCulture)
            : "none").Append('\n');
        builder.Append("ipf ").Append(instructionsPerFrame.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("length ").Append(length.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("rom ").Append(romName).Append(' ').Append(RomHash.format(romHash)).Append('\n');

        foreach (var pair in frames)
        {
            builder.Append("frame ")
                .Append(pair.Key.ToString(CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(pair.Value.toKeyList())
                .Append('\n');
        }

        return builder.ToString();
    }

    public static InputMovie parseText(string text)
    {
        var movie = new InputMovie();
        bool headerSeen = false;
        int explicitLength = -1;

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (!headerSeen)
            {
                string[] header = line.Split(' ');
                if (header.Length < 2 || header[0] != textMagic)
                {
                    throw new FormatException("not a chip8 movie (missing '" + textMagic + "' header)");
                }
                if (!int.TryParse(header[1], out int version) || version != textVersion)
                {
                    throw new FormatException("unsupported movie version '" + header[1] + "'");
                }
                headerSeen = true;
                continue;
            }

            int separator = line.IndexOf(' ');
            string keyword = separator < 0 ? line : line.Substring(0, separator);
            string value = separator < 0 ? string.Empty : line.Substring(separator + 1).Trim();

            switch (keyword)
            {
                case "platform":
                    movie.platform = Enum.Parse<Platform>(value, ignoreCase: true);
                    break;
                case "quirks":
                    movie.quirks = QuirksCodec.fromTokens(value);
                    break;
                case "seed":
                    movie.randomSeed = value == "none"
                        ? (int?)null
                        : int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "ipf":
                    movie.instructionsPerFrame = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "length":
                    explicitLength = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "rom":
                    parseRomLine(movie, value);
                    break;
                case "frame":
                    parseFrameLine(movie, value);
                    break;
                default:
                    throw new FormatException("unknown movie directive '" + keyword + "'");
            }
        }

        if (!headerSeen)
        {
            throw new FormatException("empty movie text");
        }

        if (explicitLength > movie.length)
        {
            movie.length = explicitLength;
        }

        return movie;
    }

    static void parseRomLine(InputMovie movie, string value)
    {
        int split = value.LastIndexOf(' ');
        if (split < 0)
        {
            movie.romName = value;
            movie.romHash = 0;
            return;
        }

        movie.romName = value.Substring(0, split).Trim();
        movie.romHash = RomHash.parse(value.Substring(split + 1).Trim());
    }

    static void parseFrameLine(InputMovie movie, string value)
    {
        int split = value.IndexOf(' ');
        string indexPart = split < 0 ? value : value.Substring(0, split);
        string keyPart = split < 0 ? "-" : value.Substring(split + 1).Trim();
        int index = int.Parse(indexPart, CultureInfo.InvariantCulture);
        movie.setFrame(index, KeyMask.parseKeyList(keyPart));
    }

    public byte[] toBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write(binaryMagic);
        writer.Write(binaryVersion);
        writer.Write((byte)platform);
        writer.Write(QuirksCodec.toByte(quirks));
        writer.Write(randomSeed.HasValue);
        writer.Write(randomSeed ?? 0);
        writer.Write(instructionsPerFrame);
        writer.Write(length);
        writer.Write(romHash);
        writer.Write(romName ?? "-");
        writer.Write(frames.Count);
        foreach (var pair in frames)
        {
            writer.Write(pair.Key);
            writer.Write(pair.Value.bits);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static InputMovie parseBytes(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream, Encoding.ASCII);

        byte[] magic = reader.ReadBytes(binaryMagic.Length);
        for (int index = 0; index < binaryMagic.Length; index++)
        {
            if (magic.Length <= index || magic[index] != binaryMagic[index])
            {
                throw new InvalidDataException("not a chip8 movie (bad magic)");
            }
        }

        byte version = reader.ReadByte();
        if (version != binaryVersion)
        {
            throw new InvalidDataException("unsupported movie binary version " + version);
        }

        var movie = new InputMovie
        {
            platform = (Platform)reader.ReadByte(),
            quirks = QuirksCodec.fromByte(reader.ReadByte()),
        };

        bool hasSeed = reader.ReadBoolean();
        int seed = reader.ReadInt32();
        movie.randomSeed = hasSeed ? seed : (int?)null;
        movie.instructionsPerFrame = reader.ReadInt32();
        int storedLength = reader.ReadInt32();
        movie.romHash = reader.ReadUInt64();
        movie.romName = reader.ReadString();

        int entries = reader.ReadInt32();
        for (int entry = 0; entry < entries; entry++)
        {
            int frameIndex = reader.ReadInt32();
            ushort keyBits = reader.ReadUInt16();
            movie.setFrame(frameIndex, new KeyMask(keyBits));
        }

        if (storedLength > movie.length)
        {
            movie.length = storedLength;
        }

        return movie;
    }

    public InputMovie clone()
    {
        var copy = new InputMovie
        {
            platform = platform,
            quirks = QuirksCodec.clone(quirks),
            randomSeed = randomSeed,
            instructionsPerFrame = instructionsPerFrame,
            romHash = romHash,
            romName = romName,
        };
        foreach (var pair in frames)
        {
            copy.frames[pair.Key] = pair.Value;
        }
        copy.length = length;
        return copy;
    }

    public bool sameContent(InputMovie other)
    {
        if (platform != other.platform
            || !QuirksCodec.sameFlags(quirks, other.quirks)
            || randomSeed != other.randomSeed
            || instructionsPerFrame != other.instructionsPerFrame
            || length != other.length
            || romHash != other.romHash
            || romName != other.romName
            || frames.Count != other.frames.Count)
        {
            return false;
        }

        foreach (var pair in frames)
        {
            if (!other.frames.TryGetValue(pair.Key, out KeyMask keys) || keys != pair.Value)
            {
                return false;
            }
        }

        return true;
    }
}
