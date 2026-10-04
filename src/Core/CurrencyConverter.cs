using System.Globalization;
using System.Text.Json;

namespace Core;

/// <summary>Converts an amount at the rate that ExchangeRate-API gives for a pair of currencies.</summary>
public sealed class CurrencyConverter(HttpClient http)
{
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    private const string BaseAddress = "https://v6.exchangerate-api.com/v6";

    /// <summary>Converts the amount, writes the result or one line of error, and returns the exit code.</summary>
    public async Task<int> ConvertAsync(
        decimal amount, string from, string to, string? key, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(key))
        {
            await error.WriteLineAsync($"The environment variable {KeyVariable} is not set.").ConfigureAwait(false);
            return 1;
        }

        string source = from.ToUpperInvariant();
        string target = to.ToUpperInvariant();
        string url = $"{BaseAddress}/{Uri.EscapeDataString(key)}/pair/{Uri.EscapeDataString(source)}/{Uri.EscapeDataString(target)}";

        string body;
        try
        {
            using HttpResponseMessage response = await http.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // The message of the exception can hold the request address, and so the key: it stays unwritten.
            await error.WriteLineAsync("The service gave no answer.").ConfigureAwait(false);
            return 1;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            string? result = root.TryGetProperty("result", out JsonElement r) ? r.GetString() : null;
            if (result is "success" && root.TryGetProperty("conversion_rate", out JsonElement rate))
            {
                decimal converted = Math.Round(amount * rate.GetDecimal(), 2, MidpointRounding.AwayFromZero);
                await output.WriteLineAsync($"{converted.ToString("F2", CultureInfo.InvariantCulture)} {target}").ConfigureAwait(false);
                return 0;
            }

            string? errorType = root.TryGetProperty("error-type", out JsonElement e) ? e.GetString() : null;
            string message = errorType switch
            {
                "unsupported-code" => $"The service lacks one of the codes {source} and {target}.",
                not null => $"The service answered with the error {errorType}.",
                _ => "The service gave an answer that the program cannot read.",
            };
            await error.WriteLineAsync(message).ConfigureAwait(false);
            return 1;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            await error.WriteLineAsync("The service gave an answer that the program cannot read.").ConfigureAwait(false);
            return 1;
        }
    }
}
