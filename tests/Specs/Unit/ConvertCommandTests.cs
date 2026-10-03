using Core;
using Xunit;

namespace Specs.Unit;

public sealed class ConvertCommandTests
{
    [Fact]
    public async Task AnOverflowingConversionIsAnErrorWithCodeOne()
    {
        using var http = new HttpClient(new AnswerHandler());
        var root = ConvertCommand.Create(new ExchangeRateClient(http), _ => "key");
        using var output = new StringWriter();
        using var error = new StringWriter();
        int code = await ConvertCommand.RunAsync(
            root.Parse(["79228162514264337593543950335", "USD", "JPY"]),
            new System.CommandLine.InvocationConfiguration { Output = output, Error = error },
            CancellationToken.None);
        Assert.Equal(1, code);
        Assert.Equal("", output.ToString());
        Assert.Contains("too large", error.ToString(), StringComparison.Ordinal);
    }

    private sealed class AnswerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage { Content = new StringContent("""{"result":"success","conversion_rate":150}""") });
    }

    [Theory]
    [InlineData("10", "0.8888", "EUR", "8.89 EUR")]
    [InlineData("2.50", "157.8014", "JPY", "394.50 JPY")]
    [InlineData("1", "0.005", "EUR", "0.01 EUR")]
    [InlineData("10", "1", "EUR", "10.00 EUR")]
    [InlineData("1234567.891", "1", "EUR", "1234567.89 EUR")]
    public void FormatRoundsToTwoDecimalsAwayFromZero(string amount, string rate, string code, string expected) =>
        Assert.Equal(expected, ConvertCommand.Format(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture), code));
}
