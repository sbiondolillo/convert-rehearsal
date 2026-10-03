using System.Net;
using Core;
using Specs.Steps;
using Xunit;

namespace Specs.Unit;

public sealed class ConvertCommandTests
{
    private static async Task<(int Code, string Out, string Err, StubHandler Handler)> RunAsync(string[] args, string? key, Action<StubHandler> setup)
    {
        var handler = new StubHandler();
        setup(handler);
        using var http = new HttpClient(handler);
        var output = new StringWriter();
        var error = new StringWriter();
        int code = await ConvertCommand.RunAsync(args, _ => key, http, output, error);
        return (code, output.ToString(), error.ToString(), handler);
    }

    [Theory]
    [InlineData(0.8888, "8.89")]
    [InlineData(1, "10.00")]
    public void FormatRoundsToTwoDecimals(decimal rate, string expected) =>
        Assert.Equal(expected, RateClient.Format(10 * rate));

    [Fact]
    public async Task WrongArgumentCountIsRefused()
    {
        var r = await RunAsync(["10", "USD"], "k", h => h.Respond(HttpStatusCode.OK, "{}"));
        Assert.Equal(2, r.Code);
        Assert.Empty(r.Handler.Requests);
        Assert.Equal("", r.Out);
    }

    [Fact]
    public async Task BadCodeIsRefused()
    {
        var r = await RunAsync(["10", "US1", "EUR"], "k", h => h.Respond(HttpStatusCode.OK, "{}"));
        Assert.Equal(2, r.Code);
        Assert.Empty(r.Handler.Requests);
    }

    [Fact]
    public async Task LowerCaseCodesAreSentInUpperCase()
    {
        var r = await RunAsync(["10", "usd", "eur"], "k", h => h.Respond(HttpStatusCode.OK, """{"result":"success","conversion_rate":0.8888}"""));
        Assert.Equal(["GET https://v6.exchangerate-api.com/v6/k/pair/USD/EUR"], r.Handler.Requests);
        Assert.Equal("8.89 EUR", r.Out.Trim());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"result":"success"}""")]
    [InlineData("[]")]
    public async Task UnusableAnswerIsReportedWithoutTheKey(string body)
    {
        var r = await RunAsync(["10", "USD", "EUR"], "secret", h => h.Respond(HttpStatusCode.OK, body));
        Assert.Equal(1, r.Code);
        Assert.Equal("", r.Out);
        Assert.NotEqual("", r.Err.Trim());
        Assert.DoesNotContain("secret", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OverflowingAmountIsReportedWithExitCodeTwo()
    {
        var r = await RunAsync(["79228162514264337593543950335", "USD", "EUR"], "secret", h => h.Respond(HttpStatusCode.OK, """{"result":"success","conversion_rate":2}"""));
        Assert.Equal(2, r.Code);
        Assert.Equal("", r.Out);
        Assert.Contains("too large", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErrorAnswerWithoutErrorTypeIsReported()
    {
        var r = await RunAsync(["10", "USD", "EUR"], "secret", h => h.Respond(HttpStatusCode.Forbidden, """{"result":"error"}"""));
        Assert.Equal(1, r.Code);
        Assert.Contains("unknown", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreachableServiceDoesNotLeakTheKey()
    {
        var r = await RunAsync(["10", "USD", "EUR"], "secret", h => h.Fail());
        Assert.Equal(1, r.Code);
        Assert.Contains("gave no answer", r.Err, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", r.Err, StringComparison.Ordinal);
    }
}
