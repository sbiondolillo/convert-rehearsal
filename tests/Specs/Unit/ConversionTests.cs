using Core;
using Xunit;

namespace Specs.Unit;

public sealed class ConversionTests
{
    [Theory]
    [InlineData("10", "0.8888", "EUR", "8.89 EUR")]
    [InlineData("2.50", "157.8014", "jpy", "394.50 JPY")]
    [InlineData("10", "1", "EUR", "10.00 EUR")]
    [InlineData("1", "0.005", "EUR", "0.01 EUR")]
    public void FormatsTheRoundedAmountWithTheUpperCaseCode(string amount, string rate, string code, string expected) =>
        Assert.Equal(expected, Conversion.Format(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture), code));

    [Fact]
    public void RoundsAMidpointAwayFromZero()
    {
        Assert.Equal(0.01m, Conversion.Convert(1m, 0.005m));
        Assert.Equal(-0.01m, Conversion.Convert(-1m, 0.005m));
    }
}
