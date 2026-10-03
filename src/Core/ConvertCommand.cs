using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;

namespace Core;

/// <summary>The command tree of the console program: <c>amount</c>, <c>from</c> and <c>to</c>.</summary>
public static class ConvertCommand
{
    /// <summary>The root command, which prints the amount in the target currency.</summary>
    /// <param name="client">The client of the rate service.</param>
    /// <param name="environment">Looks up an environment variable by name.</param>
    public static RootCommand Create(ExchangeRateClient client, Func<string, string?> environment)
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

                result.AddError($"'{(result.Tokens.Count > 0 ? result.Tokens[0].Value : "")}' is not an amount.");
                return 0m;
            },
        };
        var from = new Argument<string>("from") { Description = "The code of the currency the amount is in, such as USD." };
        var to = new Argument<string>("to") { Description = "The code of the currency to convert to, such as EUR." };

        var root = new RootCommand("Converts an amount of money to another currency at the rate of the day.");
        root.Arguments.Add(amount);
        root.Arguments.Add(from);
        root.Arguments.Add(to);
        root.SetAction(async (result, cancellationToken) =>
        {
            TextWriter error = result.InvocationConfiguration.Error;
            string? key = environment(ExchangeRateClient.KeyVariable);
            if (string.IsNullOrEmpty(key))
            {
                await error.WriteLineAsync($"The environment variable {ExchangeRateClient.KeyVariable} holds no key.").ConfigureAwait(false);
                return 1;
            }

            string fromCode = result.GetRequiredValue(from).ToUpperInvariant();
            string toCode = result.GetRequiredValue(to).ToUpperInvariant();
            RateOutcome outcome = await client.GetRateAsync(key, fromCode, toCode, cancellationToken).ConfigureAwait(false);
            if (outcome.Rate is not decimal rate)
            {
                await error.WriteLineAsync(outcome.Error).ConfigureAwait(false);
                return 1;
            }

            await result.InvocationConfiguration.Output.WriteLineAsync(Format(result.GetRequiredValue(amount), rate, toCode)).ConfigureAwait(false);
            return 0;
        });
        return root;
    }

    /// <summary>Runs the parsed arguments. A parse error writes its message on standard error and no help on standard output.</summary>
    public static Task<int> RunAsync(ParseResult parse, InvocationConfiguration? configuration, CancellationToken cancellationToken)
    {
        if (parse.Action is ParseErrorAction action)
        {
            action.ShowHelp = false;
        }

        return parse.InvokeAsync(configuration, cancellationToken);
    }

    /// <summary>The converted amount, rounded to 2 decimals away from zero, and the code.</summary>
    public static string Format(decimal amount, decimal rate, string code)
    {
        decimal converted = Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{converted:F2} {code}");
    }
}
