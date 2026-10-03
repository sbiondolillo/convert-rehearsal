using System.Globalization;

namespace Core;

/// <summary>The console program: converts an amount from one currency to another at the rate of the day.</summary>
public static class ConvertCommand
{
    /// <summary>The name of the environment variable that holds the key of the service.</summary>
    public const string KeyVariable = "EXCHANGERATE_API_KEY";

    /// <summary>Runs the program and returns its exit code.</summary>
    public static async Task<int> RunAsync(
        string[] args,
        Func<string, string?> getEnvironmentVariable,
        HttpClient http,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length != 3)
        {
            await error.WriteLineAsync("Usage: <amount> <from-currency-code> <to-currency-code>").ConfigureAwait(false);
            return 2;
        }

        if (!decimal.TryParse(args[0], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount))
        {
            await error.WriteLineAsync($"The amount '{args[0]}' is not a number.").ConfigureAwait(false);
            return 2;
        }

        string from = args[1].ToUpperInvariant();
        string to = args[2].ToUpperInvariant();
        if (!IsCode(from) || !IsCode(to))
        {
            await error.WriteLineAsync("A currency code is three letters.").ConfigureAwait(false);
            return 2;
        }

        string? key = getEnvironmentVariable(KeyVariable);
        if (string.IsNullOrEmpty(key))
        {
            await error.WriteLineAsync($"The environment variable {KeyVariable} must hold the key of the service.").ConfigureAwait(false);
            return 1;
        }

        RateAnswer answer;
        try
        {
            answer = await new RateClient(http).GetRateAsync(key, from, to, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceUnreachableException)
        {
            await error.WriteLineAsync("The service gave no answer.").ConfigureAwait(false);
            return 1;
        }

        if (answer.Rate is decimal rate)
        {
            decimal converted;
            try
            {
                converted = checked(amount * rate);
            }
            catch (OverflowException)
            {
                await error.WriteLineAsync("The converted amount is too large to be handled.").ConfigureAwait(false);
                return 2;
            }

            await output.WriteLineAsync($"{RateClient.Format(converted)} {to}").ConfigureAwait(false);
            return 0;
        }

        string message = answer.ErrorType switch
        {
            "unsupported-code" => $"The service lacks one of the two codes {from} and {to}.",
            string errorType => $"The service answered with the error {errorType}.",
            _ => answer.Problem ?? "The service gave an answer that is not understood.",
        };
        await error.WriteLineAsync(message).ConfigureAwait(false);
        return 1;
    }

    private static bool IsCode(string code) => code.Length == 3 && code.All(char.IsAsciiLetterUpper);
}
