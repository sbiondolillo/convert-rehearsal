using System.CommandLine;
using System.Globalization;

namespace Core;

/// <summary>The command tree of the console program: <c>amount</c>, <c>from</c> and <c>to</c>.</summary>
public static class ConvertCommand
{
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    /// <summary>The root command, which prints the amount converted at the rate of the day.</summary>
    public static RootCommand Create(ExchangeRateClient client, Func<string, string?> getEnvironmentVariable)
    {
        var amount = new Argument<string>("amount") { Description = "The amount of money to convert." };
        var from = new Argument<string>("from") { Description = "The code of the currency the amount is in." };
        var to = new Argument<string>("to") { Description = "The code of the currency to convert to." };
        var root = new RootCommand("Converts an amount of money to another currency at the rate of the day.");
        root.Arguments.Add(amount);
        root.Arguments.Add(from);
        root.Arguments.Add(to);
        root.SetAction(async (result, cancellationToken) =>
        {
            TextWriter error = result.InvocationConfiguration.Error;
            string? key = getEnvironmentVariable(KeyVariable);
            if (string.IsNullOrEmpty(key))
            {
                await error.WriteLineAsync($"The environment variable {KeyVariable} is not set.").ConfigureAwait(false);
                return 1;
            }

            string amountText = result.GetRequiredValue(amount);
            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amountValue))
            {
                await error.WriteLineAsync($"'{amountText}' is not an amount.").ConfigureAwait(false);
                return 2;
            }

            string fromCode = result.GetRequiredValue(from).ToUpperInvariant();
            string toCode = result.GetRequiredValue(to).ToUpperInvariant();
            RateResult rate = await client.GetRateAsync(key, fromCode, toCode, cancellationToken).ConfigureAwait(false);
            if (rate.Rate is not decimal value)
            {
                await error.WriteLineAsync(rate.Error).ConfigureAwait(false);
                return 1;
            }

            await result.InvocationConfiguration.Output
                .WriteLineAsync(ExchangeRateClient.Format(amountValue, value, toCode)).ConfigureAwait(false);
            return 0;
        });
        return root;
    }
}
