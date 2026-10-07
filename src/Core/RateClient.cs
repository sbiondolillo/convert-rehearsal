using System.Globalization;
using System.Text.Json;

namespace Core;

/// <summary>The outcome of one rate request: a rate, or the text of a failure that holds no text of the key.</summary>
public sealed record RateResult(decimal? Rate, string? Error)
{
    public static RateResult Success(decimal rate) => new(rate, null);

    public static RateResult Failure(string error) => new(null, error);
}

/// <summary>Asks ExchangeRate-API for the rate of a pair of currencies.</summary>
public sealed class RateClient(HttpClient http)
{
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    public async Task<RateResult> GetRateAsync(string key, string from, string to, CancellationToken cancellationToken)
    {
        var uri = new Uri($"https://v6.exchangerate-api.com/v6/{Uri.EscapeDataString(key)}/pair/{Uri.EscapeDataString(from)}/{Uri.EscapeDataString(to)}");
        try
        {
            using HttpResponseMessage response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseAnswer(body, from, to);
        }
        catch (HttpRequestException)
        {
            return NoAnswer;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NoAnswer;
        }
    }

    /// <summary>Reads the JSON body of an answer, by its <c>result</c> field and not by the status.</summary>
    public static RateResult ParseAnswer(string body, string from, string to)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind is not JsonValueKind.Object || !root.TryGetProperty("result", out JsonElement result))
            {
                return Unreadable;
            }

            if (result.ValueKind is JsonValueKind.String && result.GetString() is "success")
            {
                return root.TryGetProperty("conversion_rate", out JsonElement rate) && rate.ValueKind is JsonValueKind.Number && rate.TryGetDecimal(out decimal value)
                    ? RateResult.Success(value)
                    : Unreadable;
            }

            string? errorType = root.TryGetProperty("error-type", out JsonElement type) && type.ValueKind is JsonValueKind.String ? type.GetString() : null;
            return errorType switch
            {
                "unsupported-code" => RateResult.Failure($"The service lacks one of the currency codes {from} and {to}."),
                not null => RateResult.Failure($"The service answered with an error: {errorType}."),
                _ => Unreadable,
            };
        }
        catch (JsonException)
        {
            return Unreadable;
        }
    }

    /// <summary>The amount times the rate, rounded half away from zero to two decimals, in the invariant culture.</summary>
    public static string FormatAmount(decimal amount, decimal rate, string code) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero):0.00} {code}");

    private static RateResult NoAnswer { get; } = RateResult.Failure("The service gave no answer.");

    private static RateResult Unreadable { get; } = RateResult.Failure("The service gave an answer that the program cannot read.");
}
