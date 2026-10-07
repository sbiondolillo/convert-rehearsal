using System.CommandLine;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Core;

/// <summary>The command tree of the console program: the arguments <c>amount</c>, <c>from</c> and <c>to</c>.</summary>
public static class ConvertCommand
{
    /// <summary>The name of the environment variable that holds the key of the service.</summary>
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    /// <summary>The root command, which converts an amount at the rate of the day.</summary>
    /// <param name="http">The client that sends the request to the service.</param>
    /// <param name="getEnvironmentVariable">Reads an environment variable, null when it is unset.</param>
    public static RootCommand Create(HttpClient http, Func<string, string?> getEnvironmentVariable)
    {
        var amount = new Argument<decimal>("amount")
        {
            Description = "The amount of money to convert.",
            CustomParser = result =>
            {
                if (result.Tokens.Count is 1
                    && decimal.TryParse(result.Tokens[0].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                {
                    return value;
                }

                result.AddError("The amount is not a number.");
                return 0m;
            },
        };
        var from = new Argument<string>("from") { Description = "The code of the currency the amount is in." };
        var to = new Argument<string>("to") { Description = "The code of the currency to convert to." };
        var root = new RootCommand("Converts an amount of money to another currency at the rate of the day.");
        root.Arguments.Add(amount);
        root.Arguments.Add(from);
        root.Arguments.Add(to);
        root.SetAction(async (result, cancellationToken) =>
        {
            TextWriter output = result.InvocationConfiguration.Output;
            TextWriter error = result.InvocationConfiguration.Error;
            string? key = getEnvironmentVariable(KeyVariable);
            if (string.IsNullOrEmpty(key))
            {
                await error.WriteLineAsync($"The environment variable {KeyVariable} holds no key.").ConfigureAwait(false);
                return 1;
            }

            string baseCode = result.GetRequiredValue(from).ToUpperInvariant();
            string targetCode = result.GetRequiredValue(to).ToUpperInvariant();
            ConversionResult conversion = await new RateClient(http)
                .GetRateAsync(key, baseCode, targetCode, cancellationToken).ConfigureAwait(false);
            if (conversion.Error is not null)
            {
                await error.WriteLineAsync(conversion.Error).ConfigureAwait(false);
                return 1;
            }

            decimal converted = Math.Round(result.GetRequiredValue(amount) * conversion.Rate, 2, MidpointRounding.AwayFromZero);
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{converted:F2} {targetCode}")).ConfigureAwait(false);
            return 0;
        });
        return root;
    }
}

/// <summary>The rate of a pair, or the line to write on standard error.</summary>
public readonly record struct ConversionResult(decimal Rate, string? Error);

/// <summary>Asks ExchangeRate-API for the rate of a pair of currencies.</summary>
public sealed class RateClient(HttpClient http)
{
    public async Task<ConversionResult> GetRateAsync(string key, string baseCode, string targetCode, CancellationToken cancellationToken)
    {
        const string NoAnswer = "The service gave no answer.";
        try
        {
            var uri = new Uri($"https://v6.exchangerate-api.com/v6/{Uri.EscapeDataString(key)}/pair/{Uri.EscapeDataString(baseCode)}/{Uri.EscapeDataString(targetCode)}");
            using HttpResponseMessage response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
            string? status = body.TryGetProperty("result", out JsonElement resultValue) ? resultValue.GetString() : null;
            if (status is "success" && body.TryGetProperty("conversion_rate", out JsonElement rate))
            {
                return new ConversionResult(rate.GetDecimal(), null);
            }

            string? errorType = body.TryGetProperty("error-type", out JsonElement errorValue) ? errorValue.GetString() : null;
            return errorType switch
            {
                "unsupported-code" => new ConversionResult(0m, $"The service lacks one of the codes {baseCode} and {targetCode}."),
                not null => new ConversionResult(0m, $"The service answered with the error {errorType}."),
                _ => new ConversionResult(0m, "The service gave an answer that the program does not understand."),
            };
        }
        catch (HttpRequestException)
        {
            return new ConversionResult(0m, NoAnswer);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ConversionResult(0m, NoAnswer);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return new ConversionResult(0m, "The service gave an answer that the program does not understand.");
        }
    }
}
