using System.CommandLine;
using System.Net;
using System.Text.Json;
using Core;
using Xunit;

namespace Integration;

// The build suite alone covers "the service gives no answer": the real service has no stable counterpart.
public sealed class ExchangeRateTests
{
    private static readonly HttpClient Http = new();

    private static string Key =>
        Environment.GetEnvironmentVariable(ConvertCommand.KeyVariable) is { Length: > 0 } key
            ? key
            : throw new InvalidOperationException($"{ConvertCommand.KeyVariable} is not set.");

    private static async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(string key, string pair)
    {
        using HttpResponseMessage response = await Http.GetAsync(
            new Uri($"https://v6.exchangerate-api.com/v6/{key}/pair/{pair}"), TestContext.Current.CancellationToken);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        RootCommand root = ConvertCommand.Create(new ExchangeRateClient(Http), Environment.GetEnvironmentVariable);
        int exitCode = await root.Parse(args).InvokeAsync(
            new InvocationConfiguration { Output = output, Error = error }, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    [Theory]
    [InlineData("USD/EUR", "USD", "EUR")]
    [InlineData("USD/JPY", "USD", "JPY")]
    [InlineData("usd/eur", "USD", "EUR")]
    public async Task AnswersASuccessWithARate(string pair, string baseCode, string targetCode)
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync(Key, pair);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("success", body.GetProperty("result").GetString());
        Assert.Equal(baseCode, body.GetProperty("base_code").GetString());
        Assert.Equal(targetCode, body.GetProperty("target_code").GetString());
        Assert.Equal(JsonValueKind.Number, body.GetProperty("conversion_rate").ValueKind);
    }

    [Fact]
    public async Task AnswersTheRateOneForOneCurrency()
    {
        (_, JsonElement body) = await GetAsync(Key, "EUR/EUR");
        Assert.Equal(1m, body.GetProperty("conversion_rate").GetDecimal());
        (int exitCode, string output, string error) = await RunAsync("10", "EUR", "EUR");
        Assert.Equal(0, exitCode);
        Assert.Equal("10.00 EUR", output.Trim());
        Assert.Equal("", error);
    }

    [Fact]
    public async Task ConvertsAtTheRateOfTheDay()
    {
        (int exitCode, string output, _) = await RunAsync("10", "usd", "eur");
        Assert.Equal(0, exitCode);
        Assert.EndsWith(" EUR", output.Trim(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnswersUnsupportedCodeForACodeItLacks()
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync(Key, "USD/ZZZ");
        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Equal("unsupported-code", body.GetProperty("error-type").GetString());
        (int exitCode, string output, string error) = await RunAsync("10", "USD", "ZZZ");
        Assert.Equal(1, exitCode);
        Assert.Equal("", output);
        Assert.Contains("lacks", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusesAKeyThatIsUnknown()
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync("made-up-key", "USD/EUR");
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.True(body.GetProperty("error-type").GetString() is "invalid-key" or "inactive-account");
    }
}
