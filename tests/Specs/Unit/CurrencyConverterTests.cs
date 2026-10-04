using Core;
using System.Net;
using Xunit;

namespace Specs.Unit;

public sealed class CurrencyConverterTests
{
    private static async Task<(int Code, string Out, string Err, StubHandler Handler)> Run(
        decimal amount, string from, string to, Func<HttpRequestMessage, HttpResponseMessage> answer, string? key = "k")
    {
        var handler = new StubHandler(answer);
        using var http = new HttpClient(handler);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int code = await new CurrencyConverter(http).ConvertAsync(amount, from, to, key, output, error, CancellationToken.None);
        return (code, output.ToString(), error.ToString(), handler);
    }

    [Theory]
    [InlineData("10", "0.8888", "8.89 EUR")]
    [InlineData("2.50", "157.8014", "394.50 EUR")]
    [InlineData("1", "0.005", "0.01 EUR")]
    [InlineData("10", "1", "10.00 EUR")]
    public async Task RoundsAwayFromZeroToTwoDecimals(string amount, string rate, string expected)
    {
        var run = await Run(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), "USD", "EUR", _ => StubHandler.Rate(rate));
        Assert.Equal(0, run.Code);
        Assert.Equal(expected + Environment.NewLine, run.Out);
    }

    [Fact]
    public async Task UpperCasesTheCodes()
    {
        var run = await Run(1m, "usd", "eur", _ => StubHandler.Rate("2"));
        Assert.Equal(["GET https://v6.exchangerate-api.com/v6/k/pair/USD/EUR"], run.Handler.Requests);
    }

    [Fact]
    public async Task UnsetKeySendsNothing()
    {
        var run = await Run(1m, "USD", "EUR", _ => StubHandler.Rate("2"), key: null);
        Assert.Equal(1, run.Code);
        Assert.Empty(run.Handler.Requests);
        Assert.Contains(CurrencyConverter.KeyVariable, run.Err);
    }

    [Fact]
    public async Task UnsupportedCodeNamesBothCodes()
    {
        var run = await Run(1m, "usd", "zzz", _ => StubHandler.Error("unsupported-code"));
        Assert.Equal(1, run.Code);
        Assert.Contains("USD", run.Err);
        Assert.Contains("ZZZ", run.Err);
    }

    [Fact]
    public async Task OtherErrorHoldsTheErrorType()
    {
        var run = await Run(1m, "USD", "EUR", _ => StubHandler.Error("invalid-key"), key: "secret");
        Assert.Equal(1, run.Code);
        Assert.Contains("invalid-key", run.Err);
        Assert.DoesNotContain("secret", run.Err);
        Assert.Equal("", run.Out);
    }

    [Fact]
    public async Task NoAnswerHidesTheKey()
    {
        var run = await Run(1m, "USD", "EUR", r => throw new HttpRequestException(r.RequestUri!.ToString()), key: "secret");
        Assert.Equal(1, run.Code);
        Assert.Contains("gave no answer", run.Err);
        Assert.DoesNotContain("secret", run.Err);
    }

    [Fact]
    public async Task UnreadableAnswerFails()
    {
        var run = await Run(1m, "USD", "EUR", _ => StubHandler.Json(HttpStatusCode.OK, "<html>"));
        Assert.Equal(1, run.Code);
        Assert.Equal("", run.Out);
    }
}
