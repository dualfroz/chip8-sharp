using System;
using System.Collections.Generic;
using System.Text;
using Chip8.Core;

namespace Chip8.Replay;

public static class QuirksCodec
{
    const int shiftUsesVyBit = 0;
    const int jumpWithVxBit = 1;
    const int loadStoreIncrementsIBit = 2;
    const int resetVfOnLogicBit = 3;
    const int clipSpritesBit = 4;
    const int addToIndexSetsCarryBit = 5;

    static readonly (string token, int bit)[] tokens =
    {
        ("shiftVy", shiftUsesVyBit),
        ("jumpVx", jumpWithVxBit),
        ("incI", loadStoreIncrementsIBit),
        ("resetVf", resetVfOnLogicBit),
        ("clip", clipSpritesBit),
        ("carryI", addToIndexSetsCarryBit),
    };

    public static byte toByte(Quirks quirks)
    {
        int packed = 0;
        if (quirks.shiftUsesVy) packed |= 1 << shiftUsesVyBit;
        if (quirks.jumpWithVx) packed |= 1 << jumpWithVxBit;
        if (quirks.loadStoreIncrementsI) packed |= 1 << loadStoreIncrementsIBit;
        if (quirks.resetVfOnLogic) packed |= 1 << resetVfOnLogicBit;
        if (quirks.clipSprites) packed |= 1 << clipSpritesBit;
        if (quirks.addToIndexSetsCarry) packed |= 1 << addToIndexSetsCarryBit;
        return (byte)packed;
    }

    public static Quirks fromByte(byte packed) => new Quirks
    {
        shiftUsesVy = (packed & (1 << shiftUsesVyBit)) != 0,
        jumpWithVx = (packed & (1 << jumpWithVxBit)) != 0,
        loadStoreIncrementsI = (packed & (1 << loadStoreIncrementsIBit)) != 0,
        resetVfOnLogic = (packed & (1 << resetVfOnLogicBit)) != 0,
        clipSprites = (packed & (1 << clipSpritesBit)) != 0,
        addToIndexSetsCarry = (packed & (1 << addToIndexSetsCarryBit)) != 0,
    };

    public static string toTokens(Quirks quirks)
    {
        byte packed = toByte(quirks);
        var active = new List<string>();
        foreach (var (token, bit) in tokens)
        {
            if ((packed & (1 << bit)) != 0)
            {
                active.Add(token);
            }
        }
        return active.Count == 0 ? "none" : string.Join("+", active);
    }

    public static Quirks fromTokens(string text)
    {
        byte packed = 0;
        string trimmed = text.Trim();
        if (trimmed == "none" || trimmed.Length == 0)
        {
            return fromByte(0);
        }

        foreach (string chunk in trimmed.Split('+'))
        {
            string token = chunk.Trim();
            if (token.Length == 0)
            {
                continue;
            }

            bool matched = false;
            foreach (var (candidate, bit) in tokens)
            {
                if (string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase))
                {
                    packed |= (byte)(1 << bit);
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                throw new FormatException("unknown quirk token '" + token + "'");
            }
        }

        return fromByte(packed);
    }

    public static Quirks clone(Quirks quirks) => fromByte(toByte(quirks));

    public static bool sameFlags(Quirks left, Quirks right) => toByte(left) == toByte(right);
}
