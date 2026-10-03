using System.Globalization;
using System.Text.Json;

namespace Core;

/// <summary>The outcome of one conversion: the line to print, and whether it went well.</summary>
public sealed record Conversion(bool Succeeded, string Message);

/// <summary>Converts an amount at the rate that ExchangeRate-API gives for a pair of currencies.</summary>
public sealed class RateService(HttpClient http)
{
    /// <summary>The name of the environment variable that holds the key of the service.</summary>
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    /// <summary>Asks the service for the rate of the pair, and converts the amount. The messages never hold the key.</summary>
    public async Task<Conversion> ConvertAsync(decimal amount, string from, string to, string key, CancellationToken cancellationToken)
    {
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://v6.exchangerate-api.com/v6/{key}/pair/{from}/{to}"));
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            string? result = root.GetProperty("result").GetString();
            if (result is "success")
            {
                decimal rate = root.GetProperty("conversion_rate").GetDecimal();
                decimal value = Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
                return new Conversion(true, string.Create(CultureInfo.InvariantCulture, $"{value:F2} {to}"));
            }

            if (result is "error" && root.TryGetProperty("error-type", out JsonElement type) && type.GetString() is string errorType)
            {
                return errorType is "unsupported-code"
                    ? new Conversion(false, $"The service lacks one of the currency codes {from} and {to}.")
                    : new Conversion(false, $"The service answered with the error {errorType}.");
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            // Fall through: the text of these exceptions can hold the address, and so the key.
        }

        return new Conversion(false, "The service gave no answer.");
    }
}
