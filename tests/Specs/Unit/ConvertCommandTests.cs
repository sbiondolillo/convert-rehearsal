using Core;
using Xunit;

namespace Specs.Unit;

public sealed class ConvertCommandTests
{
    [Theory]
    [InlineData("10", "0.8888", "EUR", "8.89 EUR")]
    [InlineData("2.50", "157.8014", "JPY", "394.50 JPY")]
    [InlineData("1", "0.005", "EUR", "0.01 EUR")]
    [InlineData("10", "1", "EUR", "10.00 EUR")]
    [InlineData("1234567.891", "1", "EUR", "1234567.89 EUR")]
    public void FormatRoundsToTwoDecimalsAwayFromZero(string amount, string rate, string code, string expected) =>
        Assert.Equal(expected, ConvertCommand.Format(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture), code));
}
