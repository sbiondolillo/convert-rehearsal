using System.Text.Json;

namespace Core;

/// <summary>How a request for a rate ended: the rate, or the line that tells why not.</summary>
public sealed record RateResult(decimal? Rate, string? Error);

/// <summary>Gets the rate of a pair of currencies from ExchangeRate-API.</summary>
public sealed class ExchangeRateClient(HttpClient http)
{
    private const string BaseAddress = "https://v6.exchangerate-api.com/v6/";

    /// <summary>The rate from <paramref name="from"/> to <paramref name="to"/>. No message holds the key or the address.</summary>
    public async Task<RateResult> GetRateAsync(string key, string from, string to, CancellationToken cancellationToken)
    {
        string body;
        try
        {
            using HttpResponseMessage response = await http
                .GetAsync(new Uri($"{BaseAddress}{key}/pair/{from}/{to}"), cancellationToken)
                .ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return NoAnswer();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NoAnswer();
        }

        return Parse(body, from, to);
    }

    /// <summary>Reads an answer of the service.</summary>
    public static RateResult Parse(string body, string from, string to)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            string? result = root.TryGetProperty("result", out JsonElement r) ? r.GetString() : null;
            if (result is "success" && root.TryGetProperty("conversion_rate", out JsonElement rate) && rate.TryGetDecimal(out decimal value))
            {
                return new RateResult(value, null);
            }

            string? errorType = root.TryGetProperty("error-type", out JsonElement e) ? e.GetString() : null;
            return errorType switch
            {
                "unsupported-code" => new RateResult(null, $"The service lacks one of the two codes: {from} or {to}."),
                not null => new RateResult(null, $"The service answered with the error {errorType}."),
                _ => NoAnswer(),
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return NoAnswer();
        }
    }

    private static RateResult NoAnswer() => new(null, "The service gave no answer.");
}
