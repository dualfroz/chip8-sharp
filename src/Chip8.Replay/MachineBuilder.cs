using Chip8.Core;

namespace Chip8.Replay;

public static class MachineBuilder
{
    public static Machine fromMovie(InputMovie movie)
    {
        return new Machine(QuirksCodec.clone(movie.quirks), movie.randomSeed, movie.platform);
    }

    public static Machine fromMovie(InputMovie movie, byte[] rom)
    {
        var machine = fromMovie(movie);
        machine.loadRom(rom);
        return machine;
    }

    public static InputMovie headerFor(Machine machine, byte[] rom, string romName, int instructionsPerFrame, int? randomSeed)
    {
        var movie = new InputMovie
        {
            platform = machine.platform,
            quirks = QuirksCodec.clone(machine.quirks),
            randomSeed = randomSeed,
            instructionsPerFrame = instructionsPerFrame,
        };
        movie.setRom(rom, romName);
        return movie;
    }
}
