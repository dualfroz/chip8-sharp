using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class ArithmeticTests
{
    [Fact]
    public void addImmediateWrapsWithoutTouchingFlag()
    {
        var machine = TestHarness.withOpcode(0x70FF);
        machine.v[0] = 0x05;
        machine.v[0xF] = 0xAB;
        machine.step();
        Assert.Equal(0x04, machine.v[0]);
        Assert.Equal(0xAB, machine.v[0xF]);
    }

    [Fact]
    public void moveCopiesVyIntoVx()
    {
        var machine = TestHarness.withOpcode(0x8010);
        machine.v[1] = 0x42;
        machine.step();
        Assert.Equal(0x42, machine.v[0]);
    }

    [Fact]
    public void orAndXorProduceExpectedBits()
    {
        var or = TestHarness.withOpcode(0x8011);
        or.v[0] = 0b1100; or.v[1] = 0b1010; or.step();
        Assert.Equal(0b1110, or.v[0]);

        var and = TestHarness.withOpcode(0x8012);
        and.v[0] = 0b1100; and.v[1] = 0b1010; and.step();
        Assert.Equal(0b1000, and.v[0]);

        var xor = TestHarness.withOpcode(0x8013);
        xor.v[0] = 0b1100; xor.v[1] = 0b1010; xor.step();
        Assert.Equal(0b0110, xor.v[0]);
    }

    [Fact]
    public void logicLeavesFlagUntouchedOnModernButClearsOnCosmac()
    {
        var modern = TestHarness.withOpcode(0x8011, Quirks.modern());
        modern.v[0] = 1; modern.v[1] = 2; modern.v[0xF] = 0x7;
        modern.step();
        Assert.Equal(0x7, modern.v[0xF]);

        var cosmac = TestHarness.withOpcode(0x8011, Quirks.cosmacVip());
        cosmac.v[0] = 1; cosmac.v[1] = 2; cosmac.v[0xF] = 0x7;
        cosmac.step();
        Assert.Equal(0, cosmac.v[0xF]);
    }

    [Fact]
    public void addSetsCarryFlag()
    {
        var overflow = TestHarness.withOpcode(0x8014);
        overflow.v[0] = 200; overflow.v[1] = 100;
        overflow.step();
        Assert.Equal(44, overflow.v[0]);
        Assert.Equal(1, overflow.v[0xF]);

        var noOverflow = TestHarness.withOpcode(0x8014);
        noOverflow.v[0] = 20; noOverflow.v[1] = 30;
        noOverflow.step();
        Assert.Equal(50, noOverflow.v[0]);
        Assert.Equal(0, noOverflow.v[0xF]);
    }

    [Fact]
    public void addIntoVfKeepsCarryNotSum()
    {
        var machine = TestHarness.withOpcode(0x8F14);
        machine.v[0xF] = 200; machine.v[1] = 100;
        machine.step();
        Assert.Equal(1, machine.v[0xF]);
    }

    [Fact]
    public void subtractSetsNotBorrowFlag()
    {
        var positive = TestHarness.withOpcode(0x8015);
        positive.v[0] = 50; positive.v[1] = 20;
        positive.step();
        Assert.Equal(30, positive.v[0]);
        Assert.Equal(1, positive.v[0xF]);

        var borrow = TestHarness.withOpcode(0x8015);
        borrow.v[0] = 20; borrow.v[1] = 50;
        borrow.step();
        Assert.Equal(226, borrow.v[0]);
        Assert.Equal(0, borrow.v[0xF]);
    }

    [Fact]
    public void subtractReverseUsesVyMinusVx()
    {
        var machine = TestHarness.withOpcode(0x8017);
        machine.v[0] = 20; machine.v[1] = 50;
        machine.step();
        Assert.Equal(30, machine.v[0]);
        Assert.Equal(1, machine.v[0xF]);
    }

    [Fact]
    public void shiftRightUsesConfiguredSource()
    {
        var modern = TestHarness.withOpcode(0x8016, Quirks.modern());
        modern.v[0] = 0b0000_0101; modern.v[1] = 0xFF;
        modern.step();
        Assert.Equal(0b0000_0010, modern.v[0]);
        Assert.Equal(1, modern.v[0xF]);

        var cosmac = TestHarness.withOpcode(0x8016, Quirks.cosmacVip());
        cosmac.v[0] = 0x00; cosmac.v[1] = 0b0000_0101;
        cosmac.step();
        Assert.Equal(0b0000_0010, cosmac.v[0]);
        Assert.Equal(1, cosmac.v[0xF]);
    }

    [Fact]
    public void shiftLeftUsesConfiguredSource()
    {
        var modern = TestHarness.withOpcode(0x801E, Quirks.modern());
        modern.v[0] = 0b1000_0001;
        modern.step();
        Assert.Equal(0b0000_0010, modern.v[0]);
        Assert.Equal(1, modern.v[0xF]);

        var cosmac = TestHarness.withOpcode(0x801E, Quirks.cosmacVip());
        cosmac.v[0] = 0x00; cosmac.v[1] = 0b1000_0001;
        cosmac.step();
        Assert.Equal(0b0000_0010, cosmac.v[0]);
        Assert.Equal(1, cosmac.v[0xF]);
    }
}
