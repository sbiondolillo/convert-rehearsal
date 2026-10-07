using System.CommandLine;

namespace Core;

/// <summary>The command tree of the console program: <c>amount</c>, <c>from</c> and <c>to</c>.</summary>
public static class ConvertCommand
{
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    /// <summary>The root command, which prints the amount in the target currency at the rate of the day.</summary>
    /// <param name="http">The client for the requests to the service.</param>
    /// <param name="key">Gives the key of the service, or null when it is unset.</param>
    public static RootCommand Create(HttpClient http, Func<string?> key)
    {
        var amount = new Argument<decimal>("amount") { Description = "The amount of money to convert." };
        var from = new Argument<string>("from") { Description = "The code of the currency the amount is in." };
        var to = new Argument<string>("to") { Description = "The code of the currency to convert to." };
        var root = new RootCommand("Converts an amount of money to another currency at the rate of the day.");
        root.Arguments.Add(amount);
        root.Arguments.Add(from);
        root.Arguments.Add(to);
        var client = new RateClient(http);
        root.SetAction(async (result, cancellationToken) =>
        {
            TextWriter error = result.InvocationConfiguration.Error;
            string? apiKey = key();
            if (string.IsNullOrEmpty(apiKey))
            {
                await error.WriteLineAsync($"The environment variable {KeyVariable} holds no key of the service.").ConfigureAwait(false);
                return 1;
            }

            string fromCode = result.GetRequiredValue(from).ToUpperInvariant();
            string toCode = result.GetRequiredValue(to).ToUpperInvariant();
            RateAnswer answer = await client.GetRateAsync(apiKey, fromCode, toCode, cancellationToken).ConfigureAwait(false);
            if (answer.Rate is decimal rate)
            {
                await result.InvocationConfiguration.Output.WriteLineAsync(Conversion.Format(result.GetRequiredValue(amount), rate, toCode)).ConfigureAwait(false);
                return 0;
            }

            string message = answer switch
            {
                { NoAnswer: true } => "The service gave no answer.",
                { ErrorType: "unsupported-code" } => $"The service lacks one of the codes {fromCode} and {toCode}.",
                _ => $"The service answered with the error {answer.ErrorType}.",
            };
            await error.WriteLineAsync(message).ConfigureAwait(false);
            return 1;
        });
        return root;
    }
}
