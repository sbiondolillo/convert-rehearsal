using System.CommandLine;
using System.Net;
using System.Text.Json;
using Core;
using Xunit;

namespace Integration;

public sealed class ExchangeRateServiceTests
{
    private static readonly HttpClient Http = new();

    private static string Key =>
        Environment.GetEnvironmentVariable(ExchangeRateClient.KeyVariable) is { Length: > 0 } key
            ? key
            : throw new InvalidOperationException($"{ExchangeRateClient.KeyVariable} is not set.");

    private static async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(string key, string pair)
    {
        using HttpResponseMessage response = await Http.GetAsync(new Uri($"https://v6.exchangerate-api.com/v6/{key}/pair/{pair}"), TestContext.Current.CancellationToken);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    [Theory]
    [InlineData("USD/EUR", "USD", "EUR")]
    [InlineData("USD/JPY", "USD", "JPY")]
    [InlineData("usd/eur", "USD", "EUR")]
    public async Task APairAnswersWithItsRate(string pair, string baseCode, string targetCode)
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync(Key, pair);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("success", body.GetProperty("result").GetString());
        Assert.Equal(baseCode, body.GetProperty("base_code").GetString());
        Assert.Equal(targetCode, body.GetProperty("target_code").GetString());
        Assert.Equal(JsonValueKind.Number, body.GetProperty("conversion_rate").ValueKind);
    }

    [Fact]
    public async Task ACurrencyToItselfHasTheRate1()
    {
        (_, JsonElement body) = await GetAsync(Key, "EUR/EUR");
        Assert.Equal(1m, body.GetProperty("conversion_rate").GetDecimal());
    }

    [Fact]
    public async Task TheProgramConvertsACurrencyToItself()
    {
        using var output = new StringWriter();
        RootCommand root = ConvertCommand.Create(new ExchangeRateClient(Http), Environment.GetEnvironmentVariable);
        int exitCode = await ConvertCommand.RunAsync(root.Parse("10 EUR EUR"), new InvocationConfiguration { Output = output }, TestContext.Current.CancellationToken);
        Assert.Equal(0, exitCode);
        Assert.Equal("10.00 EUR", output.ToString().TrimEnd());
    }

    [Fact]
    public async Task ACodeThatTheServiceLacksIsAnUnsupportedCode()
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync(Key, "USD/ZZZ");
        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Equal("unsupported-code", body.GetProperty("error-type").GetString());
    }

    [Fact]
    public async Task AnUnknownKeyIsRefused()
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync("unknown-key-for-the-test", "USD/EUR");
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.True(body.GetProperty("error-type").GetString() is "invalid-key" or "inactive-account");
    }
}
