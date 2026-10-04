using System.Net;
using System.Text.Json;
using Core;
using Xunit;

namespace Integration;

public sealed class ExchangeRateTests
{
    private static readonly HttpClient Http = new();
    private static readonly string[] KeyErrors = ["invalid-key", "inactive-account"];

    private static string Key =>
        Environment.GetEnvironmentVariable("EXCHANGERATE_API_KEY") is { Length: > 0 } key
            ? key
            : throw new InvalidOperationException("EXCHANGERATE_API_KEY is not set.");

    private static async Task<(HttpStatusCode Status, string Body, JsonElement Json)> GetAsync(string key, string path)
    {
        using HttpResponseMessage response = await Http.GetAsync(new Uri($"https://v6.exchangerate-api.com/v6/{key}{path}"), TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, body, JsonDocument.Parse(body).RootElement.Clone());
    }

    [Fact]
    public async Task PairAnswersTheRate()
    {
        var (status, _, json) = await GetAsync(Key, "/pair/USD/EUR");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("success", json.GetProperty("result").GetString());
        Assert.Equal("USD", json.GetProperty("base_code").GetString());
        Assert.Equal("EUR", json.GetProperty("target_code").GetString());
        Assert.Equal(JsonValueKind.Number, json.GetProperty("conversion_rate").ValueKind);
    }

    [Fact]
    public async Task PairOfOneCurrencyHasTheRateOne()
    {
        var (_, _, json) = await GetAsync(Key, "/pair/EUR/EUR");
        Assert.Equal(1m, json.GetProperty("conversion_rate").GetDecimal());
    }

    [Fact]
    public async Task PairTakesLowerCaseCodes()
    {
        var (_, _, json) = await GetAsync(Key, "/pair/usd/eur");
        Assert.Equal("USD", json.GetProperty("base_code").GetString());
        Assert.Equal("EUR", json.GetProperty("target_code").GetString());
    }

    [Fact]
    public async Task PairWithAnUnsupportedCodeIsAnError()
    {
        var (status, body, json) = await GetAsync(Key, "/pair/USD/ZZZ");
        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("error", json.GetProperty("result").GetString());
        Assert.Equal("unsupported-code", json.GetProperty("error-type").GetString());
        Assert.DoesNotContain("USD", body);
        Assert.DoesNotContain("ZZZ", body);
    }

    [Fact]
    public async Task PairWithAnUnknownKeyIsAnError()
    {
        var (status, _, json) = await GetAsync("unknown-key", "/pair/USD/EUR");
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("error", json.GetProperty("result").GetString());
        Assert.Contains(json.GetProperty("error-type").GetString(), KeyErrors);
    }

    [Fact]
    public async Task ProgramConvertsOneCurrencyToItself()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await ConvertCommand.RunAsync(["10", "EUR", "EUR"], Http, new System.CommandLine.InvocationConfiguration { Output = output, Error = error }, TestContext.Current.CancellationToken);
        Assert.Equal(0, exitCode);
        Assert.Equal("10.00 EUR", output.ToString().Trim());
    }
}
