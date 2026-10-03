using System.Globalization;
using System.Text.Json;

namespace Core;

/// <summary>What the rate service answered for a pair.</summary>
/// <param name="Rate">The conversion rate, when the answer was a success.</param>
/// <param name="ErrorType">The <c>error-type</c> of an error answer, when it was one.</param>
/// <param name="Problem">A description of an answer that is neither a success nor an error answer.</param>
public sealed record RateAnswer(decimal? Rate, string? ErrorType, string? Problem);

/// <summary>Raised when the service gives no answer. The message never holds the request address, which holds the key.</summary>
public sealed class ServiceUnreachableException() : Exception("The service gave no answer.");

/// <summary>A client of ExchangeRate-API.</summary>
public sealed class RateClient(HttpClient http)
{
    private const string BaseAddress = "https://v6.exchangerate-api.com/v6/";

    /// <summary>The address of the rate of a pair; the key is a part of it.</summary>
    public static Uri PairAddress(string key, string from, string to) =>
        new($"{BaseAddress}{Uri.EscapeDataString(key)}/pair/{Uri.EscapeDataString(from)}/{Uri.EscapeDataString(to)}");

    /// <summary>Gets the rate of a pair.</summary>
    /// <exception cref="ServiceUnreachableException">The request got no answer.</exception>
    public async Task<RateAnswer> GetRateAsync(string key, string from, string to, CancellationToken cancellationToken = default)
    {
        string body;
        try
        {
            using HttpResponseMessage response = await http.GetAsync(PairAddress(key, from, to), cancellationToken).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new ServiceUnreachableException();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServiceUnreachableException();
        }

        return Parse(body);
    }

    private static RateAnswer Parse(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("result", out JsonElement result))
            {
                return new RateAnswer(null, null, "The service gave an answer that is not understood.");
            }

            string? kind = result.ValueKind == JsonValueKind.String ? result.GetString() : null;
            if (kind == "error")
            {
                string errorType = root.TryGetProperty("error-type", out JsonElement type) && type.ValueKind == JsonValueKind.String
                    ? type.GetString() ?? "unknown"
                    : "unknown";
                return new RateAnswer(null, errorType, null);
            }

            if (kind == "success"
                && root.TryGetProperty("conversion_rate", out JsonElement rate)
                && rate.ValueKind == JsonValueKind.Number
                && rate.TryGetDecimal(out decimal value))
            {
                return new RateAnswer(value, null, null);
            }
        }
        catch (JsonException)
        {
            // Falls through to the problem below.
        }

        return new RateAnswer(null, null, "The service gave an answer that is not understood.");
    }

    /// <summary>Formats an amount with two decimals, the same on every machine.</summary>
    public static string Format(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture);
}
