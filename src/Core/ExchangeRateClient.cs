using System.Globalization;
using System.Text.Json;

namespace Core;

/// <summary>How a request for a rate ended.</summary>
/// <param name="Rate">The rate, when the service gave one.</param>
/// <param name="Error">The line to write on standard error, when it did not.</param>
public sealed record RateOutcome(decimal? Rate, string? Error);

/// <summary>Gets the rate of a pair of currencies from ExchangeRate-API.</summary>
/// <param name="http">The client that sends the request.</param>
public sealed class ExchangeRateClient(HttpClient http)
{
    /// <summary>The environment variable that holds the key of the service.</summary>
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    private const string NoAnswer = "The exchange rate service gave no answer.";

    /// <summary>Asks the service for the rate. No error line holds the key or the address.</summary>
    public async Task<RateOutcome> GetRateAsync(string key, string from, string to, CancellationToken cancellationToken)
    {
        var address = new Uri($"https://v6.exchangerate-api.com/v6/{Uri.EscapeDataString(key)}/pair/{Uri.EscapeDataString(from)}/{Uri.EscapeDataString(to)}");
        try
        {
            using HttpResponseMessage response = await http.GetAsync(address, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseAnswer(body, from, to);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or Polly.Timeout.TimeoutRejectedException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new RateOutcome(null, NoAnswer);
        }
    }

    /// <summary>Reads the JSON of an answer, whatever the status of the response.</summary>
    public static RateOutcome ParseAnswer(string body, string from, string to)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind is not JsonValueKind.Object || !root.TryGetProperty("result", out JsonElement result))
            {
                return new RateOutcome(null, NoAnswer);
            }

            if (result.ValueKind is JsonValueKind.String && result.GetString() is "success"
                && root.TryGetProperty("conversion_rate", out JsonElement rate)
                && rate.ValueKind is JsonValueKind.Number)
            {
                return new RateOutcome(rate.GetDecimal(), null);
            }

            string? errorType = root.TryGetProperty("error-type", out JsonElement type) && type.ValueKind is JsonValueKind.String
                ? type.GetString()
                : null;
            return errorType switch
            {
                "unsupported-code" => new RateOutcome(null, $"The exchange rate service lacks one of the codes {from} and {to}."),
                not null => new RateOutcome(null, string.Create(CultureInfo.InvariantCulture, $"The exchange rate service answered with an error: {errorType}.")),
                null => new RateOutcome(null, NoAnswer),
            };
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            return new RateOutcome(null, NoAnswer);
        }
    }
}
