using Core;
using Xunit;

namespace Specs.Unit;

public sealed class ConversionTests
{
    [Theory]
    [InlineData("10", "0.8888", "eur", "8.89 EUR")]
    [InlineData("2.50", "157.8014", "JPY", "394.50 JPY")]
    [InlineData("1", "1.005", "usd", "1.01 USD")]
    [InlineData("5", "1", "eur", "5.00 EUR")]
    public void FormatRoundsToTwoDecimalsAndUpperCasesTheCode(string amount, string rate, string code, string expected) =>
        Assert.Equal(expected, Conversion.Format(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture), code));
}
