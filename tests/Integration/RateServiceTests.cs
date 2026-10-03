using System.Text.Json;
using Xunit;

namespace Integration;

public sealed class RateServiceTests
{
    private static readonly HttpClient Http = new();
    private static readonly string[] UnknownKeyErrors = ["invalid-key", "inactive-account"];

    private static string Key => Environment.GetEnvironmentVariable("EXCHANGERATE_API_KEY") is { Length: > 0 } key
        ? key
        : throw new InvalidOperationException("EXCHANGERATE_API_KEY is unset or empty.");

    private static async Task<(int Status, JsonElement Body)> Get(string key, string pair)
    {
        using HttpResponseMessage response = await Http.GetAsync(new Uri($"https://v6.exchangerate-api.com/v6/{key}/pair/{pair}"), TestContext.Current.CancellationToken);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return ((int)response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    [Theory]
    [InlineData("USD/EUR", "USD", "EUR")]
    [InlineData("USD/JPY", "USD", "JPY")]
    [InlineData("usd/eur", "USD", "EUR")]
    public async Task AnswersAPairWithARate(string pair, string baseCode, string targetCode)
    {
        (int status, JsonElement body) = await Get(Key, pair);
        Assert.Equal(200, status);
        Assert.Equal("success", body.GetProperty("result").GetString());
        Assert.Equal(baseCode, body.GetProperty("base_code").GetString());
        Assert.Equal(targetCode, body.GetProperty("target_code").GetString());
        Assert.True(body.GetProperty("conversion_rate").TryGetDecimal(out _));
    }

    [Fact]
    public async Task AnswersOneForACurrencyAgainstItself()
    {
        (int status, JsonElement body) = await Get(Key, "EUR/EUR");
        Assert.Equal(200, status);
        Assert.Equal(1m, body.GetProperty("conversion_rate").GetDecimal());
    }

    [Fact]
    public async Task AnswersUnsupportedCodeWithoutNamingTheCodes()
    {
        (int status, JsonElement body) = await Get(Key, "USD/ZZZ");
        Assert.Equal(404, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Equal("unsupported-code", body.GetProperty("error-type").GetString());
        string text = body.GetRawText();
        Assert.DoesNotContain("ZZZ", text);
        Assert.DoesNotContain("USD", text);
    }

    [Fact]
    public async Task AnswersAnUnknownKeyWithAnError()
    {
        (int status, JsonElement body) = await Get("unknown-key", "USD/EUR");
        Assert.Equal(403, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Contains(body.GetProperty("error-type").GetString(), UnknownKeyErrors);
    }
}
