using System.Net;
using Xunit;

namespace Specs.Unit;

public sealed class ConvertCommandTests
{
    [Theory]
    [InlineData("10", "USD", "EUR", "0.8888", "8.89 EUR")]
    [InlineData("2.50", "USD", "JPY", "157.8014", "394.50 JPY")]
    [InlineData("10", "usd", "eur", "0.8888", "8.89 EUR")]
    [InlineData("10", "EUR", "EUR", "1", "10.00 EUR")]
    [InlineData("0.005", "USD", "EUR", "1", "0.01 EUR")]
    public async Task PrintsTheConvertedAmount(string amount, string from, string to, string rate, string expected)
    {
        var handler = StubHandler.Rate(rate);
        RunResult r = await Runner.RunAsync(handler, "k", amount, from, to);
        Assert.Equal(0, r.ExitCode);
        Assert.Equal(expected + Environment.NewLine, r.Output);
        Assert.Equal("", r.Error);
    }

    [Fact]
    public async Task SendsThePairRequestWithTheKey()
    {
        var handler = StubHandler.Rate("1");
        await Runner.RunAsync(handler, "test-key", "10", "usd", "EUR");
        Assert.Equal(["GET https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR"], handler.Requests);
    }

    [Theory]
    [InlineData("abc", "USD", "EUR")]
    [InlineData("10", "U5D", "EUR")]
    [InlineData("1,5", "USD", "EUR")]
    public async Task BadArgumentsSendNoRequest(string amount, string from, string to)
    {
        var handler = StubHandler.Rate("1");
        RunResult r = await Runner.RunAsync(handler, "k", amount, from, to);
        Assert.Empty(handler.Requests);
        Assert.NotEqual(0, r.ExitCode);
        Assert.NotEmpty(r.Error);
        Assert.Equal("", r.Output);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AMissingKeySendsNoRequest(string? key)
    {
        var handler = StubHandler.Rate("1");
        RunResult r = await Runner.RunAsync(handler, key, "10", "USD", "EUR");
        Assert.Empty(handler.Requests);
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("EXCHANGERATE_API_KEY", r.Error, StringComparison.Ordinal);
        Assert.Equal("", r.Output);
    }

    [Fact]
    public async Task UnsupportedCodeNamesBothCodes()
    {
        var handler = StubHandler.Json(HttpStatusCode.NotFound, "{\"result\":\"error\",\"error-type\":\"unsupported-code\"}");
        RunResult r = await Runner.RunAsync(handler, "secret", "10", "usd", "zzz");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("USD", r.Error, StringComparison.Ordinal);
        Assert.Contains("ZZZ", r.Error, StringComparison.Ordinal);
        Assert.Equal("", r.Output);
    }

    [Theory]
    [InlineData("invalid-key")]
    [InlineData("inactive-account")]
    public async Task OtherErrorsShowTheErrorType(string errorType)
    {
        var handler = StubHandler.Json(HttpStatusCode.Forbidden, $"{{\"result\":\"error\",\"error-type\":\"{errorType}\"}}");
        RunResult r = await Runner.RunAsync(handler, "secret", "10", "USD", "EUR");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains(errorType, r.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", r.Error + r.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnErrorTypeThatEchoesTheKeyIsScrubbed()
    {
        var handler = StubHandler.Json(HttpStatusCode.Forbidden, "{\"result\":\"error\",\"error-type\":\"secret\"}");
        RunResult r = await Runner.RunAsync(handler, "secret", "10", "USD", "EUR");
        Assert.DoesNotContain("secret", r.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoAnswerIsReportedWithoutTheKey()
    {
        RunResult r = await Runner.RunAsync(StubHandler.Throwing(), "secret", "10", "USD", "EUR");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("no answer", r.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", r.Error + r.Output, StringComparison.Ordinal);
        Assert.Equal("", r.Output);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "{\"result\":\"success\"}")]
    [InlineData(HttpStatusCode.OK, "{\"result\":\"success\",\"conversion_rate\":0}")]
    [InlineData(HttpStatusCode.BadGateway, "<html>")]
    public async Task AnUnusableAnswerIsAFailure(HttpStatusCode status, string body)
    {
        RunResult r = await Runner.RunAsync(StubHandler.Json(status, body), "k", "10", "USD", "EUR");
        Assert.Equal(1, r.ExitCode);
        Assert.NotEmpty(r.Error);
        Assert.Equal("", r.Output);
    }

    [Fact]
    public async Task AnOverflowingProductIsAControlledFailure()
    {
        string max = decimal.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RunResult r = await Runner.RunAsync(StubHandler.Rate("2"), "k", max, "USD", "EUR");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("too large", r.Error, StringComparison.Ordinal);
        Assert.Equal("", r.Output);
    }

    [Fact]
    public async Task AnHttpErrorWithAnUnusableBodyIsNotReportedAsNoAnswer()
    {
        RunResult r = await Runner.RunAsync(StubHandler.Json(HttpStatusCode.BadGateway, "<html>"), "k", "10", "USD", "EUR");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("does not understand", r.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("no answer", r.Error, StringComparison.Ordinal);
    }
}
