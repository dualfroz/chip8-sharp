using System;
using System.Text;

namespace Chip8.Replay;

public readonly struct KeyMask : IEquatable<KeyMask>
{
    public const int keyCount = 16;
    public const ushort fullMask = 0xFFFF;

    public ushort bits { get; }

    public KeyMask(ushort bits)
    {
        this.bits = bits;
    }

    public static KeyMask empty => new KeyMask(0);

    public bool isEmpty => bits == 0;

    public int pressedCount
    {
        get
        {
            int count = 0;
            for (int key = 0; key < keyCount; key++)
            {
                if ((bits & (1 << key)) != 0)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public bool isPressed(int key) => (bits & (1 << (key & 0xF))) != 0;

    public KeyMask withPressed(int key) => new KeyMask((ushort)(bits | (1 << (key & 0xF))));

    public KeyMask withReleased(int key) => new KeyMask((ushort)(bits & ~(1 << (key & 0xF))));

    public KeyMask with(int key, bool pressed) => pressed ? withPressed(key) : withReleased(key);

    public KeyMask union(KeyMask other) => new KeyMask((ushort)(bits | other.bits));

    public KeyMask intersect(KeyMask other) => new KeyMask((ushort)(bits & other.bits));

    public static KeyMask fromKeys(params int[] keys)
    {
        ushort acc = 0;
        foreach (int key in keys)
        {
            acc |= (ushort)(1 << (key & 0xF));
        }
        return new KeyMask(acc);
    }

    public static KeyMask fromKeypad(bool[] keypad)
    {
        ushort acc = 0;
        for (int key = 0; key < keyCount && key < keypad.Length; key++)
        {
            if (keypad[key])
            {
                acc |= (ushort)(1 << key);
            }
        }
        return new KeyMask(acc);
    }

    public void applyTo(bool[] keypad)
    {
        for (int key = 0; key < keyCount && key < keypad.Length; key++)
        {
            keypad[key] = (bits & (1 << key)) != 0;
        }
    }

    public string toHex() => bits.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);

    public string toKeyList()
    {
        if (bits == 0)
        {
            return "-";
        }

        var builder = new StringBuilder();
        for (int key = 0; key < keyCount; key++)
        {
            if ((bits & (1 << key)) != 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append(',');
                }
                builder.Append(key.ToString("X1", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        return builder.ToString();
    }

    public static KeyMask parseHex(string text)
    {
        ushort value = ushort.Parse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        return new KeyMask(value);
    }

    public static KeyMask parseKeyList(string text)
    {
        if (text == "-" || text.Length == 0)
        {
            return empty;
        }

        ushort acc = 0;
        foreach (string chunk in text.Split(','))
        {
            string trimmed = chunk.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }
            int key = Convert.ToInt32(trimmed, 16);
            acc |= (ushort)(1 << (key & 0xF));
        }
        return new KeyMask(acc);
    }

    public bool Equals(KeyMask other) => bits == other.bits;

    public override bool Equals(object? obj) => obj is KeyMask other && Equals(other);

    public override int GetHashCode() => bits;

    public override string ToString() => "KeyMask(" + toKeyList() + ")";

    public static bool operator ==(KeyMask left, KeyMask right) => left.bits == right.bits;

    public static bool operator !=(KeyMask left, KeyMask right) => left.bits != right.bits;
}
