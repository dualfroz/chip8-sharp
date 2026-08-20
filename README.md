# chip8-sharp

A complete, tested CHIP-8 emulator written in C# (.NET 6).

The core is a pure library with no rendering or input dependencies, so the full
instruction set is verified headlessly against the standard community test ROMs.
A separate Raylib frontend provides a scaled window, keyboard input and a square-wave
beep.

## What it does

- All 35 CHIP-8 opcodes.
- 4 KB memory, 16 V registers, index register, program counter, 16-level stack.
- 64x32 monochrome display with XOR sprite drawing and collision (VF).
- 16-key hex keypad, including `FX0A` wait-for-key with release semantics.
- Delay and sound timers at 60 Hz; beep while the sound timer is non-zero.
- Built-in 4x5 hex font at 0x50.
- Selectable interpreter quirks (`modern`, `cosmac`, `superchip`) for the
  ambiguous instructions (shift source, `BNNN` vs `BXNN`, `FX55/FX65` index
  increment, logic-op VF reset).

## Layout

    src/Chip8.Core     pure emulator library (no frontend dependency)
      Machine.cs         state, reset, ROM loading, timers, step loop
      Machine.Execute.cs opcode decode and execution
      Quirks.cs          per-platform behaviour toggles
      FontSet.cs         built-in hex font
      AsciiScreen.cs     framebuffer to ASCII (used by CLI and tests)
    src/Chip8.Cli      Raylib window + headless ASCII runner
    tests/Chip8.Tests  xUnit: per-opcode unit tests + ROM framebuffer goldens
    roms/              standard test ROMs (see Sources below)

## Build and run

    dotnet build
    dotnet test

Run a ROM in a window:

    dotnet run --project src/Chip8.Cli -- roms/2-ibm-logo.ch8

Run headless and dump the framebuffer as ASCII (no display needed):

    dotnet run --project src/Chip8.Cli -- roms/2-ibm-logo.ch8 --headless --cycles 40

Options:

    --headless        run without a window, print the framebuffer as ASCII
    --cycles N        instructions to run in headless mode (default 2000)
    --ipf N           instructions per 60 Hz frame (default 11, ~660 Hz)
    --scale N         window pixel scale (default 14)
    --frames N        window mode: auto-close after N frames
    --shot PATH       window mode: save a PNG screenshot before closing
    --seed N          deterministic RNG seed for CXNN
    --quirks NAME     modern (default), cosmac, or superchip

## Key mapping

    CHIP-8            Keyboard
    1 2 3 C           1 2 3 4
    4 5 6 D           Q W E R
    7 8 9 E           A S D F
    A 0 B F           Z X C V

## Verification

`dotnet test` runs 40 tests:

- Per-opcode unit tests with hand-computed expected values for every instruction
  group, including the flag semantics of `8XY4/5/6/7/E` and the quirk variants.
- Full 64x32 framebuffer golden comparisons for the standard test ROMs. The IBM
  golden is produced by an independent decoder; the corax+, flags and corax89
  goldens are derived from the test authors' own reference screenshots.

ROM results (all pass under the default `modern` profile):

| ROM                | Result                          |
|--------------------|---------------------------------|
| 2-ibm-logo.ch8     | IBM logo renders (230 pixels)   |
| 3-corax+.ch8       | all opcode groups pass          |
| 4-flags.ch8        | all flag cases pass             |
| test_opcode.ch8    | every cell reads OK             |
| BC_test.ch8        | displays BON (all good)         |
| 7-beep.ch8         | sound timer activates           |

IBM logo, rendered headless (`# ` = lit pixel). The horizontal striping is
inherent to the ROM's sprite data, not an artifact:

```
            ######## #########   #####         #####  # #
            ######## ########### ######       ######   #

              ####     ###   ###   #####     #####    # #
                                                      ###
              ####     #######     ####### #######      #
                                                        #
              ####     #######     ### ####### ###
                                                       #
              ####     ###   ###   ###  #####  ###
                                                      ###
            ######## ########### #####   ###   #####    #
                                                      ##
            ######## #########   #####    #    #####  ###
```

## Quirk profiles

CHIP-8 interpreters disagree on a handful of instructions. The profile selects
the behaviour:

| Behaviour                    | modern | cosmac | superchip |
|------------------------------|--------|--------|-----------|
| `8XY6/8XYE` shift source     | Vx     | Vy     | Vx        |
| `BNNN` offset register       | V0     | V0     | Vx        |
| `FX55/FX65` increment I      | no     | yes    | no        |
| `8XY1/2/3` reset VF          | no     | yes    | no        |

`modern` passes every ROM listed above, including BC_test. `cosmac` matches the
original COSMAC VIP behaviour and also passes the corax opcode tests.

## Sources for the ROMs

- `1-chip8-logo` .. `8-scrolling`: Timendus CHIP-8 test suite
  (https://github.com/Timendus/chip8-test-suite, MIT).
- `test_opcode.ch8`: corax89 opcode test
  (https://github.com/corax89/chip8-test-rom).
- `BC_test.ch8`: BestCoder's opcode test (widely mirrored).

## Not in this version

SUPER-CHIP high-res mode and its extra opcodes, XO-CHIP, and save states.
The quirk plumbing and a `superChip` profile are already in place as the
starting point for that work.
