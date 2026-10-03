using System.CommandLine;
using Core;
using Xunit;

namespace Integration;

public sealed class ConversionTests
{
    private static async Task<(int Code, string Output, string Error)> RunAsync(params string[] args)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var output = new StringWriter();
        using var error = new StringWriter();
        RootCommand root = ConvertCommand.Create(http, Environment.GetEnvironmentVariable);
        int code = await root.Parse(args).InvokeAsync(new InvocationConfiguration { Output = output, Error = error });
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task ACurrencyConvertedToItselfKeepsItsAmount()
    {
        (int code, string output, _) = await RunAsync("10", "EUR", "EUR");
        Assert.Equal(0, code);
        Assert.Equal("10.00 EUR" + Environment.NewLine, output);
    }

    [Fact]
    public async Task TheOutputHasTheShapeOfAnAmountAndACode()
    {
        (int code, string output, _) = await RunAsync("10", "USD", "EUR");
        Assert.Equal(0, code);
        Assert.Matches(@"^\d+\.\d{2} EUR\r?\n$", output);
    }

    [Fact]
    public async Task ACodeTheServiceLacksFails()
    {
        (int code, string output, string error) = await RunAsync("10", "USD", "ZZZ");
        Assert.Equal(1, code);
        Assert.Equal("", output);
        Assert.Contains("lacks", error, StringComparison.Ordinal);
    }
}
