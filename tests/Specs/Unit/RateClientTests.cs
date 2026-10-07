using Core;
using Xunit;

namespace Specs.Unit;

public sealed class RateClientTests
{
    [Theory]
    [InlineData("10", "0.8888", "8.89 EUR")]
    [InlineData("2.50", "157.8014", "394.50 EUR")]
    [InlineData("1", "0.005", "0.01 EUR")]
    [InlineData("0", "1", "0.00 EUR")]
    public void FormatAmountRoundsToTwoDecimals(string amount, string rate, string expected) =>
        Assert.Equal(expected, RateClient.FormatAmount(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture), "EUR"));

    [Fact]
    public void ParseAnswerReadsTheRate()
    {
        RateResult result = RateClient.ParseAnswer("""{"result":"success","conversion_rate":0.8888}""", "USD", "EUR");
        Assert.Equal(0.8888m, result.Rate);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ParseAnswerNamesBothCodesForUnsupportedCode()
    {
        RateResult result = RateClient.ParseAnswer("""{"result":"error","error-type":"unsupported-code"}""", "USD", "ZZZ");
        Assert.Null(result.Rate);
        Assert.Contains("USD", result.Error);
        Assert.Contains("ZZZ", result.Error);
    }

    [Fact]
    public void ParseAnswerHoldsTheErrorType()
    {
        RateResult result = RateClient.ParseAnswer("""{"result":"error","error-type":"invalid-key"}""", "USD", "EUR");
        Assert.Contains("invalid-key", result.Error);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"result":"success"}""")]
    [InlineData("""{"result":"error"}""")]
    public void ParseAnswerFailsOnAnUnreadableBody(string body)
    {
        RateResult result = RateClient.ParseAnswer(body, "USD", "EUR");
        Assert.Null(result.Rate);
        Assert.NotNull(result.Error);
    }
}
