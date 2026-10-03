using System.CommandLine;
using System.Globalization;

namespace Core;

/// <summary>The command tree of the console program: the arguments <c>amount</c>, <c>from</c> and <c>to</c>.</summary>
public static class ConvertCommand
{
    /// <summary>The root command, which prints the amount in the target currency at the rate of the day.</summary>
    public static RootCommand Create(HttpClient http, Func<string, string?> getEnvironmentVariable)
    {
        var amount = new Argument<decimal>("amount")
        {
            Description = "The amount of money to convert.",
            CustomParser = result => decimal.TryParse(result.Tokens[0].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
                ? value
                : Fail(result, $"The amount '{result.Tokens[0].Value}' is not a number."),
        };
        var from = new Argument<string>("from") { Description = "The code of the currency the amount is in." };
        var to = new Argument<string>("to") { Description = "The code of the currency to convert to." };
        var root = new RootCommand("Converts an amount of money to another currency at the rate of the day.");
        root.Arguments.Add(amount);
        root.Arguments.Add(from);
        root.Arguments.Add(to);
        root.SetAction(async (result, cancellationToken) =>
        {
            string? key = getEnvironmentVariable(RateService.KeyVariable);
            if (string.IsNullOrEmpty(key))
            {
                await result.InvocationConfiguration.Error.WriteLineAsync($"The environment variable {RateService.KeyVariable} is unset or empty.").ConfigureAwait(false);
                return 1;
            }

            Conversion conversion = await new RateService(http)
                .ConvertAsync(result.GetRequiredValue(amount), result.GetRequiredValue(from), result.GetRequiredValue(to), key, cancellationToken)
                .ConfigureAwait(false);
            TextWriter writer = conversion.Succeeded ? result.InvocationConfiguration.Output : result.InvocationConfiguration.Error;
            await writer.WriteLineAsync(conversion.Message).ConfigureAwait(false);
            return conversion.Succeeded ? 0 : 1;
        });
        return root;
    }

    private static decimal Fail(System.CommandLine.Parsing.ArgumentResult result, string message)
    {
        result.AddError(message);
        return 0;
    }
}
