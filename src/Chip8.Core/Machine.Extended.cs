namespace Chip8.Core;

public sealed partial class Machine
{
    bool executeExtended(ushort opcode, int x, int y, int n, byte nn, ushort nnn)
    {
        switch (opcode & 0xF000)
        {
            case 0x0000:
                return executeSystem(opcode, n);

            case 0x5000:
                if (platform == Platform.XoChip && n == 0x2)
                {
                    storeRange(x, y);
                    return true;
                }
                if (platform == Platform.XoChip && n == 0x3)
                {
                    loadRange(x, y);
                    return true;
                }
                return false;

            case 0xD000:
                v[0xF] = (byte)(video.drawSprite(memory, i, v[x], v[y], n, planeMask, !quirks.clipSprites) ? 1 : 0);
                drawPending = true;
                return true;

            case 0xF000:
                return executeExtendedMisc(opcode, x, nn);

            default:
                return false;
        }
    }

    bool executeSystem(ushort opcode, int n)
    {
        if ((opcode & 0xFFF0) == 0x00C0)
        {
            video.scrollDown(n, planeMask);
            drawPending = true;
            return true;
        }

        if ((opcode & 0xFFF0) == 0x00D0)
        {
            video.scrollUp(n, planeMask);
            drawPending = true;
            return true;
        }

        switch (opcode)
        {
            case 0x00E0:
                video.clear(planeMask);
                drawPending = true;
                return true;
            case 0x00FB:
                video.scrollRight(Video.scrollStep, planeMask);
                drawPending = true;
                return true;
            case 0x00FC:
                video.scrollLeft(Video.scrollStep, planeMask);
                drawPending = true;
                return true;
            case 0x00FD:
                exited = true;
                pc -= 2;
                return true;
            case 0x00FE:
                video.setResolution(false);
                drawPending = true;
                return true;
            case 0x00FF:
                video.setResolution(true);
                drawPending = true;
                return true;
            default:
                return false;
        }
    }

    bool executeExtendedMisc(ushort opcode, int x, byte nn)
    {
        if (platform == Platform.XoChip && opcode == 0xF000)
        {
            i = (ushort)((memory[pc] << 8) | memory[pc + 1]);
            pc += 2;
            return true;
        }

        if (platform == Platform.XoChip && (opcode & 0xF0FF) == 0xF001)
        {
            planeMask = x & 0x3;
            return true;
        }

        if (platform == Platform.XoChip && opcode == 0xF002)
        {
            for (int b = 0; b < audioPattern.Length; b++)
            {
                audioPattern[b] = memory[(i + b) & (memory.Length - 1)];
            }
            return true;
        }

        switch (nn)
        {
            case 0x30:
                i = (ushort)(bigFontStart + (v[x] & 0xF) * FontSet.bigGlyphBytes);
                return true;
            case 0x3A:
                if (platform != Platform.XoChip)
                {
                    return false;
                }
                audioPitch = v[x];
                return true;
            case 0x75:
                for (int r = 0; r <= (x & 0xF); r++)
                {
                    rplFlags[r] = v[r];
                }
                return true;
            case 0x85:
                for (int r = 0; r <= (x & 0xF); r++)
                {
                    v[r] = rplFlags[r];
                }
                return true;
            default:
                return false;
        }
    }

    void storeRange(int x, int y)
    {
        int step = x <= y ? 1 : -1;
        int addr = i;
        for (int r = x; ; r += step)
        {
            memory[addr++ & (memory.Length - 1)] = v[r];
            if (r == y)
            {
                break;
            }
        }
    }

    void loadRange(int x, int y)
    {
        int step = x <= y ? 1 : -1;
        int addr = i;
        for (int r = x; ; r += step)
        {
            v[r] = memory[addr++ & (memory.Length - 1)];
            if (r == y)
            {
                break;
            }
        }
    }
}
