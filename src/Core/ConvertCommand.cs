using System.CommandLine;
using System.Globalization;

namespace Core;

/// <summary>The command tree of the console program: <c>amount</c>, <c>from</c> and <c>to</c>.</summary>
public static class ConvertCommand
{
    /// <summary>The root command, which converts the amount at the rate of the day.</summary>
    public static RootCommand Create(HttpClient http)
    {
        var amount = new Argument<decimal>("amount")
        {
            Description = "The amount of money to convert.",
            CustomParser = parse =>
            {
                string text = parse.Tokens[0].Value;
                if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                {
                    return value;
                }

                parse.AddError($"'{text}' is not an amount.");
                return 0m;
            },
        };
        var from = new Argument<string>("from") { Description = "The code of the currency the amount is in." };
        var to = new Argument<string>("to") { Description = "The code of the currency to convert to." };
        var root = new RootCommand("Converts an amount to another currency at the rate of the day.");
        root.Arguments.Add(amount);
        root.Arguments.Add(from);
        root.Arguments.Add(to);
        root.SetAction((result, cancellationToken) => new CurrencyConverter(http).ConvertAsync(
            result.GetRequiredValue(amount),
            result.GetRequiredValue(from),
            result.GetRequiredValue(to),
            Environment.GetEnvironmentVariable(CurrencyConverter.KeyVariable),
            result.InvocationConfiguration.Output,
            result.InvocationConfiguration.Error,
            cancellationToken));
        return root;
    }
}
