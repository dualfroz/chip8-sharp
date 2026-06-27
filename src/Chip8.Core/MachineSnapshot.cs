using System;
using System.Globalization;
using System.Text;

namespace Chip8.Core;

public sealed class MachineSnapshot
{
    public ushort pc { get; init; }
    public ushort i { get; init; }
    public byte sp { get; init; }
    public byte delayTimer { get; init; }
    public byte soundTimer { get; init; }
    public int planeMask { get; init; }
    public bool hires { get; init; }
    public bool exited { get; init; }
    public byte[] v { get; init; } = Array.Empty<byte>();
    public ushort[] stack { get; init; } = Array.Empty<ushort>();

    public static MachineSnapshot capture(Machine machine)
    {
        var stackFrames = new ushort[machine.sp];
        Array.Copy(machine.stack, stackFrames, machine.sp);
        return new MachineSnapshot
        {
            pc = machine.pc,
            i = machine.i,
            sp = machine.sp,
            delayTimer = machine.delayTimer,
            soundTimer = machine.soundTimer,
            planeMask = machine.planeMask,
            hires = machine.hires,
            exited = machine.exited,
            v = (byte[])machine.v.Clone(),
            stack = stackFrames,
        };
    }

    public string format()
    {
        var builder = new StringBuilder();
        builder.Append("PC=0x").Append(pc.ToString("X4", CultureInfo.InvariantCulture));
        builder.Append(" I=0x").Append(i.ToString("X4", CultureInfo.InvariantCulture));
        builder.Append(" SP=").Append(sp);
        builder.Append(" DT=").Append(delayTimer);
        builder.Append(" ST=").Append(soundTimer);
        builder.Append(" plane=").Append(planeMask);
        builder.Append(" hires=").Append(hires ? "1" : "0");
        builder.Append('\n');

        for (int register = 0; register < v.Length; register++)
        {
            builder.Append('V').Append(register.ToString("X1", CultureInfo.InvariantCulture));
            builder.Append('=').Append(v[register].ToString("X2", CultureInfo.InvariantCulture));
            builder.Append((register & 0x7) == 0x7 ? '\n' : ' ');
        }

        builder.Append("stack=[");
        for (int frame = 0; frame < stack.Length; frame++)
        {
            if (frame > 0)
            {
                builder.Append(' ');
            }
            builder.Append("0x").Append(stack[frame].ToString("X4", CultureInfo.InvariantCulture));
        }
        builder.Append("]\n");

        return builder.ToString();
    }
}
