using Core;
using System.CommandLine;
using System.Text.Json;
using Xunit;

namespace Integration;

public sealed class RateServiceTests
{
    private static string Key =>
        Environment.GetEnvironmentVariable(ConvertCommand.KeyVariable) is { Length: > 0 } key
            ? key
            : throw new InvalidOperationException($"{ConvertCommand.KeyVariable} is not set.");

    private static async Task<(int Status, JsonElement Body)> GetAsync(string key, string from, string to)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using HttpResponseMessage response = await http.GetAsync(RateClient.Address(key, from, to), TestContext.Current.CancellationToken);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return ((int)response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    [Fact]
    public async Task ConvertingACurrencyToItselfGivesARateOfOne()
    {
        (int status, JsonElement body) = await GetAsync(Key, "EUR", "EUR");
        Assert.Equal(200, status);
        Assert.Equal(1m, body.GetProperty("conversion_rate").GetDecimal());
    }

    [Fact]
    public async Task TheProgramPrintsTheAmountOfACurrencyConvertedToItself()
    {
        using var http = new HttpClient();
        using var output = new StringWriter();
        using var error = new StringWriter();
        RootCommand root = ConvertCommand.Create(http, () => Key);
        int exitCode = await root.Parse("10 EUR EUR").InvokeAsync(new InvocationConfiguration { Output = output, Error = error }, TestContext.Current.CancellationToken);
        Assert.Equal(0, exitCode);
        Assert.Equal("10.00 EUR", output.ToString().Trim());
    }

    [Fact]
    public async Task AnAnswerForUsdEurHoldsTheCodesAndANumber()
    {
        (int status, JsonElement body) = await GetAsync(Key, "USD", "EUR");
        Assert.Equal(200, status);
        Assert.Equal("success", body.GetProperty("result").GetString());
        Assert.Equal("USD", body.GetProperty("base_code").GetString());
        Assert.Equal("EUR", body.GetProperty("target_code").GetString());
        Assert.Equal(JsonValueKind.Number, body.GetProperty("conversion_rate").ValueKind);
    }

    [Fact]
    public async Task TheServiceTakesLowerCaseCodesAndAnswersInUpperCase()
    {
        (_, JsonElement body) = await GetAsync(Key, "usd", "eur");
        Assert.Equal("USD", body.GetProperty("base_code").GetString());
        Assert.Equal("EUR", body.GetProperty("target_code").GetString());
    }

    [Fact]
    public async Task ACodeThatTheServiceLacksGivesUnsupportedCodeAndNamesNeitherCode()
    {
        (int status, JsonElement body) = await GetAsync(Key, "USD", "ZZZ");
        Assert.Equal(404, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Equal("unsupported-code", body.GetProperty("error-type").GetString());
        string text = body.GetRawText();
        Assert.DoesNotContain("ZZZ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("USD", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKeyThatIsUnknownToTheServiceGivesAnError()
    {
        (int status, JsonElement body) = await GetAsync("unknown-key-for-the-test", "USD", "EUR");
        Assert.Equal(403, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.True(body.GetProperty("error-type").GetString() is "invalid-key" or "inactive-account");
    }
}
