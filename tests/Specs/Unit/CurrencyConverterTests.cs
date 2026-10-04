using System.Globalization;
using Core;
using Xunit;

namespace Specs.Unit;

public sealed class CurrencyConverterTests
{
    [Theory]
    [InlineData("8.888", "8.89")]
    [InlineData("394.5035", "394.50")]
    [InlineData("2.125", "2.13")]
    [InlineData("-2.125", "-2.13")]
    [InlineData("1", "1.00")]
    public void FormatAmountRoundsToTwoDecimals(string amount, string expected) =>
        Assert.Equal(expected, CurrencyConverter.FormatAmount(decimal.Parse(amount, CultureInfo.InvariantCulture)));

    [Fact]
    public void FormatAmountIgnoresTheCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("8.89", CurrencyConverter.FormatAmount(8.888m));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ParseMultipliesByTheRate()
    {
        ConversionResult result = CurrencyConverter.Parse("""{"result":"success","conversion_rate":157.8014}""", 2.50m, "USD", "JPY");
        Assert.Equal(394.5035m, result.Amount);
    }

    [Fact]
    public void ParseNamesBothCodesOnUnsupportedCode()
    {
        ConversionResult result = CurrencyConverter.Parse("""{"result":"error","error-type":"unsupported-code"}""", 1m, "USD", "ZZZ");
        Assert.Contains("USD", result.Error);
        Assert.Contains("ZZZ", result.Error);
    }

    [Fact]
    public void ParseHoldsTheErrorType()
    {
        ConversionResult result = CurrencyConverter.Parse("""{"result":"error","error-type":"invalid-key"}""", 1m, "USD", "EUR");
        Assert.Contains("invalid-key", result.Error);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"result":"success"}""")]
    [InlineData("""{"result":"other"}""")]
    public void ParseFailsOnAnUnreadableAnswer(string body) =>
        Assert.Null(CurrencyConverter.Parse(body, 1m, "USD", "EUR").Amount);
}
