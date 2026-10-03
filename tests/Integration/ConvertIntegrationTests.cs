using Core;
using Xunit;

namespace Integration;

public sealed class ConvertIntegrationTests
{
    private static async Task<(int Code, string Out, string Err)> RunAsync(string[] args, string? key)
    {
        using var http = new HttpClient();
        var output = new StringWriter();
        var error = new StringWriter();
        int code = await ConvertCommand.RunAsync(args, _ => key, http, output, error, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    private static string Key => Environment.GetEnvironmentVariable(ConvertCommand.KeyVariable)
        ?? throw new InvalidOperationException($"{ConvertCommand.KeyVariable} is not set.");

    [Fact]
    public async Task ACurrencyConvertsToItselfUnchanged()
    {
        var r = await RunAsync(["12.50", "EUR", "EUR"], Key);
        Assert.Equal(0, r.Code);
        Assert.Equal("12.50 EUR", r.Out.Trim());
    }

    [Fact]
    public async Task AnUnsupportedCodeIsReported()
    {
        var r = await RunAsync(["10", "USD", "ZZZ"], Key);
        Assert.Equal(1, r.Code);
        Assert.Contains("USD", r.Err, StringComparison.Ordinal);
        Assert.Contains("ZZZ", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownKeyIsReportedWithoutItsText()
    {
        const string bad = "not-a-real-key-0123456789";
        var r = await RunAsync(["10", "USD", "EUR"], bad);
        Assert.Equal(1, r.Code);
        Assert.True(r.Err.Contains("invalid-key", StringComparison.Ordinal) || r.Err.Contains("inactive-account", StringComparison.Ordinal));
        Assert.DoesNotContain(bad, r.Err, StringComparison.Ordinal);
        Assert.Equal("", r.Out);
    }
}
