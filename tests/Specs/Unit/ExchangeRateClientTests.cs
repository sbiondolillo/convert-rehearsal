using System.Net;
using Core;
using Specs.Steps;
using Xunit;

namespace Specs.Unit;

public sealed class ExchangeRateClientTests
{
    [Fact]
    public void ParseReadsTheRateOfASuccess()
    {
        RateResult result = ExchangeRateClient.Parse("""{"result":"success","conversion_rate":0.8888}""", "USD", "EUR");
        Assert.Equal(0.8888m, result.Rate);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ParseNamesBothCodesOfAnUnsupportedCode()
    {
        RateResult result = ExchangeRateClient.Parse("""{"result":"error","error-type":"unsupported-code"}""", "USD", "ZZZ");
        Assert.Null(result.Rate);
        Assert.Contains("USD", result.Error);
        Assert.Contains("ZZZ", result.Error);
    }

    [Fact]
    public void ParseHoldsTheErrorTypeOfAnotherError()
    {
        RateResult result = ExchangeRateClient.Parse("""{"result":"error","error-type":"invalid-key"}""", "USD", "EUR");
        Assert.Contains("invalid-key", result.Error);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"result":"success"}""")]
    public void ParseTakesAnAnswerWithoutARateForNoAnswer(string body) =>
        Assert.Contains("gave no answer", ExchangeRateClient.Parse(body, "USD", "EUR").Error);

    [Fact]
    public async Task GetRateRequestsThePairAndHidesTheKeyWhenTheRequestFails()
    {
        using var handler = new StubHandler { Status = HttpStatusCode.OK };
        using var http = new HttpClient(handler, disposeHandler: false);
        RateResult result = await new ExchangeRateClient(http).GetRateAsync("secret", "USD", "EUR", TestContext.Current.CancellationToken);
        Assert.Equal(["GET https://v6.exchangerate-api.com/v6/secret/pair/USD/EUR"], handler.Requests);
        Assert.DoesNotContain("secret", result.Error);
    }
}
