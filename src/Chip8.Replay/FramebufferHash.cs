using System.Globalization;
using Chip8.Core;

namespace Chip8.Replay;

public static class FramebufferHash
{
    const ulong fnvOffsetBasis = 14695981039346656037UL;
    const ulong fnvPrime = 1099511628211UL;

    public static ulong compute(Machine machine) => compute(FramebufferSnapshot.capture(machine));

    public static ulong compute(FramebufferSnapshot snapshot)
    {
        ulong hash = fnvOffsetBasis;
        hash = mix(hash, (byte)(snapshot.width & 0xFF));
        hash = mix(hash, (byte)((snapshot.width >> 8) & 0xFF));
        hash = mix(hash, (byte)(snapshot.height & 0xFF));
        hash = mix(hash, (byte)((snapshot.height >> 8) & 0xFF));
        foreach (byte color in snapshot.colors)
        {
            hash = mix(hash, color);
        }
        return hash;
    }

    public static ulong computeWithRegisters(Machine machine)
    {
        ulong hash = compute(machine);
        foreach (byte register in machine.v)
        {
            hash = mix(hash, register);
        }
        hash = mix16(hash, machine.i);
        hash = mix16(hash, machine.pc);
        hash = mix(hash, machine.sp);
        hash = mix(hash, machine.delayTimer);
        hash = mix(hash, machine.soundTimer);
        return hash;
    }

    public static string toHex(Machine machine) => format(compute(machine));

    public static string format(ulong hash) => hash.ToString("X16", CultureInfo.InvariantCulture);

    static ulong mix(ulong hash, byte value)
    {
        hash ^= value;
        hash *= fnvPrime;
        return hash;
    }

    static ulong mix16(ulong hash, ushort value)
    {
        hash = mix(hash, (byte)(value & 0xFF));
        hash = mix(hash, (byte)((value >> 8) & 0xFF));
        return hash;
    }
}
