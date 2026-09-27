# chip8-sharp

A CHIP-8, SUPER-CHIP and XO-CHIP emulator and toolchain written in C# for .NET 6. The
emulator core is a plain library with no rendering or input dependencies, so every opcode
is tested headlessly against unit tests and community test ROMs. Around it sit two
assemblers (classic mnemonics and Octo), a console debugger with breakpoints and
watchpoints, save states, PPM and WAV export, a static and dynamic ROM analyzer, and an
input movie system for frame-exact record and replay. A Raylib frontend provides a window that
follows the 64x32 and 128x64 resolutions and draws all XO-CHIP planes in the selected
palette, keyboard input and a beep.

## Features

### Emulator core (`Chip8.Core`)

- Complete CHIP-8 instruction set plus the SUPER-CHIP and XO-CHIP extensions, selected per
  machine with a platform setting.
- 16 V registers, I, PC, 16-level stack, delay and sound timers at 60 Hz, 16-key keypad.
  `FX0A` waits for a key to be pressed and released.
- Small 4x5 hex font at 0x050 and large 8x10 hex font at 0x0A0 (`FX30`).
- Six interpreter quirks, grouped into presets (see below).
- Deterministic random numbers (`CXNN`) when a seed is given.
- Instruction stepping or whole 60 Hz frames (`runFrame(instructionsPerFrame)`).

### Platforms

| Platform | Memory | Display | Additions |
|----------|--------|---------|-----------|
| `chip8` | 4 KB | 64x32, 1 bit | - |
| `superchip` | 4 KB | 64x32 or 128x64 (`00FE`/`00FF`) | `00CN` scroll down, `00FB`/`00FC` scroll right/left by 4, `00FD` exit, `DXY0` 16x16 sprites, `FX30` big font, `FX75`/`FX85` RPL flags |
| `xochip` | 64 KB | as SUPER-CHIP, 2 bit planes (4 colours) | `00DN` scroll up, `FN01` plane select, `F002` audio pattern, `FX3A` pitch, `5XY2`/`5XY3` register range save/load, `F000 NNNN` 16-bit index load (skips step over it as one instruction) |

### Quirks

| Behaviour | modern | cosmac | superchip | xochip |
|-----------|--------|--------|-----------|--------|
| `8XY6`/`8XYE` shift `VY` instead of `VX` | no | yes | no | no |
| `BNNN` jumps to `NNN + VX` (`BXNN`) instead of `NNN + V0` | no | no | yes | no |
| `FX55`/`FX65` increment I | no | yes | no | no |
| `8XY1`/`8XY2`/`8XY3` reset VF | no | yes | no | no |
| Sprites clip at the screen edge (otherwise wrap) | yes | yes | yes | no |
| `FX1E` sets VF on overflow past 0xFFF | no | no | no | no |

With `--quirks auto` (the default) each platform uses its own preset: `chip8` uses `modern`.

### Assemblers

**Classic syntax** (`Chip8.Core.Assembly`, `chip8 asm file.asm`): Cowgod-style mnemonics
(`CLS`, `RET`, `JP`, `CALL`, `SE`, `SNE`, `LD`, `ADD`, `OR`, `AND`, `XOR`, `SUB`, `SUBN`,
`SHR`, `SHL`, `RND`, `DRW`, `SKP`, `SKNP`) plus `SCD`, `SCU`, `SCR`, `SCL`, `EXIT`,
`LOW`/`LORES`, `HIGH`/`HIRES`, `PLANE`, `AUDIO`, `PITCH`, `SAVE Vx-Vy`, `LOAD Vx-Vy`,
`LD HF, Vx` and `LD R, Vx`. Labels (`name:`), `EQU` constants, `DB`/`DW` data, `ORG`,
`ALIGN`, `MACRO`/`ENDM` with parameters, expressions with `+ - * / & | ^ ~ << >>` and
parentheses, numbers as decimal, `0x`/`$` hex and `0b`/`%` binary, `;` and `//` comments.
`LD I, addr` with an address above 0xFFF assembles to the 4-byte XO-CHIP long load. The
assembler resolves forward references by iterating the layout and can print a listing.

**Octo syntax** (`Chip8.OctoAsm`, `chip8 asm file.8o`): a compiler for the Octo language:

- statements: `clear`, `return`/`;`, `jump`, `jump0`, `native`, calls by label name or
  `:call`, `bcd`, `save`/`load` (also `save vx - vy`), `sprite`, `delay :=`, `buzzer :=`,
  `pitch :=`, `i := addr`, `i := long addr`, `i := hex vx`, `i := bighex vx`, `i += vx`,
  `vx := n|vy|random n|key|delay`, `+=`, `-=`, `=-`, `|=`, `&=`, `^=`, `>>=`, `<<=`,
  `hires`, `lores`, `exit`, `scroll-up/down/left/right`, `plane`, `audio`, `saveflags`,
  `loadflags`;
