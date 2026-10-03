using Core;
using Xunit;

namespace Specs.Unit;

public sealed class ExchangeRateClientTests
{
    [Fact]
    public void ASuccessAnswerGivesTheRate()
    {
        RateOutcome outcome = ExchangeRateClient.ParseAnswer("""{"result":"success","conversion_rate":0.8888}""", "USD", "EUR");
        Assert.Equal(0.8888m, outcome.Rate);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public void AnUnsupportedCodeNamesBothCodes()
    {
        RateOutcome outcome = ExchangeRateClient.ParseAnswer("""{"result":"error","error-type":"unsupported-code"}""", "USD", "ZZZ");
        Assert.Null(outcome.Rate);
        Assert.Contains("USD", outcome.Error, StringComparison.Ordinal);
        Assert.Contains("ZZZ", outcome.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherErrorHoldsItsErrorType()
    {
        RateOutcome outcome = ExchangeRateClient.ParseAnswer("""{"result":"error","error-type":"invalid-key"}""", "USD", "EUR");
        Assert.Contains("invalid-key", outcome.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"result":"success"}""")]
    [InlineData("""{"result":"error"}""")]
    public void AnAnswerThatIsNotReadableCountsAsNoAnswer(string body)
    {
        RateOutcome outcome = ExchangeRateClient.ParseAnswer(body, "USD", "EUR");
        Assert.Null(outcome.Rate);
        Assert.Contains("no answer", outcome.Error, StringComparison.Ordinal);
    }
}
