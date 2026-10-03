using System.Globalization;
using System.Text.Json;
using System.CommandLine;

namespace Core;

/// <summary>The command tree of the console program: convert an amount at the rate of the day.</summary>
public static class ConvertCommand
{
    /// <summary>The environment variable that holds the key of ExchangeRate-API.</summary>
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    private const int ParseErrorCode = 2;

    /// <summary>The root command: <c>amount</c>, <c>from</c> code, <c>to</c> code.</summary>
    /// <param name="http">The client that sends the request; its handler is the stub of the build suite.</param>
    /// <param name="getEnvironmentVariable">Reads an environment variable; null when unset.</param>
    public static RootCommand Create(HttpClient http, Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var amount = new Argument<string>("amount") { Description = "The amount to convert." };
        var from = new Argument<string>("from") { Description = "The code of the currency the amount is in." };
        var to = new Argument<string>("to") { Description = "The code of the currency to convert to." };
        var root = new RootCommand("Converts an amount to another currency at the rate of the day.");
        root.Arguments.Add(amount);
        root.Arguments.Add(from);
        root.Arguments.Add(to);
        root.SetAction(async (result, cancellationToken) =>
        {
            TextWriter output = result.InvocationConfiguration.Output;
            TextWriter error = result.InvocationConfiguration.Error;
            return await RunAsync(
                http,
                getEnvironmentVariable,
                result.GetRequiredValue(amount),
                result.GetRequiredValue(from),
                result.GetRequiredValue(to),
                output,
                error,
                cancellationToken).ConfigureAwait(false);
        });
        return root;
    }

    private static async Task<int> RunAsync(
        HttpClient http,
        Func<string, string?> getEnvironmentVariable,
        string amountText,
        string fromText,
        string toText,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        // Order of checks: the amount and the codes, then the key, then the request.
        if (!decimal.TryParse(amountText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount))
        {
            await error.WriteLineAsync($"The amount '{amountText}' is not a number.").ConfigureAwait(false);
            return ParseErrorCode;
        }

        if (!IsCode(fromText) || !IsCode(toText))
        {
            await error.WriteLineAsync("A currency code is made of letters only.").ConfigureAwait(false);
            return ParseErrorCode;
        }

        string from = fromText.ToUpperInvariant();
        string to = toText.ToUpperInvariant();

        string? key = getEnvironmentVariable(KeyVariable);
        if (string.IsNullOrEmpty(key))
        {
            await error.WriteLineAsync($"The environment variable {KeyVariable} is unset or empty.").ConfigureAwait(false);
            return 1;
        }

        var uri = new Uri($"https://v6.exchangerate-api.com/v6/{Uri.EscapeDataString(key)}/pair/{from}/{to}");

        string body;
        try
        {
            using HttpResponseMessage response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // The message of the exception holds the address, and so the key: write a fixed text.
            return await FailAsync(error, "The service gave no answer.").ConfigureAwait(false);
        }

        (string? result, string? errorType, decimal? rate) = ReadAnswer(body);

        if (result == "error")
        {
            string text = errorType == "unsupported-code"
                ? $"The service lacks one of the two codes: {from} and {to}."
                : $"The service answered with an error: {Scrub(errorType, key)}.";
            return await FailAsync(error, text).ConfigureAwait(false);
        }

        if (result == "success" && rate is > 0)
        {
            // Away from zero is the usual rounding of money.
            decimal converted;
            try
            {
                converted = Math.Round(amount * rate.Value, 2, MidpointRounding.AwayFromZero);
            }
            catch (OverflowException)
            {
                return await FailAsync(error, "The converted amount is too large.").ConfigureAwait(false);
            }

            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{converted:F2} {to}")).ConfigureAwait(false);
            return 0;
        }

        return await FailAsync(error, "The service gave an answer that the program does not understand.").ConfigureAwait(false);
    }

    private static bool IsCode(string text) => text.Length > 0 && text.All(char.IsAsciiLetter);

    private static string Scrub(string? text, string key)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "unknown";
        }

        string safe = new(text.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').Take(64).ToArray());
        return safe.Replace(key, "***", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> FailAsync(TextWriter error, string text)
    {
        await error.WriteLineAsync(text).ConfigureAwait(false);
        return 1;
    }

    private static (string? Result, string? ErrorType, decimal? Rate) ReadAnswer(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null, null);
            }

            string? result = Text(root, "result");
            string? errorType = Text(root, "error-type");
            decimal? rate = root.TryGetProperty("conversion_rate", out JsonElement r)
                && r.ValueKind == JsonValueKind.Number
                && r.TryGetDecimal(out decimal value)
                ? value
                : null;
            return (result, errorType, rate);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
