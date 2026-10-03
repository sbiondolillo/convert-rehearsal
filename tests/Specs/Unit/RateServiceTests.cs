using Core;
using System.Net;
using System.Text;
using Xunit;

namespace Specs.Unit;

public sealed class RateServiceTests
{
    private static async Task<(Conversion Result, List<string> Requests)> Run(decimal amount, string from, string to, Func<HttpResponseMessage> answer)
    {
        var requests = new List<string>();
        using var http = new HttpClient(new StubHandler(requests, answer));
        Conversion result = await new RateService(http).ConvertAsync(amount, from, to, "secret", CancellationToken.None);
        return (result, requests);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Theory]
    [InlineData("10", "0.8888", "8.89 EUR")]
    [InlineData("2.50", "157.8014", "394.50 JPY")]
    [InlineData("1", "1", "1.00 EUR")]
    [InlineData("0.5", "0.01", "0.01 EUR")]
    public async Task RoundsToTwoDecimals(string amount, string rate, string expected)
    {
        string code = expected[^3..];
        (Conversion result, _) = await Run(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), "usd", code,
            () => Json(HttpStatusCode.OK, $$"""{"result":"success","conversion_rate":{{rate}}}"""));
        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.Message);
    }

    [Fact]
    public async Task UpperCasesTheCodesInTheRequest()
    {
        (_, List<string> requests) = await Run(1, "usd", "eur", () => Json(HttpStatusCode.OK, """{"result":"success","conversion_rate":2}"""));
        Assert.Equal(["GET https://v6.exchangerate-api.com/v6/secret/pair/USD/EUR"], requests);
    }

    [Fact]
    public async Task NamesBothCodesOnUnsupportedCode()
    {
        (Conversion result, _) = await Run(1, "usd", "zzz", () => Json(HttpStatusCode.NotFound, """{"result":"error","error-type":"unsupported-code"}"""));
        Assert.False(result.Succeeded);
        Assert.Contains("USD", result.Message);
        Assert.Contains("ZZZ", result.Message);
    }

    [Fact]
    public async Task HoldsTheErrorTypeOfAnotherError()
    {
        (Conversion result, _) = await Run(1, "USD", "EUR", () => Json(HttpStatusCode.Forbidden, """{"result":"error","error-type":"invalid-key"}"""));
        Assert.False(result.Succeeded);
        Assert.Contains("invalid-key", result.Message);
        Assert.DoesNotContain("secret", result.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"result":"success"}""")]
    [InlineData("""{"result":"error"}""")]
    public async Task GivesNoAnswerOnAnUnreadableBody(string body)
    {
        (Conversion result, _) = await Run(1, "USD", "EUR", () => Json(HttpStatusCode.OK, body));
        Assert.False(result.Succeeded);
        Assert.Contains("no answer", result.Message);
    }

    [Fact]
    public async Task GivesNoAnswerWithoutLeakingTheKey()
    {
        (Conversion result, _) = await Run(1, "USD", "EUR", () => throw new HttpRequestException("failed https://v6.exchangerate-api.com/v6/secret/pair/USD/EUR"));
        Assert.False(result.Succeeded);
        Assert.Contains("no answer", result.Message);
        Assert.DoesNotContain("secret", result.Message);
    }
}
