using System.CommandLine;
using Core;
using Xunit;

namespace Specs.Unit;

public sealed class GreetCommandTests
{
    private static (int ExitCode, string Output) Run(params string[] arguments)
    {
        using var output = new StringWriter();
        int exitCode = GreetCommand.Create().Parse(arguments).Invoke(new InvocationConfiguration { Output = output });
        return (exitCode, output.ToString().Trim());
    }

    [Theory]
    [InlineData("Hello, Ada!", "Ada")]
    [InlineData("Hello, world!")]
    public void WithoutShoutTheGreetingKeepsItsCase(string expected, params string[] arguments)
    {
        Assert.Equal((0, expected), Run(arguments));
    }

    [Theory]
    [InlineData("HELLO, ADA!", "--shout", "Ada")]
    [InlineData("HELLO, WORLD!", "--shout")]
    public void ShoutPrintsTheGreetingInUpperCase(string expected, params string[] arguments)
    {
        Assert.Equal((0, expected), Run(arguments));
    }

    [Fact]
    public void HelpListsShout()
    {
        (int exitCode, string output) = Run("--help");
        Assert.Equal(0, exitCode);
        Assert.Contains("--shout", output);
    }
}
