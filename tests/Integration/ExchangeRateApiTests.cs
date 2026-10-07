using Core;
using System.CommandLine;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Integration;

public sealed class ExchangeRateApiTests
{
    private static readonly HttpClient Http = new();

    private static string Key => Environment.GetEnvironmentVariable(ConvertCommand.KeyVariable)
        is { Length: > 0 } key ? key : throw new InvalidOperationException($"{ConvertCommand.KeyVariable} is not set.");

    private static async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(string key, string path)
    {
        using HttpResponseMessage response = await Http.GetAsync(new Uri($"https://v6.exchangerate-api.com/v6/{key}{path}"), TestContext.Current.CancellationToken);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (response.StatusCode, body);
    }

    [Theory]
    [InlineData("/pair/USD/EUR", "USD", "EUR")]
    [InlineData("/pair/USD/JPY", "USD", "JPY")]
    [InlineData("/pair/usd/eur", "USD", "EUR")]
    public async Task PairAnswersTheRate(string path, string baseCode, string targetCode)
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync(Key, path);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("success", body.GetProperty("result").GetString());
        Assert.Equal(baseCode, body.GetProperty("base_code").GetString());
        Assert.Equal(targetCode, body.GetProperty("target_code").GetString());
        Assert.Equal(JsonValueKind.Number, body.GetProperty("conversion_rate").ValueKind);
    }

    [Fact]
    public async Task PairOfOneCurrencyHasTheRateOne()
    {
        (_, JsonElement body) = await GetAsync(Key, "/pair/EUR/EUR");
        Assert.Equal(1m, body.GetProperty("conversion_rate").GetDecimal());
    }

    [Fact]
    public async Task UnsupportedCodeAnswers404()
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync(Key, "/pair/USD/ZZZ");
        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Equal("unsupported-code", body.GetProperty("error-type").GetString());
    }

    [Fact]
    public async Task UnknownKeyAnswers403()
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync("integration-made-up-key", "/pair/USD/EUR");
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Matches("^(invalid-key|inactive-account)$", body.GetProperty("error-type").GetString()!);
    }

    [Fact]
    public async Task ProgramConvertsOneCurrencyIntoItself()
    {
        var output = new StringWriter();
        RootCommand root = ConvertCommand.Create(Http, Environment.GetEnvironmentVariable);
        int code = await root.Parse(["1", "EUR", "EUR"]).InvokeAsync(new InvocationConfiguration { Output = output, Error = new StringWriter() }, TestContext.Current.CancellationToken);
        Assert.Equal(0, code);
        Assert.Equal("1.00 EUR", output.ToString().TrimEnd());
    }
}
