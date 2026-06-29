using System;
using System.IO;
using System.Text;

namespace Chip8.Core;

public sealed partial class Machine
{
    static readonly byte[] stateMagic = Encoding.ASCII.GetBytes("C8ST");
    const byte stateVersion = 1;

    public byte[] saveState()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write(stateMagic);
        writer.Write(stateVersion);
        writer.Write((byte)platform);
        writeQuirks(writer);
        writeBody(writer);

        writer.Flush();
        return stream.ToArray();
    }

    public void loadState(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream, Encoding.ASCII);

        readHeader(reader);
        var savedPlatform = (Platform)reader.ReadByte();
        if (savedPlatform != platform)
        {
            throw new InvalidDataException(
                "state platform " + savedPlatform + " does not match machine platform " + platform);
        }

        readQuirksInto(reader, quirks);
        readBody(reader);
    }

    public static Machine restore(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream, Encoding.ASCII);

        readHeader(reader);
        var savedPlatform = (Platform)reader.ReadByte();
        Quirks savedQuirks = readQuirks(reader);

        var machine = new Machine(savedQuirks, null, savedPlatform);
        machine.readBody(reader);
        return machine;
    }

    static void readHeader(BinaryReader reader)
    {
        byte[] magic = reader.ReadBytes(stateMagic.Length);
        for (int index = 0; index < stateMagic.Length; index++)
        {
            if (magic.Length <= index || magic[index] != stateMagic[index])
            {
                throw new InvalidDataException("not a chip8-sharp save state (bad magic)");
            }
        }

        byte version = reader.ReadByte();
        if (version != stateVersion)
        {
            throw new InvalidDataException("unsupported save state version " + version);
        }
    }

    void writeQuirks(BinaryWriter writer)
    {
        writer.Write(quirks.shiftUsesVy);
        writer.Write(quirks.jumpWithVx);
        writer.Write(quirks.loadStoreIncrementsI);
        writer.Write(quirks.resetVfOnLogic);
        writer.Write(quirks.clipSprites);
        writer.Write(quirks.addToIndexSetsCarry);
    }

    static Quirks readQuirks(BinaryReader reader)
    {
        var target = new Quirks();
        readQuirksInto(reader, target);
        return target;
    }

    static void readQuirksInto(BinaryReader reader, Quirks target)
    {
        target.shiftUsesVy = reader.ReadBoolean();
        target.jumpWithVx = reader.ReadBoolean();
        target.loadStoreIncrementsI = reader.ReadBoolean();
        target.resetVfOnLogic = reader.ReadBoolean();
        target.clipSprites = reader.ReadBoolean();
        target.addToIndexSetsCarry = reader.ReadBoolean();
    }

    void writeBody(BinaryWriter writer)
    {
        writer.Write(memory.Length);
        writer.Write(memory);
        writer.Write(v);
        writer.Write((byte)stack.Length);
        foreach (ushort frame in stack)
        {
            writer.Write(frame);
        }

        foreach (bool lit in display)
        {
            writer.Write(lit);
        }
        foreach (bool down in keypad)
        {
            writer.Write(down);
        }
        foreach (bool down in keypadPrevious)
        {
            writer.Write(down);
        }

        writer.Write(rplFlags);
        writer.Write(audioPattern);

        writer.Write(i);
        writer.Write(pc);
        writer.Write(sp);
        writer.Write(delayTimer);
        writer.Write(soundTimer);
        writer.Write(planeMask);
        writer.Write(audioPitch);
        writer.Write(exited);
        writer.Write(drawPending);
        writer.Write(waitingForKey);

        video.writeTo(writer);
    }

    void readBody(BinaryReader reader)
    {
        int memoryLength = reader.ReadInt32();
        if (memoryLength != memory.Length)
        {
            throw new InvalidDataException(
                "state memory size " + memoryLength + " does not match machine memory size " + memory.Length);
        }

        readExact(reader, memory);
        readExact(reader, v);

        int stackLength = reader.ReadByte();
        if (stackLength != stack.Length)
        {
            throw new InvalidDataException("state stack depth " + stackLength + " does not match " + stack.Length);
        }
        for (int frame = 0; frame < stackLength; frame++)
        {
            stack[frame] = reader.ReadUInt16();
        }

        readBooleans(reader, display);
        readBooleans(reader, keypad);
        readBooleans(reader, keypadPrevious);

        readExact(reader, rplFlags);
        readExact(reader, audioPattern);

        i = reader.ReadUInt16();
        pc = reader.ReadUInt16();
        sp = reader.ReadByte();
        delayTimer = reader.ReadByte();
        soundTimer = reader.ReadByte();
        planeMask = reader.ReadInt32();
        audioPitch = reader.ReadByte();
        exited = reader.ReadBoolean();
        drawPending = reader.ReadBoolean();
        waitingForKey = reader.ReadBoolean();

        video.readFrom(reader);
    }

    static void readExact(BinaryReader reader, byte[] target)
    {
        int read = reader.Read(target, 0, target.Length);
        if (read != target.Length)
        {
            throw new InvalidDataException("save state truncated (expected " + target.Length + " bytes, got " + read + ")");
        }
    }

    static void readBooleans(BinaryReader reader, bool[] target)
    {
        for (int index = 0; index < target.Length; index++)
        {
            target[index] = reader.ReadBoolean();
        }
    }
}
