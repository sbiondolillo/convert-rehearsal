using System.CommandLine;
using System.Globalization;

namespace Core;

/// <summary>The command tree of the console program: the arguments <c>amount</c>, <c>from</c> and <c>to</c>.</summary>
public static class ConvertCommand
{
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    /// <summary>The root command, which prints the amount in the currency <c>to</c> at the rate of the day.</summary>
    public static RootCommand Create(HttpClient http)
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
        var root = new RootCommand("Converts an amount to another currency at the rate of the day.");
        root.Arguments.Add(amount);
        root.Arguments.Add(from);
        root.Arguments.Add(to);
        root.SetAction(async (parse, cancellationToken) =>
        {
            string? key = Environment.GetEnvironmentVariable(KeyVariable);
            if (string.IsNullOrEmpty(key))
            {
                parse.InvocationConfiguration.Error.WriteLine($"The environment variable {KeyVariable} is not set.");
                return 1;
            }

            string target = parse.GetRequiredValue(to).ToUpperInvariant();
            ConversionResult conversion = await new CurrencyConverter(http, key)
                .ConvertAsync(parse.GetRequiredValue(amount), parse.GetRequiredValue(from), target, cancellationToken)
                .ConfigureAwait(false);
            if (conversion.Amount is decimal converted)
            {
                parse.InvocationConfiguration.Output.WriteLine($"{CurrencyConverter.FormatAmount(converted)} {target}");
                return 0;
            }

            parse.InvocationConfiguration.Error.WriteLine(conversion.Error);
            return 1;
        });
        return root;
    }

    /// <summary>Parses the arguments and runs the command. A parse error goes to standard error, with no help text, and exits with 1.</summary>
    public static async Task<int> RunAsync(string[] args, HttpClient http, InvocationConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ParseResult parse = Create(http).Parse(args);
        if (parse.Errors.Count is not 0)
        {
            foreach (System.CommandLine.Parsing.ParseError error in parse.Errors)
            {
                configuration.Error.WriteLine(error.Message);
            }

            return 1;
        }

        return await parse.InvokeAsync(configuration, cancellationToken).ConfigureAwait(false);
    }
}
