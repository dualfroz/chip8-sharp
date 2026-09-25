namespace Chip8.Core;

public sealed partial class Machine
{
    void execute(ushort opcode)
    {
        int x = (opcode & 0x0F00) >> 8;
        int y = (opcode & 0x00F0) >> 4;
        int n = opcode & 0x000F;
        byte nn = (byte)(opcode & 0x00FF);
        ushort nnn = (ushort)(opcode & 0x0FFF);

        if (platform != Platform.Chip8 && executeExtended(opcode, x, y, n, nn, nnn))
        {
            return;
        }

        switch (opcode & 0xF000)
        {
            case 0x0000:
                switch (opcode)
                {
                    case 0x00E0:
                        clearScreen();
                        break;
                    case 0x00EE:
                        pc = stack[--sp];
                        break;
                    default:
                        break;
                }
                break;

            case 0x1000:
                pc = nnn;
                break;

            case 0x2000:
                stack[sp++] = pc;
                pc = nnn;
                break;

            case 0x3000:
                if (v[x] == nn)
                {
                    skipNext();
                }
                break;

            case 0x4000:
                if (v[x] != nn)
                {
                    skipNext();
                }
                break;

            case 0x5000:
                if (v[x] == v[y])
                {
                    skipNext();
                }
                break;

            case 0x6000:
                v[x] = nn;
                break;

            case 0x7000:
                v[x] = (byte)(v[x] + nn);
                break;

            case 0x8000:
                arithmetic(x, y, n);
                break;

            case 0x9000:
                if (v[x] != v[y])
                {
                    skipNext();
                }
                break;

            case 0xA000:
                i = nnn;
                break;

            case 0xB000:
                pc = (ushort)(nnn + (quirks.jumpWithVx ? v[x] : v[0]));
                break;

            case 0xC000:
                v[x] = (byte)(rng.nextBelow(256) & nn);
                break;

            case 0xD000:
                drawSprite(v[x], v[y], n);
                break;

            case 0xE000:
                switch (nn)
                {
                    case 0x9E:
                        if (keypad[v[x] & 0xF])
                        {
                            skipNext();
                        }
                        break;
                    case 0xA1:
                        if (!keypad[v[x] & 0xF])
                        {
                            skipNext();
                        }
                        break;
                }
                break;

            case 0xF000:
                misc(x, nn);
                break;
        }
    }

    void skipNext()
    {
        if (platform == Platform.XoChip && memory[pc] == 0xF0 && memory[pc + 1] == 0x00)
        {
            pc += 4;
        }
        else
        {
            pc += 2;
        }
    }

    void arithmetic(int x, int y, int n)
    {
        switch (n)
        {
            case 0x0:
                v[x] = v[y];
                break;

            case 0x1:
                v[x] = (byte)(v[x] | v[y]);
                if (quirks.resetVfOnLogic)
                {
                    v[0xF] = 0;
                }
                break;

            case 0x2:
                v[x] = (byte)(v[x] & v[y]);
                if (quirks.resetVfOnLogic)
                {
                    v[0xF] = 0;
                }
                break;

            case 0x3:
                v[x] = (byte)(v[x] ^ v[y]);
                if (quirks.resetVfOnLogic)
                {
                    v[0xF] = 0;
                }
                break;

            case 0x4:
            {
                int sum = v[x] + v[y];
                v[x] = (byte)sum;
                v[0xF] = (byte)(sum > 0xFF ? 1 : 0);
                break;
            }

            case 0x5:
            {
                byte noBorrow = (byte)(v[x] >= v[y] ? 1 : 0);
                v[x] = (byte)(v[x] - v[y]);
                v[0xF] = noBorrow;
                break;
            }

            case 0x6:
            {
                byte source = quirks.shiftUsesVy ? v[y] : v[x];
                byte lostBit = (byte)(source & 0x1);
                v[x] = (byte)(source >> 1);
                v[0xF] = lostBit;
                break;
            }

            case 0x7:
            {
                byte noBorrow = (byte)(v[y] >= v[x] ? 1 : 0);
                v[x] = (byte)(v[y] - v[x]);
                v[0xF] = noBorrow;
                break;
            }

            case 0xE:
            {
                byte source = quirks.shiftUsesVy ? v[y] : v[x];
                byte lostBit = (byte)((source & 0x80) >> 7);
                v[x] = (byte)(source << 1);
                v[0xF] = lostBit;
                break;
            }
        }
    }

    void misc(int x, byte nn)
    {
        switch (nn)
        {
            case 0x07:
                v[x] = delayTimer;
                break;

            case 0x0A:
                awaitKey(x);
                break;

            case 0x15:
                delayTimer = v[x];
                break;

            case 0x18:
                soundTimer = v[x];
                break;

            case 0x1E:
            {
                int sum = i + v[x];
                if (quirks.addToIndexSetsCarry)
                {
                    v[0xF] = (byte)(sum > 0x0FFF ? 1 : 0);
                }
                i = (ushort)(sum & 0x0FFF);
                break;
            }

            case 0x29:
                i = (ushort)(fontStart + v[x] * FontSet.glyphBytes);
                break;

            case 0x33:
                memory[i] = (byte)(v[x] / 100);
                memory[i + 1] = (byte)(v[x] / 10 % 10);
                memory[i + 2] = (byte)(v[x] % 10);
                break;

            case 0x55:
                for (int r = 0; r <= x; r++)
                {
                    memory[i + r] = v[r];
                }
                if (quirks.loadStoreIncrementsI)
                {
                    i = (ushort)(i + x + 1);
                }
                break;

            case 0x65:
                for (int r = 0; r <= x; r++)
                {
                    v[r] = memory[i + r];
                }
                if (quirks.loadStoreIncrementsI)
                {
                    i = (ushort)(i + x + 1);
                }
                break;
        }
    }

    void awaitKey(int x)
    {
        for (int key = 0; key < keyCount; key++)
        {
            if (keypadPrevious[key] && !keypad[key])
            {
                v[x] = (byte)key;
                waitingForKey = false;
                return;
            }
        }

        waitingForKey = true;
        pc -= 2;
    }

    void clearScreen()
    {
        System.Array.Clear(display, 0, display.Length);
        drawPending = true;
    }

    void drawSprite(byte originX, byte originY, int rows)
    {
        int startX = originX % displayWidth;
        int startY = originY % displayHeight;
        v[0xF] = 0;

        for (int row = 0; row < rows; row++)
        {
            int py = startY + row;
            if (quirks.clipSprites && py >= displayHeight)
            {
                break;
            }
            py %= displayHeight;

            byte spriteRow = memory[i + row];

            for (int col = 0; col < 8; col++)
            {
                if ((spriteRow & (0x80 >> col)) == 0)
                {
                    continue;
                }

                int px = startX + col;
                if (quirks.clipSprites && px >= displayWidth)
                {
                    break;
                }
                px %= displayWidth;

                int pixel = py * displayWidth + px;
                if (display[pixel])
                {
                    v[0xF] = 1;
                }
                display[pixel] ^= true;
            }
        }

        drawPending = true;
    }
}
