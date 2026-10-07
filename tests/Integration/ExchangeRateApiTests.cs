using System.CommandLine;
using System.Text.Json;
using Core;
using Xunit;

namespace Integration;

public sealed class ExchangeRateApiTests
{
    private static string Key =>
        Environment.GetEnvironmentVariable(ConvertCommand.KeyVariable)
        ?? throw new InvalidOperationException($"{ConvertCommand.KeyVariable} is not set.");

    private static async Task<(int Status, JsonElement Body)> GetAsync(string key, string pair)
    {
        using var http = new HttpClient();
        using HttpResponseMessage response = await http.GetAsync(
            new Uri($"https://v6.exchangerate-api.com/v6/{key}/pair/{pair}"), TestContext.Current.CancellationToken);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(text);
        return ((int)response.StatusCode, document.RootElement.Clone());
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(string key, string arguments)
    {
        string? saved = Environment.GetEnvironmentVariable(ConvertCommand.KeyVariable);
        Environment.SetEnvironmentVariable(ConvertCommand.KeyVariable, key);
        try
        {
            using var http = new HttpClient();
            using var output = new StringWriter();
            using var error = new StringWriter();
            int code = await ConvertCommand.Create(http).Parse(arguments.Split(' '))
                .InvokeAsync(new InvocationConfiguration { Output = output, Error = error }, TestContext.Current.CancellationToken);
            return (code, output.ToString(), error.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConvertCommand.KeyVariable, saved);
        }
    }

    [Fact]
    public async Task UsdEurAnswersWithANumber()
    {
        (int status, JsonElement body) = await GetAsync(Key, "USD/EUR");
        Assert.Equal(200, status);
        Assert.Equal("success", body.GetProperty("result").GetString());
        Assert.Equal("USD", body.GetProperty("base_code").GetString());
        Assert.Equal("EUR", body.GetProperty("target_code").GetString());
        Assert.True(body.GetProperty("conversion_rate").TryGetDecimal(out _));
    }

    [Fact]
    public async Task UsdJpyAnswersWithANumber()
    {
        (int status, JsonElement body) = await GetAsync(Key, "USD/JPY");
        Assert.Equal(200, status);
        Assert.Equal("success", body.GetProperty("result").GetString());
        Assert.Equal("USD", body.GetProperty("base_code").GetString());
        Assert.Equal("JPY", body.GetProperty("target_code").GetString());
        Assert.True(body.GetProperty("conversion_rate").TryGetDecimal(out _));
    }

    [Fact]
    public async Task EurEurHasTheRateOne()
    {
        (int status, JsonElement body) = await GetAsync(Key, "EUR/EUR");
        Assert.Equal(200, status);
        Assert.Equal(1m, body.GetProperty("conversion_rate").GetDecimal());

        (int code, string output, _) = await RunAsync(Key, "10 EUR EUR");
        Assert.Equal(0, code);
        Assert.Equal("10.00 EUR", output.TrimEnd());
    }

    [Fact]
    public async Task LowerCaseCodesAnswerInUpperCase()
    {
        (_, JsonElement body) = await GetAsync(Key, "usd/eur");
        Assert.Equal("USD", body.GetProperty("base_code").GetString());
        Assert.Equal("EUR", body.GetProperty("target_code").GetString());
    }

    [Fact]
    public async Task AnUnsupportedCodeAnswersWithNotFound()
    {
        (int status, JsonElement body) = await GetAsync(Key, "USD/ZZZ");
        Assert.Equal(404, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        Assert.Equal("unsupported-code", body.GetProperty("error-type").GetString());
        string text = body.GetRawText();
        Assert.DoesNotContain("USD", text);
        Assert.DoesNotContain("ZZZ", text);
    }

    [Fact]
    public async Task AnUnknownKeyAnswersWithAnErrorAndTheProgramHidesTheKey()
    {
        const string unknownKey = "unknown-key-for-the-test";
        (int status, JsonElement body) = await GetAsync(unknownKey, "USD/EUR");
        Assert.Equal(403, status);
        Assert.Equal("error", body.GetProperty("result").GetString());
        string errorType = body.GetProperty("error-type").GetString()!;
        Assert.True(errorType is "invalid-key" or "inactive-account", errorType);

        (int code, string output, string error) = await RunAsync(unknownKey, "10 USD EUR");
        Assert.Equal(1, code);
        Assert.Equal("", output);
        Assert.True(error.Contains("invalid-key", StringComparison.Ordinal) || error.Contains("inactive-account", StringComparison.Ordinal), error);
        Assert.DoesNotContain(unknownKey, error + output, StringComparison.Ordinal);
    }
}
