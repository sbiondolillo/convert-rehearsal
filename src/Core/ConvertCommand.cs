using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

namespace Core;

/// <summary>The command tree of the console program: <c>amount</c>, <c>from</c> and <c>to</c>.</summary>
public static class ConvertCommand
{
    /// <summary>The name of the environment variable that holds the key of the service.</summary>
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    /// <summary>The root command, which prints the amount in the target currency at the rate of the day.</summary>
    public static RootCommand Create(HttpClient http)
    {
        var amount = new Argument<decimal>("amount")
        {
            Description = "The amount of money to convert.",
            CustomParser = ParseAmount,
        };
        var from = new Argument<string>("from") { Description = "The code of the currency the amount is in." };
        var to = new Argument<string>("to") { Description = "The code of the currency to convert to." };
        var root = new RootCommand("Converts an amount between two currencies at the rate of the day.");
        root.Arguments.Add(amount);
        root.Arguments.Add(from);
        root.Arguments.Add(to);
        root.SetAction(async (result, cancellationToken) =>
        {
            TextWriter output = result.InvocationConfiguration.Output;
            TextWriter error = result.InvocationConfiguration.Error;
            string? key = Environment.GetEnvironmentVariable(KeyVariable);
            if (string.IsNullOrEmpty(key))
            {
                await error.WriteLineAsync($"The environment variable {KeyVariable} holds no key.").ConfigureAwait(false);
                return 1;
            }

            string fromCode = result.GetRequiredValue(from).ToUpperInvariant();
            string toCode = result.GetRequiredValue(to).ToUpperInvariant();
            RateResult rate = await new ExchangeRateClient(http)
                .GetRateAsync(key, fromCode, toCode, cancellationToken)
                .ConfigureAwait(false);
            if (rate.Rate is not decimal value)
            {
                await error.WriteLineAsync(rate.Error).ConfigureAwait(false);
                return 1;
            }

            await output.WriteLineAsync(Conversion.Format(result.GetRequiredValue(amount), value, toCode)).ConfigureAwait(false);
            return 0;
        });
        return root;
    }

    private static decimal ParseAmount(ArgumentResult result)
    {
        string text = result.Tokens[0].Value;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
        {
            return value;
        }

        result.AddError($"'{text}' is not an amount.");
        return 0;
    }
}
