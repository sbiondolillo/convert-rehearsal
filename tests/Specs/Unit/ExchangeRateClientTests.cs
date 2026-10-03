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

    [Fact]
    public async Task AResilienceTimeoutCountsAsNoAnswer()
    {
        using var http = new HttpClient(new ThrowingHandler());
        RateOutcome outcome = await new ExchangeRateClient(http).GetRateAsync("key", "USD", "EUR", CancellationToken.None);
        Assert.Null(outcome.Rate);
        Assert.Contains("no answer", outcome.Error, StringComparison.Ordinal);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new Polly.Timeout.TimeoutRejectedException("timeout");
    }
}