- control flow: `if ... then`, `if ... begin ... else ... end`, `loop ... while ... again`,
  with `==`, `!=`, `<`, `>`, `<=`, `>=`, `key` and `-key` conditions (the relational ones
  use `vf` as scratch, as in Octo);
- directives: `: label`, `:const`, `:alias`, `:calc` (Octo expression syntax, `HERE`),
  `:byte`, `:pointer`, `:org`, `:next`, `:unpack` (also `:unpack long`), `:macro`,
  `:breakpoint`, `:assert`; bare numbers emit bytes;
- forward references are patched at the end; a `jump main` is inserted at 0x200 when
  `main` is not the first thing in the program;
- errors carry line and column; the listing marks data bytes and decodes code.

### Debugger (`Debugger`, `DebuggerConsole`, `chip8 debug`)

Single step, step over a `CALL`, run with a step limit, run until an address, breakpoints,
memory watchpoints and register watchpoints (execution stops on the first change), register
and stack view, hex dump, disassembly around PC, machine reset. The same command set is
available as a library (`DebuggerConsole.execute(line)`) and as the interactive
`chip8 debug` prompt.

### Save states

`Machine.saveState()` serialises the whole machine (memory, registers, stack, timers,
keypad, both display buffers, RPL flags, audio pattern and pitch, random number generator,
quirks and platform) into a versioned binary blob. `loadState()` restores it into a machine
of the same platform and `Machine.restore()` builds a new machine from it, so a loaded state
or a rewound timeline draws the same `CXNN` numbers as the original run. Version 1 states
(written before the generator was saved) still load: `loadState()` keeps the machine's
current generator and `restore()` starts an unseeded one.

### Export

- PPM (P6) images of the current frame with a scale factor and a palette: `mono`,
  `grayscale`, `octo` (default) or `extended16`.
- WAV (16-bit mono PCM, 44.1 kHz) recordings of the sound output: a 440 Hz square wave
  for CHIP-8 and SUPER-CHIP, the 128-bit audio pattern played at the pitch-derived rate
  (`4000 * 2^((pitch - 64) / 48)` Hz) for XO-CHIP.

### Analysis (`Chip8.Analysis`, `chip8 analyze`)

- Recursive-descent decoder that follows jumps, calls and both sides of skips from the
  entry point.
- Control flow graph (basic blocks, fallthrough/jump/branch/call edges, predecessors,
  indirect `BNNN` jumps, targets outside the ROM).
- Call graph with callers, callees, leaf and recursive subroutines and orphaned call
  targets (calls found in bytes the reachability pass never reaches, pointing at code that
  is not reachable either).
- ROM profile: size, instruction, block, subroutine, draw, call, jump and skip counts,
  instruction histogram, used SUPER-CHIP/XO-CHIP features and a guessed platform.
- Reachability: reachable share of the ROM, byte ranges never reached as code.
- Sprite scanner that pairs I loads with draws and renders the sprites as ASCII art.
- Dynamic passes: coverage profiler (coverage, hotspot, execution heatmap),
  self-modifying code detector (writes into code bytes, plus a static check for I loaded
  with a code address) and an execution trace.

### Replay and test tooling (`Chip8.Replay`, `chip8 replay`, `chip8 bench`, `chip8 conformance`)

- Input movies: platform, quirks, RNG seed, instructions per frame, ROM name and FNV-1a
  hash, and the keys held on each frame, in a text format and a compact binary format.
- Recording from the window (`--record`) or from an input script; frame-exact playback,
  ROM hash check and a determinism check (replay twice, compare end states).
- Input scripts with `press`, `release`, `hold K for N`, `wait N`, `frame N: <command>`
  and assertions on registers, I, PC, pixels and the exit state.
- Timeline with periodic keyframes in a ring buffer for rewinding and stepping back.
- Framebuffer and register hashes, framebuffer snapshots.
- Conformance runner with expectations for the bundled test ROMs and a benchmark that
  reports instructions and frames per second.

## Build and run

Requires the .NET 6 SDK. The window frontend uses the `Raylib-cs` NuGet package, which
ships the native raylib library.

    dotnet build Chip8Sharp.sln
    dotnet test Chip8Sharp.sln

Run a ROM in a window:

    dotnet run --project src/Chip8.Cli -- tests/roms/2-ibm-logo.ch8

Run headless and print the framebuffer:

    dotnet run --project src/Chip8.Cli -- tests/roms/2-ibm-logo.ch8 --headless --cycles 40

