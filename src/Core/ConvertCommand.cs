using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

namespace Core;

/// <summary>The command tree of the console program: the arguments <c>amount</c>, <c>from</c> and <c>to</c>.</summary>
public static class ConvertCommand
{
    /// <summary>The root command, which converts an amount at the rate of the day.</summary>
    /// <param name="client">The client of the rate service.</param>
    /// <param name="getVariable">Reads an environment variable, or gives null when it is unset.</param>
    public static RootCommand Create(RateClient client, Func<string, string?> getVariable)
    {
        var amount = new Argument<decimal>("amount")
        {
            Description = "The amount of money to convert.",
            CustomParser = result =>
            {
                string text = result.Tokens[0].Value;
                if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                {
                    return value;
                }

                result.AddError($"'{text}' is not an amount.");
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
            string key = getVariable(RateClient.KeyVariable) ?? "";
            if (key.Length is 0)
            {
                await error.WriteLineAsync($"The environment variable {RateClient.KeyVariable} is unset or empty.").ConfigureAwait(false);
                return 1;
            }

            string source = result.GetRequiredValue(from).ToUpperInvariant();
            string target = result.GetRequiredValue(to).ToUpperInvariant();
            RateResult rate = await client.GetRateAsync(key, source, target, cancellationToken).ConfigureAwait(false);
            if (rate.Rate is decimal value)
            {
                await output.WriteLineAsync(RateClient.FormatAmount(result.GetRequiredValue(amount), value, target)).ConfigureAwait(false);
                return 0;
            }

            await error.WriteLineAsync(rate.Error).ConfigureAwait(false);
            return 1;
        });
        return root;
    }
}
