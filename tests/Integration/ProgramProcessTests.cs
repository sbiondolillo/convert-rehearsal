using System.Diagnostics;
using Xunit;

namespace Integration;

/// <summary>Runs the built program, with its real host, DI and logging, and reads its standard streams.</summary>
public sealed class ProgramProcessTests
{
    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(string? key, params string[] arguments)
    {
        string program = Path.Combine(AppContext.BaseDirectory, "dotnet-software-factory.dll");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(program);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Remove("EXCHANGERATE_API_KEY");
        if (key is not null)
        {
            start.Environment["EXCHANGERATE_API_KEY"] = key;
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("The program did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, await output, await error);
    }

    [Fact]
    public async Task StandardOutputHoldsOnlyTheResultLineAndNoTextOfTheKey()
    {
        string key = Environment.GetEnvironmentVariable("EXCHANGERATE_API_KEY") is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException("EXCHANGERATE_API_KEY is unset or empty.");
        (int exitCode, string output, string error) = await RunAsync(key, "10", "EUR", "EUR");
        Assert.Equal(0, exitCode);
        Assert.Equal("10.00 EUR" + Environment.NewLine, output);
        Assert.DoesNotContain(key, output + error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedRequestLeavesStandardOutputEmptyAndHoldsNoTextOfTheKey()
    {
        const string key = "secret123";
        (int exitCode, string output, string error) = await RunAsync(key, "10", "USD", "EUR");
        Assert.Equal(1, exitCode);
        Assert.Equal("", output);
        Assert.DoesNotContain(key, error, StringComparison.Ordinal);
        Assert.Single(error.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }
}
