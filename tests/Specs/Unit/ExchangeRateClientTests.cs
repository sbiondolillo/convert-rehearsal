using System.Net;
using Core;
using Xunit;

namespace Specs.Unit;

public sealed class ExchangeRateClientTests
{
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private static Task<RateResult> Get(HttpStatusCode status, string body)
    {
        using var http = new HttpClient(new Handler(status, body));
        return new ExchangeRateClient(http).GetRateAsync("k", "USD", "EUR", CancellationToken.None);
    }

    [Fact]
    public async Task ReadsTheRate()
    {
        RateResult result = await Get(HttpStatusCode.OK, """{"result":"success","conversion_rate":0.8888}""");
        Assert.Equal(0.8888m, result.Rate);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task NamesBothCodesOnUnsupportedCode()
    {
        RateResult result = await Get(HttpStatusCode.NotFound, """{"result":"error","error-type":"unsupported-code"}""");
        Assert.Null(result.Rate);
        Assert.Contains("USD", result.Error);
        Assert.Contains("EUR", result.Error);
    }

    [Fact]
    public async Task HoldsTheOtherErrorType()
    {
        RateResult result = await Get(HttpStatusCode.Forbidden, """{"result":"error","error-type":"invalid-key"}""");
        Assert.Contains("invalid-key", result.Error);
        Assert.DoesNotContain("k/", result.Error);
    }

    [Fact]
    public async Task GivesNoAnswerOnABodyThatIsNotJson()
    {
        RateResult result = await Get(HttpStatusCode.OK, "<html>");
        Assert.Contains("no answer", result.Error);
    }

    [Theory]
    [InlineData("10", "0.8888", "8.89 EUR")]
    [InlineData("2.50", "157.8014", "394.50 EUR")]
    [InlineData("1", "0.125", "0.13 EUR")]
    public void FormatsRoundingAwayFromZero(string amount, string rate, string expected) =>
        Assert.Equal(expected, ExchangeRateClient.Format(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture), "EUR"));
}
