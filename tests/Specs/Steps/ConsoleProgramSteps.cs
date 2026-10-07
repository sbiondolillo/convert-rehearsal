using System.CommandLine;
using Core;
using Reqnroll;
using Xunit;

namespace Specs.Steps;

[Binding]
public sealed class ConsoleProgramSteps
{
    private string _output = "";
    private int _exitCode = -1;

    [When("the person runs the console program with {string}")]
    public void WhenThePersonRunsTheConsoleProgramWith(string arguments)
    {
        using var output = new StringWriter();
        var configuration = new InvocationConfiguration { Output = output };
        _exitCode = GreetCommand.Create().Parse(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Invoke(configuration);
        _output = output.ToString();
    }

    [Then("the console program writes {string} to standard output")]
    public void ThenTheConsoleProgramWritesToStandardOutput(string expected) => Assert.Equal(expected, _output.Trim());

    [Then("the standard output lists the option {string}")]
    public void ThenTheStandardOutputListsTheOption(string option) => Assert.Contains(option, _output);

    [Then("the console program exits with the code {int}")]
    public void ThenTheConsoleProgramExitsWithTheCode(int expected) => Assert.Equal(expected, _exitCode);
}
