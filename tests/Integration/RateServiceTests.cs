using System.Net;
using System.Text.Json;
using Xunit;

namespace Integration;

public sealed class RateServiceTests
{
    private static readonly HttpClient Http = new();
    private static readonly string[] KeyErrors = ["invalid-key", "inactive-account"];

    private static string Key =>
        Environment.GetEnvironmentVariable("EXCHANGERATE_API_KEY") is { Length: > 0 } key
            ? key
            : throw new InvalidOperationException("EXCHANGERATE_API_KEY is unset or empty.");

    private static async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(string key, string path)
    {
        using HttpResponseMessage response = await Http.GetAsync(new Uri($"https://v6.exchangerate-api.com/v6/{key}{path}"), TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (response.StatusCode, document.RootElement.Clone());
    }

    [Theory]
    [InlineData("/pair/USD/EUR", "USD", "EUR")]
    [InlineData("/pair/USD/JPY", "USD", "JPY")]
    [InlineData("/pair/usd/eur", "USD", "EUR")]
    public async Task PairAnswersWithTheCodesAndANumericRate(string path, string baseCode, string targetCode)
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
    public async Task ProgramConvertsOneCurrencyToItself()
    {
        using var http = new HttpClient();
        var root = Core.ConvertCommand.Create(new Core.RateClient(http), Environment.GetEnvironmentVariable);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = await root.Parse("10 EUR EUR").InvokeAsync(new System.CommandLine.InvocationConfiguration { Output = output, Error = error }, TestContext.Current.CancellationToken);
        Assert.Equal(0, exitCode);
        Assert.Equal("10.00 EUR", output.ToString().TrimEnd());
    }

    [Fact]
    public async Task PairWithAnUnsupportedCodeIsNotFound()
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync(Key, "/pair/USD/ZZZ");
        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Equal("unsupported-code", body.GetProperty("error-type").GetString());
    }

    [Fact]
    public async Task PairWithAnUnknownKeyIsForbidden()
    {
        (HttpStatusCode status, JsonElement body) = await GetAsync("unknown-key", "/pair/USD/EUR");
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Contains(body.GetProperty("error-type").GetString(), KeyErrors);
    }
}
