# Test ROMs

The ROMs in this directory are used by the test suites and by `chip8 conformance`.
They are third-party works distributed unmodified under their own licenses, not under
the MIT license of this repository. Each file below is byte-identical to the upstream
build linked in its row.

## Timendus CHIP-8 test suite

Source: https://github.com/Timendus/chip8-test-suite (files from `bin/`; the Octo
sources are in `src/tests/` of the same repository).
Author: Tim Franssen. License: GNU General Public License v3.0, full text in
[LICENSE-GPL-3.0.txt](LICENSE-GPL-3.0.txt). The corresponding source code is available
from the upstream repository linked above.

| File | Test |
|------|------|
| `1-chip8-logo.ch8` | CHIP-8 splash screen |
| `2-ibm-logo.ch8` | IBM logo |
| `3-corax+.ch8` | Corax+ opcode test (extended version of corax89's test) |
| `4-flags.ch8` | VF flag behaviour of the arithmetic opcodes |
| `5-quirks.ch8` | Quirk detection (interactive) |
| `6-keypad.ch8` | Keypad input (interactive) |
| `7-beep.ch8` | Sound timer |
| `8-scrolling.ch8` | SUPER-CHIP / XO-CHIP scrolling |

## corax89 opcode test

Source: https://github.com/corax89/chip8-test-rom
Author: corax89. License: MIT, full text in
[LICENSE-corax89-MIT.txt](LICENSE-corax89-MIT.txt).

| File | Test |
|------|------|
| `test_opcode.ch8` | Opcode test grid |

## Project sources

`selfcheck.asm` is part of this repository (MIT). It is assembled at test time with the
project's own assembler (`Chip8.Core.Assembly.Assembler`) and runs 17 checks covering
8XY4/8XY5/8XY7 carries and borrows, shifts, logic ops, 7XNN without carry, FX33, FX55/FX65,
CALL/RET, FX1E, BNNN and 9XY0. It draws the number of passed checks as two decimal digits.
