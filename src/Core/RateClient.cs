using System.Globalization;
using System.Text.Json;

namespace Core;

/// <summary>What the service said about a pair: a rate, an <c>error-type</c>, or nothing usable.</summary>
public sealed record RateAnswer(decimal? Rate, string? ErrorType, bool NoAnswer)
{
    public static RateAnswer Success(decimal rate) => new(rate, null, false);

    public static RateAnswer Error(string errorType) => new(null, errorType, false);

    public static RateAnswer None { get; } = new(null, null, true);
}

/// <summary>Asks ExchangeRate-API for the rate of a pair. The key is a part of the address, so nothing here writes the address or an exception message.</summary>
public sealed class RateClient(HttpClient http)
{
    private const string BaseAddress = "https://v6.exchangerate-api.com/v6/";

    /// <summary>The address of the request for a pair.</summary>
    public static Uri Address(string key, string from, string to) =>
        new($"{BaseAddress}{Uri.EscapeDataString(key)}/pair/{Uri.EscapeDataString(from)}/{Uri.EscapeDataString(to)}");

    public async Task<RateAnswer> GetRateAsync(string key, string from, string to, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync(Address(key, from, to), cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return Parse(body);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return RateAnswer.None;
        }
    }

    /// <summary>Reads the body of an answer.</summary>
    public static RateAnswer Parse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind is not JsonValueKind.Object || !root.TryGetProperty("result", out JsonElement result))
            {
                return RateAnswer.None;
            }

            switch (result.GetString())
            {
                case "success" when root.TryGetProperty("conversion_rate", out JsonElement rate) && rate.ValueKind is JsonValueKind.Number:
                    return RateAnswer.Success(decimal.Parse(rate.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture));
                case "error":
                    string? errorType = root.TryGetProperty("error-type", out JsonElement type) && type.ValueKind is JsonValueKind.String ? type.GetString() : null;
                    return RateAnswer.Error(errorType ?? "unknown");
                default:
                    return RateAnswer.None;
            }
        }
        catch (Exception exception) when (exception is JsonException or FormatException or OverflowException)
        {
            return RateAnswer.None;
        }
    }
}