```
+----------------------------------------------------------------+
|                                                                |
|            ######## #########   #####         #####  # #       |
|                                                      # #       |
|            ######## ########### ######       ######   #        |
|                                                                |
|              ####     ###   ###   #####     #####    # #       |
|                                                      ###       |
|              ####     #######     ####### #######      #       |
|                                                        #       |
|              ####     #######     ### ####### ###              |
|                                                       #        |
|              ####     ###   ###   ###  #####  ###              |
|                                                                |
...
rom=2-ibm-logo.ch8 cycles=40 litPixels=230 soundActive=False
```

(The horizontal striping comes from the ROM's sprite data.)

Assemble and run the Octo example, then record and replay an input movie:

    dotnet run --project src/Chip8.Cli -- asm examples/mover.8o -o mover.ch8 --listing
    dotnet run --project src/Chip8.Cli -- replay record mover.ch8 examples/mover.script -o mover.c8m
    dotnet run --project src/Chip8.Cli -- replay play mover.c8m mover.ch8 --verify --screen

In the examples below `chip8` stands for `dotnet run --project src/Chip8.Cli --` or the
built `Chip8.Cli` executable.

## Command line

Every command prints its options with `--help`.

### Run a ROM: `chip8 <rom> [options]`

    --headless        run without a window, print the framebuffer as ASCII
    --cycles N        instructions to run in headless mode (default 2000)
    --ipf N           instructions per 60 Hz frame (default 11)
    --scale N         window / PPM pixel scale (default 14)
    --seed N          deterministic RNG seed
    --platform NAME   chip8 (default), superchip, or xochip
    --quirks NAME     auto (default), modern, cosmac, superchip, xochip
    --palette NAME    octo (default), mono, grayscale, extended16
    --ppm PATH        export the final frame as a PPM image (implies headless)
    --wav PATH        export the sound output as a WAV file (implies headless)
    --frames N        window mode: close after N frames
    --shot PATH       window mode: save a PNG screenshot before closing
    --record PATH     window mode: record keyboard input to a movie file

### `chip8 asm <source> [options]`

    -o, --out PATH    output file (default: source name with .ch8)
    --listing         print an address / bytes / mnemonic listing
    --syntax NAME     chip8 or octo (default: octo for .8o files, chip8 otherwise)

### `chip8 disasm <rom> [--base ADDR]`

Linear disassembly of the ROM, base address 0x200 by default.

### `chip8 debug <rom> [--platform NAME] [--quirks NAME] [--seed N]`

Commands at the `(chip8)` prompt:

    s [n]        step n instructions (default 1)
    so           step over a CALL
    c [n]        continue up to n steps (default until a stop)
    u <addr>     run until pc reaches addr
    b <addr>     add breakpoint          rb <addr>   remove breakpoint
    bl           list breakpoints
    wm <addr>    watch a memory byte     wr <Vx>     watch a register
    wl           list watchpoints        wc          clear watchpoints
    r            show registers          mem <a> [n] hex dump n bytes from a
    dis [n]      disassemble n instructions from pc (default 8)
    reset        reset the machine       q           quit

### `chip8 analyze <rom> [options]`

Without options it prints the ROM profile and the reachable share of the ROM.

    --cfg             control flow graph
    --calls           call graph
    --sprites         sprites referenced by draw instructions, as ASCII art
    --dead            unreachable words and byte ranges never reached as code
    --coverage N      execute N instructions, report coverage and the hotspot
    --heatmap         with --coverage: execution count of every address
    --smc N           execute N instructions, report self-modifying code
    --trace N         print the first N executed instructions
    --all             all static passes plus coverage and smc over 100000 steps
    --platform NAME   platform for dynamic passes (default: guessed from the ROM)
    --quirks NAME     quirk preset for dynamic passes
    --seed N          RNG seed for dynamic passes (default 1)

### `chip8 replay record|play|info`

    chip8 replay record <rom> <script> -o <movie> [--platform] [--quirks] [--seed N] [--ipf N] [--binary]
    chip8 replay play <movie> <rom> [--verify] [--screen] [--ppm PATH] [--palette NAME] [--scale N] [--window] [--force]
    chip8 replay info <movie> [--frames]

Movies are text unless the file name ends in `.c8b` or `--binary` is given; both formats
are detected when reading. `play` refuses a ROM whose hash differs from the one in the
movie unless `--force` is given.

Input script example (`examples/mover.script`):

    wait 2
    hold 9 for 12
    wait 4
    assert pixel 31 14 off
    hold 8 for 6
    wait 4
    frame 60: press 7
    frame 64: release 7
    wait 4
    assert running

### `chip8 bench <rom> [--frames N] [--ipf N] [--platform NAME] [--quirks NAME] [--seed N]`

Runs the ROM as fast as possible (60000 frames by default) and prints instructions and
frames per second plus the final framebuffer hash.

### `chip8 conformance [--roms DIR]`

Runs the built-in expectations for the bundled test ROMs and exits with status 1 if any
fails. Without `--roms` it uses the nearest `roms/` or `tests/roms/` directory above the
executable.

## Key mapping

    CHIP-8            Keyboard
    1 2 3 C           1 2 3 4
    4 5 6 D           Q W E R
    7 8 9 E           A S D F
    A 0 B F           Z X C V

## Tests

`dotnet test Chip8Sharp.sln` runs four xUnit projects, 551 tests in total:

- `Chip8.Tests` (244): per-opcode unit tests with hand-computed expectations, quirk
  variants, SUPER-CHIP and XO-CHIP opcodes, video planes and scrolling, the debugger and
  watchpoints, save states, palettes, PPM and WAV output, the classic assembler and the
  disassembler, and full-framebuffer golden comparisons for the community test ROMs.
  `tests/roms/selfcheck.asm` is assembled at test time and must report all 17 checks
  passed under both the `modern` and `cosmac` quirks.
- `Chip8.OctoAsm.Tests` (166): lexer, expression evaluator, encodings of every statement,
  program layout, control flow, macros, relational comparisons executed on the emulator
  for boundary values, diagnostics, and the `examples/mover.8o` program.
- `Chip8.Analysis.Tests` (74): opcode classification, control flow and call graphs,
  reachability, platform detection, sprite scanning, coverage, tracing and self-modifying
  code detection, plus the profile of the IBM logo ROM.
- `Chip8.Replay.Tests` (67): key masks, quirk codec, ROM and framebuffer hashes, movie
  formats, input scripts and their errors, record/playback equivalence, determinism,
  timeline rewinding and the conformance runner.

Test ROM results (default `modern` quirks unless noted):

| ROM | Result |
|-----|--------|
| `1-chip8-logo.ch8` | splash logo renders (520 pixels) |
| `2-ibm-logo.ch8` | IBM logo renders (230 pixels) |
| `3-corax+.ch8` | all opcode groups pass |
| `4-flags.ch8` | all flag cases pass |
| `test_opcode.ch8` | every cell reads OK, also under `cosmac` |
| `7-beep.ch8` | sound timer activates |
| `8-scrolling.ch8` | menu renders under `superchip` (254 pixels) |

Sources and licenses of the test ROMs are listed in [tests/roms/README.md](tests/roms/README.md).

## Project layout

    src/Chip8.Core        emulator library: machine, video, quirks, debugger, disassembler,
                          classic assembler (Assembly/), audio and WAV (Audio/), PPM (Export/)
    src/Chip8.OctoAsm     Octo lexer, expression evaluator and compiler
    src/Chip8.Analysis    decoder, control flow and call graphs, profiler, sprite scanner,
                          coverage, self-modifying code detector, tracer
    src/Chip8.Replay      input movies and scripts, replay engine, timeline and rewind,
                          hashes, conformance runner, benchmark result
    src/Chip8.Cli         command line and Raylib frontend
    tests/Chip8.Tests           core tests and ROM goldens (goldens/)
    tests/Chip8.OctoAsm.Tests   Octo compiler tests
    tests/Chip8.Analysis.Tests  analysis tests
    tests/Chip8.Replay.Tests    replay and tooling tests
    tests/roms            third-party test ROMs with licenses, selfcheck.asm
    examples              mover.8o (Octo example) and mover.script (input script)

## Limitations

- The window beep is always a 440 Hz square wave; XO-CHIP audio patterns are only
  rendered in WAV export.
- Timing is a fixed number of instructions per 60 Hz frame; there is no display-wait
  (vblank) quirk and no cycle-accurate COSMAC VIP timing. SUPER-CHIP left/right scrolls
  always move 4 pixels, independent of the resolution.
- The Octo compiler covers the language listed above; `:stringmode`, `:monitor`,
  `:proto` and other directives are rejected with an error, and `:alias` only names
  registers.
- Static analysis cannot follow `BNNN` jumps (they are reported as indirect). The dead
  word list (`--dead`) comes from a linear decode, so data placed between code can show up
  there. Dynamic passes stop at the first jump-to-self loop.
- `5-quirks.ch8` and `6-keypad.ch8` are interactive and are not part of the automated
  tests.

## License

MIT, see [LICENSE](LICENSE). The test ROMs in `tests/roms` keep their own licenses
(GPL-3.0 and MIT), see [tests/roms/README.md](tests/roms/README.md).
