using Core;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.Net;
using Xunit;

namespace Specs.Unit;

public sealed class ConsoleHostTests
{
    private const string Key = "secretkey";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HostLoggingLeavesTheKeyOutOfTheConsole(bool fails)
    {
        var handler = new StubHandler(_ => fails
            ? StubHandler.Error("invalid-key")
            : StubHandler.Rate("0.8888"));
        TextWriter oldOut = Console.Out;
        TextWriter oldError = Console.Error;
        string? oldKey = Environment.GetEnvironmentVariable(CurrencyConverter.KeyVariable);
        using var console = new StringWriter();
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(console);
            Console.SetError(console);
            Environment.SetEnvironmentVariable(CurrencyConverter.KeyVariable, Key);
            int code = await ConsoleHost.RunAsync(
                ["10", "USD", "EUR"],
                services => services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler)),
                new InvocationConfiguration { Output = output, Error = error });
            Assert.True(code == (fails ? 1 : 0), error.ToString());
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldError);
            Environment.SetEnvironmentVariable(CurrencyConverter.KeyVariable, oldKey);
        }

        Assert.Equal([$"GET https://v6.exchangerate-api.com/v6/{Key}/pair/USD/EUR"], handler.Requests);
        Assert.Equal("", console.ToString());
        Assert.DoesNotContain(Key, error.ToString());
        Assert.DoesNotContain(Key, output.ToString());
        Assert.Equal(fails, output.ToString() is "");
    }
}
