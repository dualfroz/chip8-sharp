namespace Chip8.Core;

public sealed class Quirks
{
    public bool shiftUsesVy { get; set; }

    public bool jumpWithVx { get; set; }

    public bool loadStoreIncrementsI { get; set; }

    public bool resetVfOnLogic { get; set; }

    public bool clipSprites { get; set; }

    public bool addToIndexSetsCarry { get; set; }

    public static Quirks cosmacVip() => new()
    {
        shiftUsesVy = true,
        jumpWithVx = false,
        loadStoreIncrementsI = true,
        resetVfOnLogic = true,
        clipSprites = true,
        addToIndexSetsCarry = false,
    };

    public static Quirks modern() => new()
    {
        shiftUsesVy = false,
        jumpWithVx = false,
        loadStoreIncrementsI = false,
        resetVfOnLogic = false,
        clipSprites = true,
        addToIndexSetsCarry = false,
    };

    public static Quirks superChip() => new()
    {
        shiftUsesVy = false,
        jumpWithVx = true,
        loadStoreIncrementsI = false,
        resetVfOnLogic = false,
        clipSprites = true,
        addToIndexSetsCarry = false,
    };

    public static Quirks xoChip() => new()
    {
        shiftUsesVy = false,
        jumpWithVx = false,
        loadStoreIncrementsI = false,
        resetVfOnLogic = false,
        clipSprites = false,
        addToIndexSetsCarry = false,
    };
}
