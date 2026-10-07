using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Core;

/// <summary>The outcome of a request for the rate of a pair: a rate, or the text of an error.</summary>
public sealed record RateResult(decimal? Rate, string? Error);

/// <summary>Gets the rate of a pair of currencies from ExchangeRate-API.</summary>
public sealed class ExchangeRateClient(HttpClient http)
{
    private const string BaseAddress = "https://v6.exchangerate-api.com/v6/";

    public async Task<RateResult> GetRateAsync(string key, string from, string to, CancellationToken cancellationToken)
    {
        var uri = new Uri($"{BaseAddress}{Uri.EscapeDataString(key)}/pair/{Uri.EscapeDataString(from)}/{Uri.EscapeDataString(to)}");
        try
        {
            using var response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            string? result = root.GetProperty("result").GetString();
            if (result is "success" && response.StatusCode is HttpStatusCode.OK)
            {
                JsonElement rate = root.GetProperty("conversion_rate");
                return new RateResult(rate.GetDecimal(), null);
            }

            string? errorType = root.TryGetProperty("error-type", out JsonElement type) ? type.GetString() : null;
            return errorType switch
            {
                "unsupported-code" => new RateResult(null, $"The service lacks one of the currency codes {from} and {to}."),
                string text when result is "error" => new RateResult(null, $"The service answered with the error {text}."),
                _ => NoAnswer,
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException
            or FormatException or KeyNotFoundException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return NoAnswer;
        }
    }

    private static RateResult NoAnswer { get; } = new(null, "The service gave no answer.");

    public static string Format(decimal amount, decimal rate, string code) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero):F2} {code}");
}
