using System;
using System.IO;
using Chip8.Core.Assembly;

namespace Chip8.Analysis.Tests;

static class Programs
{
    public static readonly byte[] branchy = Assembler.assembleRom(@"
        LD V0, 0
        SE V0, 1
        JP skip
        CALL sub
skip:
        CALL sub
        JP end
sub:
        ADD V0, 1
        RET
end:
        JP end
");

    public static readonly byte[] recursive = Assembler.assembleRom(@"
        CALL first
halt:
        JP halt
first:
        CALL second
        RET
second:
        SE V0, 0
        CALL first
        RET
        CALL lost
lost:
        RET
");

    public static readonly byte[] dataBetweenCode = Assembler.assembleRom(@"
        CALL draw
        JP main
sprite:
        DB 0x22, 0x0B, 0x3C
main:
        LD V0, 0x22
        LD V1, 0x15
        JP main
draw:
        LD I, sprite
        DRW V0, V1, 3
        RET
        CALL orphan
        RET
orphan:
        CLS
        RET
");

    public static readonly byte[] spriteWithDeadCode = Assembler.assembleRom(@"
        LD I, sprite
        LD V0, 8
        LD V1, 4
        DRW V0, V1, 3
halt:
        JP halt
        CLS
sprite:
        DB 0x81, 0x42, 0x24
");

    public static readonly byte[] selfModifying = Assembler.assembleRom(@"
        LD I, target
        LD V0, 0x60
        LD V1, 0x2A
        LD [I], V1
target:
        LD V0, 0x00
halt:
        JP halt
");

    public static byte[] assemble(string source) => Assembler.assembleRom(source);

    public static byte[] testRom(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "roms", name);
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }
        throw new FileNotFoundException("test ROM not found: " + name);
    }
}
