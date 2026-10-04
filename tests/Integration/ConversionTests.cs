using Core;
using System.CommandLine;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Integration;

public sealed class ConversionTests
{
    private static string Key =>
        Environment.GetEnvironmentVariable(CurrencyConverter.KeyVariable)
        ?? throw new InvalidOperationException($"{CurrencyConverter.KeyVariable} is not set.");

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Get(string key, string pair)
    {
        using var http = new HttpClient();
        using HttpResponseMessage response = await http.GetAsync(new Uri($"https://v6.exchangerate-api.com/v6/{key}/pair/{pair}"), TestContext.Current.CancellationToken);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    [Theory]
    [InlineData("USD/EUR", "USD", "EUR")]
    [InlineData("USD/JPY", "USD", "JPY")]
    [InlineData("usd/eur", "USD", "EUR")]
    public async Task PairAnswersWithARate(string pair, string baseCode, string targetCode)
    {
        var (status, body) = await Get(Key, pair);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("success", body.GetProperty("result").GetString());
        Assert.Equal(baseCode, body.GetProperty("base_code").GetString());
        Assert.Equal(targetCode, body.GetProperty("target_code").GetString());
        Assert.True(body.GetProperty("conversion_rate").GetDecimal() > 0);
    }

    [Fact]
    public async Task SameCurrencyHasTheRateOne()
    {
        var (status, body) = await Get(Key, "EUR/EUR");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1m, body.GetProperty("conversion_rate").GetDecimal());
    }

    [Fact]
    public async Task UnsupportedCodeAnswers404()
    {
        var (status, body) = await Get(Key, "USD/ZZZ");
        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Equal("unsupported-code", body.GetProperty("error-type").GetString());
        Assert.False(body.TryGetProperty("base_code", out _));
        Assert.False(body.TryGetProperty("target_code", out _));
    }

    [Fact]
    public async Task UnknownKeyAnswers403()
    {
        var (status, body) = await Get("unknown-key-for-test", "USD/EUR");
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.True(body.GetProperty("error-type").GetString() is "invalid-key" or "inactive-account");
    }

    [Fact]
    public async Task ProgramPrintsTheAmountForTheSameCurrency()
    {
        using var http = new HttpClient();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var configuration = new InvocationConfiguration { Output = output, Error = error };
        int code = await ConvertCommand.Create(http).Parse(["12.5", "EUR", "EUR"]).InvokeAsync(configuration, TestContext.Current.CancellationToken);
        Assert.Equal(0, code);
        Assert.Equal("12.50 EUR", output.ToString().TrimEnd());
    }

    [Fact]
    public async Task ProgramHidesAnUnknownKey()
    {
        const string key = "unknown-key-for-test";
        using var http = new HttpClient();
        using var output = new StringWriter();
        using var error = new StringWriter();
        int code = await new CurrencyConverter(http).ConvertAsync(10m, "USD", "EUR", key, output, error, TestContext.Current.CancellationToken);
        Assert.Equal(1, code);
        Assert.Equal("", output.ToString());
        Assert.NotEqual("", error.ToString());
        Assert.DoesNotContain(key, error.ToString());
    }
}
