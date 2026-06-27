using System;
using System.Collections.Generic;

namespace Chip8.Core;

public sealed class Palette
{
    readonly Rgb[] colors;

    public Palette(params Rgb[] colors)
    {
        if (colors is null || colors.Length < 2)
        {
            throw new ArgumentException("a palette needs at least two colours", nameof(colors));
        }
        this.colors = (Rgb[])colors.Clone();
    }

    public int colorCount => colors.Length;

    public Rgb this[int index] => colors[index];

    public Rgb resolve(int index) => colors[index < 0 ? 0 : index % colors.Length];

    public IReadOnlyList<Rgb> toList() => (Rgb[])colors.Clone();

    public static Palette monochrome() => new(
        new Rgb(0x00, 0x00, 0x00),
        new Rgb(0xFF, 0xFF, 0xFF));

    public static Palette grayscale() => new(
        new Rgb(0x00, 0x00, 0x00),
        new Rgb(0x55, 0x55, 0x55),
        new Rgb(0xAA, 0xAA, 0xAA),
        new Rgb(0xFF, 0xFF, 0xFF));

    public static Palette octo() => new(
        Rgb.fromHex("#996600"),
        Rgb.fromHex("#FFCC00"),
        Rgb.fromHex("#FF6600"),
        Rgb.fromHex("#662200"));

    public static Palette extended16() => new(
        Rgb.fromHex("#000000"),
        Rgb.fromHex("#1D2B53"),
        Rgb.fromHex("#7E2553"),
        Rgb.fromHex("#008751"),
        Rgb.fromHex("#AB5236"),
        Rgb.fromHex("#5F574F"),
        Rgb.fromHex("#C2C3C7"),
        Rgb.fromHex("#FFF1E8"),
        Rgb.fromHex("#FF004D"),
        Rgb.fromHex("#FFA300"),
        Rgb.fromHex("#FFEC27"),
        Rgb.fromHex("#00E436"),
        Rgb.fromHex("#29ADFF"),
        Rgb.fromHex("#83769C"),
        Rgb.fromHex("#FF77A8"),
        Rgb.fromHex("#FFCCAA"));

    public static Palette byName(string name) => name.ToLowerInvariant() switch
    {
        "mono" or "monochrome" => monochrome(),
        "gray" or "grey" or "grayscale" => grayscale(),
        "octo" => octo(),
        "extended" or "extended16" or "pico8" => extended16(),
        _ => throw new ArgumentException("unknown palette '" + name + "'", nameof(name)),
    };
}
