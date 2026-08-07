using System;
using System.Globalization;

namespace Chip8.Replay;

public static class RomHash
{
    const ulong fnvOffsetBasis = 14695981039346656037UL;
    const ulong fnvPrime = 1099511628211UL;

    public static ulong compute(byte[] rom)
    {
        ulong hash = fnvOffsetBasis;
        foreach (byte value in rom)
        {
            hash ^= value;
            hash *= fnvPrime;
        }
        return hash;
    }

    public static string toHex(byte[] rom) => format(compute(rom));

    public static string format(ulong hash) => hash.ToString("X16", CultureInfo.InvariantCulture);

    public static ulong parse(string hex) =>
        ulong.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
