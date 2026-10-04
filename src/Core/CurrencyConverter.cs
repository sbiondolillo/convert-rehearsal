using System.Globalization;
using System.Text.Json;

namespace Core;

/// <summary>The outcome of one conversion: the converted amount, or a failure with the line to print.</summary>
public sealed record ConversionResult(decimal? Amount, string? Error)
{
    public static ConversionResult Success(decimal amount) => new(amount, null);

    public static ConversionResult Failure(string error) => new(null, error);
}

/// <summary>Converts an amount at the rate of the day, which it gets from ExchangeRate-API.</summary>
public sealed class CurrencyConverter(HttpClient http, string key)
{
    private const string BaseAddress = "https://v6.exchangerate-api.com/v6/";

    /// <summary>Rounds to two decimals, away from zero at a midpoint, and writes the number with the invariant culture.</summary>
    public static string FormatAmount(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture);

    public async Task<ConversionResult> ConvertAsync(decimal amount, string from, string to, CancellationToken cancellationToken)
    {
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();
        var uri = new Uri($"{BaseAddress}{Uri.EscapeDataString(key)}/pair/{Uri.EscapeDataString(from)}/{Uri.EscapeDataString(to)}");

        string body;
        try
        {
            using HttpResponseMessage response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            // The message of the exception may carry the address, which holds the key.
            return ConversionResult.Failure("The service gave no answer.");
        }

        return Parse(body, amount, from, to);
    }

    public static ConversionResult Parse(string body, decimal amount, string from, string to)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            string? result = root.TryGetProperty("result", out JsonElement resultElement) ? resultElement.GetString() : null;
            if (result is "success" && root.TryGetProperty("conversion_rate", out JsonElement rate) && rate.TryGetDecimal(out decimal value))
            {
                try
                {
                    return ConversionResult.Success(amount * value);
                }
                catch (OverflowException)
                {
                    return ConversionResult.Failure("The converted amount is out of range.");
                }
            }

            if (result is "error" && root.TryGetProperty("error-type", out JsonElement type) && type.GetString() is string errorType)
            {
                return errorType is "unsupported-code"
                    ? ConversionResult.Failure($"The service lacks one of the currency codes {from} and {to}.")
                    : ConversionResult.Failure($"The service answered with the error-type {errorType}.");
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // An unreadable body counts as an unexpected answer.
        }

        return ConversionResult.Failure("The service gave an answer that the program cannot read.");
    }
}
