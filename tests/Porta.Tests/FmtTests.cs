using Porta.Services;
using Xunit;

namespace Porta.Tests;

public class FmtTests
{
    [Theory]
    [InlineData(15250000, false, "Rp 15.250.000")]
    [InlineData(9747.93, false, "Rp 9.747,93")]
    [InlineData(25000, true, "+Rp 25.000")]
    [InlineData(-10000, true, "-Rp 10.000")]
    [InlineData(0, true, "Rp 0")]
    public void Money_UsesIndonesianSeparators(decimal value, bool signed, string expected)
    {
        Assert.Equal(expected, Fmt.Money(value, signed));
    }

    [Theory]
    [InlineData("9800", 9800)]
    [InlineData("9.800.000", 9800000)]
    [InlineData("0,025", 0.025)]
    [InlineData("0.025", 0.025)]
    [InlineData("Rp 1.500,50", 1500.50)]
    public void TryParse_AcceptsCommonInputs(string text, decimal expected)
    {
        Assert.True(Fmt.TryParse(text, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void TryParse_RejectsInvalid(string text)
    {
        Assert.False(Fmt.TryParse(text, out _));
    }
}
