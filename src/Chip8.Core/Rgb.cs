using System;
using System.Globalization;

namespace Chip8.Core;

public readonly struct Rgb : IEquatable<Rgb>
{
    public Rgb(byte r, byte g, byte b)
    {
        this.r = r;
        this.g = g;
        this.b = b;
    }

    public byte r { get; }
    public byte g { get; }
    public byte b { get; }

    public static Rgb fromHex(string hex)
    {
        if (hex is null)
        {
            throw new ArgumentNullException(nameof(hex));
        }

        string digits = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
        if (digits.Length != 6)
        {
            throw new FormatException("expected a 6 digit hex colour like #RRGGBB, got '" + hex + "'");
        }

        byte channel(int start) => byte.Parse(digits.Substring(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Rgb(channel(0), channel(2), channel(4));
    }

    public string toHex() =>
        "#" + r.ToString("X2", CultureInfo.InvariantCulture)
            + g.ToString("X2", CultureInfo.InvariantCulture)
            + b.ToString("X2", CultureInfo.InvariantCulture);

    public bool Equals(Rgb other) => r == other.r && g == other.g && b == other.b;

    public override bool Equals(object? obj) => obj is Rgb other && Equals(other);

    public override int GetHashCode() => (r << 16) | (g << 8) | b;

    public override string ToString() => toHex();
}
