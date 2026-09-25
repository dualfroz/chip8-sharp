using System;
using System.IO;

namespace Chip8.Core;

internal sealed class SubtractiveRandom
{
    const int tableSize = 56;
    const int seedMagic = 161803398;
    internal const int stateSize = (tableSize + 2) * sizeof(int);

    readonly int[] table = new int[tableSize];
    int next;
    int nextPartner;

    public SubtractiveRandom(int seed)
    {
        int subtraction = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
        int mj = seedMagic - subtraction;
        table[tableSize - 1] = mj;
        int mk = 1;
        int slot = 0;
        for (int step = 1; step < tableSize - 1; step++)
        {
            slot += 21;
            if (slot >= tableSize - 1)
            {
                slot -= tableSize - 1;
            }
            table[slot] = mk;
            mk = mj - mk;
            if (mk < 0)
            {
                mk += int.MaxValue;
            }
            mj = table[slot];
        }

        for (int round = 1; round < 5; round++)
        {
            for (int index = 1; index < tableSize; index++)
            {
                int partner = index + 30;
                if (partner >= tableSize - 1)
                {
                    partner -= tableSize - 1;
                }
                table[index] -= table[1 + partner];
                if (table[index] < 0)
                {
                    table[index] += int.MaxValue;
                }
            }
        }

        next = 0;
        nextPartner = 21;
    }

    public int nextBelow(int maxValue) => (int)(sample() * (1.0 / int.MaxValue) * maxValue);

    int sample()
    {
        if (++next >= tableSize)
        {
            next = 1;
        }
        if (++nextPartner >= tableSize)
        {
            nextPartner = 1;
        }

        int value = table[next] - table[nextPartner];
        if (value == int.MaxValue)
        {
            value--;
        }
        if (value < 0)
        {
            value += int.MaxValue;
        }

        table[next] = value;
        return value;
    }

    internal void writeTo(BinaryWriter writer)
    {
        foreach (int entry in table)
        {
            writer.Write(entry);
        }
        writer.Write(next);
        writer.Write(nextPartner);
    }

    internal void readFrom(BinaryReader reader)
    {
        for (int index = 0; index < tableSize; index++)
        {
            table[index] = reader.ReadInt32();
        }
        next = reader.ReadInt32();
        nextPartner = reader.ReadInt32();
    }
}
